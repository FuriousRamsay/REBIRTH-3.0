using System;using System.Collections.Generic;
struct Vector3 {public static Vector3 up,zero;public static Vector3 operator +(Vector3 a,Vector3 b){return a;}}
class ItemStack {public int count;public static ItemStack Empty=new ItemStack();public bool IsEmpty(){return count==0;}public ItemStack Clone(){return new ItemStack{count=count};}}
class Bag {public ItemStack[] Items={new ItemStack{count=1},new ItemStack{count=7}};public ItemStack[] GetSlots(){return Items;}public void SetSlot(int i,ItemStack s){Items[i]=s;}}
class Inventory {public int holdingItemIdx=1;public ItemStack[] Items={new ItemStack{count=1},new ItemStack{count=7},new ItemStack{count=99}};public ItemStack[] GetSlots(){return Items;}public void SetItem(int i,ItemStack s){Items[i]=s;}public void SetHoldingItemIdx(int i){holdingItemIdx=i;}}
class World {public bool Remote;public GameManager gameManager;public bool IsRemote(){return Remote;}public void SpawnEntityInWorld(EntityLootContainer e){gameManager.Dropped+=e.Contents[0].count;}}
class EntityPlayer {public World world;public int entityId=1;public Inventory inventory=new Inventory();public Bag bag=new Bag();public Vector3 GetPosition(){return Vector3.zero;}}
class ConnectionManager {public bool IsServer=true;}
class SingletonMonoBehaviour<T>{public static T Instance;}
class GameManager {public static GameManager Instance;public World World;public int Dropped;public bool Fail;public void ItemDropServer(ItemStack s,Vector3 a,Vector3 b,int id,float life,bool head){if(Fail)throw new Exception("Drop rejected");if(life!=1800)throw new Exception("Lifetime");Dropped+=s.count;}}
class EntityLootContainer {public int spawnById;public ItemStack[] Contents;public void SetContent(ItemStack[] c){Contents=c;}}
class EntityFactory {public static object CreateEntity(int id,Vector3 p,Vector3 r){return new EntityLootContainer();}}
class RebirthSurvivorGearService {public const string BackpackSlotId="backpack",BeltSlotId="belt";}
class RebirthToolbeltCapacity {public const int BackingPublicSlots=18;}
class Log {public static void Warning(string s){}}
class Subject {public const float ToolbeltLifetimeSeconds=1800;public const string BackpackEntity="bag";
// METHODS
}
class Check {
 static EntityPlayer Setup(){var w=new World();var gm=new GameManager{World=w};w.gameManager=gm;GameManager.Instance=gm;SingletonMonoBehaviour<ConnectionManager>.Instance=new ConnectionManager();return new EntityPlayer{world=w};}
 static void Reject(EntityPlayer p){if(Subject.TryRelease(p,"belt",1,1)||p.inventory.Items[1].count!=7||p.inventory.Items[2].count!=99)throw new Exception("Rejected state mutated");}
 static void Main(){
  var p=Setup();p.world.Remote=true;Reject(p);
  p=Setup();SingletonMonoBehaviour<ConnectionManager>.Instance=null;Reject(p);
  p=Setup();SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer=false;Reject(p);
  p=Setup();GameManager.Instance.World=null;Reject(p);
  p=Setup();GameManager.Instance.World=new World();Reject(p);
  p=Setup();p.world.gameManager=new GameManager();Reject(p);
  p=Setup();GameManager.Instance=null;Reject(p);
  p=Setup();GameManager.Instance.Fail=true;Reject(p);
  p=Setup();if(!Subject.TryRelease(p,"belt",1,1)||GameManager.Instance.Dropped!=7||p.inventory.Items[1].count!=0||p.inventory.Items[2].count!=99||p.inventory.holdingItemIdx!=0)throw new Exception("Normal belt recovery");
  p=Setup();if(!Subject.TryRelease(p,"backpack",1,1)||GameManager.Instance.Dropped!=7||p.bag.Items[1].count!=0||p.bag.Items[0].count!=1)throw new Exception("Normal pack recovery");
  Console.WriteLine("PASS: actual TryRelease rejects invalid authority/world without mutation; failed pre-drop retains source; valid belt excludes dummy and pack releases tail. Native transport/spawn stubbed.");
 }
}
