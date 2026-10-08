using System;
public class EntityPlayer {public World world;public int entityId=7;public bool isEntityRemote=true,Dead;public bool IsDead()=>Dead;}
public class World {public EntityPlayer Player;public bool Remote;public bool IsRemote()=>Remote;public object GetEntity(int id)=>Player?.entityId==id?Player:null;}
public class GameManager {public static GameManager Instance=new GameManager();public World World;}
public class ClientInfo {public object InternalId=new object();public int entityId=7;public Data latestPlayerData=new Data();}
public class Data {public ItemStack[] Bag=new ItemStack[52],Belt=new ItemStack[20];}
public class ItemStack {}
public class Origin {public string CreationId="current";}
public class RebirthWorldCharacterRecord {public object Support=new object();public bool IsComplete=true;public Origin Origin=new Origin();}
public static class RebirthWorldCharacterRepository {public static bool IsServerAuthority=true;}
public static class RebirthSurvivorMode {public static bool Enabled=true;public static bool IsEnabledForCurrentWorld()=>Enabled;}
public static class RebirthCharacterCreationHoldService {public static bool Held;public static bool IsHeld(EntityPlayer p)=>Held;}
public static class RebirthWorldCharacterService {public static RebirthWorldCharacterRecord Record=new RebirthWorldCharacterRecord();public static bool TryGet(EntityPlayer p,out RebirthWorldCharacterRecord r){r=Record;return r!=null;}}
public static class RebirthSurvivorRequestScope {public static bool Matches(string a,string b)=>!string.IsNullOrEmpty(a)&&a==b;}
public static class RebirthPlayerDataInventory {public static Action OnRead;public static int Reads;public static ItemStack[] ReadSlots(Data d,bool bag){Reads++;OnRead?.Invoke();return d==null?null:bag?d.Bag:d.Belt;}}
public static class RebirthSurvivorGearService {public static int Desired=52;public static int GetDesiredPhysicalBagSlots(RebirthWorldCharacterRecord r)=>Desired;}
public static class RebirthToolbeltCapacity {public static int GetOwnedSlotCount(EntityPlayer p,int length)=>Math.Min(8,length);}
public class RebirthGearInventorySnapshot {public ItemStack[] Bag,Belt;public int Owned;public static bool TryCapture(ItemStack[] b,ItemStack[] t,int n,out RebirthGearInventorySnapshot s){s=new RebirthGearInventorySnapshot{Bag=b,Belt=t,Owned=n};return true;}}
class Program {
 static int count;static EntityPlayer player;static ClientInfo sender;
 static void Reset(){player=new EntityPlayer();var w=new World{Player=player};player.world=w;GameManager.Instance.World=w;sender=new ClientInfo();RebirthWorldCharacterRepository.IsServerAuthority=true;RebirthSurvivorMode.Enabled=true;RebirthCharacterCreationHoldService.Held=false;RebirthWorldCharacterService.Record=new RebirthWorldCharacterRecord();RebirthPlayerDataInventory.Reads=0;RebirthPlayerDataInventory.OnRead=null;RebirthSurvivorGearService.Desired=52;}
 static bool Capture(string creation="current")=>RebirthRemoteGearInventorySource.TryCapture(player,sender,creation,out _);
 static void Check(bool b,string n){if(!b)throw new Exception(n);count++;}
 static void Reject(Action change,string name){Reset();change();Check(!Capture(),name);}
 static void Main(){
 Reset();Check(RebirthRemoteGearInventorySource.TryCapture(player,sender,"current",out var s)&&ReferenceEquals(s.Bag,sender.latestPlayerData.Bag)&&s.Owned==8,"authenticated native snapshot unavailable");
 Reject(()=>sender.entityId=8,"other sender accepted");Check(RebirthPlayerDataInventory.Reads==0,"unauthenticated snapshot decoded");
 Reject(()=>sender.InternalId=null,"missing identity accepted");
 Reject(()=>player.isEntityRemote=false,"local mirror route accepted");
 Reject(()=>player.Dead=true,"dead owner accepted");
 Reject(()=>RebirthCharacterCreationHoldService.Held=true,"creation hold bypassed");
 Reject(()=>GameManager.Instance.World.Remote=true,"remote world accepted");
 Reject(()=>GameManager.Instance.World=new World{Player=player},"wrong world accepted");
 Reject(()=>player.world.Player=new EntityPlayer(),"replaced actor accepted");
 Reset();Check(!Capture("old")&&RebirthPlayerDataInventory.Reads==0,"stale Survivor accepted");
 Reject(()=>sender.latestPlayerData=null,"missing native snapshot fell back");
 Reject(()=>sender.latestPlayerData.Bag=null,"missing bag fell back");
 Reject(()=>sender.latestPlayerData.Belt=null,"missing belt fell back");
 Reject(()=>sender.latestPlayerData.Bag=new ItemStack[65],"capacity mismatch accepted");
 Reject(()=>RebirthWorldCharacterService.Record.IsComplete=false,"incomplete record accepted");
 Reject(()=>RebirthWorldCharacterService.Record.Support=null,"missing gear state accepted");
 Reject(()=>RebirthWorldCharacterRepository.IsServerAuthority=false,"nonauthoritative route accepted");
 Reject(()=>RebirthPlayerDataInventory.OnRead=()=>sender.latestPlayerData=new Data(),"replacement upload during read accepted");
 Reject(()=>RebirthPlayerDataInventory.OnRead=()=>RebirthWorldCharacterService.Record=new RebirthWorldCharacterRecord(),"replacement character record during read accepted");
 Reject(()=>RebirthPlayerDataInventory.OnRead=()=>GameManager.Instance.World=new World(),"world replacement during read accepted");
 Console.WriteLine("PASS "+count+" actual admission-source checks with native/service doubles; no serialization, networking or equip validation.");
 }
}