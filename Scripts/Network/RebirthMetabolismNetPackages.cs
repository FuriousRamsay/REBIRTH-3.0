using System.IO;
using UnityEngine.Scripting;

#nullable disable

[Preserve]
public sealed class NetPackageRebirthMetabolismConsumeRequest : NetPackage
{
    private int playerId;
    private int itemType;
    private ushort seed;
    private bool autoSip;

    public override NetPackageDirection PackageDirection { get { return NetPackageDirection.ToServer; } }

    public NetPackageRebirthMetabolismConsumeRequest Setup(int entityId, ItemValue itemValue, bool isAutoSip)
    {
        playerId = entityId;
        itemType = itemValue != null ? itemValue.type : 0;
        seed = itemValue != null ? itemValue.Seed : (ushort)0;
        autoSip = isAutoSip;
        return this;
    }

    public override void read(PooledBinaryReader reader)
    {
        BinaryReader binary = (BinaryReader)reader;
        playerId = binary.ReadInt32();
        itemType = binary.ReadInt32();
        seed = binary.ReadUInt16();
        autoSip = binary.ReadBoolean();
    }

    public override void write(PooledBinaryWriter writer)
    {
        base.write(writer);
        // Bind through BinaryWriter. PooledBinaryWriter exposes ReadOnlySpan overloads in
        // current 3.x that are incompatible with RebirthUtils' legacy compile surface (CS7069).
        BinaryWriter binary = (BinaryWriter)writer;
        binary.Write(playerId);
        binary.Write(itemType);
        binary.Write(seed);
        binary.Write(autoSip);
    }

    public override void ProcessPackage(World world, GameManager callbacks)
    {
        if (world == null || world.IsRemote() || !ValidEntityIdForSender(playerId))
            return;
        EntityPlayer player = world.GetEntity(playerId) as EntityPlayer;
        if (player == null)
            return;
        RebirthConsumeResult result = RebirthMetabolismService.ConsumeMatchingInventoryItem(player, itemType, seed, autoSip);
        ConnectionManager c = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (c != null && c.IsServer)
            c.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthMetabolismConsumeResult>().Setup(result), _attachedToEntityId: playerId);
    }

    public int GetLength() { return 16; }
}

[Preserve]
public sealed class NetPackageRebirthMetabolismConsumeResult : NetPackage
{
    private RebirthConsumeResult result;

    public override NetPackageDirection PackageDirection { get { return NetPackageDirection.ToClient; } }

    public NetPackageRebirthMetabolismConsumeResult Setup(RebirthConsumeResult value)
    {
        result = value;
        return this;
    }

    public override void read(PooledBinaryReader reader)
    {
        BinaryReader binary = (BinaryReader)reader;
        result.Success = binary.ReadBoolean();
        result.Failure = (RebirthMetabolismConsumeFailure)binary.ReadByte();
        result.ConsumedMl = binary.ReadSingle();
        result.RemainingMl = binary.ReadSingle();
        result.AddedEntryId = binary.ReadInt32();
        result.CreatedEmptyItem = binary.ReadBoolean();
        result.Message = binary.ReadString();
    }

    public override void write(PooledBinaryWriter writer)
    {
        base.write(writer);
        BinaryWriter binary = (BinaryWriter)writer;
        binary.Write(result.Success);
        binary.Write((byte)result.Failure);
        binary.Write(result.ConsumedMl);
        binary.Write(result.RemainingMl);
        binary.Write(result.AddedEntryId);
        binary.Write(result.CreatedEmptyItem);
        binary.Write(result.Message ?? string.Empty);
    }

    public override void ProcessPackage(World world, GameManager callbacks)
    {
        RebirthMetabolismUiFeedback.Receive(result);
    }

    public int GetLength() { return 32 + (result.Message != null ? result.Message.Length * 2 : 0); }
}

public enum RebirthHydrationSlotOperation : byte
{
    EquipHeld = 0,
    Unequip = 1,
    ToggleAutoSip = 2,
    ManualSip = 3
}

[Preserve]
public sealed class NetPackageRebirthHydrationSlotRequest : NetPackage
{
    private int playerId;
    private RebirthHydrationSlotOperation operation;

    public override NetPackageDirection PackageDirection { get { return NetPackageDirection.ToServer; } }

    public NetPackageRebirthHydrationSlotRequest Setup(int entityId, RebirthHydrationSlotOperation op)
    {
        playerId = entityId;
        operation = op;
        return this;
    }

    public override void read(PooledBinaryReader reader)
    {
        BinaryReader binary = (BinaryReader)reader;
        playerId = binary.ReadInt32();
        operation = (RebirthHydrationSlotOperation)binary.ReadByte();
    }

