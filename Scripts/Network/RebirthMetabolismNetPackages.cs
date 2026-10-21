using System.IO;
using System;
using System.Collections.Generic;
using UnityEngine.Scripting;

#nullable disable

[Preserve]
public sealed class NetPackageRebirthMetabolismConsumeRequest : NetPackage
{
    private int playerId;
    private int itemType;
    private ushort seed;
    private bool autoSip;
    private byte[] sourceFingerprint;
    private byte[] requestScope;
    private Guid operation;private RebirthMetabolismSourceKind requestedKind;private int requestedSlot,requestedCount;

    public override NetPackageDirection PackageDirection { get { return NetPackageDirection.ToServer; } }

    public NetPackageRebirthMetabolismConsumeRequest Setup(EntityPlayer player, ItemValue itemValue, bool isAutoSip)
    {
        sourceFingerprint = requestScope = null;
        RebirthProductiveEarnedUiDeferredTransport.ForgetReceivedConnection(this);operation=Guid.NewGuid();
        if (!RebirthConsumptionOwnerPublication.TryCaptureCreation(player, out var creation)) throw new InvalidOperationException("Consumption owner is not current.");
        playerId = player.entityId;
        itemType = itemValue != null ? itemValue.type : 0;
        seed = itemValue != null ? itemValue.Seed : (ushort)0;
        autoSip = isAutoSip;
        sourceFingerprint = RebirthConsumptionSourceIdentity.Fingerprint(itemValue);
        requestScope = RebirthConsumptionSourceIdentity.ScopeFingerprint(player.world.worldState?.Guid, creation, player.Died);
        if(!RebirthMetabolismService.TryFindMatchingStack(player,itemType,seed,out requestedKind,out requestedSlot,out var source,itemValue))throw new InvalidOperationException("Selected consumption source is no longer current.");
        requestedCount=source.count;
        return this;
    }

    public override void read(PooledBinaryReader reader)
    {
        sourceFingerprint = requestScope = null;
        RebirthProductiveEarnedUiDeferredTransport.ForgetReceivedConnection(this);operation=Guid.Empty;
        BinaryReader binary = (BinaryReader)reader;
        playerId = binary.ReadInt32();
        itemType = binary.ReadInt32();
        seed = binary.ReadUInt16();
        autoSip = binary.ReadBoolean();
        if (binary.ReadByte() != 2) throw new InvalidDataException("Unsupported consumption source protocol.");
        sourceFingerprint = binary.ReadBytes(32);
        if (sourceFingerprint.Length != 32) throw new EndOfStreamException();
        requestScope = binary.ReadBytes(32);
        if (requestScope.Length != 32) throw new EndOfStreamException();
        var operationBytes=binary.ReadBytes(16);if(operationBytes.Length!=16)throw new EndOfStreamException();operation=new Guid(operationBytes);
        requestedKind=(RebirthMetabolismSourceKind)binary.ReadByte();requestedSlot=binary.ReadInt32();requestedCount=binary.ReadInt32();
        if(operation==Guid.Empty||requestedKind!=RebirthMetabolismSourceKind.Backpack&&requestedKind!=RebirthMetabolismSourceKind.HeldToolbelt||
            requestedSlot<0||requestedSlot>=169||requestedCount<1||requestedCount>ushort.MaxValue)throw new InvalidDataException("Invalid consumption original source address.");
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
        if (sourceFingerprint == null || sourceFingerprint.Length != 32 || requestScope == null || requestScope.Length != 32) throw new InvalidDataException("Missing consumption source identity.");
        binary.Write((byte)2);
        binary.Write(sourceFingerprint);
        binary.Write(requestScope);
        binary.Write(operation.ToByteArray());binary.Write((byte)requestedKind);binary.Write(requestedSlot);binary.Write(requestedCount);
    }

