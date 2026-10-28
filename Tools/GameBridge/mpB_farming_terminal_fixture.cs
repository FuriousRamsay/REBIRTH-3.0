// Deferred actual-method fixture. Native storage/action primitives are doubles.
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Collections.Generic;
struct Vector3i : IEquatable<Vector3i> {public int x,y,z;public Vector3i(int x,int y,int z){this.x=x;this.y=y;this.z=z;}public static Vector3i up=>new Vector3i(0,1,0);public static Vector3i operator+(Vector3i a,Vector3i b)=>new Vector3i(a.x+b.x,a.y+b.y,a.z+b.z);public bool Equals(Vector3i b)=>x==b.x&&y==b.y&&z==b.z;public override bool Equals(object o)=>o is Vector3i b&&Equals(b);public override int GetHashCode()=>x^y^z;}
struct BlockValueRef {public Vector3i Position;public BlockValueRef(Vector3i p){Position=p;}}
enum EnumDropEvent {Destroy,Harvest}
class Block {public BlockValue DowngradeBlock;public int Stage2Health,MaxDamage=100;public Dictionary<EnumDropEvent,List<Drop>> itemsToDrop=new Dictionary<EnumDropEvent,List<Drop>>();public virtual int DamageBlock(World w,BlockValueRef p,BlockValue value,int damage,int actor,object hit,bool tool,bool bypass){w.Damages++;w.Values[p.Position]=BlockValue.Air;AdvancedFarmingHarvestSettlement.NoteNativeDamage(w,p.Position);if(w.ThrowAfter)throw new IOException();return value.damage+damage;}}
class BlockFarmPlotRebirth:Block {}
class Drop {public string name;}
struct BlockValue {public uint rawData;public static Dictionary<uint,Block> Types=new Dictionary<uint,Block>();public static BlockValue Air=>new BlockValue();public bool isair=>rawData==0;public int damage=>rawData==7?80:81;public Block Block=>Types.TryGetValue(rawData,out var b)?b:null;}
class ItemStack {public string Id;public int count;public ItemStack(string id,int count){Id=id;this.count=count;}public ItemStack Clone()=>new ItemStack(Id,count);}
class EntityPlayer {public int entityId=4;}
class Hit {public bool bKilled;public int damageGiven,damageMax;public float damageTotalOfTarget;public BlockValue blockBeingDamaged;}
class ItemActionData {public Hit attackDetails;}
class World {public Dictionary<Vector3i,BlockValue> Values=new Dictionary<Vector3i,BlockValue>();public int Damages;public bool ThrowAfter;public BlockValue GetBlock(Vector3i p)=>Values.TryGetValue(p,out var value)?value:BlockValue.Air;}
static class AdvancedFarmingHarvestReceipt {
 internal sealed class Record {internal Guid Operation,Source;internal int X=1,Y=61,Z=1;internal string Terminal;}
 internal static string OutputImage(ItemStack[] items){using(var s=new MemoryStream())using(var w=new BinaryWriter(s)){w.Write(items.Length);foreach(var i in items){w.Write(i.Id);w.Write(i.count);}return Convert.ToBase64String(s.ToArray());}}
 internal static ItemStack[] ReadOutputs(string image){using(var s=new MemoryStream(Convert.FromBase64String(image)))using(var r=new BinaryReader(s)){int count=r.ReadInt32();if(count<0||count>128)throw new InvalidDataException();var items=new ItemStack[count];for(int i=0;i<count;i++)items[i]=new ItemStack(r.ReadString(),r.ReadInt32());if(s.Position!=s.Length)throw new InvalidDataException();return items;}}
}
static class AdvancedFarmingHarvestProtocol {
// PRODUCTION_TERMINAL_XML
}
static class AdvancedFarmingHarvestSettlement {
 internal sealed class NativeMutation:IDisposable {internal World World;internal Vector3i Position;internal Guid Operation,Source;internal uint Before,After;internal bool Observed;public void Dispose(){if(ReferenceEquals(Active,this))Active=null;}}
 private static NativeMutation Active;
 internal static NativeMutation BeginNativeMutation(World w,Vector3i p,Guid operation,Guid source,uint before,uint after){var value=new NativeMutation{World=w,Position=p,Operation=operation,Source=source,Before=before,After=after};Active=value;return value;}
 internal static void NoteNativeDamage(World w,Vector3i p){if(Active!=null&&ReferenceEquals(Active.World,w)&&Active.Position.Equals(p))Active.Observed=true;}
 internal static bool PublishNativeMutation(NativeMutation value)=>value.Observed&&RecordCommit(value.World,value.Position,value.Operation,value.Source,value.Before,value.After);
 internal static Guid Source;internal static bool Bound=true;internal static HashSet<Guid> Commits=new HashSet<Guid>();
 internal static bool MatchesSource(World world,Vector3i p,string source)=>Bound&&source==Source.ToString("N");
 internal static bool HasCommit(World world,Vector3i p,Guid operation,Guid source,uint before,uint after)=>Commits.Contains(operation);
 internal static bool BindDestination(World world,Vector3i p,Guid op,BlockValue value)=>true;
 internal static bool RecordCommit(World world,Vector3i p,Guid operation,Guid source,uint before,uint after){Commits.Add(operation);return true;}
}
static class AdvancedFarmingHarvestTerminalPlot {
 internal static bool Committing;
 private static World committingWorld;private static Vector3i committingPosition;private static uint committingRaw;
 private sealed class Pending {internal bool Harvesting,PriorKilled;internal int PriorGiven,PriorMax,SyntheticGiven,SyntheticMax;internal float PriorTotal,SyntheticTotal;internal ItemActionData Action;internal Hit Hit;}
// PRODUCTION_CAPSULE
// PRODUCTION_METHODS
 public static void CheckRestore(){var hit=new Hit{bKilled=true,damageGiven=20,damageMax=100,damageTotalOfTarget=100,blockBeingDamaged=BlockValue.Air};var p=new Pending{Harvesting=true,PriorKilled=false,PriorGiven=0,PriorMax=100,PriorTotal=110,SyntheticGiven=20,SyntheticMax=100,SyntheticTotal=100,Hit=hit,Action=new ItemActionData{attackDetails=hit}};RestoreSynthesized(p);if(hit.bKilled||hit.damageGiven!=0||hit.damageTotalOfTarget!=110||!hit.blockBeingDamaged.isair)throw new Exception("synthesized fields escaped or native Air clear was undone");hit.bKilled=false;hit.damageGiven=77;hit.damageMax=99;hit.damageTotalOfTarget=88;RestoreSynthesized(p);if(hit.bKilled||hit.damageGiven!=77||hit.damageMax!=99||hit.damageTotalOfTarget!=88)throw new Exception("native observer changes overwritten");}
}
static class Fixture {
 static void Check(bool value,string text){if(!value)throw new Exception(text);}
 static AdvancedFarmingHarvestReceipt.Record Make(){var c=new AdvancedFarmingHarvestTerminalPlot.Capsule{Source=AdvancedFarmingHarvestSettlement.Source,Raw=7,Damage=20,CropOutputs=AdvancedFarmingHarvestReceipt.OutputImage(new[]{new ItemStack("corn",2)}),PlotOutputs=AdvancedFarmingHarvestReceipt.OutputImage(new[]{new ItemStack("plot",1)}),Tool=AdvancedFarmingHarvestReceipt.OutputImage(new[]{new ItemStack("shovel",1)})};return new AdvancedFarmingHarvestReceipt.Record{Operation=Guid.NewGuid(),Source=Guid.NewGuid(),Terminal=AdvancedFarmingHarvestTerminalPlot.Encode(c)};}
 static void Main(){var plot=new BlockFarmPlotRebirth();plot.itemsToDrop[EnumDropEvent.Destroy]=new List<Drop>{new Drop{name="farmPlotBlockVariantHelper"}};BlockValue.Types[7]=plot;BlockValue.Types[8]=plot;AdvancedFarmingHarvestSettlement.Source=Guid.NewGuid();var r=Make();var world=new World();var owner=new EntityPlayer();var pos=new Vector3i(1,60,1);world.Values[pos]=new BlockValue{rawData=7};
  Check(AdvancedFarmingHarvestTerminalPlot.Commit(world,owner,r),"accepted original terminal action failed");Check(world.GetBlock(pos).isair&&world.Damages==1,"original native plot continuation missing");
  Check(AdvancedFarmingHarvestTerminalPlot.Commit(world,owner,r)&&world.Damages==1,"original terminal continuation replayed");
  r=Make();world.Values[pos]=new BlockValue{rawData=8};Check(!AdvancedFarmingHarvestTerminalPlot.Commit(world,owner,r)&&world.Damages==1,"later plot damage was rebound");
  world.Values[pos]=new BlockValue{rawData=7};AdvancedFarmingHarvestSettlement.Bound=false;Check(!AdvancedFarmingHarvestTerminalPlot.Commit(world,owner,r)&&world.Damages==1,"replacement incarnation was consumed");AdvancedFarmingHarvestSettlement.Bound=true;
  world.Values[pos+Vector3i.up]=new BlockValue{rawData=7};Check(!AdvancedFarmingHarvestTerminalPlot.Commit(world,owner,r)&&world.Damages==1,"new crop above plot was removed");world.Values.Remove(pos+Vector3i.up);
  world.ThrowAfter=true;try{AdvancedFarmingHarvestTerminalPlot.Commit(world,owner,r);throw new Exception("expected native partial failure");}catch(IOException){}Check(AdvancedFarmingHarvestTerminalPlot.Committed(world,r)&&!AdvancedFarmingHarvestTerminalPlot.Committing,"partial native terminal completion lost its marker");
  world.ThrowAfter=false;Check(AdvancedFarmingHarvestTerminalPlot.Commit(world,owner,r)&&world.Damages==2,"partial original continuation replayed");
  var malformed=XElement.Parse(r.Terminal);malformed.SetAttributeValue("damage",1);try{AdvancedFarmingHarvestTerminalPlot.Decode(malformed.ToString(SaveOptions.DisableFormatting));throw new Exception("nonterminal damage accepted");}catch(InvalidDataException){}
  AdvancedFarmingHarvestTerminalPlot.CheckRestore();
  Console.WriteLine("PASS actual terminal methods: combined capsule, exact damage/incarnation, no extra hit, partial failure marker, no replay, scoped synthetic field restore preserving native Air/observer changes; native damage/storage doubled");
 }
}