    public override void write(PooledBinaryWriter writer)
    {
        base.write(writer);
        BinaryWriter binary = (BinaryWriter)writer;
        binary.Write(playerId);
        binary.Write((byte)operation);
    }

    public override void ProcessPackage(World world, GameManager callbacks)
    {
        if (world == null || world.IsRemote() || !ValidEntityIdForSender(playerId))
            return;
        EntityPlayer player = world.GetEntity(playerId) as EntityPlayer;
        if (player == null) return;
        string message = string.Empty;
        bool success = false;
        switch (operation)
        {
            case RebirthHydrationSlotOperation.EquipHeld:
                success = RebirthMetabolismService.EquipHeldHydrationContainer(player, out message);
                break;
            case RebirthHydrationSlotOperation.Unequip:
                success = RebirthMetabolismService.UnequipHydrationContainer(player, out message);
                break;
            case RebirthHydrationSlotOperation.ToggleAutoSip:
                RebirthMetabolismService.ToggleAutoSip(player);
                success = true;
                message = "Auto-sip setting changed.";
                break;
            case RebirthHydrationSlotOperation.ManualSip:
                RebirthConsumeResult consume = RebirthMetabolismService.ConsumeHydrationSlot(player, false);
                success = consume.Success;
                message = consume.Message;
                break;
        }
        RebirthMetabolismService.SendSnapshotToOwner(player, true);
        ConnectionManager c = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (c != null && c.IsServer)
            c.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthMetabolismConsumeResult>()
                .Setup(new RebirthConsumeResult { Success = success, Message = message, Failure = success ? RebirthMetabolismConsumeFailure.None : RebirthMetabolismConsumeFailure.SlotMismatch }), _attachedToEntityId: playerId);
    }

    public int GetLength() { return 8; }
}

[Preserve]
public class NetPackageRebirthMetabolismState : NetPackage
{
    protected RebirthMetabolismSnapshot s;
    public override NetPackageDirection PackageDirection { get { return NetPackageDirection.ToClient; } }

    public NetPackageRebirthMetabolismState Setup(RebirthMetabolismSnapshot value) { s = value; return this; }