    public override void ProcessPackage(World world, GameManager callbacks)
    {
        using(RebirthConsumptionTrace.Begin())
        {
        ConnectionManager connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (!ThreadManager.IsMainThread() || world == null || world.IsRemote() ||
            !ReferenceEquals(world, GameManager.Instance?.World) ||!ReferenceEquals(callbacks,GameManager.Instance)||
            connection == null || !connection.IsServer || Sender?.loginDone!=true ||Sender.disconnecting||
            !ReferenceEquals(connection.Clients?.ForEntityId(playerId), Sender) ||
            !ValidEntityIdForSender(playerId))
        {
            RebirthConsumptionTrace.Note("request.refused",reason:"world-or-sender",success:false);
            return;
        }
        EntityPlayer player = world.GetEntity(playerId) as EntityPlayer;
        if (player == null || player.IsDead() || !ReferenceEquals(player.world, world) ||
            !ReferenceEquals(world.GetEntity(playerId), player))
        {
            RebirthConsumptionTrace.Note("request.refused",reason:"missing-player",success:false);
            return;
        }
        RebirthMetabolismSourceKind sourceKind;
        string ownerCreation;
        if(!RebirthConsumptionOwnerPublication.TryCaptureCreation(player,out ownerCreation))return;
        if(!RebirthConsumptionSourceIdentity.MatchesScope(world.worldState?.Guid,ownerCreation,player.Died,requestScope))return;
        // Only requests already recorded by the audit protocol use its old
        // recovery path. Fresh servings use the existing server ConsumeAt path.
        if(RebirthConsumptionDurableDelivery.HasRecordedInventoryOperation(player,operation))
        {
            if(!RebirthRemoteResourceNetworkScope.Receive(this,world,callbacks,true,out _))return;
            RebirthConsumptionDurableDelivery.ReceiveInventoryRequest(player,operation,requestedKind,requestedSlot,requestedCount,sourceFingerprint,autoSip);
            return;
        }
        int originalDeaths=player.Died;var originalWorldState=world.worldState;string originalWorldGuid=world.worldState?.Guid;
        int sourceSlot; ItemStack source;
        if(sourceFingerprint==null||sourceFingerprint.Length!=32)return;
        bool found=RebirthMetabolismService.TryFindMatchingStack(player,itemType,seed,out sourceKind,out sourceSlot,out source,expectedFingerprint:sourceFingerprint);
        ItemStack before=found?source.Clone():null;
        RebirthConsumptionTrace.Note("request.admitted",player,kind:sourceKind,slot:sourceSlot,before:before,
            reason:"type="+itemType+" seed="+seed+" autoSip="+autoSip);
        RebirthConsumeResult result = RebirthMetabolismService.ConsumeInventorySource(player, sourceKind, sourceSlot, before, autoSip);
        if(!ReferenceEquals(GameManager.Instance,callbacks)||!ReferenceEquals(callbacks.World,world)||!ReferenceEquals(player.world,world)||
            !ReferenceEquals(world.worldState,originalWorldState)||world.worldState?.Guid!=originalWorldGuid||
            !ReferenceEquals(world.GetEntity(playerId),player)||player.IsDead()||player.Died!=originalDeaths||
            !ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance,connection)||!connection.IsServer||
            !ReferenceEquals(connection.Clients?.ForEntityId(playerId),Sender)||Sender.disconnecting||!Sender.loginDone||
            !RebirthConsumptionOwnerPublication.TryCaptureCreation(player,out var currentCreation)||currentCreation!=ownerCreation)return;
        ItemStack after=null;
        if(found)
        {
            var slots=sourceKind==RebirthMetabolismSourceKind.Backpack?player.bag?.ItemGrid?.items:
                sourceKind==RebirthMetabolismSourceKind.HeldToolbelt?player.inventory?.ItemGrid?.items:null;
            if(slots==null||sourceSlot<0||sourceSlot>=slots.Length||slots[sourceSlot]==null)return;
            after=slots[sourceSlot].Clone();
        }
        ConnectionManager c = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (c != null && c.IsServer)
        {
            var packet=NetPackageManager.GetPackage<NetPackageRebirthMetabolismConsumeResult>()
                .Setup(result).WithInventory(playerId,sourceKind,sourceSlot,before,after,originalDeaths,ownerCreation);
            packet.TryGetPublicationIdentity(out var tracedOwner,out var tracedPublication);
            RebirthConsumptionTrace.Note("result.send",player,tracedPublication,sourceKind,sourceSlot,before,after,result.Failure.ToString(),result.Success);
            c.SendPackage(packet, _attachedToEntityId: playerId);
        }
        else RebirthConsumptionTrace.Note("result.no-transport",player,kind:sourceKind,slot:sourceSlot,before:before,after:after,success:result.Success);
        }
    }

    public int GetLength() { return 106; }
}

[Preserve]
public sealed class NetPackageRebirthMetabolismConsumeResult : NetPackage
{
    private RebirthConsumeResult result;
    private Guid publication;
    private int ownerId,sourceSlot;
    private RebirthMetabolismSourceKind sourceKind;
    private ItemStack before,after;
    private string beforeItem,afterItem;
    private string ownerCreation=string.Empty;
    private int ownerDeaths=-1;

    public override NetPackageDirection PackageDirection { get { return NetPackageDirection.ToClient; } }

