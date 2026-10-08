using System.Collections.Generic;
using System.IO;
using UnityEngine.Scripting;

[Preserve]
public sealed class NetPackageRebirthMusicLibraryRequest : NetPackage
{
    private int playerId, operation, index, itemType;
    private ushort seed;
    private long revision;
    private string expectedCreationId,sourceProof;
    public override NetPackageDirection PackageDirection => NetPackageDirection.ToServer;
    public NetPackageRebirthMusicLibraryRequest Setup(int player, int op, int slot, ItemValue item, long expected, string creation)
    { playerId=player; operation=op; index=slot; itemType=item?.type??0; seed=item?.Seed??0; revision=expected; expectedCreationId=creation; sourceProof=string.Empty; if(op==1||op==5)RebirthMusicSourceProof.TryCreate(item,out sourceProof); return this; }
    public override void read(PooledBinaryReader r)
    { playerId=r.ReadInt32(); operation=r.ReadByte(); index=r.ReadInt32(); itemType=r.ReadInt32(); seed=r.ReadUInt16(); revision=r.ReadInt64(); expectedCreationId=RebirthSurvivorNetworkCodec.ReadString(r,RebirthSurvivorNetworkProtocol.MaxIdLength); sourceProof=RebirthSurvivorNetworkCodec.ReadBoundedString(r,RebirthMusicSourceProof.Length); }
    public override void write(PooledBinaryWriter w)
    { base.write(w); ((BinaryWriter)w).Write(playerId); ((BinaryWriter)w).Write((byte)operation); ((BinaryWriter)w).Write(index); ((BinaryWriter)w).Write(itemType); ((BinaryWriter)w).Write(seed); ((BinaryWriter)w).Write(revision); RebirthSurvivorNetworkCodec.WriteString(w,expectedCreationId,RebirthSurvivorNetworkProtocol.MaxIdLength); RebirthSurvivorNetworkCodec.WriteString(w,sourceProof,RebirthMusicSourceProof.Length); }
    public int GetLength() => 27+RebirthSurvivorNetworkCodec.EstimateString(expectedCreationId,RebirthSurvivorNetworkProtocol.MaxIdLength)+RebirthSurvivorNetworkCodec.EstimateString(sourceProof,RebirthMusicSourceProof.Length);
    public override void ProcessPackage(World world, GameManager callbacks)
    {
        if(world==null || world.IsRemote() || !ValidEntityIdForSender(playerId) || operation<0 || operation>9)return;
        var player=world.GetEntity(playerId) as EntityPlayer;
        if(player==null)return;
        RebirthWorldCharacterRecord record;
        if(!RebirthWorldCharacterService.TryGet(player,out record) || record?.Support==null)return;
        bool sameCharacter=RebirthSurvivorRequestScope.Matches(expectedCreationId,record.Origin?.CreationId);
        RebirthMusicTransferState offer=null;
        if((operation==0 && record.Support.PendingMusicTransfer!=null) || (sameCharacter && (operation==1 || operation==2 || operation==5 || operation==6)))
        {
            bool prepared=operation==5 || operation==6
                ? RebirthMusicTransferServer.PrepareAudiobookSource(player,Sender,operation-4,index,itemType,seed,revision,expectedCreationId,sourceProof,out offer)
                : RebirthMusicTransferServer.PrepareMusicSource(player,Sender,operation,index,itemType,seed,revision,
                operation==0?record.Origin.CreationId:expectedCreationId,sourceProof,out offer);
            if(!prepared && operation!=0)
                SingletonMonoBehaviour<ConnectionManager>.Instance.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthSurvivorSupportActionResult>().Setup(false,Localization.Get("xuiRebirthMusicRefresh")),_attachedToEntityId:playerId);
        }
        else if((operation==8||operation==9)&&sameCharacter)
        {
            string message=Localization.Get("xuiRebirthMusicRefresh");
            bool valid=record.Support.PendingMusicTransfer==null&&record.Support.AudiobookRevision==revision;
            bool ok=valid&&(operation==8 ? RebirthAudiobookListeningSessionService.TryPause(player,out message)
                : RebirthAudiobookListeningSessionService.TryResume(player,out message));
            SingletonMonoBehaviour<ConnectionManager>.Instance.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthSurvivorSupportActionResult>().Setup(ok,message),_attachedToEntityId:playerId);
        }
        else if(operation==7&&sameCharacter)
        {
            string message=Localization.Get("xuiRebirthMusicRefresh");
            bool ok=record.Support.PendingMusicTransfer==null&&record.Support.AudiobookRevision==revision&&
                index>=0&&index<record.Support.AudiobookCassettes.Count&&
                RebirthAudiobookListeningSessionService.TryBeginStored(player,record.Support.AudiobookCassettes[index].SlotId,out message);
            SingletonMonoBehaviour<ConnectionManager>.Instance.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthSurvivorSupportActionResult>().Setup(ok,message),_attachedToEntityId:playerId);
        }
        else if((operation==3 || operation==4) && sameCharacter && record.Support.PendingMusicTransfer==null)
        {
            string message;
            bool ok=RebirthMusicLibraryService.TryChange(player,operation,index,itemType,seed,revision,out message);
            SingletonMonoBehaviour<ConnectionManager>.Instance.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthSurvivorSupportActionResult>().Setup(ok,message),_attachedToEntityId:playerId);
        }
        if(RebirthWorldCharacterService.TryGet(player,out record) && record?.Support!=null)
            SingletonMonoBehaviour<ConnectionManager>.Instance.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthMusicLibrarySnapshot>().Setup(playerId,record),_attachedToEntityId:playerId);
        if(offer!=null)
            SingletonMonoBehaviour<ConnectionManager>.Instance.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthMusicTransferOffer>().Setup(playerId,offer),_attachedToEntityId:playerId);
    }
}