    public override void read(PooledBinaryReader reader)
    {
        BinaryReader binary = (BinaryReader)reader;
        s = default(RebirthMetabolismSnapshot); // Clear pooled legacy scope before reading.
        s.OwnerEntityId = binary.ReadInt32();
        s.Sequence = binary.ReadInt64();
        s.Revision = binary.ReadInt32();
        s.Hydration = binary.ReadSingle(); s.HydrationMax = binary.ReadSingle();
        s.Food = binary.ReadSingle(); s.FoodMax = binary.ReadSingle();
        s.Energy = binary.ReadSingle(); s.EnergyMax = binary.ReadSingle();
        s.PendingHydration = binary.ReadSingle(); s.PendingFood = binary.ReadSingle(); s.PendingEnergy = binary.ReadSingle();
        s.ProjectedHydration = binary.ReadSingle(); s.ProjectedFood = binary.ReadSingle(); s.ProjectedEnergy = binary.ReadSingle();
        s.StomachFluidMl = binary.ReadSingle(); s.StomachSolidMl = binary.ReadSingle();
        s.FullnessMl = binary.ReadSingle(); s.StomachCapacityMl = binary.ReadSingle();
        s.IntestinalFluidMl = binary.ReadSingle(); s.IntestinalSolidMl = binary.ReadSingle(); s.IntestinalCapacityMl = binary.ReadSingle();
        s.IntestinalNutritionUnits = binary.ReadSingle(); s.IntestinalEnergyUnits = binary.ReadSingle(); s.IntestinalContentPercent = binary.ReadSingle();
        s.IntestinalFluidResidenceSecondsRemaining = binary.ReadSingle(); s.IntestinalNutritionResidenceSecondsRemaining = binary.ReadSingle();
        s.IntestinalStagePercent = binary.ReadSingle();
        s.GastricFluidTransferMlPerRealMinute = binary.ReadSingle(); s.GastricSolidTransferMlPerRealMinute = binary.ReadSingle();
        s.FluidAbsorbedMlPerRealMinute = binary.ReadSingle(); s.HydrationGainPointsPerRealMinute = binary.ReadSingle(); s.HydrationNetPointsPerRealMinute = binary.ReadSingle();
        s.NutritionAbsorbedUnitsPerRealMinute = binary.ReadSingle(); s.NutritionGainPointsPerRealMinute = binary.ReadSingle(); s.NutritionNetPointsPerRealMinute = binary.ReadSingle();
        s.BeverageEnergyGainPerRealMinute = binary.ReadSingle(); s.IntestinalActivityPercent = binary.ReadSingle();
        s.GastricHoldSecondsRemaining = binary.ReadSingle();
        s.FluidGastricHoldSecondsRemaining = binary.ReadSingle(); s.SolidGastricHoldSecondsRemaining = binary.ReadSingle();
        s.GastricEmptyingMlPerRealMinute = binary.ReadSingle();
        s.DigestiveHealth = binary.ReadSingle();
        s.HydrationLossMlPerReal60Minutes = binary.ReadSingle(); s.FoodUsePerReal60Minutes = binary.ReadSingle();
        s.EnergyUsePerRealMinute = binary.ReadSingle(); s.EnergyRecoveryPerRealMinute = binary.ReadSingle(); s.EnergyRecoveryNutritionUsePerRealMinute = binary.ReadSingle();
        s.EnergyRecoveryHydrationMultiplier = binary.ReadSingle(); s.StaminaRecoveryMultiplier = binary.ReadSingle();
        s.FluidAbsorptionMlPerRealMinute = binary.ReadSingle(); s.SolidGastricEmptyingMlPerRealMinute = binary.ReadSingle();
        s.NutrientAbsorptionUnitsPerRealMinute = binary.ReadSingle();
        s.HydrationRequirementMultiplier = binary.ReadSingle(); s.FoodRequirementMultiplier = binary.ReadSingle();
        s.FluidAbsorptionMultiplier = binary.ReadSingle(); s.FluidUtilizationMultiplier = binary.ReadSingle();
        s.NutrientUtilizationMultiplier = binary.ReadSingle(); s.DigestionSpeedMultiplier = binary.ReadSingle(); s.GutResilienceMultiplier = binary.ReadSingle();
        s.AutoSipEnabled = binary.ReadBoolean();
        s.HydrationSlotItemName = binary.ReadString();
        s.HydrationSlotVolumeMl = binary.ReadSingle(); s.HydrationSlotCapacityMl = binary.ReadSingle();
        s.HydrationSlotLiquidProfile = binary.ReadString();
        s.ActiveIntakeItemName = binary.ReadString();
        s.ActiveIntakeRemainingMl = binary.ReadSingle();
    }

    public override void write(PooledBinaryWriter writer)
    {
        base.write(writer);
        BinaryWriter binary = (BinaryWriter)writer;
        binary.Write(s.OwnerEntityId);
        binary.Write(s.Sequence);
        binary.Write(s.Revision);
        binary.Write(s.Hydration); binary.Write(s.HydrationMax); binary.Write(s.Food); binary.Write(s.FoodMax);
        binary.Write(s.Energy); binary.Write(s.EnergyMax);
        binary.Write(s.PendingHydration); binary.Write(s.PendingFood); binary.Write(s.PendingEnergy); binary.Write(s.ProjectedHydration); binary.Write(s.ProjectedFood); binary.Write(s.ProjectedEnergy);
        binary.Write(s.StomachFluidMl); binary.Write(s.StomachSolidMl); binary.Write(s.FullnessMl); binary.Write(s.StomachCapacityMl);
        binary.Write(s.IntestinalFluidMl); binary.Write(s.IntestinalSolidMl); binary.Write(s.IntestinalCapacityMl);
        binary.Write(s.IntestinalNutritionUnits); binary.Write(s.IntestinalEnergyUnits); binary.Write(s.IntestinalContentPercent);
        binary.Write(s.IntestinalFluidResidenceSecondsRemaining); binary.Write(s.IntestinalNutritionResidenceSecondsRemaining); binary.Write(s.IntestinalStagePercent);
        binary.Write(s.GastricFluidTransferMlPerRealMinute); binary.Write(s.GastricSolidTransferMlPerRealMinute);
        binary.Write(s.FluidAbsorbedMlPerRealMinute); binary.Write(s.HydrationGainPointsPerRealMinute); binary.Write(s.HydrationNetPointsPerRealMinute);
        binary.Write(s.NutritionAbsorbedUnitsPerRealMinute); binary.Write(s.NutritionGainPointsPerRealMinute); binary.Write(s.NutritionNetPointsPerRealMinute);
        binary.Write(s.BeverageEnergyGainPerRealMinute); binary.Write(s.IntestinalActivityPercent);
        binary.Write(s.GastricHoldSecondsRemaining); binary.Write(s.FluidGastricHoldSecondsRemaining); binary.Write(s.SolidGastricHoldSecondsRemaining); binary.Write(s.GastricEmptyingMlPerRealMinute);
        binary.Write(s.DigestiveHealth); binary.Write(s.HydrationLossMlPerReal60Minutes); binary.Write(s.FoodUsePerReal60Minutes);
        binary.Write(s.EnergyUsePerRealMinute); binary.Write(s.EnergyRecoveryPerRealMinute); binary.Write(s.EnergyRecoveryNutritionUsePerRealMinute);
        binary.Write(s.EnergyRecoveryHydrationMultiplier); binary.Write(s.StaminaRecoveryMultiplier);
        binary.Write(s.FluidAbsorptionMlPerRealMinute); binary.Write(s.SolidGastricEmptyingMlPerRealMinute);
        binary.Write(s.NutrientAbsorptionUnitsPerRealMinute);
        binary.Write(s.HydrationRequirementMultiplier); binary.Write(s.FoodRequirementMultiplier);
        binary.Write(s.FluidAbsorptionMultiplier); binary.Write(s.FluidUtilizationMultiplier); binary.Write(s.NutrientUtilizationMultiplier);
        binary.Write(s.DigestionSpeedMultiplier); binary.Write(s.GutResilienceMultiplier); binary.Write(s.AutoSipEnabled);
        binary.Write(s.HydrationSlotItemName ?? string.Empty); binary.Write(s.HydrationSlotVolumeMl); binary.Write(s.HydrationSlotCapacityMl);
        binary.Write(s.HydrationSlotLiquidProfile ?? string.Empty);
        binary.Write(s.ActiveIntakeItemName ?? string.Empty); binary.Write(s.ActiveIntakeRemainingMl);
    }