    // Dormant retry component access; existing receive/send paths do not invoke it.
    internal bool TryGetPublicationIdentity(out int owner,out Guid id)
    { owner=ownerId;id=publication;return publication!=Guid.Empty&&before!=null&&after!=null; }
    internal bool ApplyPublicationForAcknowledgement(World world)
        =>CurrentPublicationLife(world)&&(RebirthConsumptionOwnerPublication.WasApplied(world,ownerId,publication,ownerCreation)
            ||RebirthConsumptionOwnerPublication.Apply(world,ownerId,publication,sourceKind,sourceSlot,before,after,ownerCreation));
    private bool CurrentPublicationLife(World world)
    {
        var player=world?.GetPrimaryPlayer();
        return player!=null&&ReferenceEquals(player.world,world)&&ReferenceEquals(world.GetEntity(player.entityId),player)&&
            player.entityId==ownerId&&!player.IsDead()&&ownerDeaths>=0&&player.Died==ownerDeaths;
    }

    public NetPackageRebirthMetabolismConsumeResult Setup(RebirthConsumeResult value)
    {
        result = value;
        ownerCreation=string.Empty;
        ownerDeaths=-1;
        publication=Guid.Empty;before=after=null;beforeItem=afterItem=null;
        return this;
    }
    internal NetPackageRebirthMetabolismConsumeResult WithInventory(int owner,RebirthMetabolismSourceKind kind,int slot,ItemStack prior,ItemStack next,int deaths,string creation="")
    {
        if(prior==null||next==null||RebirthStationGridIngredients.IsSameStackSnapshot(prior,next))return this;
        if(deaths<0)throw new InvalidDataException("Missing consumption owner life.");
        ownerDeaths=deaths;
        ownerId=owner;sourceKind=kind;sourceSlot=slot;publication=Guid.NewGuid();
        ownerCreation=creation??string.Empty;
        before=prior.Clone();after=next.Clone();
        beforeItem=RebirthNativeItemCodec.Encode(before.itemValue);afterItem=RebirthNativeItemCodec.Encode(after.itemValue);
        return this;
    }

    public override void read(PooledBinaryReader reader)
    {
        BinaryReader binary = (BinaryReader)reader;
        ownerDeaths=-1;publication=Guid.Empty;before=after=null;
        if(binary.ReadByte()!=3)throw new InvalidDataException("Consumption result protocol requires matching updated REBIRTH peers (version 3).");
        ownerCreation=RebirthSurvivorNetworkCodec.ReadBoundedString(binary,71);
        if(ownerCreation.Length!=0&&(!RebirthSurvivorRequestScope.TryNormalize(ownerCreation,out var normalizedCreation)||
            !string.Equals(ownerCreation,normalizedCreation,StringComparison.Ordinal)))
            throw new InvalidDataException("Invalid consumption owner creation scope.");
        result.Success = binary.ReadBoolean();
        result.Failure = (RebirthMetabolismConsumeFailure)binary.ReadByte();
        result.ConsumedMl = binary.ReadSingle();
        result.RemainingMl = binary.ReadSingle();
        result.AddedEntryId = binary.ReadInt32();
        result.CreatedEmptyItem = binary.ReadBoolean();
        result.Message = binary.ReadString();
        publication=new Guid(binary.ReadBytes(16));before=after=null;beforeItem=afterItem=null;
        if(publication!=Guid.Empty)
        {
            ownerId=binary.ReadInt32();sourceKind=(RebirthMetabolismSourceKind)binary.ReadByte();sourceSlot=binary.ReadInt32();
            ownerDeaths=binary.ReadInt32();if(ownerDeaths<0)throw new InvalidDataException("Invalid consumption owner life.");
            int priorCount=binary.ReadInt32();beforeItem=binary.ReadString();int nextCount=binary.ReadInt32();afterItem=binary.ReadString();
            ItemValue prior,next;
            if(priorCount<1||nextCount<0||beforeItem.Length>1048576||afterItem.Length>1048576
                ||!RebirthNativeItemCodec.TryDecode(beforeItem,out prior))
            {
                RebirthConsumptionTrace.Note("result.decode-refused",publication:publication,reason:"invalid-before-or-counts",success:false);
                throw new InvalidDataException("Invalid consumption inventory publication.");
            }
            before=new ItemStack(prior,priorCount);
            if(nextCount==0)
            {
                // The canonical item reader deliberately admits registered nonempty items
                // only. A final serving instead publishes the native empty slot image.
                var empty=ItemStack.Empty.Clone();
                if(!string.Equals(afterItem,RebirthNativeItemCodec.Encode(empty.itemValue),StringComparison.Ordinal))
                {
                    RebirthConsumptionTrace.Note("result.decode-refused",publication:publication,reason:"noncanonical-empty-after",success:false);
                    throw new InvalidDataException("Invalid empty consumption inventory publication.");
                }
                after=empty;
            }
            else
            {
                if(!RebirthNativeItemCodec.TryDecode(afterItem,out next))
                {
                    RebirthConsumptionTrace.Note("result.decode-refused",publication:publication,reason:"invalid-positive-after",success:false);
                    throw new InvalidDataException("Invalid consumption inventory publication.");
                }
                after=new ItemStack(next,nextCount);
            }
        }
    }

