// AUTHORED_NOT_RUN. Actual hydration journal + actual underlying WAL and models.
// ACTUAL_PLAN_SOURCES
internal class EntityPlayer {internal World world;internal int entityId=1,Died=2;internal bool Dead;internal bool IsDead()=>Dead;internal string Creation=Guid.NewGuid().ToString("N"),Owner="account";}
internal sealed class EntityPlayerLocal:EntityPlayer {}
internal sealed class World {internal string Guid=System.Guid.NewGuid().ToString("N");internal bool Remote;internal EntityPlayer Actor,Primary;internal bool IsRemote()=>Remote;internal EntityPlayer GetEntity(int id)=>id==1?Actor:null;internal EntityPlayer GetPrimaryPlayer()=>Primary;}
internal sealed class GameManager {internal static GameManager Instance=new GameManager();internal World World;}
internal static class ThreadManager {internal static bool Main=true;internal static bool IsMainThread()=>Main;}
internal sealed class NativeHydrationTransport {internal bool Disconnected;internal bool IsDisconnected()=>Disconnected;}
internal sealed class ClientInfo {internal bool loginDone=true,disconnecting;internal string Owner="account";internal NativeHydrationTransport[] netConnection=new[]{new NativeHydrationTransport()};}
internal sealed class Clients {internal ClientInfo Peer;internal ClientInfo ForEntityId(int id)=>Peer;}
internal sealed class ConnectionManager {internal bool IsServer=true;internal Clients Clients=new Clients();}
internal static class SingletonMonoBehaviour<T> where T:new() {internal static T Instance=new T();}
internal sealed class RebirthStablePlayerIdentity {
 internal string StorageKey;internal static string Local="account";
 internal static bool TryFromLocalPlatform(out RebirthStablePlayerIdentity identity){identity=new RebirthStablePlayerIdentity{StorageKey=Local};return true;}
 internal static bool TryFromClientInfo(ClientInfo peer,out RebirthStablePlayerIdentity identity){identity=new RebirthStablePlayerIdentity{StorageKey=peer.Owner};return true;}
 internal static bool TryResolveServerEntity(EntityPlayer player,out RebirthStablePlayerIdentity identity){identity=new RebirthStablePlayerIdentity{StorageKey=player.Owner};return true;}
}
internal static class RebirthCamperCharacterAccess {internal static string CurrentCreation(EntityPlayer player)=>player.Creation;}
internal static class GameIO {internal static string Root=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"native-save");internal static string GetSaveGameDir()=>Root;}
internal sealed class IndexAdapter {
 internal RebirthCamperTransferJournal.Record FindPending(Guid group)=>null;
 internal IEnumerable<RebirthCamperTransferJournal.Record> FindApplied(Guid group,string creation)=>new RebirthCamperTransferJournal.Record[0];
 internal IEnumerable<RebirthCamperTransferJournal.Record> FindRejected(Guid group,string creation)=>new RebirthCamperTransferJournal.Record[0];
}
internal static class RebirthCamperTransferHistoryScope {internal static bool TryGet(RebirthCamperTransferJournal journal,out IndexAdapter index){index=null;return false;}}
internal static class Program {
 private static EntityPlayerLocal player;
 private static void Need(bool value,string message){if(!value)throw new Exception(message);}
 private static RebirthGearInventoryPlan.Stack[] Cells(int length){var cells=new RebirthGearInventoryPlan.Stack[length];for(int i=0;i<length;i++)cells[i]=new RebirthGearInventoryPlan.Stack();return cells;}
 private static RebirthHydrationTransferOriginal Original(){
  var bag=Cells(44);bag[3]=new RebirthGearInventoryPlan.Stack{Count=2,ItemData=Convert.ToBase64String(new byte[]{7,9,3,25})};
  Need(RebirthHydrationTransferPlan.TryCreate(bag,Cells(20),null,4,new RebirthGearInventoryPlan.Stack(),true,3,out var plan),"fixture plan refused");
  Need(RebirthHydrationTransferOriginal.TryCreate(Guid.NewGuid(),Guid.Parse(player.world.Guid),player.Owner,player.Creation,player.Died,8,4,null,plan,out var original),"fixture original refused");return original;
 }
 private static void Fresh(){var world=new World();player=new EntityPlayerLocal{world=world};world.Actor=world.Primary=player;GameManager.Instance.World=world;ThreadManager.Main=true;RebirthStablePlayerIdentity.Local="account";SingletonMonoBehaviour<ConnectionManager>.Instance=new ConnectionManager();}
 public static void Main(){
  Fresh();Need(RebirthHydrationTransferJournal.TryReadPending(player,out var pending,out _)&&pending==null&&!Directory.Exists(GameIO.Root),"read-only lookup created missing save/journal");
  var original=Original();Need(RebirthHydrationTransferJournal.TryPrepare(player,original,out _),"exact prepared original not saved");
  Need(RebirthHydrationTransferJournal.TryPrepare(player,original,out _)&&RebirthHydrationTransferJournal.TryReadPending(player,out pending,out _)&&pending.Digest==original.Digest,"original prepare/reload dedup failed");
  Need(RebirthHydrationTransferJournal.TryMarkAttempted(player,original,out _)&&RebirthHydrationTransferJournal.TryMarkAttempted(player,original,out _),"same WAL metadata could not resume");
  Need(!RebirthHydrationTransferJournal.TryPrepare(player,Original(),out _),"replacement operation bypassed unresolved original");
  player.Died++;Need(RebirthHydrationTransferJournal.TryReadPending(player,out pending,out _)&&pending.Deaths==original.Deaths&&!RebirthHydrationTransferJournal.TryMarkAttempted(player,original,out _)&&!RebirthHydrationTransferJournal.TryPrepare(player,Original(),out _),"earlier life observation became native permission/replacement");player.Died--;
  string primary=Directory.GetFiles(GameIO.Root,original.Operation.ToString("N")+".0.rcj",SearchOption.AllDirectories).Single();string sentinel=Path.Combine(Path.GetDirectoryName(primary),"unknown.tmp");File.WriteAllText(sentinel,"fixture staged candidate");
  Need(!RebirthHydrationTransferJournal.TryReadPending(player,out _,out _)&&!RebirthHydrationTransferJournal.TryPrepare(player,original,out _)&&File.Exists(sentinel),"unknown candidate ignored/deleted");File.Delete(sentinel);
  byte[] exact=File.ReadAllBytes(primary);File.WriteAllBytes(primary,exact.Concat(new byte[]{0}).ToArray());Need(!RebirthHydrationTransferJournal.TryReadPending(player,out _,out _),"corrupt original became empty");File.WriteAllBytes(primary,exact);
  Fresh();original=Original();ThreadManager.Main=false;Need(!RebirthHydrationTransferJournal.TryPrepare(player,original,out _),"worker journal admission");ThreadManager.Main=true;
  Action<string,RebirthCamperTransferJournal.Record,bool> fault=(path,record,published)=>{if(published)throw new IOException("after actual journal publication");};
  RebirthCamperTransferJournal.PublicationFinished+=fault;try{Need(!RebirthHydrationTransferJournal.TryPrepare(player,original,out _),"faulted caller reported positive publication");}finally{RebirthCamperTransferJournal.PublicationFinished-=fault;}
  Need(RebirthHydrationTransferJournal.TryReadPending(player,out pending,out _)&&pending.Digest==original.Digest&&RebirthHydrationTransferJournal.TryPrepare(player,original,out _),"positive same original publication lost after caller exception");
  Fresh();original=Original();string priorRoot=GameIO.Root;fault=(path,record,published)=>{if(published)GameIO.Root=Path.Combine(priorRoot,"replacement-save");};
  RebirthCamperTransferJournal.PublicationFinished+=fault;try{Need(!RebirthHydrationTransferJournal.TryPrepare(player,original,out _),"changed save path during publication admitted");}finally{RebirthCamperTransferJournal.PublicationFinished-=fault;GameIO.Root=priorRoot;}
  Need(RebirthHydrationTransferJournal.TryReadPending(player,out pending,out _)&&pending.Digest==original.Digest,"old save original lost after scope change");
  Fresh();original=Original();Need(RebirthHydrationTransferJournal.TryPrepare(player,original,out _),"cross-generation original unavailable");
  player.Creation=Guid.NewGuid().ToString("N");
  Need(RebirthHydrationTransferJournal.TryReadPending(player,out pending,out _)&&pending.Digest==original.Digest&&pending.Creation!=player.Creation,"generation rotation hid pending original");
  Need(!RebirthHydrationTransferJournal.TryMarkAttempted(player,pending,out _),"old generation discovery became replay authority");
  var secondGeneration=Original();Need(!RebirthHydrationTransferJournal.TryPrepare(player,secondGeneration,out _),"direct durable producer bypassed older generation original");
  Need(RebirthHydrationTransferJournal.TryReadPending(player,out pending,out _)&&pending.Digest==original.Digest,"rejected replacement changed older generation history");
  Fresh();original=Original();Need(RebirthHydrationTransferJournal.TryPrepare(player,original,out _),"applied recovery fixture original unavailable");
  string financialPath=Path.GetDirectoryName(Directory.GetFiles(GameIO.Root,original.Operation.ToString("N")+".0.rcj",SearchOption.AllDirectories).Single());
  var financialJournal=new RebirthCamperTransferJournal(financialPath);financialJournal.Advance(original.Operation,RebirthCamperTransferJournal.Phase.Prepared,RebirthCamperTransferJournal.Phase.Uncertain);financialJournal.Advance(original.Operation,RebirthCamperTransferJournal.Phase.Uncertain,RebirthCamperTransferJournal.Phase.Applied);
  Need(RebirthHydrationTransferJournal.TryReadPending(player,out pending,out _)&&pending.Digest==original.Digest,"unacknowledged Applied history became clean admission");
  player.Creation=Guid.NewGuid().ToString("N");Need(RebirthHydrationTransferJournal.TryReadPending(player,out pending,out _)&&pending.Digest==original.Digest,"generation rotation hid unacknowledged Applied history");
  string closurePath=Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(financialPath)),"HydrationAcknowledgments",Path.GetFileName(financialPath));
  var closureJournal=new RebirthCamperTransferJournal(closurePath);byte[] closureBytes=System.Text.Encoding.UTF8.GetBytes(original.Write().ToString(SaveOptions.DisableFormatting));
  closureJournal.Prepare(original.Operation,Guid.ParseExact(Path.GetFileName(financialPath),"N"),original.Creation,original.Revision,closureBytes,closureBytes);
  Need(RebirthHydrationTransferJournal.TryReadPending(player,out pending,out _)&&pending!=null,"Prepared closure granted admission");
  closureJournal.Advance(original.Operation,RebirthCamperTransferJournal.Phase.Prepared,RebirthCamperTransferJournal.Phase.Uncertain);
  Need(RebirthHydrationTransferJournal.TryReadPending(player,out pending,out _)&&pending!=null,"Uncertain closure granted admission");
  closureJournal.Advance(original.Operation,RebirthCamperTransferJournal.Phase.Uncertain,RebirthCamperTransferJournal.Phase.Applied);
  Need(RebirthHydrationTransferJournal.TryReadPending(player,out pending,out _)&&pending==null,"exact saved historical closure did not retire admission barrier");
  Console.WriteLine("PASS actual hydration WAL/model; native peer and live owner/metabolism settlement remain NOT RUN");
 }
}
