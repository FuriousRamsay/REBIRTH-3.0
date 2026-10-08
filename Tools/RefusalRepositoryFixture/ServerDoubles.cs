using System;using System.Xml.Linq;
public class EntityPlayer{public int entityId=7;}
public class ClientInfo{public PlayerDataFile latestPlayerData=new();}
public static class ThreadManager{public static bool IsMainThread()=>ServerDoubles.Main;}
public class GameManager{public static GameManager Instance;public World World;}
public class World{public WorldState worldState;public bool Remote;public bool IsRemote()=>Remote;}
public class WorldState{public string Guid;}
public class SingletonMonoBehaviour<T>{public static T Instance;}
public class ConnectionManager{public bool IsServer=true;public Clients Clients=new();}
public class Clients{public ClientInfo Sender;public ClientInfo ForEntityId(int id)=>Sender;}
static class ServerDoubles{
public static bool Main=true,Resolve=true,Identity=true,File=true,Upload=true,Base=true,Final=true,ThrowSave;
public static float Phase;public static string Marker;public static RebirthGearPreparationIntent Intent;
public static RebirthGearInventorySnapshot Image;public static RebirthWorldCharacterRecord Record;
public static RebirthStablePlayerIdentity Owner=new(),Peer=new();public static Action DuringSave;public static int Saves;
}
static class RebirthRemoteGearInventorySource{
public static bool TryResolve(EntityPlayer p,ClientInfo c,string creation,out RebirthWorldCharacterRecord r){r=ServerDoubles.Record;return ServerDoubles.Resolve;}
public static bool TryCapture(EntityPlayer p,ClientInfo c,string creation,out RebirthGearInventorySnapshot image){image=ServerDoubles.Image;return ServerDoubles.Upload;}
}
static class RebirthWorldCharacterService{public static bool TryGetIdentity(EntityPlayer p,out RebirthStablePlayerIdentity i){i=ServerDoubles.Owner;return ServerDoubles.Identity;}}
static class RebirthGearPreparationPlayerFileWitness{
public static bool TryReadOriginalPhase(RebirthStablePlayerIdentity i,Guid w,out string m,out RebirthGearPreparationIntent t,out RebirthGearInventorySnapshot image,out float phase){m=ServerDoubles.Marker;t=ServerDoubles.Intent;image=ServerDoubles.Image;phase=ServerDoubles.Phase;return ServerDoubles.File;}
}
static class RebirthWorldCharacterRepository{
public static bool HasSavedGearRefusalRetirementBase(RebirthStablePlayerIdentity i,RebirthWorldCharacterRecord r,RebirthGearPreparationRefusal f)=>ServerDoubles.Base;public static bool HasSavedGearPreparationRefusalRetirement(RebirthStablePlayerIdentity i,RebirthGearPreparationRefusal f)=>ServerDoubles.Final;public static bool IsCurrentCachedRecord(RebirthWorldCharacterRecord r)=>ReferenceEquals(r,ServerDoubles.Record);
public static bool HasSavedUnpreparedGearBase(RebirthStablePlayerIdentity i,RebirthWorldCharacterRecord r,string m)=>ServerDoubles.Base;
public static bool HasSavedGearPreparationRefusal(RebirthStablePlayerIdentity i,RebirthGearPreparationRefusal r)=>ServerDoubles.Final;
public static void SaveIfDirty(RebirthStablePlayerIdentity i,string reason){ServerDoubles.Saves++;ServerDoubles.DuringSave?.Invoke();if(ServerDoubles.ThrowSave)throw new Exception("uncertain save");}
}
public static partial class Program{
static int serverChecks;
static void ServerTests(RebirthWorldSupportState state,string creation,string marker,RebirthGearPreparationRefusal refusal,RebirthGearPreparationIntent intent,RebirthGearInventorySnapshot image){
void S(bool b,string n){Check(b,n);serverChecks++;}
var player=new EntityPlayer();var sender=new ClientInfo();
void Reset(){ServerDoubles.Main=ServerDoubles.Resolve=ServerDoubles.Identity=ServerDoubles.File=ServerDoubles.Upload=ServerDoubles.Base=ServerDoubles.Final=true;ServerDoubles.ThrowSave=false;ServerDoubles.Phase=0;ServerDoubles.Marker=marker;ServerDoubles.Intent=intent;ServerDoubles.Image=image;ServerDoubles.Owner=new();ServerDoubles.Peer=new();ServerDoubles.Record=new(){Support=state.Clone(),Origin=new(){CreationId=creation}};ServerDoubles.Record.Support.PendingGearPreparationRefusal=null;ServerDoubles.Saves=0;ServerDoubles.DuringSave=null;RebirthGearPreparationMarker.TryRead(marker,1,out var world,out _,out _);GameManager.Instance=new(){World=new(){worldState=new(){Guid=world.ToString("N")}}};SingletonMonoBehaviour<ConnectionManager>.Instance=new(){Clients=new(){Sender=sender}};}
bool Run(out RebirthGearPreparationRefusal r)=>RebirthRemoteGearPreparationRefusal.TryRecordStale(player,sender,marker,out r);
Reset();S(Run(out var result)&&result!=null&&ServerDoubles.Saves==1&&ServerDoubles.Record.Touches==1,"server success original refusal only");
Reset();ServerDoubles.Base=false;S(!Run(out result)&&result==null&&ServerDoubles.Record.Support.PendingGearPreparationRefusal==null&&ServerDoubles.Saves==0,"base refuses before stage");
Reset();ServerDoubles.ThrowSave=true;ServerDoubles.Final=false;S(!Run(out result)&&result==null,"uncertain no positive");var retained=ServerDoubles.Record.Support.PendingGearPreparationRefusal;S(retained!=null&&ServerDoubles.Record.Touches==1,"uncertain retained");ServerDoubles.ThrowSave=false;ServerDoubles.Final=true;S(Run(out result)&&ReferenceEquals(result,retained)&&ServerDoubles.Record.Touches==2,"same candidate retry touched");
foreach(var refusalCase in new[]{"main","file","upload","phase","owner","peer","world","sender","notStale","pending","terminal"}){
Reset();switch(refusalCase){case "main":ServerDoubles.Main=false;break;case "file":ServerDoubles.File=false;break;case "upload":ServerDoubles.Upload=false;break;case "phase":ServerDoubles.Phase=1;break;case "owner":ServerDoubles.Owner.StorageKey="foreign";break;case "peer":ServerDoubles.Peer.CanonicalId="foreign";break;case "world":GameManager.Instance.World.worldState.Guid=Guid.NewGuid().ToString();break;case "sender":SingletonMonoBehaviour<ConnectionManager>.Instance.Clients.Sender=new();break;case "notStale":ServerDoubles.Record.Support.GearRevision=intent.ExpectedRevision;break;case "pending":ServerDoubles.Record.Support.PendingMusicTransfer=new();break;case "terminal":var x=new XElement("support",new XElement("gearSettlement",new XAttribute("version",1),new XAttribute("creation",creation),new XAttribute("transaction",intent.TransactionId.ToString("N")),new XAttribute("revision",5),new XAttribute("applied",false)));RebirthGearSettlement.TryRead(x,5,out var terminal);ServerDoubles.Record.Support.LastGearSettlement=terminal;break;}
S(!Run(out result)&&result==null&&ServerDoubles.Saves==0,"server preflight "+refusalCase);}
foreach(var change in new[]{"revision","world","record","image","phase","owner","sender"}){
Reset();ServerDoubles.DuringSave=()=>{switch(change){case "revision":ServerDoubles.Record.Support.GearRevision++;break;case "world":GameManager.Instance.World=new();break;case "record":ServerDoubles.Record=new(){Support=state.Clone(),Origin=new(){CreationId=creation}};break;case "image":ServerDoubles.Image=null;break;case "phase":ServerDoubles.Phase=1;break;case "owner":ServerDoubles.Owner.StorageKey="foreign";break;case "sender":SingletonMonoBehaviour<ConnectionManager>.Instance.Clients.Sender=new();break;}};
S(!Run(out result)&&result==null&&ServerDoubles.Saves==1,"server post-save "+change);}
}
}
