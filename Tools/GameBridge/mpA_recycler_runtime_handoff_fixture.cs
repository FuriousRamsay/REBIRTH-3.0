// AUTHORED_NOT_RUN. Production methods are injected by the companion runner.
// Native actor/source capture, fsync publication and controller are explicit
// doubles: these assertions do not qualify the native integration chain.
using System;using System.Linq;using System.Collections.Generic;using System.Text;using System.Xml.Linq;using System.IO;
class Fixture
{
 public class World{internal bool Remote;internal EntityPlayer Primary;internal bool IsRemote()=>Remote;internal EntityPlayer GetPrimaryPlayer()=>Primary;internal object GetEntity(int id)=>Primary;}
 public class EntityPlayer{internal World world=new World();internal int Died,entityId=1;internal bool IsSpawned()=>true;internal bool IsDead()=>false;}
 public class EntityPlayerLocal:EntityPlayer{}
 class GameManager{internal static GameManager Instance=new GameManager();internal World World;}
 static class ThreadManager{internal static bool Main=true;internal static bool IsMainThread()=>Main;}
 public struct Vector3i{internal int x,y,z;internal Vector3i(int x,int y,int z){this.x=x;this.y=y;this.z=z;}}
 public class TileEntityWorkstation{internal Vector3i ToWorldPos()=>new Vector3i(1,2,3);}
 public class ItemStack{}
 public class RecipeQueueItem{}
 class Original:IDisposable
 {
  internal Guid Operation;internal string Purpose,WorldKey="save";internal EntityPlayerLocal Actor;internal TileEntityWorkstation Station;
  internal byte[] Image=Encoding.UTF8.GetBytes("<placedRecyclerOriginal />");internal bool Live=true,Disposed;
  internal bool Current()=>Live&&!Disposed;
  internal static Original Last;internal static bool Refuse;
  internal static Original Capture(EntityPlayerLocal actor,TileEntityWorkstation station,Guid operation,string purpose,ItemStack[] input,
   RecipeQueueItem[] queue,IList<ItemStack> returns,ItemStack[] output,ItemStack[] tools,Func<bool> current,bool cycle,float workSeconds)
  {return Refuse||current?.Invoke()!=true?null:Last=new Original{Actor=actor,Station=station,Operation=operation,Purpose=purpose};}
  public void Dispose(){Disposed=true;}
 }
 class AuthorityOriginal:IDisposable
 {internal Original Source;internal object Station,Owner;internal bool UnusedAdmission;internal AccessHold Access;public void Dispose(){Source.Dispose();}}
 class AccessHold {internal bool Released;internal bool ObserveUnused(Original original){Released=true;return true;}}
 static List<AuthorityOriginal> authorityOriginals=new List<AuthorityOriginal>();
 class RecyclerClient{internal Original Source;}static List<RecyclerClient> recyclerClients=new List<RecyclerClient>();
 static bool Accepted,Terminal,Throw;
 class RebirthStablePlayerIdentity{internal string StorageKey="owner",CanonicalId="account";internal static bool TryFromLocalPlatform(out RebirthStablePlayerIdentity identity){identity=new RebirthStablePlayerIdentity();return true;}}
 static class RebirthCamperCharacterAccess{internal static string CurrentCreation(EntityPlayer actor)=>"creation";}
 static class RebirthProductiveEarnedObservationBudget
 {class Lease:IDisposable{public void Dispose(){}}internal static bool Denied;internal static bool TryRetainDetached(long bytes,out IDisposable lease){lease=Denied?null:new Lease();return !Denied;}}
 static class RebirthWorkstationCompletionOriginal{internal static string Hash(string text)=>Convert.ToBase64String(Encoding.UTF8.GetBytes(text));}
 static bool ArtifactNodeExists(string path)=>RebirthWorkstationCompletionPublication.Images.ContainsKey(path);
 // PRODUCTION_RETRY
 // ACTUAL_RETRY_CORE
 // Other retained controller families are empty in this authority-local cohort.
 static Dictionary<Guid,object> remoteRecyclerOriginals=new Dictionary<Guid,object>();
 class ClosedCompletion{internal Guid Operation;}
 static List<ClosedCompletion> closedAuthorityCompletions=new List<ClosedCompletion>();
 static bool TryRetainAuthority(Original original)
 {if(Throw)throw new Exception("before ownership");if(!Accepted)return false;authorityOriginals.Add(new AuthorityOriginal{Source=original});return true;}
 static bool HasAuthorityTerminal(EntityPlayerLocal actor,TileEntityWorkstation station,Guid operation,string purpose)=>Terminal;
 static class RebirthWorkstationCompletionOutbox{internal static string Root(string purpose)=>"durable-root";internal static string CurrentGuid(World world)=>"save";}
 static class RebirthWorkstationCompletionPublication
 {
  internal static bool Writable=true,Readable=true;internal static XElement Saved;
  internal static Dictionary<string,XElement> Images=new Dictionary<string,XElement>();
  internal static bool Write(string path,XElement value){if(!Writable)return false;Saved=new XElement(value);Images[path]=new XElement(value);return true;}
  internal static XElement ReadPrimary(string path)=>Readable&&Images.TryGetValue(path,out var value)?new XElement(value):null;
  internal static long PublishedLength(string path)=>Images.TryGetValue(path,out var value)?Encoding.UTF8.GetByteCount(value.ToString()):-1;
 }
 // PRODUCTION_HANDOFF
 // PRODUCTION_UNUSED
 static int checks;static void Check(bool value,string name){checks++;if(!value)throw new Exception(name);}
 static bool Begin(EntityPlayerLocal actor,TileEntityWorkstation station,Guid operation,out string reason)=>BeginAuthorityAction(actor,station,operation,"cancel",
  new ItemStack[0],new RecipeQueueItem[0],new List<ItemStack>(),new ItemStack[0],new ItemStack[0],()=>true,out reason);
 static void Main()
 {
  var actor=new EntityPlayerLocal();var station=new TileEntityWorkstation();var operation=Guid.NewGuid();
  actor.world.Primary=actor;GameManager.Instance.World=actor.world;
  Check(!Begin(actor,station,operation,out _)&&Original.Last.Disposed,"refused controller disposes untransferred source");
  Throw=true;Check(!Begin(actor,station,operation,out _)&&Original.Last.Disposed,"preownership exception disposes source");Throw=false;
  Accepted=true;Check(Begin(actor,station,operation,out _)&&!Original.Last.Disposed,"accepted frozen source belongs to controller");
  var retained=Original.Last;Check(Begin(actor,station,operation,out _)&&ReferenceEquals(Original.Last,retained),"duplicate pending original never recaptures");
  Check(!Begin(new EntityPlayerLocal(),station,operation,out _),"different actual actor cannot borrow pending identity");
  retained.Live=false;Check(!Begin(actor,station,operation,out _),"stale pending source remains unknown");retained.Live=true;
  authorityOriginals.Clear();Terminal=true;Original.Last=null;Check(Begin(actor,station,operation,out _)&&Original.Last==null,"terminal acknowledgement makes no new source");Terminal=false;
  actor.world.Remote=true;Check(!Begin(actor,station,operation,out _)&&Original.Last==null,"remote actor cannot enter authority controller");actor.world.Remote=false;
  var unused=new AuthorityOriginal{Source=retained,UnusedAdmission=true};authorityOriginals.Add(unused);
  RebirthWorkstationCompletionPublication.Writable=false;RetireUnusedAdmission(unused);Check(authorityOriginals.Contains(unused)&&!retained.Disposed,"failed unused fsync retains original");
  RebirthWorkstationCompletionPublication.Writable=true;RebirthWorkstationCompletionPublication.Readable=false;RetireUnusedAdmission(unused);
  Check(authorityOriginals.Contains(unused)&&!retained.Disposed,"failed unused readback retains original");
  RebirthWorkstationCompletionPublication.Readable=true;unused.Station=new object();RetireUnusedAdmission(unused);Check(authorityOriginals.Contains(unused),"entered marker source cannot claim unused admission");
  unused.Station=null;RetireUnusedAdmission(unused);Check(!authorityOriginals.Contains(unused)&&retained.Disposed,"explicit never-entered durable outcome retires resources");
  Check((bool)RebirthWorkstationCompletionPublication.Saved.Element("recyclerUnusedAdmission").Attribute("nativeOwnerEntered")==false,"unused receipt states actual no setter boundary");
  var retryOperation=Guid.NewGuid();
  string retryPath=Path.Combine("durable-root",retryOperation.ToString("N")+".unused-admission.xml");
  var unusedImage=new XElement("workstationCompletionIntent",new XAttribute("operation",retryOperation.ToString("N")),new XAttribute("world","save"),
   new XElement("before",new XElement("placedRecyclerOriginal",new XAttribute("operation",retryOperation.ToString("N")),new XAttribute("purpose","cancel"),
    new XAttribute("owner","owner"),new XAttribute("account","account"),new XAttribute("creation","creation"),new XAttribute("deaths",0),
    new XAttribute("x",1),new XAttribute("y",2),new XAttribute("z",3))),
   new XElement("recyclerUnusedAdmission",new XAttribute("nativeStationEntered",false),new XAttribute("nativeOwnerEntered",false)));
  RebirthWorkstationCompletionPublication.Images[retryPath]=unusedImage;
  Check(TrySelectAuthorityAttempt(actor,station,retryOperation,"cancel",out var selected)&&selected!=retryOperation&&selected!=Guid.Empty,"positive unused picks fresh original identity");
  Check(TrySelectAuthorityAttempt(actor,station,retryOperation,"cancel",out var repeated)&&repeated==selected,"same immutable retry link survives metadata repeat");
  authorityOriginals.Add(new AuthorityOriginal{Source=new Original{Operation=retryOperation}});
  Check(TrySelectAuthorityAttempt(actor,station,retryOperation,"cancel",out var active)&&active==retryOperation,"retained uncertain original cannot be bypassed by retry metadata");authorityOriginals.Clear();
  RebirthProductiveEarnedObservationBudget.Denied=true;Check(!TrySelectAuthorityAttempt(actor,station,retryOperation,"cancel",out _),"retry budget denial grants nothing");RebirthProductiveEarnedObservationBudget.Denied=false;
  unusedImage.Element("recyclerUnusedAdmission").SetAttributeValue("nativeOwnerEntered",true);
  Check(!TrySelectAuthorityAttempt(actor,station,retryOperation,"cancel",out _),"entered owner cannot be treated as unused");
  unusedImage.Element("recyclerUnusedAdmission").SetAttributeValue("nativeOwnerEntered",false);
  unusedImage.Element("before").Element("placedRecyclerOriginal").SetAttributeValue("creation","new-life");
  Check(!TrySelectAuthorityAttempt(actor,station,retryOperation,"cancel",out _),"later owner life cannot borrow old unused original");
  Console.WriteLine("PASS "+checks+" actual runtime handoff/unused cases (native doubles)");
 }
}
