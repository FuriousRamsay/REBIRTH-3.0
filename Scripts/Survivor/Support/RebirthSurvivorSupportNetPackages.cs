using System.IO;
using UnityEngine.Scripting;

#nullable disable

public enum RebirthSurvivorSupportAction : byte
{
    ConsumeSupportItem = 0,
    EquipGearItem = 1,
    UnequipGearSlot = 2,
    StudyLiterature = 3,
    SetLiteratureActivity = 4,
    ListenAudiobook = 5,
    UseRageCapsule = 6,
    EquipWaterContainer = 7,
    UnequipWaterContainer = 8,
    SoloTheoryStudy = 9,
    CancelSoloTheory = 10
}

[Preserve]
public sealed class NetPackageRebirthSurvivorSupportActionRequest : NetPackage
{
    private int playerId;
    private RebirthSurvivorSupportAction action;
    private int itemType;
    private ushort seed;
    private string slotId = string.Empty;
    private string creationId = string.Empty;

    public override NetPackageDirection PackageDirection { get { return NetPackageDirection.ToServer; } }

    public NetPackageRebirthSurvivorSupportActionRequest Setup(int entityId, RebirthSurvivorSupportAction value, ItemValue itemValue, string slot)
    {
        playerId = entityId; action = value; itemType = itemValue != null ? itemValue.type : 0; seed = itemValue != null ? itemValue.Seed : (ushort)0; slotId = slot ?? string.Empty;
        var player = GameManager.Instance?.World?.GetPrimaryPlayer();
        creationId = player != null && player.entityId == entityId
            ? RebirthSurvivorClientState.GetProjectedCreationId(player) : string.Empty;
        return this;
    }

    public override void read(PooledBinaryReader reader)
    {
        BinaryReader b=(BinaryReader)reader; playerId=b.ReadInt32(); action=(RebirthSurvivorSupportAction)b.ReadByte(); itemType=b.ReadInt32(); seed=b.ReadUInt16(); slotId=RebirthSurvivorNetworkCodec.ReadString(b,RebirthSurvivorNetworkProtocol.MaxIdLength);
        creationId=RebirthSurvivorNetworkCodec.ReadString(b,RebirthSurvivorNetworkProtocol.MaxIdLength);
    }

    public override void write(PooledBinaryWriter writer)
    {
        base.write(writer); BinaryWriter b=(BinaryWriter)writer; b.Write(playerId); b.Write((byte)action); b.Write(itemType); b.Write(seed); RebirthSurvivorNetworkCodec.WriteString(b,slotId,RebirthSurvivorNetworkProtocol.MaxIdLength);
        RebirthSurvivorNetworkCodec.WriteString(b,creationId,RebirthSurvivorNetworkProtocol.MaxIdLength);
    }