[Preserve]
public sealed class NetPackageRebirthMusicLibrarySnapshot : NetPackage
{
    private int playerId;
    private string creationId;
    private long revision;
    private bool shuffle;
    private bool pending;
    private readonly List<string> ids=new List<string>();
    private long audioRevision;
    private readonly List<string> audioIds=new List<string>();
    private readonly List<string> audioSlots=new List<string>();
    public override NetPackageDirection PackageDirection => NetPackageDirection.ToClient;
    public NetPackageRebirthMusicLibrarySnapshot Setup(int ownerId, RebirthWorldCharacterRecord record)
    {
        playerId=ownerId; creationId=record.Origin.CreationId;
        revision=record.Support.MusicRevision; shuffle=record.Support.MusicShuffle; ids.Clear();
        audioRevision=record.Support.AudiobookRevision;audioIds.Clear();audioSlots.Clear();
        foreach(var audio in record.Support.AudiobookCassettes)
        {audioIds.Add(audio.ItemId);audioSlots.Add(audio.SlotId);}
        var transfer=record.Support.PendingMusicTransfer;pending=transfer!=null;
        foreach(var cassette in record.Support.MusicCassettes)
        { if(ids.Count>=RebirthMusicLibraryService.Capacity)break; ids.Add(transfer!=null && !transfer.IsAudiobook && transfer.Operation==2 && transfer.LibraryIndex==ids.Count?string.Empty:cassette.ItemId??string.Empty); }
        return this;
    }
    public override void read(PooledBinaryReader r)
    {
        playerId=r.ReadInt32(); creationId=RebirthSurvivorNetworkCodec.ReadString(r,RebirthSurvivorNetworkProtocol.MaxIdLength);
        revision=r.ReadInt64(); shuffle=r.ReadBoolean(); pending=r.ReadBoolean(); ids.Clear(); int count=r.ReadByte();
        if(count>RebirthMusicLibraryService.Capacity)throw new InvalidDataException("Invalid music library size");
        for(int i=0;i<count;i++)ids.Add(RebirthSurvivorNetworkCodec.ReadString(r,RebirthSurvivorNetworkProtocol.MaxIdLength));
        audioRevision=r.ReadInt64();audioIds.Clear();audioSlots.Clear();int audioCount=r.ReadByte();
        if(audioRevision<0||audioCount>RebirthAudiobookLibraryPersistence.Capacity)throw new InvalidDataException("Invalid audiobook snapshot");
        var seen=new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        for(int i=0;i<audioCount;i++)
        {
            string slot=RebirthSurvivorNetworkCodec.ReadString(r,32);System.Guid parsed;
            string id=RebirthSurvivorNetworkCodec.ReadString(r,RebirthSurvivorNetworkProtocol.MaxIdLength);
            if(!System.Guid.TryParseExact(slot,"N",out parsed)||parsed==System.Guid.Empty||!seen.Add(slot)||string.IsNullOrEmpty(id))throw new InvalidDataException("Invalid audiobook snapshot entry");
            audioSlots.Add(slot);audioIds.Add(id);
        }
    }
    public override void write(PooledBinaryWriter w)
    {
        base.write(w); ((BinaryWriter)w).Write(playerId);
        RebirthSurvivorNetworkCodec.WriteString(w,creationId,RebirthSurvivorNetworkProtocol.MaxIdLength);
        ((BinaryWriter)w).Write(revision); ((BinaryWriter)w).Write(shuffle); ((BinaryWriter)w).Write(pending); ((BinaryWriter)w).Write((byte)ids.Count);
        foreach(var id in ids)RebirthSurvivorNetworkCodec.WriteString(w,id,RebirthSurvivorNetworkProtocol.MaxIdLength);
        ((BinaryWriter)w).Write(audioRevision);((BinaryWriter)w).Write((byte)audioIds.Count);
        for(int i=0;i<audioIds.Count;i++)
        {RebirthSurvivorNetworkCodec.WriteString(w,audioSlots[i],32);RebirthSurvivorNetworkCodec.WriteString(w,audioIds[i],RebirthSurvivorNetworkProtocol.MaxIdLength);}
    }
    public int GetLength()
    { int length=19+RebirthSurvivorNetworkCodec.EstimateString(creationId,RebirthSurvivorNetworkProtocol.MaxIdLength); foreach(var id in ids)length+=RebirthSurvivorNetworkCodec.EstimateString(id,RebirthSurvivorNetworkProtocol.MaxIdLength); length+=9;for(int i=0;i<audioIds.Count;i++)length+=RebirthSurvivorNetworkCodec.EstimateString(audioSlots[i],32)+RebirthSurvivorNetworkCodec.EstimateString(audioIds[i],RebirthSurvivorNetworkProtocol.MaxIdLength);return length; }
    public override void ProcessPackage(World world, GameManager callbacks)
    {
        var player=world?.GetPrimaryPlayer();
        if(world==null || !world.IsRemote() || player==null || player.entityId!=playerId)return;
        RebirthMusicLibraryClient.Receive(creationId,revision,shuffle,ids,pending,audioRevision);
        RebirthAudiobookLibraryClient.Receive(creationId,audioRevision,audioSlots,audioIds);
    }
}