    public override void write(PooledBinaryWriter writer)
    {
        base.write(writer);
        BinaryWriter binary = (BinaryWriter)writer;
        binary.Write((byte)3);
        RebirthSurvivorNetworkCodec.WriteString(binary,ownerCreation??string.Empty,71);
        binary.Write(result.Success);
        binary.Write((byte)result.Failure);
        binary.Write(result.ConsumedMl);
        binary.Write(result.RemainingMl);
        binary.Write(result.AddedEntryId);
        binary.Write(result.CreatedEmptyItem);
        binary.Write(result.Message ?? string.Empty);
        binary.Write(publication.ToByteArray());
        if(publication!=Guid.Empty)
        {
            binary.Write(ownerId);binary.Write((byte)sourceKind);binary.Write(sourceSlot);
            if(ownerDeaths<0)throw new InvalidDataException("Missing consumption owner life.");binary.Write(ownerDeaths);
            binary.Write(before.count);binary.Write(beforeItem);binary.Write(after.count);binary.Write(afterItem);
        }
    }

    public override void ProcessPackage(World world, GameManager callbacks)
    {
        ConnectionManager connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if(!ThreadManager.IsMainThread() || world==null || !world.IsRemote() ||
            !ReferenceEquals(world,GameManager.Instance?.World) ||
            connection==null || connection.IsServer)return;
        RebirthConsumptionTrace.Note("result.receive",world.GetPrimaryPlayer(),publication,sourceKind,sourceSlot,before,after,result.Failure.ToString(),result.Success);
        if(publication!=Guid.Empty&&!CurrentPublicationLife(world))return;
        if(publication!=Guid.Empty&&RebirthConsumptionPublicationRetry.AcknowledgementProtocolEnabled)
        {
            bool alreadyApplied=RebirthConsumptionOwnerPublication.WasApplied(world,ownerId,publication,ownerCreation);
            string creation=RebirthSurvivorClientState.GetProjectedCreationId(world.GetPrimaryPlayer());
            if(!RebirthConsumptionAcknowledgement.TrySend(world,this,creation)||alreadyApplied)return;
            RebirthMetabolismUiFeedback.Receive(result);
            return;
        }
        if(publication!=Guid.Empty&&!RebirthConsumptionOwnerPublication.Apply(world,ownerId,publication,sourceKind,sourceSlot,before,after,ownerCreation))return;
        RebirthMetabolismUiFeedback.Receive(result);
    }

    public int GetLength() { return 70 + (ownerCreation?.Length??0)*2 + (result.Message?.Length??0)*2+(beforeItem?.Length??0)*2+(afterItem?.Length??0)*2; }
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
        ConnectionManager connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (!ThreadManager.IsMainThread() || world == null || world.IsRemote() ||
            !ReferenceEquals(world, GameManager.Instance?.World) ||
            connection == null || !connection.IsServer || Sender == null ||
            !ReferenceEquals(connection.Clients?.ForEntityId(playerId), Sender) ||
            !ValidEntityIdForSender(playerId))
            return;
        EntityPlayer player = world.GetEntity(playerId) as EntityPlayer;
        if (player == null || player.IsDead() || !ReferenceEquals(player.world, world) ||
            !ReferenceEquals(world.GetEntity(playerId), player)) return;
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
        ConnectionManager connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (!ThreadManager.IsMainThread() || world == null || !world.IsRemote() ||
            !ReferenceEquals(world, GameManager.Instance?.World) ||
            connection == null || connection.IsServer) return;
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
    private static long consumptionFeedbackSequence;
    internal static Newtonsoft.Json.Linq.JObject CaptureConsumptionAuditFeedback()
        =>new Newtonsoft.Json.Linq.JObject{["sequence"]=consumptionFeedbackSequence,["success"]=last.Success,["failure"]=(int)last.Failure,["message"]=last.Message??string.Empty};

    public static void Receive(RebirthConsumeResult result)
    {
        last = result;
        consumptionFeedbackSequence++;
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
