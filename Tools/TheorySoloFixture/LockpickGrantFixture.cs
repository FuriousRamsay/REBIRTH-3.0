using System;using System.Collections.Generic;
class TileEntityComposite{}
class TEFeatureAbs{}
class TEFeatureLockPickable:TEFeatureAbs{public TileEntityComposite Parent=new TileEntityComposite();public float lockPickTime=15,unlockCompletion=0;public BlockValue lockpickDowngradeBlock=new BlockValue{type=2};public bool Shared;public bool IsSharedLock(ushort c)=>Shared;public Vector3i ToWorldPos()=>new Vector3i();}
struct BlockValue{public int type;public bool isair=>type==0;}
struct Vector3i{}
class Inventory{public object holdingItemItemValue;}
class EntityPlayer{public World world;public bool Dead;public Inventory inventory=new Inventory();public bool IsDead()=>Dead;}
class World{public bool Remote;public EntityPlayer Player;public object Tile;public bool IsRemote()=>Remote;public EntityPlayer GetEntity(int id)=>Player;public object GetTileEntity(Vector3i p)=>Tile;}
class GameManager{public static GameManager Instance=new GameManager();public World World;}
class PooledBinaryWriter{}
class ConnectionManager{public bool IsServer=true;}
class SingletonMonoBehaviour<T>{public static T Instance;}
static class ThreadManager{public static bool Main=true;public static bool IsMainThread()=>Main;}
class LockManager{public static LockManager Instance=new LockManager();public DictionaryGuard singleLocks=new DictionaryGuard();public struct LockEntry{public object Target;public ushort Channel;public LockEntry(object t,ushort c){Target=t;Channel=c;}}}
class DictionaryGuard{public object Target;public ushort Channel;public int Owner;public bool TryGetByValue(LockManager.LockEntry e,out int owner){owner=Owner;return ReferenceEquals(e.Target,Target)&&Channel==e.Channel;}}
static class EffectManager{public static float Result=15;public static float GetValue(PassiveEffects e,object i,float f,EntityPlayer p)=>Result;}
enum PassiveEffects{LockPickTime}
static class Mathf{public static float Clamp01(float f)=>Math.Min(1,Math.Max(0,f));}
static class RebirthSkillWaveAService{public static int Calls;public static float Remaining;public static void RegisterServerLockpickAttempt(EntityPlayer p,Vector3i x,float a,float b,float remaining,int c){Calls++;Remaining=remaining;}}
static class RebirthTheorySoloLockpickService{public static void Grant(TEFeatureLockPickable f,EntityPlayer p,ushort c,float n,float e,float r){}}
class Check{
// SOURCE
static int count;static void Test(bool accepted,TEFeatureAbs feature,int owner=7,ushort channel=3,bool original=true){int before=RebirthSkillWaveAService.Calls;Postfix(feature,owner,null,channel,original);if((RebirthSkillWaveAService.Calls>before)!=accepted)throw new Exception("grant "+count);count++;}
static void Main(){var f=new TEFeatureLockPickable();var w=new World{Tile=f.Parent};var p=new EntityPlayer{world=w};w.Player=p;GameManager.Instance.World=w;SingletonMonoBehaviour<ConnectionManager>.Instance=new ConnectionManager();var locks=LockManager.Instance.singleLocks;locks.Target=f;locks.Channel=3;locks.Owner=7;Test(true,f);Test(false,f,original:false);ThreadManager.Main=false;Test(false,f);ThreadManager.Main=true;Test(false,new TEFeatureAbs());Test(false,null);Test(false,f,owner:8);Test(false,f,channel:2);locks.Target=new object();Test(false,f);locks.Target=f;w.Tile=new object();Test(false,f);w.Tile=f.Parent;w.Remote=true;Test(false,f);w.Remote=false;SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer=false;Test(false,f);SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer=true;f.Shared=true;Test(false,f);f.Shared=false;p.Dead=true;Test(false,f);p.Dead=false;f.unlockCompletion=1;Test(false,f);f.unlockCompletion=0;f.lockPickTime=float.NaN;Test(false,f);f.lockPickTime=float.PositiveInfinity;Test(false,f);f.lockPickTime=0;Test(false,f);f.lockPickTime=15;f.unlockCompletion=float.NaN;Test(false,f);f.unlockCompletion=.5f;EffectManager.Result=float.NaN;Test(false,f);EffectManager.Result=15;Test(true,f);if(RebirthSkillWaveAService.Remaining!=7.5f)throw new Exception("native remaining duration changed");count++;Console.WriteLine(count+" PASS actual composite native grant postfix");}
}