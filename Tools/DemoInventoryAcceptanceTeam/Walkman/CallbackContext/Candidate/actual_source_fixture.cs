using System.IO;using System;using System.Collections.Generic;using System.Security.Cryptography;using System.Text;using System.Reflection;
public class ItemValue{public int type=1;public ushort Seed=5;public string Metadata="exact";public bool IsEmpty(){return type==0;}public ItemValue Clone(){return new ItemValue{type=type,Seed=Seed,Metadata=Metadata};}}
public class World{public bool Remote=true;public EntityPlayerLocal Player;public bool IsRemote(){return Remote;}public EntityPlayerLocal GetPrimaryPlayer(){return Player;}public EntityPlayerLocal GetEntity(int id){return Player!=null&&Player.entityId==id?Player:null;}}
public class EntityPlayer{}
public class EntityPlayerLocal:EntityPlayer{public World world;public int entityId=7;public bool Spawned=true;public bool IsSpawned(){return Spawned;}public Serializer inventory=new Serializer(),bag=new Serializer(),equipment=new Serializer();public ItemStack DragAndDropItem=new ItemStack();public bool Dead;public bool IsDead(){return Dead;}}
public class GameManager{public static GameManager Instance;public World World;}
public class RebirthMusicCassetteState{public string ItemId;}
public class RebirthAudiobookCassetteState{public string SlotId,ItemId;}
public class RebirthWorldSupportState{public long MusicRevision=3,AudiobookRevision=2;public bool MusicShuffle;public object PendingMusicTransfer;public List<RebirthMusicCassetteState> MusicCassettes=new List<RebirthMusicCassetteState>();public List<RebirthAudiobookCassetteState>AudiobookCassettes=new List<RebirthAudiobookCassetteState>();}
public class Origin{public string CreationId="11111111111111111111111111111111";}
public class RebirthWorldCharacterRecord{public bool IsComplete=true;public Origin Origin=new Origin();public RebirthWorldSupportState Support=new RebirthWorldSupportState();}
public static class RebirthWorldCharacterService{public static RebirthWorldCharacterRecord Record;public static bool TryGet(EntityPlayerLocal p,out RebirthWorldCharacterRecord r){r=Record;return r!=null;}}
public static class RebirthSurvivorClientState{public static string Projected="11111111111111111111111111111111";public static string GetProjectedCreationId(EntityPlayerLocal p){return Projected;}}
public static class RebirthBackpackLibraryReservation{public static bool Held;public static bool IsHeld(EntityPlayerLocal p){return Held;}}
public static class RebirthSurvivorSupportUiFeedback{public static int Failures;public static void Receive(bool ok,string m){if(!ok)Failures++;}}
public static class Localization{public static string Get(string s){return s;}}
public class NetPackage{public int Channel;}
public class NetPackagePlayerInventory:NetPackage{public MemoryStream inventoryData,bagData,equipmentData;public ItemStack dragAndDropItem;public NetPackagePlayerInventory Setup(EntityPlayerLocal _player, bool _changedToolbelt, bool _changedBag, bool _changedEquipment, bool _changedDragAndDropItem)
	{
		if (_changedToolbelt)
		{
			inventoryData = StreamUtils.ToBlob(delegate(PooledBinaryWriter pbw)
			{
				_player.inventory.Write(pbw, StreamModeWrite.Persistency);
			});
		}
		if (_changedBag)
		{
			bagData = StreamUtils.ToBlob(delegate(PooledBinaryWriter pbw)
			{
				_player.bag.Write(pbw, StreamModeWrite.Persistency);
			});
		}
		if (_changedEquipment)
		{
			equipmentData = StreamUtils.ToBlob(delegate(PooledBinaryWriter pbw)
			{
				_player.equipment.Write(pbw, StreamModeWrite.Persistency);
			});
		}
		if (_changedDragAndDropItem)
		{
			dragAndDropItem = _player.DragAndDropItem.Clone();
		}
		return this;
	}}
public class NetPackageRebirthMusicLibraryRequest:NetPackage{private int playerId,operation,index,itemType;private ushort seed;private long revision;private string expectedCreationId,sourceProof;public int Operation=>operation;public string Proof=>sourceProof;public string Creation=>expectedCreationId;public long Revision=>revision;    public NetPackageRebirthMusicLibraryRequest Setup(int player, int op, int slot, ItemValue item, long expected, string creation)
    { playerId=player; operation=op; index=slot; itemType=item?.type??0; seed=item?.Seed??0; revision=expected; expectedCreationId=creation; sourceProof=string.Empty; if(op==1||op==5)RebirthMusicSourceProof.TryCreate(item,out sourceProof); return this; }}
