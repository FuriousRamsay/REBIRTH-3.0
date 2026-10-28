using System;
using System.IO;
using System.Collections.Generic;
using System.Text;using System.Xml.Linq;
using HarmonyLib;
namespace HarmonyLib {class Harmony{}}
static class RebirthHarmonyBootstrap {public static void PatchClassOnce(Harmony h,Type t){}}
class AdvancedFarmingHarvestSourceCommit{}
struct Vector3i {public static bool operator==(Vector3i a,Vector3i b)=>a.x==b.x&&a.y==b.y&&a.z==b.z;public static bool operator!=(Vector3i a,Vector3i b)=>!(a==b);public override bool Equals(object o)=>o is Vector3i v&&this==v;public override int GetHashCode()=>x^y^z;public int x,y,z;public Vector3i(int x,int y,int z){this.x=x;this.y=y;this.z=z;}}
class Block{}
class BlockFarmPlotRebirth:Block{}
class BlockPlantGrowingRebirth:Block{}
class BlockWildCropPickupRebirth:Block{}
struct BlockValue {public uint rawData;public ushort type;public byte meta,meta2,rotation;public int damage;public Block Block;}
class TileEntity{}
class ChunkCustomData {public byte[] data;public ChunkCustomData(string key,ulong ticks,bool sync){}}
class CustomData {public Dictionary<string,ChunkCustomData> dict=new Dictionary<string,ChunkCustomData>();public void Set(string key,ChunkCustomData value){dict[key]=value;}public void Remove(string key){dict.Remove(key);}}
class Chunk {public int X,Z;public bool InProgressUnloading,IsLocked,NeedsDecoration,InProgressDecorating,isModified;public CustomData ChunkCustomData=new CustomData();public BlockValue Block;public TileEntity Tile=new TileEntity();public BlockValue GetBlock(Vector3i p)=>Block;public TileEntity GetTileEntity(Vector3i p)=>Tile;}
static class AdvancedFarmingHarvestProtocol {public static void SourceChanged(WorldBase w,Vector3i p){}}
class WorldState {public string Guid=System.Guid.NewGuid().ToString();}
static class GameIO {public static string Root=Path.Combine(Path.GetTempPath(),"rebirth-harvest-source-"+Guid.NewGuid().ToString("N"));public static string GetSaveGameDir()=>Root;}
static class RebirthProductiveEarnedObservationBudget {private sealed class Lease:IDisposable {public void Dispose(){}}public static bool TryRetainDetached(long bytes,out IDisposable lease){lease=new Lease();return true;}}
static class RebirthDurableFileCommit {public static bool TryPublish(string a,string b,out string why){why="fixture";try{File.Move(a,b);return true;}catch{return false;}}}
class WorldBase {public WorldState worldState=new WorldState();public Chunk Chunk=new Chunk();public bool Remote;public bool IsRemote()=>Remote;public object GetChunkSync(int x,int z)=>Chunk;public BlockValue GetBlock(Vector3i p)=>Chunk.Block;}
class GameManager {public static GameManager Instance=new GameManager();public WorldBase World;}
class World:WorldBase {public static int toChunkXZ(int value)=>value>>4;}
static class ThreadManager {public static bool IsMainThread()=>true;}
// PRODUCTION_SOURCE
static class Fixture {
 static void Check(bool value,string message){if(!value)throw new Exception(message);}

