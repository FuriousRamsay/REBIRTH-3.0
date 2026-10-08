using System;using System.Collections.Generic;using System.Security.Cryptography;using System.Text;using System.Reflection;
public class ItemValue{public int type=1;public ushort Seed=5;public string Metadata="exact";public bool IsEmpty(){return type==0;}public ItemValue Clone(){return new ItemValue{type=type,Seed=Seed,Metadata=Metadata};}}
public class World{public bool Remote=true;public EntityPlayerLocal Player;public bool IsRemote(){return Remote;}public EntityPlayerLocal GetPrimaryPlayer(){return Player;}}
public class EntityPlayer{}
public class EntityPlayerLocal:EntityPlayer{public World world;public int entityId=7;public bool Dead;public bool IsDead(){return Dead;}}
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
public class NetPackagePlayerInventory:NetPackage{public NetPackagePlayerInventory Setup(params object[] args){return this;}}
public class NetPackageRebirthMusicLibraryRequest:NetPackage{public int Operation;public ItemValue Item;public string Proof,Creation;public long Revision;public NetPackageRebirthMusicLibraryRequest Setup(int p,int op,int index,ItemValue item,long revision,string creation){Operation=op;Item=item;Revision=revision;Creation=creation;RebirthMusicSourceProof.TryCreate(item,out Proof);return this;}}
public class Channel{public bool Disconnected;public bool IsDisconnected(){return Disconnected;}}
public class ConnectionManager{public bool IsServer,IsConnected=true;public Channel[] Channels=new[]{new Channel()};public List<NetPackage>Sent=new List<NetPackage>();public Channel[]GetConnectionToServer(){return Channels;}public void SendToServer(NetPackage p){Sent.Add(p);}}
public class SingletonMonoBehaviour<T>where T:new(){public static T Instance=new T();}
public static class NetPackageManager{public static T GetPackage<T>()where T:new(){return new T();}}
public static class RebirthMusicLibraryClient{public static string CreationId;public static bool Current,PendingTransfer;public static int Refreshes;public static bool EnsureCurrent(EntityPlayerLocal p){return Current&&p!=null&&ReferenceEquals(p.world,GameManager.Instance.World)&&ReferenceEquals(p,GameManager.Instance.World.Player)&&RebirthSurvivorRequestScope.Matches(CreationId,RebirthSurvivorClientState.Projected);}
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
public static class RebirthMusicLibraryService{public static string Encode(ItemValue v){return Convert.ToBase64String(Encoding.UTF8.GetBytes(v.type+":"+v.Seed+":"+v.Metadata));}}
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
        if(player?.world==null||item==null||item.IsEmpty()||player.IsDead()||RebirthBackpackLibraryReservation.IsHeld(player))return false;
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
        // Clear before dispatch: a delayed snapshot must not send the same Use again.
        var item=pendingUse;ClearUse();
        if(!RequestTransfer(player,true,0,item))
        {useWorld=player.world;useOwner=player.entityId;useCreation=CreationId;pendingUse=item;}
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
        if(player?.world==null||RebirthBackpackLibraryReservation.IsHeld(player))return false;
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
        if(
            !RebirthMusicLibraryClient.EnsureCurrent(player)||Revision<0||
            !RebirthSurvivorRequestScope.Matches(CreationId,RebirthMusicLibraryClient.CreationId)||
            RebirthMusicLibraryClient.PendingTransfer)return false;
        if(insert && (item==null||item.IsEmpty()) || !insert && (index<0||index>=Items.Count))return false;
        var connection=SingletonMonoBehaviour<ConnectionManager>.Instance;
        var request=NetPackageManager.GetPackage<NetPackageRebirthMusicLibraryRequest>();
        var inventory=insert ? NetPackageManager.GetPackage<NetPackagePlayerInventory>() : null;
        if(!RebirthMusicLibraryClient.CanSend(connection,request)||
            insert&&!RebirthMusicLibraryClient.CanSend(connection,inventory))return false;
        if(insert)connection.SendToServer(inventory.Setup(player,true,true,false,false));
        connection.SendToServer(request.Setup(player.entityId,insert?5:6,index,item,Revision,CreationId));
        return true;
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
public static class Check{static int checks;static EntityPlayerLocal p;static ItemValue item;static ConnectionManager conn;static void A(bool ok,string why){checks++;if(!ok)throw new Exception(why);}static ItemValue Queued(){return(ItemValue)typeof(RebirthAudiobookLibraryClient).GetField("pendingUse",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);}static void Setup(bool remote=true){var w=new World{Remote=remote};p=new EntityPlayerLocal{world=w};w.Player=p;GameManager.Instance=new GameManager{World=w};item=new ItemValue();conn=new ConnectionManager{IsServer=!remote};SingletonMonoBehaviour<ConnectionManager>.Instance=conn;RebirthWorldCharacterService.Record=new RebirthWorldCharacterRecord();RebirthSurvivorClientState.Projected=RebirthWorldCharacterService.Record.Origin.CreationId;RebirthMusicLibraryClient.Current=false;RebirthMusicLibraryClient.CreationId=RebirthSurvivorClientState.Projected;RebirthMusicLibraryClient.PendingTransfer=false;RebirthMusicLibraryClient.Refreshes=0;RebirthAudiobookLibraryClient.Reset();RebirthMusicTransferServer.Prepares=RebirthMusicTransferServer.Applies=0;RebirthMusicTransferServer.Applied.Clear();RebirthMusicTransferServer.PrepareResult=RebirthMusicTransferServer.SettleResult=true;RebirthBackpackLibraryReservation.Held=false;RebirthSurvivorSupportUiFeedback.Failures=0;}
static void Snapshot(long rev=2,bool pending=false,string creation=null){RebirthMusicLibraryClient.Receive(creation??RebirthSurvivorClientState.Projected,3,false,new string[0],pending);RebirthAudiobookLibraryClient.Receive(creation??RebirthSurvivorClientState.Projected,rev,new List<string>(),new List<string>());}
static int failures;
static void Case(string name,Action body){try{body();Console.WriteLine("PASS "+name);}catch(Exception e){failures++;Console.WriteLine("FAIL "+name+": "+e.Message);}}
static void Main(){
Case("cold held RequestUse refuses without refresh or intent",()=>{Setup();RebirthBackpackLibraryReservation.Held=true;A(!RebirthAudiobookLibraryClient.RequestUse(p,item)&&conn.Sent.Count==0&&Queued()==null,"live reservation must refuse cold admission");});
Case("warm held RequestUse refuses inventory and op5",()=>{Setup();Snapshot();conn.Sent.Clear();RebirthBackpackLibraryReservation.Held=true;A(!RebirthAudiobookLibraryClient.RequestUse(p,item)&&conn.Sent.Count==0&&Queued()==null,"live reservation must refuse warm admission");});
Case("cold intent retains during held snapshot and sends once after unheld",()=>{Setup();A(RebirthAudiobookLibraryClient.RequestUse(p,item),"initial cold admission");var intent=Queued();RebirthBackpackLibraryReservation.Held=true;Snapshot();A(ReferenceEquals(intent,Queued())&&conn.Sent.Count==1,"held callback must preserve original queued intent without send");Snapshot();A(ReferenceEquals(intent,Queued())&&conn.Sent.Count==1,"repeated held callback remains unsent");RebirthBackpackLibraryReservation.Held=false;Snapshot();A(Queued()==null&&conn.Sent.Count==3&&conn.Sent[1]is NetPackagePlayerInventory&&((NetPackageRebirthMusicLibraryRequest)conn.Sent[2]).Operation==5,"unheld callback admits inventory then op5 exactly once");Snapshot();A(conn.Sent.Count==3,"duplicate accepted snapshot must not replay admission");});
Case("direct held insertion refuses",()=>{Setup();Snapshot();conn.Sent.Clear();RebirthBackpackLibraryReservation.Held=true;A(!RebirthAudiobookLibraryClient.RequestTransfer(p,true,0,item)&&conn.Sent.Count==0,"direct insertion must refuse before sends");});
Case("direct held removal refuses",()=>{Setup();Snapshot();RebirthAudiobookLibraryClient.Items.Add("stored");RebirthAudiobookLibraryClient.SlotIds.Add("22222222222222222222222222222222");conn.Sent.Clear();RebirthBackpackLibraryReservation.Held=true;A(!RebirthAudiobookLibraryClient.RequestTransfer(p,false,0,null)&&conn.Sent.Count==0,"direct removal must refuse before send");});
Console.WriteLine("SUMMARY cases=5 assertions="+checks+" failures="+failures+"; actual complete client source; native world/item/transport/reservation/server/playback adapted; no native custody or gameplay acceptance");Environment.ExitCode=failures==0?0:1;
}}