    public override void ProcessPackage(World world, GameManager callbacks)
    {
        if (world == null || world.IsRemote() || !ValidEntityIdForSender(playerId)) return;
        EntityPlayer player = world.GetEntity(playerId) as EntityPlayer; if (player == null) return;
        RebirthWorldCharacterRecord record;
        // Entity IDs identify a connection, not the Survivor that originated a click.
        // Reject delayed requests after profile replacement before any mutation.
        if (!RebirthWorldCharacterService.TryGet(player, out record) || record == null || !record.IsComplete
            || record.Origin == null || !RebirthSurvivorRequestScope.Matches(creationId, record.Origin.CreationId)) return;
        if(action==RebirthSurvivorSupportAction.ListenAudiobook)RebirthPendingRemoteStudy.Cancel(player);
        if(action==RebirthSurvivorSupportAction.StudyLiterature &&
            RebirthPendingRemoteStudy.TryDefer(player,itemType,seed,creationId))return;
        bool success=false; string message=string.Empty;
        if (action < RebirthSurvivorSupportAction.ConsumeSupportItem || action > RebirthSurvivorSupportAction.CancelSoloTheory)
            message="Unsupported Survivor support action.";
        // Authenticate first, then refuse custody changes against a pending library image.
        // Passive activity hints remain available and do not consume or move inventory.
        else if (action != RebirthSurvivorSupportAction.SetLiteratureActivity
            && RebirthBackpackLibraryReservation.BlocksResourceUse(player))
            message = Localization.Get("xuiRebirthLibraryTransferPending");
        else if (action==RebirthSurvivorSupportAction.ConsumeSupportItem) success=RebirthTraitSupportService.ConsumeAndApplyMatchingInventoryItem(player,itemType,seed,out message);
        else if (action==RebirthSurvivorSupportAction.EquipGearItem) success=RebirthSurvivorGearService.TryEquipMatchingInventoryItem(player,itemType,seed,out message);
        else if (action==RebirthSurvivorSupportAction.UnequipGearSlot) success=RebirthSurvivorGearService.TryUnequip(player,slotId,out message);
        else if (action==RebirthSurvivorSupportAction.StudyLiterature) success=RebirthLiteratureService.TryReadMatchingInventoryItem(player,itemType,seed,out message);
        else if (action==RebirthSurvivorSupportAction.SetLiteratureActivity) { RebirthLiteratureStudySessionService.SetActivityHint(player,string.Equals(slotId,"slow",System.StringComparison.OrdinalIgnoreCase)); success=true; message=string.Empty; }
        else if (action==RebirthSurvivorSupportAction.ListenAudiobook) success=RebirthAudiobookListeningSessionService.TryBegin(player,itemType,seed,out message);
        else if (action==RebirthSurvivorSupportAction.UseRageCapsule) success=RebirthRageCapsuleService.Consume(player,itemType,seed,out message);
        else if(action==RebirthSurvivorSupportAction.EquipWaterContainer) success=RebirthMetabolismService.EquipMatchingHydrationContainer(player,itemType,seed,out message);
        else if(action==RebirthSurvivorSupportAction.UnequipWaterContainer) success=RebirthMetabolismService.UnequipHydrationContainer(player,out message);
        else if(action==RebirthSurvivorSupportAction.SoloTheoryStudy&&ReferenceEquals(world,GameManager.Instance?.World)&&ReferenceEquals(player.world,world)&&itemType==0&&seed==0) success=RebirthTheorySoloService.TryBegin(player,slotId,out message);
        else if(action==RebirthSurvivorSupportAction.CancelSoloTheory&&ReferenceEquals(world,GameManager.Instance?.World)&&ReferenceEquals(player.world,world)&&itemType==0&&seed==0)
        {
            var parts=(slotId??string.Empty).Split('|');
            if(parts.Length==2&&System.Guid.TryParseExact(parts[1],"N",out var session)&&session!=System.Guid.Empty)success=RebirthTheorySoloService.TryCancel(player,parts[0],parts[1],out message);
        }
        ConnectionManager c=SingletonMonoBehaviour<ConnectionManager>.Instance;
        if(c!=null&&c.IsServer) c.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthSurvivorSupportActionResult>().Setup(success,message),_attachedToEntityId:playerId);
    }

    public int GetLength() { return 20 + RebirthSurvivorNetworkCodec.EstimateString(slotId,RebirthSurvivorNetworkProtocol.MaxIdLength)
        + RebirthSurvivorNetworkCodec.EstimateString(creationId,RebirthSurvivorNetworkProtocol.MaxIdLength); }
}

[Preserve]
public sealed class NetPackageRebirthSurvivorSupportActionResult : NetPackage
{
    private bool success; private string message=string.Empty;
    public override NetPackageDirection PackageDirection { get { return NetPackageDirection.ToClient; } }
    public NetPackageRebirthSurvivorSupportActionResult Setup(bool ok,string text){success=ok;message=text??string.Empty;return this;}
    public override void read(PooledBinaryReader reader){BinaryReader b=(BinaryReader)reader;success=b.ReadBoolean();message=RebirthSurvivorNetworkCodec.ReadString(b,256);}
    public override void write(PooledBinaryWriter writer){base.write(writer);BinaryWriter b=(BinaryWriter)writer;b.Write(success);RebirthSurvivorNetworkCodec.WriteString(b,message,256);}
    public override void ProcessPackage(World world,GameManager callbacks){RebirthSurvivorSupportUiFeedback.Receive(success,message);}
    public int GetLength(){return 12+RebirthSurvivorNetworkCodec.EstimateString(message,256);}
}

public static class RebirthSurvivorSupportUiFeedback
{
    public static void Receive(bool success,string message)
    {
        if(string.IsNullOrEmpty(message)) return;
        Log.Out("[REBIRTH Survivor] support action result="+(success?"OK":"FAIL")+" message="+message);
        try
        {
            EntityPlayerLocal player=GameManager.Instance!=null&&GameManager.Instance.World!=null?GameManager.Instance.World.GetPrimaryPlayer():null;
            if(player!=null) GameManager.ShowTooltip(player,message,true,false,2.5f);
        }
        catch { }
    }
}
