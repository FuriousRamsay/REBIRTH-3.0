using System;using System.Collections;
namespace HarmonyLib{[AttributeUsage(AttributeTargets.Class)]public class HarmonyPatch:Attribute{public HarmonyPatch(Type t,string s){}public HarmonyPatch(Type t,string s,Type[] a){}}}
namespace UnityEngine{public class WaitForSeconds{public WaitForSeconds(float f){}}}
public struct Vector3i{public int x,y,z;}
public struct BlockValue{public uint rawData;public int damage;public Block Block;}
public class Block{}
public class BlockPlantGrowingRebirth:Block{public bool IsSeedStage(BlockValue v)=>true;}
public class TextureFullArray{public bool IsDefault=true;}
public class ItemValue{public byte Id;public TextureFullArray TextureFullArray=new TextureFullArray();}
public class ItemStack{public int count;public ItemValue itemValue;public ItemStack(int n,byte id){count=n;itemValue=new ItemValue{Id=id};}}
public class ItemInventoryData{public bool IsBlock=true;public object holdingEntity;public World world;public ItemValue itemValue;}
public class World{public object Player;public bool IsRemote()=>true;public object GetPrimaryPlayer()=>Player;public BlockValue GetBlock(Vector3i p)=>default;}
public class GameManager{public static GameManager Instance;public World World;public bool IsEditMode()=>false;}
public class EntityPlayerLocal{public Inventory inventory=new Inventory();public bool IsDead()=>false;public bool IsSpawned()=>true;}
public class Inventory{public ItemInventoryData holdingItemData;public int holdingItemIdx;public ItemStack Stack;public ItemStack GetItem(int i)=>Stack;public void SetItem(int i,ItemStack s){object state;SeedNativeSetterWitnessPatch.Prefix(this,i,s,out state);Stack=s;SeedNativeSetterWitnessPatch.Postfix(state);}}
public class BlockPlacement{public enum EnumPlacement{Voxel,Free}public struct Result{public EnumPlacement placement;public BlockValue blockValue;public Vector3i blockPos;}}
public class BlockToolSelection{public bool PlaceBlock()=>true;public IEnumerator decInventoryLater()=>null;}
public static class SeedPlacementIntentCodecReview{public static bool TrySeedIdentity(ItemValue v,out byte[] b){b=v==null?null:new byte[]{v.Id};return b!=null;}}