 static void NativeMutationCases(World world,Vector3i pos){world.Remote=false;world.Chunk=new Chunk{Block=new BlockValue{rawData=7,type=7,Block=new BlockPlantGrowingRebirth()}};string source=AdvancedFarmingHarvestSettlement.EnsureSource(world,pos);var before=new AdvancedFarmingHarvestSettlement.SourceBefore{Block=world.Chunk.Block,Tile=world.Chunk.Tile};var original=world.Chunk.Block;var operation=Guid.NewGuid();
 using(var scope=AdvancedFarmingHarvestSettlement.BeginNativeMutation(world,pos,operation,Guid.ParseExact(source,"N"),7,0)){Check(scope!=null,"actual source attempt refused");var call=AdvancedFarmingHarvestSettlement.EnterNativeMutation(world.Chunk,world,pos,before);world.Chunk.Block=default(BlockValue);AdvancedFarmingHarvestSettlement.ObserveNativeMutation(world.Chunk,world,pos,false,call);Check(!AdvancedFarmingHarvestSettlement.PublishNativeMutation(scope),"skipped original/equal destination became native source proof");}
 world.Chunk.Block=original;Check(AdvancedFarmingHarvestSettlement.BeginNativeMutation(world,pos,operation,Guid.ParseExact(source,"N"),7,0)==null,"unknown source attempt replayed");operation=Guid.NewGuid();
 using(var scope=AdvancedFarmingHarvestSettlement.BeginNativeMutation(world,pos,operation,Guid.ParseExact(source,"N"),7,0)){Check(scope!=null,"fresh source attempt refused");var call=AdvancedFarmingHarvestSettlement.EnterNativeMutation(world.Chunk,world,pos,before);world.Chunk.Block=default(BlockValue);AdvancedFarmingHarvestSettlement.ObserveNativeMutation(world.Chunk,world,pos,true,call);AdvancedFarmingHarvestSettlement.SourceCommitted(world.Chunk,world,pos,before);Check(AdvancedFarmingHarvestSettlement.PublishNativeMutation(scope),"actual original mutation not published");Check(AdvancedFarmingHarvestSettlement.HasCommit(world,pos,operation,Guid.ParseExact(source,"N"),7,0),"actual commit missing");}
 Directory.Delete(GameIO.Root,true);
 }
 static void Main(){var world=new World();GameManager.Instance.World=world;var pos=new Vector3i(1,60,1);world.Chunk.Block=new BlockValue{type=7,Block=new BlockPlantGrowingRebirth()};
  string original=AdvancedFarmingHarvestSettlement.EnsureSource(world,pos);Check(original!=null&&AdvancedFarmingHarvestSettlement.MatchesSource(world,pos,original),"identity missing");
  var saved=new Dictionary<string,ChunkCustomData>(world.Chunk.ChunkCustomData.dict);world.Chunk=new Chunk{Block=world.Chunk.Block};foreach(var pair in saved)world.Chunk.ChunkCustomData.dict.Add(pair.Key,new ChunkCustomData(pair.Key,0,false){data=(byte[])pair.Value.data.Clone()});
  Check(AdvancedFarmingHarvestSettlement.EnsureSource(world,pos)==original,"reload rotated identity");
  var before=new AdvancedFarmingHarvestSettlement.SourceBefore{Block=world.Chunk.Block,Tile=world.Chunk.Tile};world.Chunk.Block.damage=4;AdvancedFarmingHarvestSettlement.SourceCommitted(world.Chunk,world,pos,before);Check(AdvancedFarmingHarvestSettlement.MatchesSource(world,pos,original),"damage rotated identity");
  before=new AdvancedFarmingHarvestSettlement.SourceBefore{Block=world.Chunk.Block,Tile=world.Chunk.Tile};world.Chunk.Block.type=8;AdvancedFarmingHarvestSettlement.SourceCommitted(world.Chunk,world,pos,before);string grown=AdvancedFarmingHarvestSettlement.EnsureSource(world,pos);Check(grown!=null&&grown!=original,"growth retained old identity");
  before=new AdvancedFarmingHarvestSettlement.SourceBefore{Block=world.Chunk.Block,Tile=world.Chunk.Tile};world.Chunk.Tile=new TileEntity();AdvancedFarmingHarvestSettlement.SourceCommitted(world.Chunk,world,pos,before);Check(AdvancedFarmingHarvestSettlement.EnsureSource(world,pos)!=grown,"replacement tile retained identity");
  var operation=Guid.NewGuid();var source=Guid.NewGuid();Check(AdvancedFarmingHarvestSettlement.RecordCommit(world,pos,operation,source,0,0),"native commit marker refused");
  before=new AdvancedFarmingHarvestSettlement.SourceBefore{Block=world.Chunk.Block,Tile=world.Chunk.Tile};world.Chunk.Block.type=9;AdvancedFarmingHarvestSettlement.SourceCommitted(world.Chunk,world,pos,before);
  world.Chunk.IsLocked=true;Check(AdvancedFarmingHarvestSettlement.HasCommit(world,pos,operation,source,0,0),"growth/save erased historical original commit");world.Chunk.IsLocked=false;
  AdvancedFarmingHarvestSettlement.SourceRecorded(world,pos,operation);Check(!AdvancedFarmingHarvestSettlement.HasCommit(world,pos,operation,source,0,0),"recorded source marker not retired");
  AdvancedFarmingHarvestSettlement.EnsureSource(world,pos);
  foreach(var entry in world.Chunk.ChunkCustomData.dict.Values)entry.data=new byte[1];Check(AdvancedFarmingHarvestSettlement.EnsureSource(world,pos)==null,"corrupt original marker overwritten");
  world.Chunk.ChunkCustomData.dict.Clear();world.Chunk.InProgressUnloading=true;Check(AdvancedFarmingHarvestSettlement.EnsureSource(world,pos)==null,"unloading source admitted");
  world.Chunk.InProgressUnloading=false;world.Remote=true;Check(AdvancedFarmingHarvestSettlement.EnsureSource(world,pos)==null,"client minted source");
  NativeMutationCases(world,pos);
  Console.WriteLine("PASS actual source identity: reload, stage/replacement, damage, quarantine, authority");
 }
}