    public override void ProcessPackage(World world, GameManager callbacks)
    {
        RebirthMetabolismClientState.Receive(s);
    }

    public virtual int GetLength() { return 304
        + (s.HydrationSlotItemName != null ? s.HydrationSlotItemName.Length * 2 : 0)
        + (s.HydrationSlotLiquidProfile != null ? s.HydrationSlotLiquidProfile.Length * 2 : 0)
        + (s.ActiveIntakeItemName != null ? s.ActiveIntakeItemName.Length * 2 : 0); }
}

// Separate package identity preserves the legacy byte layout. Native package-name
// negotiation rejects older clients that do not recognize this scoped type.
[Preserve]
public sealed class NetPackageRebirthMetabolismScopedState : NetPackageRebirthMetabolismState
{
    public new NetPackageRebirthMetabolismScopedState Setup(RebirthMetabolismSnapshot value)
    { s = value; return this; }
    public override void read(PooledBinaryReader reader)
    {
        base.read(reader);
        s.CreationId = RebirthSurvivorNetworkCodec.ReadBoundedString((BinaryReader)reader, RebirthSurvivorNetworkProtocol.MaxIdLength);
    }
    public override void write(PooledBinaryWriter writer)
    {
        base.write(writer);
        RebirthSurvivorNetworkCodec.WriteString((BinaryWriter)writer, s.CreationId, RebirthSurvivorNetworkProtocol.MaxIdLength);
    }
    public override int GetLength()
    { return base.GetLength() + RebirthSurvivorNetworkCodec.EstimateString(s.CreationId, RebirthSurvivorNetworkProtocol.MaxIdLength); }
}

public static class RebirthMetabolismUiFeedback
{
    private static RebirthConsumeResult last;
    private static float expires;

    public static void Receive(RebirthConsumeResult result)
    {
        last = result;
        expires = UnityEngine.Time.realtimeSinceStartup + 4f;
        if (string.IsNullOrEmpty(result.Message))
            return;

        Log.Out("[REBIRTH Metabolism] " + result.Message);

        // Manual intake feedback should be visible without opening the detailed metabolism
        // window. Auto-sip does not call this feedback path, so this does not create periodic
        // HUD spam. It also makes full-stomach rejection/partial-drink behavior explicit.
        try
        {
            EntityPlayerLocal player = GameManager.Instance != null && GameManager.Instance.World != null
                ? GameManager.Instance.World.GetPrimaryPlayer()
                : null;
            if (player != null)
                GameManager.ShowTooltip(player, result.Message, true, false, 2.5f);
        }
        catch
        {
            // Feedback is non-authoritative presentation only. Never let a missing local HUD
            // surface interfere with the authoritative consumption transaction.
        }
    }

    public static string CurrentMessage
    {
        get { return UnityEngine.Time.realtimeSinceStartup <= expires ? last.Message ?? string.Empty : string.Empty; }
    }
}