public static class RebirthMusicLibraryClient
{
    public static string CreationId {get;private set;}=string.Empty;
    public static long Generation {get;private set;}
    public static bool PendingTransfer {get;private set;}
    public static long Revision {get;private set;}=-1;
    public static bool Shuffle {get;private set;}=true;
    public static readonly List<string> Items=new List<string>();
    private static World cacheWorld;
    private static int cacheOwner;
    private static long cacheAudioRevision=-1;
    private static World useWorld;
    private static int useOwner;
    private static string useCreation;
    private static ItemValue pendingUse;
    private static void ClearUse(){useWorld=null;useOwner=0;useCreation=null;pendingUse=null;}
    // Using music loads it only. Playback remains an explicit player action.
    public static bool RequestUse(EntityPlayerLocal player,ItemValue item)
    {
        if(player?.world==null||player.IsDead()||item==null||item.IsEmpty()||
            RebirthBackpackLibraryReservation.IsHeld(player)||pendingUse!=null)return false;
        bool current=EnsureCurrent(player);
        if(!player.world.IsRemote())
        {if(!current||Revision<0)Dispatch(player,0);if(!EnsureCurrent(player)||Revision<0||PendingTransfer)return false;Dispatch(player,1,0,item);return true;}
        if(current&&Revision>=0){if(PendingTransfer)return false;return TryDispatchRemoteUse(player,item,false);}
        if(!RebirthSurvivorRequestScope.TryNormalize(RebirthSurvivorClientState.GetProjectedCreationId(player),out var creation))return false;
        var connection=SingletonMonoBehaviour<ConnectionManager>.Instance;
        var request=NetPackageManager.GetPackage<NetPackageRebirthMusicLibraryRequest>();
        if(!CanSend(connection,request))return false;
        useWorld=player.world;useOwner=player.entityId;useCreation=creation;pendingUse=item.Clone();
        Dispatch(player,0);return true;
    }
    private static void ApplyPendingUse(EntityPlayerLocal player)
    {
        if(pendingUse==null)return;
        if(player==null||player.IsDead()||!ReferenceEquals(player.world,useWorld)||player.entityId!=useOwner||
            !RebirthSurvivorRequestScope.Matches(useCreation,CreationId)){ClearUse();return;}
        if(PendingTransfer||RebirthBackpackLibraryReservation.IsHeld(player))return;
        if(player.world.IsRemote()){TryDispatchRemoteUse(player,pendingUse,true);return;}
        var item=pendingUse;ClearUse();
        Dispatch(player,1,0,item);
    }    public static void Receive(string creationId,long revision,bool shuffle,IEnumerable<string> ids,bool pending=false,long audioRevision=-1)
    {
        if(!RebirthSurvivorRequestScope.TryNormalize(creationId,out string canonical)||revision<0)return;
        var player=GameManager.Instance?.World?.GetPrimaryPlayer();
        if(player?.world==null || ids==null)return;
        string current;
        if(player.world.IsRemote())current=RebirthSurvivorClientState.GetProjectedCreationId(player);
        else
        {
            RebirthWorldCharacterRecord record;
            if(!RebirthWorldCharacterService.TryGet(player,out record)||record==null||!record.IsComplete)return;
            current=record.Origin?.CreationId;
            audioRevision=record.Support?.AudiobookRevision??-1;
        }
        // A delayed previous-Survivor snapshot must not reset current transfer receipts or playlist.
        if(!RebirthSurvivorRequestScope.Matches(canonical,current))return;
        bool contextChanged=!ReferenceEquals(cacheWorld,player.world)||cacheOwner!=player.entityId;
        if(!contextChanged&&canonical==CreationId)
        {
            if(revision<Revision||audioRevision>=0&&cacheAudioRevision>=0&&audioRevision<cacheAudioRevision)return;
            // Preparation retains both revisions. Terminal music or audiobook
            // settlement increments its own revision; an older unheld projection
            // with identical revisions cannot clear custody or discard its offer.
            if(PendingTransfer&&!pending&&revision==Revision&&audioRevision==cacheAudioRevision)return;
        }
        if(contextChanged||canonical!=CreationId || revision!=Revision || shuffle!=Shuffle || pending!=PendingTransfer)Generation++;
        if(contextChanged||!pending || canonical!=CreationId)RebirthMusicTransferClient.Reset();
        cacheWorld=player.world;cacheOwner=player.entityId;cacheAudioRevision=audioRevision;
        PendingTransfer=pending;
        CreationId=canonical;Revision=revision;Shuffle=shuffle;Items.Clear();Items.AddRange(ids);ApplyPendingUse(player);
    }
    public static bool EnsureCurrent(EntityPlayerLocal player)
    {
        string current=string.Empty;
        if(player?.world!=null)
        {
            if(player.world.IsRemote())current=RebirthSurvivorClientState.GetProjectedCreationId(player);
            else
            {
                RebirthWorldCharacterRecord record;
                if(RebirthWorldCharacterService.TryGet(player,out record)&&record!=null&&record.IsComplete)
                    current=record.Origin?.CreationId;
            }
        }
        if(player!=null&&ReferenceEquals(cacheWorld,player.world)&&cacheOwner==player.entityId&&
            RebirthSurvivorRequestScope.Matches(CreationId,current))return true;
        if(Revision>=0||!string.IsNullOrEmpty(CreationId))Reset();
        return false;
    }

