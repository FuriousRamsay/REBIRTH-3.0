// AUTHORED_NOT_RUN. Native event, file and UI doubles surround the actual
// returned-Process/OnOpen consumer methods, not a parallel implementation.
using System;using System.Linq;using System.Collections.Generic;
class World{}
class EntityPlayerLocal{}
class TileEntityWorkstation{internal byte[] Body=new byte[]{1};}
class WindowGroup{internal bool isShowing=true;}
class UiPlayer{internal EntityPlayerLocal entityPlayer;}
class Ui{internal UiPlayer playerUI=new UiPlayer();}
class WorkstationData{internal TileEntityWorkstation TileEntity;}
class XUiC_WorkstationWindowGroup{internal WorkstationData WorkstationData=new WorkstationData();internal Ui xui=new Ui();internal WindowGroup windowGroup=new WindowGroup();}
class LockManager{internal static LockManager Instance=new LockManager();internal bool Locked=true;internal bool IsLockedByLocalPlayer(TileEntityWorkstation station,int mode)=>Locked;}
static class RebirthWorkstationCompletionPublication{internal static bool Valid=true;internal static long PublishedLength(string path)=>Valid?42:-1;}
static class RebirthProductiveEarnedNativeBody{internal static bool MatchesCurrent(TileEntityWorkstation station,byte[] expected,int offset)=>station.Body.SequenceEqual(expected);}
static class RebirthProductiveEarnedUiDeferredTransport
{
 internal static bool Opening,Identity=true;internal static long Session=8,ReadVersion=4;
 internal class ClientHydration{internal bool Processed,ReadCompleted=true;internal string Path="actual-envelope";internal bool CurrentSource()=>Identity;}
 internal static bool IsActualCachedWindowOpen(XUiC_WorkstationWindowGroup group,long session)=>Opening&&session==Session;
 internal static bool TryGetCurrentNativeUiSession(XUiC_WorkstationWindowGroup group,out long session){session=Session;return group.windowGroup.isShowing;}
 internal static bool IsCurrentQueueOwnerNativeRead(World world,TileEntityWorkstation station,long version)=>version==ReadVersion;
}
static class Subject
{
 internal class Source{internal Guid Operation=Guid.NewGuid();internal World World=new World();internal TileEntityWorkstation Station=new TileEntityWorkstation();internal EntityPlayerLocal Actor=new EntityPlayerLocal();internal byte[] BeforeNative=new byte[]{1},AfterNative=new byte[]{2};internal int BeforeOffset,AfterOffset;}
 internal class RecyclerJoining:IDisposable{internal Guid[] Pending;internal bool Disposed;internal HashSet<Guid> Unstarted=new HashSet<Guid>();internal Dictionary<Guid,TileEntityWorkstation> UnqualifiedStations=new Dictionary<Guid,TileEntityWorkstation>();public void Dispose(){Disposed=true;}}
 internal class OwnerPublication{internal static OwnerPublication FromActualRemoteHydration(RecyclerClient client,RecyclerReadReentry reentry)=>new OwnerPublication();}
 internal class RecyclerClient{internal Source Source=new Source();internal bool Identity=true;internal RecyclerReadReentry Reentry;internal RecyclerJoining ColdJoined;internal OwnerPublication Owner;internal XUiC_WorkstationWindowGroup Window;internal TileEntityWorkstation UnqualifiedStation;internal long Session=2;internal bool MetadataCurrent()=>Identity;}
 internal class RecyclerReadReentry:IDisposable
 {internal RebirthProductiveEarnedUiDeferredTransport.ClientHydration Hydration;internal RecyclerClient Client;internal IDisposable Lease;internal bool Returned,Disposed;internal long ReadVersion=4,Length;internal string Path,Digest,Stage="before";internal XUiC_WorkstationWindowGroup OpeningWindow;internal long OpeningSession;public void Dispose(){Disposed=true;Hydration=null;OpeningWindow=null;Lease?.Dispose();Lease=null;}}
 private static List<RecyclerClient> recyclerClients=new List<RecyclerClient>();
 private static string RecyclerFileDigest(string path)=>RebirthWorkstationCompletionPublication.Valid?"actual-seal":null;
 // PRODUCTION_RETURNED
 // PRODUCTION_OPEN
 // PRODUCTION_FINISH
 static void Check(bool condition,string label){if(!condition)throw new Exception(label);Console.WriteLine("PASS "+label);}
 static RecyclerClient New(out RebirthProductiveEarnedUiDeferredTransport.ClientHydration hydration,out XUiC_WorkstationWindowGroup group)
 {
  var client=new RecyclerClient();hydration=new RebirthProductiveEarnedUiDeferredTransport.ClientHydration();
  client.Reentry=new RecyclerReadReentry{Hydration=hydration,Client=client};recyclerClients.Clear();recyclerClients.Add(client);
  group=new XUiC_WorkstationWindowGroup();group.WorkstationData.TileEntity=client.Source.Station;group.xui.playerUI.entityPlayer=client.Source.Actor;
  RebirthProductiveEarnedUiDeferredTransport.Identity=true;RebirthProductiveEarnedUiDeferredTransport.ReadVersion=4;RebirthProductiveEarnedUiDeferredTransport.Session=8;
  RebirthProductiveEarnedUiDeferredTransport.Opening=false;RebirthWorkstationCompletionPublication.Valid=true;LockManager.Instance.Locked=true;return client;
 }
 static void Open(XUiC_WorkstationWindowGroup group){RebirthProductiveEarnedUiDeferredTransport.Opening=true;ObserveActualRecyclerHydrationOpen(group,8);RebirthProductiveEarnedUiDeferredTransport.Opening=false;}
 public static void Main()
 {
  var client=New(out var hydration,out var group);var retained=client.Reentry;
  Open(group);Check(client.Session==2&&retained.OpeningSession==8&&!retained.Returned,"actual OnOpen inside native Process records event but grants no continuation");
  hydration.Processed=true;ObserveReturnedRecyclerHydration(hydration);
  Check(client.Session==8&&ReferenceEquals(client.Window,group)&&client.Reentry==null&&retained.Disposed,"actual returned Process completes previously captured OnOpen");
  client=New(out hydration,out group);hydration.Processed=true;ObserveReturnedRecyclerHydration(hydration);
  ObserveActualRecyclerHydrationOpen(group,8);Check(client.Session==2,"getter-only open outside actual bracket cannot issue continuation");
  Open(group);Check(client.Session==8,"actual later OnOpen after Process return qualifies");
  client=New(out hydration,out group);Open(group);RebirthProductiveEarnedUiDeferredTransport.ReadVersion=5;hydration.Processed=true;ObserveReturnedRecyclerHydration(hydration);
  Check(client.Session==2&&client.Reentry!=null,"later equal native read cannot renew old exact read witness");
  client=New(out hydration,out group);Open(group);client.Identity=false;hydration.Processed=true;ObserveReturnedRecyclerHydration(hydration);
  Check(client.Session==2,"foreign or retired actual owner connection cannot qualify");
  client=New(out hydration,out group);Open(group);LockManager.Instance.Locked=false;hydration.Processed=true;ObserveReturnedRecyclerHydration(hydration);
  Check(client.Session==2,"unlock before returned boundary preserves held original");
  client=New(out hydration,out group);Open(group);RebirthProductiveEarnedUiDeferredTransport.Session=9;hydration.Processed=true;ObserveReturnedRecyclerHydration(hydration);
  Check(client.Session==2,"close or reused window session cannot borrow prior actual open");
  client=New(out hydration,out group);Open(group);client.Source.Station.Body=new byte[]{2};hydration.Processed=true;ObserveReturnedRecyclerHydration(hydration);
  Check(client.Session==2,"changed full native body cannot qualify old hydration");
  client=New(out hydration,out group);Open(group);RebirthWorkstationCompletionPublication.Valid=false;hydration.Processed=true;ObserveReturnedRecyclerHydration(hydration);
  Check(client.Session==2,"failed actual immutable envelope seal preserves unknown");
  client=New(out hydration,out group);Open(group);ObserveReturnedRecyclerHydration(hydration);
  Check(client.Session==2,"throwing or nonreturned native Process never grants continuation");
  client=New(out hydration,out group);Guid other=Guid.NewGuid();var joined=new RecyclerJoining{Pending=new[]{client.Source.Operation,other}};
  joined.UnqualifiedStations.Add(client.Source.Operation,client.Source.Station);joined.UnqualifiedStations.Add(other,new TileEntityWorkstation());client.ColdJoined=joined;
  client.UnqualifiedStation=client.Source.Station;Open(group);hydration.Processed=true;ObserveReturnedRecyclerHydration(hydration);
  Check(client.Session==8&&client.UnqualifiedStation==null&&client.ColdJoined==null&&!joined.Disposed&&joined.Pending.SequenceEqual(new[]{other})&&
    joined.UnqualifiedStations.Count==1&&joined.UnqualifiedStations.ContainsKey(other),"one qualified original preserves other actual joined custody and unknown target");
  client=New(out hydration,out group);joined=new RecyclerJoining{Pending=new[]{client.Source.Operation}};client.ColdJoined=joined;
  Open(group);hydration.Processed=true;ObserveReturnedRecyclerHydration(hydration);
  Check(joined.Disposed&&client.ColdJoined==null,"last positively qualified joined original releases native join observation only");
 }
}
