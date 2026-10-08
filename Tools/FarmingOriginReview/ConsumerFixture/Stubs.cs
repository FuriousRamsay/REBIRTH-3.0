using System;using System.Collections.Generic;
public struct Vector3i{public int x,y,z;public Vector3i(int a,int b,int c){x=a;y=b;z=c;}}
public struct BlockValue{public int type,damage,rotation,meta,meta2;public Block Block;public bool isair=>type==0;}
public struct BlockValueRef{public Vector3i BlockPosition;}
public class EntityAlive{}public class EntityPlayer:EntityAlive{}
public class ItemActionAttack{public class AttackHitInfo{}}
public class WorldBase{public TileEntityPlantGrowingRebirth Tile=new TileEntityPlantGrowingRebirth{PlantIncarnation=10,StateRevision=1,PlantOrigin=AdvancedFarmingPlantOrigin.Player};public object GetTileEntity(Vector3i p)=>Tile;public bool Remote;public bool Player{get=>Tile!=null&&Tile.PlantOrigin==AdvancedFarmingPlantOrigin.Player;set{if(Tile!=null)Tile.PlantOrigin=value?AdvancedFarmingPlantOrigin.Player:AdvancedFarmingPlantOrigin.Unknown;}}public BlockValue Value=new BlockValue{type=7};public WBT Ticker=new WBT();public bool IsRemote()=>Remote;public object GetEntity(int id)=>new EntityPlayer();public WBT GetWBT()=>Ticker;public BlockValue GetBlock(Vector3i p)=>Value;}
public class WBT{public object lockObject=new object();public Dictionary<int,WorldBlockTickerEntry> scheduledTicksDict=new Dictionary<int,WorldBlockTickerEntry>();public int Cancel,Add;public void InvalidateScheduledBlockUpdate(Vector3i p,int id){Cancel++;scheduledTicksDict.Remove(WorldBlockTickerEntry.ToHashCode(p,id));}public void AddScheduledBlockUpdate(Vector3i p,int id,ulong delay){Add++;scheduledTicksDict[WorldBlockTickerEntry.ToHashCode(p,id)]=new WorldBlockTickerEntry{worldPos=p,blockID=id};}}
public class WorldBlockTickerEntry{public Vector3i worldPos;public int blockID;public static int ToHashCode(Vector3i p,int id)=>(p.GetHashCode()*397)^id;}
public class Block{} public class BlockPlantGrowing:Block{public BlockValue nextPlant=new BlockValue{type=8};public int NativeScheduled;public void addScheduledTick(WorldBase w,Vector3i p){NativeScheduled++;}public int NativeCalls,NativeAdded;public virtual void OnBlockAdded(WorldBase w,Chunk c,Vector3i p,BlockValue v,PlatformUserIdentifierAbs by){NativeAdded++;}public virtual int OnBlockDamaged(WorldBase w,BlockValueRef r,BlockValue v,int damage,int actor,ItemActionAttack.AttackHitInfo hit,bool tool,bool bypass,int depth=0){NativeCalls++;return 42;}}
public static class AdvancedFarmingRuntimePolicy{public static bool Enabled=true;public static ulong GetPlantWakeDelay(Vector3i p,ulong ticks)=>ticks;}
public static class RebirthUtilities{public static int CustomCalls;public static bool IsHoldingShovel(EntityAlive p)=>true;public static bool ReturnImmatureCropSeedAndClear(WorldBase w,Vector3i p,EntityPlayer e){CustomCalls++;return false;}public static bool IsPlayerGrownHarvestCrop(BlockValue b)=>true;public static bool HarvestPlayerCropAndReplant(WorldBase w,Vector3i p,BlockValue b,EntityPlayer e){CustomCalls++;return true;}}public class Chunk{}public class PlatformUserIdentifierAbs{}
public static class AdvancedFarmingActiveAreaRegistry{public static bool Active;public static bool IsPlantInActiveFarm(WorldBase w,Vector3i p)=>Active;}
public static class RebirthCropProvenanceAdapter{public static int Pending,Captured;public static void ApplyPending(WorldBase w,Vector3i p){Pending++;}public static void CapturePlanting(WorldBase w,Vector3i p,PlatformUserIdentifierAbs by){Captured++;}}
public class ConnectionManager{public bool IsServer=true;}public class SingletonMonoBehaviour<T>where T:new(){public static T Instance=new T();}public class GameManager{public static GameManager Instance=new GameManager();public World World;public bool IsEditMode()=>false;}
public class TileEntityPlantGrowingRebirth{public void AdvanceStateRevision()
    {
        EnsureAuthoritativeIncarnation();
        if (StateRevision == ulong.MaxValue)
        {
            // Preserve monotonic ordering by moving to a new incarnation rather than wrapping.
            var retainedOrigin = PlantOrigin;
            BeginNewPlantIncarnation();
            PlantOrigin = retainedOrigin; // same plant; revision rollover only
            return;
        }
        StateRevision++;
    }public int Modified;public void SetModified(){Modified++;}public AdvancedFarmingPlantOrigin PlantOrigin;public ulong PlantIncarnation,StateRevision;private static readonly object PlantIncarnationSync=new object();private static ulong s_nextPlantIncarnation=1UL;public void BeginNewPlantIncarnation()
    {
        PlantOrigin = AdvancedFarmingPlantOrigin.Unknown;
        PlantIncarnation = AllocatePlantIncarnation();
        StateRevision = 1UL;
    }public void EnsureAuthoritativeIncarnation()
    {
        if (PlantIncarnation == 0UL)
            PlantIncarnation = AllocatePlantIncarnation();
        ObservePlantIncarnation(PlantIncarnation);
        if (StateRevision == 0UL)
            StateRevision = 1UL;
    }private static ulong AllocatePlantIncarnation()
    {
        lock (PlantIncarnationSync)
        {
            ulong value = s_nextPlantIncarnation;
            if (value == 0UL || value == ulong.MaxValue)
                value = 1UL;
            s_nextPlantIncarnation = value + 1UL;
            return value;
        }
    }private static void ObservePlantIncarnation(ulong incarnation)
    {
        if (incarnation == 0UL)
            return;
        lock (PlantIncarnationSync)
        {
            if (incarnation >= s_nextPlantIncarnation)
                s_nextPlantIncarnation = incarnation == ulong.MaxValue ? 1UL : incarnation + 1UL;
        }
    }}public static class AdvancedFarmingHoverTextService{public static void InvalidatePlantState(Vector3i p){}}
public class World:WorldBase{public EntityPlayerLocal Primary;public EntityPlayerLocal GetPrimaryPlayer()=>Primary;}
public class EntityPlayerLocal:EntityPlayer{public Inventory inventory=new Inventory();}
public class Inventory{public ItemInventoryData holdingItemData;public ItemStack holdingItemStack=new ItemStack{count=1};}
public class ItemStack{public int count;}
public class ItemValue{public BlockValue Value;public BlockValue ToBlockValue()=>Value;}
public class ItemInventoryData{public object holdingEntity;public World world;public bool IsBlock=true;public ItemValue itemValue=new ItemValue();}
public class BlockPlacement{public enum EnumPlacement{Voxel,Prop};public struct Result{public Vector3i blockPos;public BlockValue blockValue;public EnumPlacement placement;}}