    public static void Reset(){cacheWorld=null;cacheOwner=0;cacheAudioRevision=-1;ClearUse();RebirthAudiobookLibraryClient.Reset();RebirthMusicTransferClient.Reset();PendingTransfer=false;CreationId=string.Empty;Generation++;Revision=-1;Shuffle=true;Items.Clear();}
    // Preflight refusal retains an unsent use; admission clears it before native sends.
    // SendToServer provides no delivery acknowledgement: never replay an admitted use.
    private static bool remoteUsePreparing;
    private static bool TryDispatchRemoteUse(EntityPlayerLocal player,ItemValue item,bool clearQueued)
    {
        if(remoteUsePreparing||player?.world==null||player.IsDead()||!player.world.IsRemote()||
            !EnsureCurrent(player)||PendingTransfer||RebirthBackpackLibraryReservation.IsHeld(player))return false;
        var game=GameManager.Instance;var world=player.world;
        var creation=CreationId;var revision=Revision;var generation=Generation;var owner=player.entityId;
        var connection=SingletonMonoBehaviour<ConnectionManager>.Instance;
        if(game==null||!ReferenceEquals(game.World,world)||!ReferenceEquals(world.GetPrimaryPlayer(),player)||
            clearQueued&&!ReferenceEquals(pendingUse,item))return false;
        remoteUsePreparing=true;
        try
        {
            var request=NetPackageManager.GetPackage<NetPackageRebirthMusicLibraryRequest>();
            var inventory=NetPackageManager.GetPackage<NetPackagePlayerInventory>();
            if(!CanSend(connection,request)||!CanSend(connection,inventory))
            {RebirthSurvivorSupportUiFeedback.Receive(false,Localization.Get("xuiRebirthMusicUnavailable"));return false;}
            request.Setup(owner,1,0,item,revision,creation);
            inventory.Setup(player,true,true,false,false);
            // Setup serializes through native callbacks. Refuse replaced context before admission.
            if(!ReferenceEquals(game,GameManager.Instance)||!ReferenceEquals(world,player.world)||
                !ReferenceEquals(world.GetPrimaryPlayer(),player)||player.entityId!=owner||player.IsDead()||!world.IsRemote()||
                !EnsureCurrent(player)||Generation!=generation||Revision!=revision||CreationId!=creation||
                PendingTransfer||RebirthBackpackLibraryReservation.IsHeld(player)||
                !ReferenceEquals(connection,SingletonMonoBehaviour<ConnectionManager>.Instance)||
                !CanSend(connection,request)||!CanSend(connection,inventory)||clearQueued&&!ReferenceEquals(pendingUse,item))return false;
            if(clearQueued)ClearUse();
            connection.SendToServer(inventory);
            connection.SendToServer(request);
            return true;
        }
        finally { remoteUsePreparing=false; }
    }
    internal static bool CanSend(ConnectionManager connection, NetPackage packet)
    {
        if (connection == null || connection.IsServer || !connection.IsConnected || packet == null) return false;
        var channels = connection.GetConnectionToServer();
        int channel = packet.Channel;
        return channels != null && channel >= 0 && channel < channels.Length &&
            channels[channel] != null && !channels[channel].IsDisconnected();
    }
    public static bool RequestReorder(EntityPlayerLocal player,int source,int target,long expectedGeneration,long expectedRevision)
    {
        if(!EnsureCurrent(player)||PendingTransfer||Generation!=expectedGeneration||Revision!=expectedRevision||
            source<0||target<0||source>=Items.Count||target>=Items.Count||
            source>=RebirthMusicLibraryService.Capacity||target>=RebirthMusicLibraryService.Capacity||source==target)return false;
        Dispatch(player,4,source*RebirthMusicLibraryService.Capacity+target);
        return true; // Dispatch admission, not a delivery or saved-order acknowledgement.
    }
    public static void Dispatch(EntityPlayerLocal player,int operation,int index=0,ItemValue item=null)
    {
        if(player==null)return;
        bool current=EnsureCurrent(player);
        if(operation!=0&&!current)
        { RebirthSurvivorSupportUiFeedback.Receive(false,Localization.Get("xuiRebirthMusicRefresh")); return; }
        if(operation!=0 && PendingTransfer)
        { RebirthSurvivorSupportUiFeedback.Receive(false,Localization.Get("xuiRebirthMusicTransferPending")); return; }
        var connection=SingletonMonoBehaviour<ConnectionManager>.Instance;
        if(player.world != null && player.world.IsRemote())
        {
            var request = NetPackageManager.GetPackage<NetPackageRebirthMusicLibraryRequest>();
            var inventory = operation == 1 ? NetPackageManager.GetPackage<NetPackagePlayerInventory>() : null;
            if (!CanSend(connection, request) || (inventory != null && !CanSend(connection, inventory)))
            {
                RebirthSurvivorSupportUiFeedback.Receive(false, Localization.Get("xuiRebirthMusicUnavailable"));
                return;
            }
            if(inventory != null) connection.SendToServer(inventory.Setup(player,true,true,false,false));
            connection.SendToServer(request.Setup(player.entityId,operation,index,item,Revision,CreationId)); return;
        }
        if(connection == null || !connection.IsServer) return;
        RebirthWorldCharacterRecord record;
        if(operation==1||operation==2)
        {
            RebirthMusicTransferState offer;
            string proof; RebirthMusicSourceProof.TryCreate(item,out proof);
            bool prepared=RebirthMusicTransferServer.PrepareLocalMusicSource(player,operation,index,item?.type??0,item?.Seed??0,Revision,CreationId,proof,out offer);
            bool settled=prepared&&RebirthMusicTransferServer.ApplyLocalPending(player,CreationId);
            RebirthSurvivorSupportUiFeedback.Receive(settled,settled?string.Empty:Localization.Get(prepared?"xuiRebirthMusicTransferPending":"xuiRebirthMusicRefresh"));
        }
        else if(operation==0)
        {
            // Saved custody remains recoverable after closing the UI, unequipping
            // the Walkman, or loading a native player checkpoint with its receipt.
            if(RebirthWorldCharacterService.TryGet(player,out record)&&record?.Support?.PendingMusicTransfer!=null&&record.IsComplete)
                RebirthMusicTransferServer.ApplyLocalPending(player,record.Origin.CreationId);
        }
        else
        {
            string message; bool ok=RebirthMusicLibraryService.TryChange(player,operation,index,item?.type??0,item?.Seed??0,Revision,out message);
            RebirthSurvivorSupportUiFeedback.Receive(ok,message);
        }
        if(RebirthWorldCharacterService.TryGet(player,out record) && record?.Support!=null)
        {
            var ids=new List<string>();var transfer=record.Support.PendingMusicTransfer;
            foreach(var entry in record.Support.MusicCassettes)
                ids.Add(transfer!=null&&!transfer.IsAudiobook&&transfer.Operation==2&&transfer.LibraryIndex==ids.Count?string.Empty:entry.ItemId);
            Receive(record.Origin.CreationId,record.Support.MusicRevision,record.Support.MusicShuffle,ids,transfer!=null,record.Support.AudiobookRevision);
        }
    }
}
