using System;
using System.IO;
using System.Text;
using System.Linq;using System.Collections.Generic;
using System.Xml;
using System.Xml.Linq;
using System.Security.Cryptography;
class ItemStack {public string Id;public int count;public ItemStack(string id,int count){Id=id;this.count=count;}public ItemStack Clone()=>new ItemStack(Id,count);public bool IsEmpty()=>count==0;}
static class RebirthStationGridIngredients {public static bool IsSameStackSnapshot(ItemStack a,ItemStack b)=>a!=null&&b!=null&&a.Id==b.Id&&a.count==b.count;}
static class RebirthCamperInventorySnapshot {
 public static byte[] Capture(ItemStack[] items){using(var s=new MemoryStream())using(var w=new BinaryWriter(s)){w.Write(items.Length);foreach(var i in items){w.Write(i.Id);w.Write(i.count);}return s.ToArray();}}
 public static ItemStack[] Restore(byte[] bytes){using(var s=new MemoryStream(bytes))using(var r=new BinaryReader(s)){int n=r.ReadInt32();if(n<0||n>200)throw new InvalidDataException();var items=new ItemStack[n];for(int i=0;i<n;i++)items[i]=new ItemStack(r.ReadString(),r.ReadInt32());if(s.Position!=s.Length)throw new InvalidDataException();return items;}}
}
static class RebirthSurvivorRequestScope {public static bool TryNormalize(string text,out string normalized){normalized=text;return Guid.TryParse(text,out _);}}
static class AdvancedFarmingHarvestOwnerDelta {
// PRODUCTION_PLAN
// PRODUCTION_OWNER_METHODS
}
class WorldState {public string Guid;}
class World {public WorldState worldState=new WorldState();public bool IsRemote()=>false;}
class GameManager {public static GameManager Instance=new GameManager();public World World=new World();}
static class GameIO {public static string Directory;public static string GetSaveGameDir()=>Directory;}
static class ThreadManager {public static bool IsMainThread()=>true;}
static class Log {public static void Warning(string text){}}
static class AdvancedFarmingHarvestTerminalPlot {internal sealed class Capsule {internal Guid Source;}internal static Capsule Decode(string image){if(!string.IsNullOrEmpty(image))throw new InvalidDataException();return null;}}
static class AdvancedFarmingHarvestDrops {internal sealed class Proof {internal Guid Operation,Source;internal string World,OutputDigest,NativeDigest;internal long Generation;}}
static class AdvancedFarmingHarvestPublication {internal sealed class Proof {internal Guid Operation,Source;internal string World,Digest;internal int X,Y,Z;internal long Generation;}}
// PRODUCTION_DURABLE
// PRODUCTION_RECEIPT
static class AdvancedFarmingHarvestProtocol {public static void ObservePreparedOriginal(Guid operation){}}
static class AdvancedFarmingHarvestAdmission {
private const int MaximumReceipts=100000;private static World indexedWorld;private static string indexedDirectory;private static int indexedCount;private static Dictionary<Guid,Guid> reservedSources;private static bool quarantined;private static readonly Dictionary<Guid,string> pendingOriginals=new Dictionary<Guid,string>();
// PRODUCTION_INDEX_METHODS
static void Check(bool value,string why){if(!value)throw new Exception(why);}
internal static void ClaimCases(AdvancedFarmingHarvestReceipt.Record first,AdvancedFarmingHarvestReceipt.Record second){
 second.Source=first.Source;indexedWorld=null;Check(Index(GameManager.Instance.World),"fresh reservation index refused");Check(Reserve(first)&&!Reserve(second),"competing original won before first publication");Check(PublishClaim(first)&&!AdvancedFarmingHarvestReceipt.TryRead(first.World,first.Operation,out _),"hold-only claim became native source permission");
 indexedWorld=null;Check(Index(GameManager.Instance.World)&&reservedSources[first.Source]==first.Operation&&!Reserve(second),"reload lost unpublished source exclusivity");Check(ExactClaim(ClaimPath(first),first)&&!ExactClaim(ClaimPath(first),second),"foreign original adopted prepared claim");Check(PublishReservation(first),"same prepared original could not publish strictly");
 File.WriteAllText(ClaimPath(second),AdvancedFarmingHarvestReceipt.Encode(second));indexedWorld=null;Check(!Index(GameManager.Instance.World),"conflicting persisted original claims accepted");
 GameIO.Directory=Path.Combine(GameIO.Directory,"partial");Directory.CreateDirectory(Path.Combine(GameIO.Directory,"rebirth","farming-harvest","v1"));indexedWorld=null;string partial=Path.Combine(GameIO.Directory,"rebirth","farming-harvest","v1",Guid.NewGuid().ToString("N")+".claim.tmp");File.WriteAllText(partial,"partial native original");Check(!Index(GameManager.Instance.World),"unreadable partial claim inferred unused");
}
}
static class Fixture {
 static void Check(bool value,string text){if(!value)throw new Exception(text);}
 static bool Publish(AdvancedFarmingHarvestReceipt.Record r,out AdvancedFarmingHarvestReceipt.Record next)=>AdvancedFarmingHarvestReceipt.TryPublishSource(r,new AdvancedFarmingHarvestPublication.Proof{Operation=r.Operation,Source=r.Source,World=r.World,X=r.X,Y=r.Y,Z=r.Z,Digest=new string('a',64),Generation=1},out next);
 static AdvancedFarmingHarvestReceipt.Record Make(int assigned=2){var plan=new AdvancedFarmingHarvestOwnerDelta.Plan{Operation=Guid.NewGuid(),World=GameManager.Instance.World.worldState.Guid,Creation=Guid.NewGuid().ToString("N"),Owner="owner",Deaths=2,SourceHash=new string('a',64),BeforeBag=new[]{new ItemStack("",0)},AfterBag=new[]{new ItemStack("corn",assigned)},BeforeBelt=new ItemStack[0],AfterBelt=new ItemStack[0]};return new AdvancedFarmingHarvestReceipt.Record{Operation=plan.Operation,Source=Guid.NewGuid(),World=plan.World,Creation=plan.Creation,Owner=plan.Owner,Deaths=2,X=1,Y=60,Z=1,DropX=1,DropY=60,DropZ=1,Block=7,State=AdvancedFarmingHarvestReceipt.Phase.Reserved,PlanImage=AdvancedFarmingHarvestOwnerDelta.Encode(plan),Outputs=AdvancedFarmingHarvestReceipt.OutputImage(new[]{new ItemStack("corn",2)})};}
 static void Main(string[] args){GameIO.Directory=args[0];GameManager.Instance.World.worldState.Guid=Guid.NewGuid().ToString();var r=Make();
  Check(AdvancedFarmingHarvestReceipt.TryReserve(r),"reserve failed");Check(AdvancedFarmingHarvestReceipt.TryRead(r.World,r.Operation,out var read),"read failed");
  Check(!AdvancedFarmingHarvestReceipt.TryAdvance(read,AdvancedFarmingHarvestReceipt.Phase.SourcePublished,out _),"source phase advanced without proof");
  Check(Publish(read,out var source),"forward source proof failed");
  Check(!Publish(read,out _),"stale original advanced");
  Check(AdvancedFarmingHarvestReceipt.TryAdvance(source,AdvancedFarmingHarvestReceipt.Phase.OwnerPublished,out var owner),"owner publication failed");Check(AdvancedFarmingHarvestReceipt.TryAdvance(owner,AdvancedFarmingHarvestReceipt.Phase.Retired,out var retired),"complete original failed retirement");
  Check(AdvancedFarmingHarvestReceipt.ArchiveRetired(retired)&&AdvancedFarmingHarvestReceipt.TryRead(retired.World,retired.Operation,out var archived)&&archived.State==AdvancedFarmingHarvestReceipt.Phase.Retired,"saved terminal archive not readable");
  Check(AdvancedFarmingHarvestReceipt.ExistsOriginal(r.World,r.Operation)&&!AdvancedFarmingHarvestReceipt.TryReserve(r),"archived original re-reserved/replayed");
  Check(!AdvancedFarmingHarvestReceipt.ArchiveRetired(source),"unretired source archived");
  var pending=Make(1);Check(AdvancedFarmingHarvestReceipt.TryReserve(pending),"retained remainder refused");Check(Publish(pending,out source)&&AdvancedFarmingHarvestReceipt.TryAdvance(source,AdvancedFarmingHarvestReceipt.Phase.OwnerPublished,out owner),"retained remainder phase failed");Check(!AdvancedFarmingHarvestReceipt.TryAdvance(owner,AdvancedFarmingHarvestReceipt.Phase.Retired,out _),"unassigned original output retired");
  string path=Path.Combine(GameIO.Directory,"rebirth","farming-harvest","v1",pending.Operation.ToString("N")+".xml");File.WriteAllText(path,"broken-original");Check(!AdvancedFarmingHarvestReceipt.TryReserve(pending)&&File.ReadAllText(path)=="broken-original","corrupt primary overwritten");
  var wrong=Make();wrong.Deaths++;Check(!AdvancedFarmingHarvestReceipt.TryReserve(wrong),"different original death accepted");
  var extra=Make(3);Check(!AdvancedFarmingHarvestReceipt.TryReserve(extra),"plan minted more than original outputs");
  GameIO.Directory=Path.Combine(args[0],"claim-index");AdvancedFarmingHarvestAdmission.ClaimCases(Make(),Make());
  Console.WriteLine("PASS actual receipt methods: strict original, monotone phases, remainder retention, corruption quarantine");
 }
}