public class Channel{public bool Disconnected;public bool IsDisconnected(){return Disconnected;}}
public class ConnectionManager{public bool IsServer,IsConnected=true;public Channel[] Channels=new[]{new Channel()};public List<NetPackage>Sent=new List<NetPackage>();public Channel[]GetConnectionToServer(){return Channels;}public void SendToServer(NetPackage p){Hooks.SendCalls++;if(Hooks.ThrowSend==Hooks.SendCalls)throw new Exception("injected uncertain send");Sent.Add(p);}}
public class SingletonMonoBehaviour<T>where T:new(){public static T Instance=new T();}
public static class NetPackageManager{public static T GetPackage<T>()where T:new(){Hooks.OnAllocate?.Invoke();return new T();}}
public static class RebirthMusicLibraryClient{public static long Generation;public static string CreationId;public static bool Current,PendingTransfer;public static int Refreshes;public static bool EnsureCurrent(EntityPlayerLocal p){return Current&&p!=null&&ReferenceEquals(p.world,GameManager.Instance.World)&&ReferenceEquals(p,GameManager.Instance.World.Player)&&RebirthSurvivorRequestScope.Matches(CreationId,RebirthSurvivorClientState.Projected);}
    internal static bool CanSend(ConnectionManager connection, NetPackage packet)
    {
        if (connection == null || connection.IsServer || !connection.IsConnected || packet == null) return false;
        var channels = connection.GetConnectionToServer();
        int channel = packet.Channel;
        return channels != null && channel >= 0 && channel < channels.Length &&
            channels[channel] != null && !channels[channel].IsDisconnected();
    }
public static void Receive(string creation,long rev,bool shuffle,IEnumerable<string>ids,bool pending){CreationId=creation;Current=true;PendingTransfer=pending;}
public static void Dispatch(EntityPlayerLocal p,int op){if(op!=0)throw new Exception("music manual");Refreshes++;if(!p.world.IsRemote()){RebirthMusicTransferServer.ApplyLocalPending(p,CreationId);RebirthAudiobookLibraryClient.RefreshLocal(p);}else SingletonMonoBehaviour<ConnectionManager>.Instance.SendToServer(new NetPackageRebirthMusicLibraryRequest().Setup(p.entityId,0,0,null,-1,CreationId));}}
public static class RebirthMusicLibraryService{public static string Encode(ItemValue v){Hooks.Proof?.Invoke();return Convert.ToBase64String(Encoding.UTF8.GetBytes(v.type+":"+v.Seed+":"+v.Metadata));}}
public class RebirthMusicTransferState{}
public static class RebirthMusicTransferServer{public static int Prepares,Applies;public static bool PrepareResult=true,SettleResult=true;public static object OriginalPending;public static List<object>Applied=new List<object>();public static string LastProof;public static bool PrepareLocalAudiobookSource(EntityPlayerLocal p,int op,int index,int type,ushort seed,long rev,string creation,string proof,out RebirthMusicTransferState offer){offer=new RebirthMusicTransferState();LastProof=proof;if(!PrepareResult)return false;if(RebirthWorldCharacterService.Record.Support.PendingMusicTransfer==null){Prepares++;OriginalPending=new object();RebirthWorldCharacterService.Record.Support.PendingMusicTransfer=OriginalPending;}return true;}public static bool ApplyLocalPending(EntityPlayerLocal p,string creation){if(RebirthWorldCharacterService.Record.Support.PendingMusicTransfer==null)return false;Applies++;Applied.Add(RebirthWorldCharacterService.Record.Support.PendingMusicTransfer);if(SettleResult){RebirthWorldCharacterService.Record.Support.PendingMusicTransfer=null;RebirthWorldCharacterService.Record.Support.AudiobookRevision++;return true;}return false;}}
public static class RebirthAudiobookListeningSessionService{public static bool TryResume(EntityPlayerLocal p,out string m){m="";return true;}public static bool TryPause(EntityPlayerLocal p,out string m){m="";return true;}public static bool TryBeginStored(EntityPlayerLocal p,string slot,out string m){m="";return true;}}
public static class RebirthAudiobookLibraryPersistence{public const int Capacity=24;}
public static class RebirthSurvivorRequestScope
{
    public static bool TryNormalize(string value,out string normalized)
    {
        normalized=null;
        if(Guid.TryParse(value,out var id)&&id!=Guid.Empty){normalized=id.ToString("N");return true;}
        if(IsLegacyCreation(value)){normalized=value;return true;}
        return false;
    }
    public static bool Matches(string requestedCreation, string currentCreation)
    {
        Guid requested, current;
        if(Guid.TryParse(requestedCreation,out requested)&&requested!=Guid.Empty)
            return Guid.TryParse(currentCreation,out current)&&requested==current;
        // Schema-1 migration generated a stable legacy- plus lowercase SHA256 ID.
        // Compare the complete persisted identity; never truncate it into a wire GUID.
        return IsLegacyCreation(requestedCreation)&&
            string.Equals(requestedCreation,currentCreation,StringComparison.Ordinal);
    }
    private static bool IsLegacyCreation(string value)
    {
        if(value==null||value.Length!=71||!value.StartsWith("legacy-",StringComparison.Ordinal))return false;
        for(int i=7;i<value.Length;i++)
            if(!(value[i]>='0'&&value[i]<='9'||value[i]>='a'&&value[i]<='f'))return false;
        return true;
    }
}
public static class RebirthMusicSourceProof
{
    public const int Length=64;
    public static bool TryCreate(ItemValue value,out string proof)
    {
        proof=string.Empty;
        if(value==null||value.IsEmpty())return false;
        try
        {
            byte[] native=Convert.FromBase64String(RebirthMusicLibraryService.Encode(value));
            if(native.Length==0)return false;
            using(var hash=SHA256.Create())
            {
                var bytes=hash.ComputeHash(native);
                var text=new StringBuilder(Length);
                foreach(byte part in bytes)text.Append(part.ToString("x2",System.Globalization.CultureInfo.InvariantCulture));
                proof=text.ToString();return true;
            }
        }
        catch(Exception){return false;}
    }
    public static bool IsValid(string proof)
    {
        if(proof==null||proof.Length!=Length)return false;
        foreach(char c in proof)if(!(c>='0'&&c<='9')&&!(c>='a'&&c<='f'))return false;
        return true;
    }
    public static bool Matches(ItemValue value,string expected)
    {
        string actual;
        return IsValid(expected)&&TryCreate(value,out actual)&&string.Equals(actual,expected,StringComparison.Ordinal);
    }
}
public static class RebirthAudiobookLibraryClient
{
    public static string CreationId {get;private set;}=string.Empty;
    public static long Revision {get;private set;}=-1;
    public static readonly List<string> SlotIds=new List<string>();
    public static readonly List<string> Items=new List<string>();
    private static World useWorld;
    private static int useOwner;
    private static string useCreation=string.Empty;
    private static ItemValue pendingUse;
    public static bool RequestUse(EntityPlayerLocal player,ItemValue item)
    {
        if(player?.world==null||item==null||item.IsEmpty()||player.IsDead()||pendingUse!=null||remoteTransferPreparing||RebirthBackpackLibraryReservation.IsHeld(player))return false;
        bool current=RebirthMusicLibraryClient.EnsureCurrent(player);
        if(!player.world.IsRemote())
        {if(!current||Revision<0)RefreshLocal(player);return RequestTransfer(player,true,0,item);}
        if(current&&Revision>=0)return RequestTransfer(player,true,0,item);
        string creation=RebirthSurvivorClientState.GetProjectedCreationId(player);
        string canonical;
        if(!RebirthSurvivorRequestScope.TryNormalize(creation,out canonical)||pendingUse!=null)return false;
        var connection=SingletonMonoBehaviour<ConnectionManager>.Instance;
        var request=NetPackageManager.GetPackage<NetPackageRebirthMusicLibraryRequest>();
        if(!RebirthMusicLibraryClient.CanSend(connection,request))return false;
        useWorld=player.world;useOwner=player.entityId;useCreation=canonical;pendingUse=item.Clone();
        RebirthMusicLibraryClient.Dispatch(player,0);
        return true;
    }
    private static void ClearUse(){useWorld=null;useOwner=0;useCreation=string.Empty;pendingUse=null;}
    private static void ApplyPendingUse(EntityPlayerLocal player)
    {
        if(pendingUse==null)return;
        if(player==null||player.IsDead()||!ReferenceEquals(player.world,useWorld)||player.entityId!=useOwner||
            !RebirthSurvivorRequestScope.Matches(useCreation,CreationId)){ClearUse();return;}
        if(RebirthMusicLibraryClient.PendingTransfer||RebirthBackpackLibraryReservation.IsHeld(player))return;
        // Keep unsent intent scoped until both packages are prepared and admitted.
        TryDispatchRemoteTransfer(player,true,0,pendingUse,true);
    }
    public static bool RequestPlayback(EntityPlayerLocal player,bool resume)
    {
        if(player?.world==null||!RebirthMusicLibraryClient.EnsureCurrent(player)||Revision<0||RebirthMusicLibraryClient.PendingTransfer)return false;
        if(!player.world.IsRemote())
        {
            RebirthWorldCharacterRecord record;
            if(!RebirthWorldCharacterService.TryGet(player,out record)||record?.Support==null||
                record.Support.AudiobookRevision!=Revision||!RebirthSurvivorRequestScope.Matches(CreationId,record.Origin?.CreationId))return false;
            string message;bool ok=resume ? RebirthAudiobookListeningSessionService.TryResume(player,out message)
                : RebirthAudiobookListeningSessionService.TryPause(player,out message);
            RebirthSurvivorSupportUiFeedback.Receive(ok,message);return ok;
        }
        var connection=SingletonMonoBehaviour<ConnectionManager>.Instance;
        var request=NetPackageManager.GetPackage<NetPackageRebirthMusicLibraryRequest>();
        if(!RebirthMusicLibraryClient.CanSend(connection,request))return false;
        connection.SendToServer(request.Setup(player.entityId,resume?9:8,0,null,Revision,CreationId));return true;
    }
    public static bool RequestListen(EntityPlayerLocal player,int index,string slotId)
    {
        if(player?.world==null||!RebirthMusicLibraryClient.EnsureCurrent(player)||Revision<0||
            RebirthMusicLibraryClient.PendingTransfer||index<0||index>=SlotIds.Count||SlotIds[index]!=slotId)return false;
        if(!player.world.IsRemote())
        {
            RebirthWorldCharacterRecord record;
            if(!RebirthWorldCharacterService.TryGet(player,out record)||record?.Support==null||
                record.Support.AudiobookRevision!=Revision||!RebirthSurvivorRequestScope.Matches(CreationId,record.Origin?.CreationId))return false;
            string message;return RebirthAudiobookListeningSessionService.TryBeginStored(player,slotId,out message);
        }
        var connection=SingletonMonoBehaviour<ConnectionManager>.Instance;
        var request=NetPackageManager.GetPackage<NetPackageRebirthMusicLibraryRequest>();
        if(!RebirthMusicLibraryClient.CanSend(connection,request))return false;
        connection.SendToServer(request.Setup(player.entityId,7,index,null,Revision,CreationId));return true;
    }
    public static bool RequestTransfer(EntityPlayerLocal player,bool insert,int index,ItemValue item)
    {
        if(remoteTransferPreparing||player?.world==null||RebirthBackpackLibraryReservation.IsHeld(player))return false;
        if(!player.world.IsRemote())
        {
            RebirthWorldCharacterRecord record;
            if(!RebirthWorldCharacterService.TryGet(player,out record)||record?.Support==null||!record.IsComplete)return false;
            if(!RebirthMusicLibraryClient.EnsureCurrent(player)||Revision<0||
                !RebirthSurvivorRequestScope.Matches(CreationId,record.Origin?.CreationId))
            {RefreshLocal(player);return false;}
            RebirthMusicTransferState offer;
            string sourceProof; RebirthMusicSourceProof.TryCreate(item,out sourceProof);
            if(!RebirthMusicTransferServer.PrepareLocalAudiobookSource(player,insert?1:2,index,item?.type??0,item?.Seed??0,
                Revision,CreationId,sourceProof,out offer))return false;
            bool settled=RebirthMusicTransferServer.ApplyLocalPending(player,CreationId);
            RefreshLocal(player);
            return settled;
        }
        return TryDispatchRemoteTransfer(player,insert,index,item,false);
    }
    private static bool remoteTransferPreparing;
    // Admission consumes queued intent. Native sends provide no delivery acknowledgement.
    private static bool TryDispatchRemoteTransfer(EntityPlayerLocal player,bool insert,int index,ItemValue item,bool clearQueued)
    {
        if(remoteTransferPreparing||player?.world==null||player.IsDead()||!player.IsSpawned()||!player.world.IsRemote()||
            RebirthBackpackLibraryReservation.IsHeld(player)||!RebirthMusicLibraryClient.EnsureCurrent(player)||Revision<0||
            !RebirthSurvivorRequestScope.Matches(CreationId,RebirthMusicLibraryClient.CreationId)||RebirthMusicLibraryClient.PendingTransfer)return false;
        if(insert&&(item==null||item.IsEmpty())||!insert&&(index<0||index>=Items.Count||index>=SlotIds.Count))return false;
        var game=GameManager.Instance;var world=player.world;var owner=player.entityId;
        var creation=CreationId;var revision=Revision;var generation=RebirthMusicLibraryClient.Generation;
        var slotId=insert?null:SlotIds[index];var itemId=insert?null:Items[index];
        var connection=SingletonMonoBehaviour<ConnectionManager>.Instance;
        if(game==null||!ReferenceEquals(game.World,world)||!ReferenceEquals(world.GetPrimaryPlayer(),player)||!ReferenceEquals(world.GetEntity(owner),player)||
            clearQueued&&!ReferenceEquals(pendingUse,item))return false;
        remoteTransferPreparing=true;
        try
        {
            var request=NetPackageManager.GetPackage<NetPackageRebirthMusicLibraryRequest>();
            var inventory=insert?NetPackageManager.GetPackage<NetPackagePlayerInventory>():null;
            if(!RebirthMusicLibraryClient.CanSend(connection,request)||insert&&!RebirthMusicLibraryClient.CanSend(connection,inventory))return false;
            request.Setup(owner,insert?5:6,index,item,revision,creation);
            if(insert)inventory.Setup(player,true,true,false,false);
            // Native Setup invokes serializers. Reject changed context before either send.
            if(!ReferenceEquals(game,GameManager.Instance)||!ReferenceEquals(world,game.World)||!ReferenceEquals(world,player.world)||
                !ReferenceEquals(world.GetPrimaryPlayer(),player)||!ReferenceEquals(world.GetEntity(owner),player)||player.entityId!=owner||player.IsDead()||!player.IsSpawned()||!world.IsRemote()||
                !RebirthMusicLibraryClient.EnsureCurrent(player)||Revision!=revision||CreationId!=creation||
                RebirthMusicLibraryClient.Generation!=generation||RebirthMusicLibraryClient.PendingTransfer||
                RebirthBackpackLibraryReservation.IsHeld(player)||!ReferenceEquals(connection,SingletonMonoBehaviour<ConnectionManager>.Instance)||
                !RebirthMusicLibraryClient.CanSend(connection,request)||insert&&!RebirthMusicLibraryClient.CanSend(connection,inventory)||
                !insert&&(index>=SlotIds.Count||SlotIds[index]!=slotId||index>=Items.Count||Items[index]!=itemId)||clearQueued&&!ReferenceEquals(pendingUse,item))return false;
            if(clearQueued)ClearUse();
            if(insert)connection.SendToServer(inventory);
            connection.SendToServer(request);
            return true;
        }
        finally {remoteTransferPreparing=false;}
    }
    public static void RefreshLocal(EntityPlayerLocal player)
    {
        if(player?.world==null||player.world.IsRemote())return;
        RebirthWorldCharacterRecord record;
        if(!RebirthWorldCharacterService.TryGet(player,out record)||record?.Support==null||!record.IsComplete)return;
        var music=new List<string>();foreach(var entry in record.Support.MusicCassettes)music.Add(entry.ItemId);
        RebirthMusicLibraryClient.Receive(record.Origin.CreationId,record.Support.MusicRevision,record.Support.MusicShuffle,music,record.Support.PendingMusicTransfer!=null);
        var slots=new List<string>();var ids=new List<string>();
        foreach(var entry in record.Support.AudiobookCassettes){slots.Add(entry.SlotId);ids.Add(entry.ItemId);}
        Receive(record.Origin.CreationId,record.Support.AudiobookRevision,slots,ids);
    }
    public static void Reset(){ClearUse();CreationId=string.Empty;Revision=-1;SlotIds.Clear();Items.Clear();}
    public static void Receive(string creationId,long revision,IList<string> slots,IList<string> ids)
    {
        var player=GameManager.Instance?.World?.GetPrimaryPlayer();
        if(!RebirthMusicLibraryClient.EnsureCurrent(player)||
            !RebirthSurvivorRequestScope.Matches(creationId,RebirthMusicLibraryClient.CreationId)||revision<0||
            CreationId==creationId&&revision<Revision||slots==null||ids==null||slots.Count!=ids.Count||slots.Count>RebirthAudiobookLibraryPersistence.Capacity)return;
        var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for(int i=0;i<slots.Count;i++)
        {Guid slot;if(!Guid.TryParseExact(slots[i],"N",out slot)||slot==Guid.Empty||!seen.Add(slots[i])||string.IsNullOrEmpty(ids[i]))return;}
        CreationId=creationId;Revision=revision;SlotIds.Clear();SlotIds.AddRange(slots);Items.Clear();Items.AddRange(ids);
        ApplyPendingUse(player);
    }
}
public static class ItemActionListenAudiobookRebirth{
    internal static void Dispatch(EntityPlayer player,ItemValue itemValue)
    {
        var local=player as EntityPlayerLocal;
        if(local?.world==null||itemValue==null)return;
        if(RebirthBackpackLibraryReservation.IsHeld(local))
        {RebirthSurvivorSupportUiFeedback.Receive(false,Localization.Get("xuiRebirthLibraryTransferPending"));return;}
        if(!RebirthAudiobookLibraryClient.RequestUse(local,itemValue))
        {
            RebirthMusicLibraryClient.Dispatch(local,0);
            RebirthSurvivorSupportUiFeedback.Receive(false,Localization.Get("xuiRebirthMusicRefresh"));
        }
    }
}
public static class Check{static int checks;static EntityPlayerLocal p;static ItemValue item;static ConnectionManager conn;static void A(bool ok,string why){checks++;if(!ok)throw new Exception(why);}static ItemValue Queued(){return(ItemValue)typeof(RebirthAudiobookLibraryClient).GetField("pendingUse",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);}static void Setup(bool remote=true){Hooks.Reset();RebirthMusicLibraryClient.Generation=3;var w=new World{Remote=remote};p=new EntityPlayerLocal{world=w};w.Player=p;GameManager.Instance=new GameManager{World=w};item=new ItemValue();conn=new ConnectionManager{IsServer=!remote};SingletonMonoBehaviour<ConnectionManager>.Instance=conn;RebirthWorldCharacterService.Record=new RebirthWorldCharacterRecord();RebirthSurvivorClientState.Projected=RebirthWorldCharacterService.Record.Origin.CreationId;RebirthMusicLibraryClient.Current=false;RebirthMusicLibraryClient.CreationId=RebirthSurvivorClientState.Projected;RebirthMusicLibraryClient.PendingTransfer=false;RebirthMusicLibraryClient.Refreshes=0;RebirthAudiobookLibraryClient.Reset();RebirthMusicTransferServer.Prepares=RebirthMusicTransferServer.Applies=0;RebirthMusicTransferServer.Applied.Clear();RebirthMusicTransferServer.PrepareResult=RebirthMusicTransferServer.SettleResult=true;RebirthBackpackLibraryReservation.Held=false;RebirthSurvivorSupportUiFeedback.Failures=0;}
static void Snapshot(long rev=2,bool pending=false,string creation=null){RebirthMusicLibraryClient.Receive(creation??RebirthSurvivorClientState.Projected,3,false,new string[0],pending);RebirthAudiobookLibraryClient.Receive(creation??RebirthSurvivorClientState.Projected,rev,new List<string>(),new List<string>());}
static int failures;static void Case(string n,Action a){try{a();Console.WriteLine("PASS "+n);}catch(Exception e){failures++;Console.WriteLine("FAIL "+n+": "+e.Message);}}
static void Main(){

foreach(int mode in new[]{0,1,2,3,4,5,6,7,8,9,10,11,12}){int m=mode;Case("inventory serializer context refusal "+m,()=>{Setup();Snapshot();conn.Sent.Clear();Hooks.Serialize=()=>{Hooks.Serialize=null;switch(m){case 0:p.world=new World();break;case 1:GameManager.Instance.World=new World{Player=p};break;case 2:p.world.Player=new EntityPlayerLocal();break;case 3:p.entityId=8;break;case 4:p.Dead=true;break;case 5:RebirthSurvivorClientState.Projected="55555555555555555555555555555555";break;case 6:RebirthAudiobookLibraryClient.Receive(RebirthSurvivorClientState.Projected,3,new List<string>(),new List<string>());break;case 7:RebirthMusicLibraryClient.Generation++;break;case 8:SingletonMonoBehaviour<ConnectionManager>.Instance=new ConnectionManager();break;case 9:RebirthBackpackLibraryReservation.Held=true;break;case 10:RebirthMusicLibraryClient.PendingTransfer=true;break;case 11:conn.Channels[0].Disconnected=true;break;case 12:GameManager.Instance=new GameManager{World=p.world};break;}};A(!RebirthAudiobookLibraryClient.RequestTransfer(p,true,0,item)&&conn.Sent.Count==0,"must refuse before any inventory/request send");});}
Case("request proof Setup completes before first send",()=>{Setup();Snapshot();conn.Sent.Clear();bool before=false;Hooks.Proof=()=>{Hooks.Proof=null;before=conn.Sent.Count==0;RebirthBackpackLibraryReservation.Held=true;};A(!RebirthAudiobookLibraryClient.RequestTransfer(p,true,0,item)&&conn.Sent.Count==0&&before,"proof-induced hold refuses both packages before any send");});
Case("reentrant warm insertion suppressed",()=>{Setup();Snapshot();conn.Sent.Clear();bool nested=true;Hooks.Serialize=()=>{Hooks.Serialize=null;nested=RebirthAudiobookLibraryClient.RequestTransfer(p,true,0,item);};A(RebirthAudiobookLibraryClient.RequestTransfer(p,true,0,item),"outer stable insertion admits");A(!nested&&conn.Sent.Count==2,"nested must refuse and outer sends one pair");});
Case("deferred preadmission serializer exception retains original intent",()=>{Setup();RebirthAudiobookLibraryClient.RequestUse(p,item);var intent=Queued();Hooks.Serialize=()=>{throw new Exception("injected serializer fault");};bool threw=false;try{Snapshot();}catch(Exception){threw=true;}A(threw&&ReferenceEquals(intent,Queued())&&conn.Sent.Count==1,"unsent intent must remain after Setup exception");Hooks.Serialize=null;Snapshot();A(Queued()==null&&conn.Sent.Count==3,"finally guard releases; later valid snapshot admits once");});
Case("deferred replaced scope never rebinds old intent",()=>{Setup();RebirthAudiobookLibraryClient.RequestUse(p,item);Hooks.Serialize=()=>{Hooks.Serialize=null;RebirthSurvivorClientState.Projected="55555555555555555555555555555555";RebirthMusicLibraryClient.CreationId=RebirthSurvivorClientState.Projected;RebirthAudiobookLibraryClient.Reset();RebirthAudiobookLibraryClient.Receive(RebirthSurvivorClientState.Projected,2,new List<string>(),new List<string>());};Snapshot();A(conn.Sent.Count==1&&Queued()==null,"no send and no restored old item in replacement scope");});
Case("removal preserves selected slot after pool callback",()=>{Setup();Snapshot();RebirthAudiobookLibraryClient.Items.Add("old");RebirthAudiobookLibraryClient.SlotIds.Add("22222222222222222222222222222222");conn.Sent.Clear();Hooks.OnAllocate=()=>{Hooks.OnAllocate=null;RebirthAudiobookLibraryClient.SlotIds[0]="33333333333333333333333333333333";};A(!RebirthAudiobookLibraryClient.RequestTransfer(p,false,0,null)&&conn.Sent.Count==0,"changed slot must refuse op6");});
Case("reentrant cold producer cannot replace original intent",()=>{Setup();RebirthAudiobookLibraryClient.RequestUse(p,item);var intent=Queued();bool nested=true;Hooks.Serialize=()=>{Hooks.Serialize=null;RebirthMusicLibraryClient.Current=false;nested=RebirthAudiobookLibraryClient.RequestUse(p,new ItemValue{Metadata="new"});RebirthMusicLibraryClient.Current=true;};Snapshot();A(!nested&&conn.Sent.Count==3&&Queued()==null,"nested producer refuses original outer admission stays unique");});
Case("unspawned during inventory Setup refused",()=>{Setup();Snapshot();conn.Sent.Clear();Hooks.Serialize=()=>{Hooks.Serialize=null;p.Spawned=false;};A(!RebirthAudiobookLibraryClient.RequestTransfer(p,true,0,item)&&conn.Sent.Count==0,"unspawned owner refuses both sends");});
Case("removal item identity preserved after pool callback",()=>{Setup();Snapshot();RebirthAudiobookLibraryClient.Items.Add("old");RebirthAudiobookLibraryClient.SlotIds.Add("22222222222222222222222222222222");conn.Sent.Clear();Hooks.OnAllocate=()=>{Hooks.OnAllocate=null;RebirthAudiobookLibraryClient.Items[0]="new";};A(!RebirthAudiobookLibraryClient.RequestTransfer(p,false,0,null)&&conn.Sent.Count==0,"changed item must refuse op6");});
Case("Reset cannot release preparing guard for nested cold use",()=>{Setup();RebirthAudiobookLibraryClient.RequestUse(p,item);bool nested=true;Hooks.Serialize=()=>{Hooks.Serialize=null;RebirthAudiobookLibraryClient.Reset();RebirthMusicLibraryClient.Current=false;nested=RebirthAudiobookLibraryClient.RequestUse(p,new ItemValue());RebirthMusicLibraryClient.Current=true;};Snapshot();A(!nested&&conn.Sent.Count==1&&Queued()==null,"Reset invalidates intent; busy guard remains until outer finally");});
Case("stable admission ordering and duplicate replay control",()=>{Setup();RebirthAudiobookLibraryClient.RequestUse(p,item);Snapshot();A(conn.Sent.Count==3&&conn.Sent[1]is NetPackagePlayerInventory&&((NetPackageRebirthMusicLibraryRequest)conn.Sent[2]).Operation==5&&Queued()==null,"one complete pair after stable preparation");Snapshot();A(conn.Sent.Count==3,"no admitted deferred replay");});Case("healthy direct removal admits selected slot once",()=>{Setup();Snapshot();RebirthAudiobookLibraryClient.Items.Add("old");RebirthAudiobookLibraryClient.SlotIds.Add("22222222222222222222222222222222");conn.Sent.Clear();A(RebirthAudiobookLibraryClient.RequestTransfer(p,false,0,null)&&conn.Sent.Count==1&&((NetPackageRebirthMusicLibraryRequest)conn.Sent[0]).Operation==6,"stable stored removal sends one op6");});foreach(int throwAt in new[]{1,2}){int at=throwAt;Case("uncertain admitted send never restores intent "+at,()=>{Setup();RebirthAudiobookLibraryClient.RequestUse(p,item);Hooks.SendCalls=0;Hooks.ThrowSend=at;bool threw=false;try{Snapshot();}catch(Exception){threw=true;}A(threw&&Queued()==null,"admitted intent stays cleared after send exception");int sent=conn.Sent.Count;Hooks.ThrowSend=0;Snapshot();A(conn.Sent.Count==sent,"later snapshot never replays ambiguous admission");});}
Console.WriteLine("SUMMARY cases=26 assertions="+checks+" failures="+failures+"; complete actual audio client/request Setup/native inventory Setup/ToBlob; injected adapter serializer/pool context changes; no observed native replacement, custody, MP or gameplay");Environment.ExitCode=failures==0?0:1;}}
public enum StreamModeWrite{Persistency}public class ItemStack{public ItemStack Clone(){return new ItemStack();}}public class Serializer{public void Write(PooledBinaryWriter w,StreamModeWrite m){Hooks.Serialize?.Invoke();w.Stream.WriteByte(123);}}public class PooledBinaryWriter:IDisposable{public Stream Stream;public void SetBaseStream(Stream s){Stream=s;}public void Dispose(){}}public class Pool{public PooledBinaryWriter AllocSync(bool _bReset){return new PooledBinaryWriter();}}public static class MemoryPools{public static Pool poolBinaryWriter=new Pool();}public static class StreamUtils{public static MemoryStream ToBlob(Action<PooledBinaryWriter> _write)
	{
		MemoryStream memoryStream = new MemoryStream();
		using PooledBinaryWriter pooledBinaryWriter = MemoryPools.poolBinaryWriter.AllocSync(_bReset: false);
		pooledBinaryWriter.SetBaseStream(memoryStream);
		_write(pooledBinaryWriter);
		return memoryStream;
	}}public static class Hooks{public static Action Serialize,Proof,OnAllocate;public static int SendCalls,ThrowSend;public static void Reset(){Serialize=Proof=OnAllocate=null;SendCalls=ThrowSend=0;}}