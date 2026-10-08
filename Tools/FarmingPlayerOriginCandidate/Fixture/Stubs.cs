using System;using System.Collections.Generic;
public struct Vector3i {public int x,y,z;public Vector3i(int a,int b,int c){x=a;y=b;z=c;}public override bool Equals(object value)=>value is Vector3i p&&p.x==x&&p.y==y&&p.z==z;public override int GetHashCode()=>HashCode.Combine(x,y,z);}
public class Block{}
public class BlockPlantGrowing:Block {public BlockValue nextPlant;public int Scheduled;public Action OnSchedule;public void addScheduledTick(WorldBase world,Vector3i pos){Scheduled++;OnSchedule?.Invoke();}}
public struct BlockValue {public int type,rotation,meta,meta2,damage;public Block Block;public bool isair=>type==0;}
public class WBT {public readonly object lockObject=new object();public Dictionary<int,WorldBlockTickerEntry> scheduledTicksDict=new Dictionary<int,WorldBlockTickerEntry>();public int Invalidations;public Action OnInvalidate;public void InvalidateScheduledBlockUpdate(Vector3i pos,int type){Invalidations++;scheduledTicksDict.Remove(WorldBlockTickerEntry.ToHashCode(pos,type));OnInvalidate?.Invoke();}}
public class WorldBase {public bool Remote;public readonly Dictionary<Vector3i,BlockValue> Blocks=new Dictionary<Vector3i,BlockValue>();public readonly Dictionary<Vector3i,TileEntityPlantGrowingRebirth> Tiles=new Dictionary<Vector3i,TileEntityPlantGrowingRebirth>();public WBT Scheduler=new WBT();public object GetTileEntity(Vector3i pos)=>Tiles.TryGetValue(pos,out var tile)?tile:null;public BlockValue GetBlock(Vector3i pos)=>Blocks.TryGetValue(pos,out var block)?block:default;public bool IsRemote()=>Remote;public WBT GetWBT()=>Scheduler;}
public partial class TileEntityPlantGrowingRebirth {public ulong PlantIncarnation,StateRevision;public AdvancedFarmingPlantOrigin PlantOrigin;public int Modified,Advanced;public Action OnModified,OnAdvance;public void SetModified(){Modified++;OnModified?.Invoke();}}
public static class AdvancedFarmingHoverTextService {public static int Invalidations;public static Action OnInvalidate;public static void InvalidatePlantState(Vector3i pos){Invalidations++;OnInvalidate?.Invoke();}public static void Reset(){Invalidations=0;OnInvalidate=null;}}public class World:WorldBase {public EntityPlayerLocal Primary;public EntityPlayerLocal GetPrimaryPlayer()=>Primary;}
public class EntityPlayerLocal {public Inventory inventory=new Inventory();}
public class Inventory {public ItemInventoryData holdingItemData;public ItemStack holdingItemStack=new ItemStack{count=1};}
public class ItemStack {public int count;}
public class ItemValue {public BlockValue Value;public BlockValue ToBlockValue()=>Value;}
public class ItemInventoryData {public object holdingEntity;public World world;public bool IsBlock=true;public ItemValue itemValue=new ItemValue();}
public class BlockPlantGrowingRebirth:BlockPlantGrowing {public bool Seed=true;public bool IsSeedStage(BlockValue value)=>Seed;}
public class BlockPlacement {public enum EnumPlacement{Voxel,Prop};public struct Result {public Vector3i blockPos;public BlockValue blockValue;public EnumPlacement placement;}}
public class GameManager {public static GameManager Instance;public World World;public bool Editor;public bool IsEditMode()=>Editor;}
public class WorldBlockTickerEntry {public Vector3i worldPos;public int blockID;public static int ToHashCode(Vector3i _pos,int _blockID){return (_pos.GetHashCode()*397)^_blockID;}}
