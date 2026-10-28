// AUTHORED_NOT_RUN. Actual admission + archive + full publication writer.
// Native event capture/currentness/access are explicit doubles; no game or topology claim.
using System;using System.Collections.Generic;using System.IO;using System.Linq;using System.Text;using System.Xml;using System.Xml.Linq;using System.Security.Cryptography;
namespace UnityEngine{static class Time{internal static float realtimeSinceStartup;}}
class World{}
class EntityPlayer{}
class TileEntityWorkstation{}
class RecipeQueueItem{}
class ItemStack{}
static class ThreadManager{internal static bool IsMainThread()=>true;}
static class GameIO{internal static string Save;internal static string GetSaveGameDir()=>Save;}
class GameManager{internal static GameManager Instance=new GameManager();internal World World=new World();}
static class RebirthStationCompletionOriginalScope{internal static StringComparison PathComparison=>StringComparison.OrdinalIgnoreCase;}
static class RebirthWorkstationCompletionOriginal{internal static string Hash(string value){using(var h=SHA256.Create())return Convert.ToBase64String(h.ComputeHash(Encoding.UTF8.GetBytes(value)));}}
static class RebirthWorkstationCompletionOutbox
{internal static string CurrentGuid(World world)=>"fixture-world";internal static string Root(string value)=>Path.Combine(GameIO.Save,"RebirthData","Survivor","WorkstationCompletions",value);}
// ACTUAL_PUBLICATION
static class Subject
{
 static bool authorityInstalled=true;
 static readonly List<ClosedAuthorityCompletion> closedAuthorityCompletions=new List<ClosedAuthorityCompletion>();
 sealed class Connection{internal bool IsDisconnected()=>false;}
 sealed class Assembly:IDisposable{internal World World;internal Connection Connection;public void Dispose(){}}
 static readonly List<Assembly> closedGrantAssemblies=new List<Assembly>();
 static readonly List<object> remoteLoadedRecyclerOwners=new List<object>();
 static readonly Dictionary<ClosedAuthorityCompletion,float> closedGrantQueries=new Dictionary<ClosedAuthorityCompletion,float>();
 sealed class RemoteHostOriginal{internal bool ColdNeedsOwner;}
 sealed class ClosedPendingPublication
 {internal static bool NativeSaved;internal bool Saved()=>NativeSaved;internal static void Begin(ClosedAuthorityCompletion source){source.Pending=new ClosedPendingPublication();}}
 static int Grants;static void SendClosedAuthorityGrant(ClosedAuthorityCompletion source){Grants++;}
 static void RecoverClosedPendingHot(ClosedAuthorityCompletion source){}
 sealed class AccessHold{internal static bool Available=true;internal static AccessHold CapturePending(ClosedAuthorityCompletion source)=>Available?new AccessHold():null;}
 sealed class ClosedAuthorityCompletion:IDisposable
 {
  internal Guid Operation,Group,Ticket=Guid.NewGuid();internal string WorldKey="fixture-world";internal EntityPlayer Recipient;internal TileEntityWorkstation Station;
  internal AccessHold Access;internal bool Alive=true,Disposed,ViewedOrigin;internal static int Captures;
  internal World World=GameManager.Instance.World;internal RemoteHostOriginal Attached;internal ClosedPendingPublication Pending;
  internal bool NativeLineageCurrent()=>Current();internal bool RecipientCurrent()=>Current();internal bool AdoptUnacceptedNativeOwner(object loaded)=>false;
  internal bool Current()=>Alive&&!Disposed;
  internal XElement CopyImage()=>Current()?new XElement("recyclerClosedAuthorityOriginal",new XAttribute("version",1),new XAttribute("operation",Operation.ToString("N"))):null;
  internal static ClosedAuthorityCompletion Capture(EntityPlayer actor,TileEntityWorkstation station,RecipeQueueItem row,Guid op,Guid group,float seconds,ItemStack[] input,RecipeQueueItem[] queue,IList<ItemStack> returns,ItemStack[] output,ItemStack[] tools)
  {Captures++;return new ClosedAuthorityCompletion{Recipient=actor,Station=station,Operation=op,Group=group};}
  public void Dispose(){Disposed=true;closedAuthorityCompletions.Remove(this);}
  // ACTUAL_ARCHIVE
 }
 // ACTUAL_ADMISSION
 // ACTUAL_PUMP
 static void Check(bool value,string label){if(!value)throw new Exception(label);}
 static bool Begin(EntityPlayer actor,TileEntityWorkstation station,Guid op,Guid group)=>BeginAuthorityRemoteCompletion(actor,station,new RecipeQueueItem(),op,group,1,new ItemStack[0],new RecipeQueueItem[0],new List<ItemStack>(),new ItemStack[0],new ItemStack[0]);
 static string Archive(ClosedAuthorityCompletion s)=>Path.Combine(RebirthWorkstationCompletionOutbox.Root("recyclerPlaced"),s.Operation.ToString("N")+".closed-authority."+s.Ticket.ToString("N")+".xml");
 static void Main()
 {
  GameIO.Save=Path.Combine(Path.GetTempPath(),"CodexTest-mp027-admission-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(GameIO.Save);
  var actor=new EntityPlayer();var station=new TileEntityWorkstation();var op=Guid.NewGuid();var group=Guid.NewGuid();
  // Block directory creation before writer ownership, then recover the same private event.
  string obstruction=Path.Combine(GameIO.Save,"RebirthData");File.WriteAllText(obstruction,"fixture obstruction");
  Check(Begin(actor,station,op,group),"failed archive remains admitted pending");
  var source=closedAuthorityCompletions.Single();Guid ticket=source.Ticket;
  Check(!source.Disposed&&ClosedAuthorityCompletion.Captures==1&&!source.EnsureOriginalPublished(),"pending original retained");
  Check(Begin(actor,station,op,group)&&ClosedAuthorityCompletion.Captures==1&&source.Ticket==ticket,"same operation does not recapture");
  Check(!Begin(actor,station,op,Guid.NewGuid()),"different group cannot borrow admission");
  PumpClosedAuthorityCompletions(GameManager.Instance.World);
  Check(Grants==0&&source.Pending==null,"no native pending marker or grant before archive publishes");
  File.Delete(obstruction); // Removes only the fixture's non-evidence obstruction.
  PumpClosedAuthorityCompletions(GameManager.Instance.World);
  Check(Grants==0&&source.Pending==null,"failed IO admission retry is rate limited");
  Check(source.EnsureOriginalPublished(),"same original retries after IO availability");
  string path=Archive(source);var record=RebirthWorkstationCompletionPublication.ReadPrimary(path);
  Check(record.Element("before")?.Elements().Count()==1&&XNode.DeepEquals(record.Element("before").Elements().Single(),record.Element("recyclerClosedAuthoritySource").Elements().Single()),"writer requires exact native Before");
  Check((string)record.Element("recyclerClosedAuthoritySource").Attribute("ticket")==ticket.ToString("N"),"retained ticket published");
  byte[] bytes=File.ReadAllBytes(path);Check(source.EnsureOriginalPublished()&&bytes.SequenceEqual(File.ReadAllBytes(path)),"idempotent exact archive");
  UnityEngine.Time.realtimeSinceStartup=1;PumpClosedAuthorityCompletions(GameManager.Instance.World);
  Check(source.Pending!=null&&Grants==0,"archive alone cannot grant before native pending save");
  ClosedPendingPublication.NativeSaved=true;UnityEngine.Time.realtimeSinceStartup=2;PumpClosedAuthorityCompletions(GameManager.Instance.World);
  Check(Grants==1,"saved native pending original permits grant");
  // Unknown foreign staging cannot be adopted, deleted or replaced by another event.
  var otherOp=Guid.NewGuid();Check(Begin(actor,station,otherOp,group)&&ClosedAuthorityCompletion.Captures==1&&closedAuthorityCompletions.Count==1,"same station/group reuses retained original without recapture");
  var otherGroup=Guid.NewGuid();Check(Begin(actor,station,otherOp,otherGroup),"second distinct group original captured");var other=closedAuthorityCompletions.Single(s=>s.Operation==otherOp);
  File.WriteAllText(Archive(other)+".tmp","foreign-stage");
  Check(!other.EnsureOriginalPublished()&&Begin(actor,station,otherOp,otherGroup)&&!other.Disposed,"unknown staging preserves original custody");
  Check(File.ReadAllText(Archive(other)+".tmp")=="foreign-stage"&&ClosedAuthorityCompletion.Captures==2,"no fresh ticket or evidence overwrite");
  other.Alive=false;Check(!other.EnsureOriginalPublished(),"changed native source cannot publish");
  AccessHold.Available=false;Check(!Begin(actor,station,Guid.NewGuid(),Guid.NewGuid())&&closedAuthorityCompletions.Count==2,"pre-write access refusal adds no pending event");
  Console.WriteLine("PASS MP027 actual admission/archive/publication scenarios; native producer and topology remain unproven. Evidence: "+GameIO.Save);
 }
}
