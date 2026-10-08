using System;
using System.IO;
using System.Collections.Generic;

// Narrow native substitutes: this verifies production delivery control flow and receipts,
// not actual game inventory, networking or serialization behavior.
namespace UnityEngine { public static class Time { public static float realtimeSinceStartup; } }
namespace UnityEngine.Scripting { public class PreserveAttribute : Attribute { } }
public class World { public EntityPlayerLocal Player; public EntityPlayerLocal GetPrimaryPlayer() => Player; }
public class EntityPlayer { public World world; public int entityId; }
public class EntityPlayerLocal : EntityPlayer {
    public bool Spawned=true, Dead; public Buffs Buffs=new();
    public bool IsSpawned()=>Spawned; public bool IsDead()=>Dead;
}
public class Buffs {
    public Dictionary<string,float> Values=new();
    public float GetCustomVar(string key)=>Values.GetValueOrDefault(key);
    public void SetCustomVar(string key,float value,bool sync)=>Values[key]=value;
}
public class GameManager {
    public static GameManager Instance=new(); public World World; public static int Notices;
    public static void ShowTooltip(EntityPlayerLocal p,string text,bool a,bool b,float seconds)=>Notices++;
}
public class LocalPlayerUI {
    public static LocalPlayerUI Current=new(); public Xui xui=new();
    public static LocalPlayerUI GetUIForPlayer(EntityPlayerLocal p)=>Current;
}
public class Xui { public Inventory PlayerInventory=new(); }
public class Inventory {
    public int Space=100, Calls; public bool PartialOverride; public Dictionary<string,int> Counts=new();
    public bool AddItemNoPartial(ItemStack stack,bool sound) {
        Calls++;
        if(!PartialOverride && Space<stack.count)return false;
        int moved=Math.Min(Space,stack.count); Space-=moved; stack.count-=moved;
        Counts[stack.itemValue.ItemClass.Id]=Counts.GetValueOrDefault(stack.itemValue.ItemClass.Id)+moved;
        return stack.count==0;
    }
}
public class ItemClass {
    public string Id; public int MaxCount=100;
    public static ItemValue GetItem(string id)=>id=="missing" ? new ItemValue() : new ItemValue{type=1,ItemClass=new ItemClass{Id=id}};
    public static ItemValue CreateItemValue(string id,int quality)=>GetItem(id);
}
public class ItemValue { public int type; public ItemClass ItemClass; }
public class ItemStack { public ItemValue itemValue; public int count; public ItemStack(ItemValue value,int number){itemValue=value;count=number;} }
public class RebirthStartingItemDefinition { public string ItemId; public int Count,Quality; public bool HasQuality; }
public class RebirthBackgroundDefinition { public List<RebirthStartingItemDefinition> StartingItems=new(); }
public static class RebirthSurvivorDefinitionRegistry {
    public static string SemanticHash="same"; public static RebirthBackgroundDefinition Kit=new();
    public static bool TryGetBackground(string id,out RebirthBackgroundDefinition background){background=Kit;return id=="chef";}
}
public static class RebirthWorldCharacterRepository { public static bool IsServerAuthority=true; }
public static class RebirthSurvivorMode { public static bool Enabled=true; public static bool IsEnabledForCurrentWorld()=>Enabled; }
public class Origin { public string CreationId,BackgroundId="chef"; }
public class RebirthWorldCharacterRecord { public bool IsComplete=true; public Origin Origin=new(); }
public static class RebirthWorldCharacterService {
    public static RebirthWorldCharacterRecord Record;
    public static bool TryGet(EntityPlayer player,out RebirthWorldCharacterRecord record){record=Record;return record!=null;}
}
public class ConnectionManager { public bool IsServer=true; public void SendPackage(NetPackage package,int _attachedToEntityId){} }
public class SingletonMonoBehaviour<T> where T:new() { public static T Instance=new(); }
public static class NetPackageManager { public static T GetPackage<T>() where T:new()=>new(); }
public enum NetPackageDirection { ToClient }
public class PooledBinaryReader : BinaryReader { public PooledBinaryReader():base(new MemoryStream()){} }
public class PooledBinaryWriter : BinaryWriter { public PooledBinaryWriter():base(new MemoryStream()){} }
public class NetPackage {
    public virtual NetPackageDirection PackageDirection=>NetPackageDirection.ToClient;
    public virtual void read(PooledBinaryReader r){} public virtual void write(PooledBinaryWriter w){}
    public virtual void ProcessPackage(World w,GameManager g){} public virtual int GetLength()=>0;
}
public static class RebirthSurvivorNetworkCodec {
    public static string ReadString(BinaryReader r,int max)=>r.ReadString();
    public static void WriteString(BinaryWriter w,string s,int max)=>w.Write(s);
    public static int EstimateString(string s,int max)=>s.Length;
}
public static class Localization { public static string Get(string key)=>key; }

public static class Program {
    static string origin; static World world; static EntityPlayerLocal player;
    static Inventory Inventory=>LocalPlayerUI.Current.xui.PlayerInventory;
    static int checks;
    static void Check(bool condition,string name){if(!condition)throw new Exception(name);checks++;Console.WriteLine("PASS "+name);}
    static void Setup(int count=7){
        RebirthBackgroundStarterItems.Reset();UnityEngine.Time.realtimeSinceStartup=0;GameManager.Notices=0;
        origin=Guid.NewGuid().ToString("N");world=new World();player=new EntityPlayerLocal{world=world,entityId=17};world.Player=player;
        GameManager.Instance.World=world;LocalPlayerUI.Current=new();RebirthSurvivorMode.Enabled=true;
        RebirthSurvivorDefinitionRegistry.Kit=new();RebirthSurvivorDefinitionRegistry.Kit.StartingItems.Add(new(){ItemId="food",Count=count});
        RebirthWorldCharacterService.Record=new(){Origin=new(){CreationId=origin}};
    }
    static void Tick(){UnityEngine.Time.realtimeSinceStartup+=2.1f;RebirthBackgroundStarterItems.Tick();}
    static void Offer()=>RebirthBackgroundStarterItems.Offer(player);
    static int Count=>Inventory.Counts.GetValueOrDefault("food");
    public static void Main(){
        Setup();Offer();Tick();Check(Count==7,"whole kit delivered");Offer();Tick();Check(Count==7,"completed offer does not duplicate");
        RebirthSurvivorDefinitionRegistry.Kit.StartingItems[0].Count=10;Offer();Tick();Check(Count==7,"completed kit not reissued after balance edit");
        Setup();Inventory.Space=0;Offer();Tick();Tick();Offer();Tick();Check(Count==0&&GameManager.Notices==1,"full inventory stays pending and notice does not spam");
        Inventory.Space=3;Tick();Check(Count==3,"smaller batch fits available space");Offer();Tick();Check(Count==3,"partial receipt survives repeated offer");
        Inventory.Space=4;Tick();Check(Count==7,"only remaining amount delivered");Offer();Tick();Check(Count==7,"completed partial kit not duplicated");
        Setup();Inventory.PartialOverride=true;Inventory.Space=2;Offer();Tick();Check(Count==2,"partial native override counted despite false return");Inventory.Space=5;Tick();Check(Count==7,"partial override remaining count is exact");
        Setup();player.Spawned=false;Offer();Tick();Check(Count==0,"waits for spawn");player.Spawned=true;player.Dead=true;Tick();Check(Count==0,"waits while dead");player.Dead=false;Tick();Check(Count==7,"resumes alive spawned player");
        Setup();RebirthSurvivorMode.Enabled=false;Offer();RebirthSurvivorMode.Enabled=true;Tick();Check(Count==0,"vanilla mode issues no offer");
        Setup();RebirthBackgroundStarterItems.Receive(world,17,origin,"chef","different");Tick();Check(Count==0,"incompatible definitions do not grant");
        Setup();RebirthBackgroundStarterItems.Receive(world,99,origin,"chef","same");Tick();Check(Count==0,"wrong player does not grant");
        Setup();RebirthBackgroundStarterItems.Receive(world,17,"invalid","chef","same");Tick();Check(Count==0,"invalid creation ID rejected");
        Setup();Offer();GameManager.Instance.World=new World();Tick();GameManager.Instance.World=world;Tick();Check(Count==0,"world change clears pending offer");
        Setup();Offer();Tick();RebirthBackgroundStarterItems.Reset();Offer();Tick();Check(Count==7,"transient reset retains persistent receipt");
        RebirthWorldCharacterService.Record.Origin.CreationId=Guid.NewGuid().ToString("N");Offer();Tick();Check(Count==14,"new character creation gets a distinct kit");
        Setup();RebirthSurvivorDefinitionRegistry.Kit.StartingItems.Add(new(){ItemId="missing",Count=1});Offer();Tick();Check(Count==7&&player.Buffs.GetCustomVar("rbStarter_"+origin)==0,"missing item does not mark kit complete");
        Setup();Inventory.Space=0;Offer();Tick();int calls=Inventory.Calls;RebirthBackgroundStarterItems.Tick();Check(Inventory.Calls==calls,"pending retries are rate limited");
        Setup();Inventory.Space=3;RebirthSurvivorDefinitionRegistry.Kit.StartingItems.Add(new(){ItemId="knife",Count=1});Offer();Tick();
        RebirthSurvivorDefinitionRegistry.Kit.StartingItems.Reverse();Inventory.Space=5;Offer();Tick();
        Check(Count==7&&Inventory.Counts.GetValueOrDefault("knife")==1,"reordered kit preserves per-item partial receipts");
        Setup();RebirthWorldCharacterRepository.IsServerAuthority=false;Offer();Tick();
        Check(Count==0,"non-authoritative side cannot offer a kit");RebirthWorldCharacterRepository.IsServerAuthority=true;
        Setup(250);Inventory.Space=300;Offer();Tick();
        Check(Count==100&&player.Buffs.GetCustomVar("rbStarter_"+origin)==0,"large grant respects stack batch limit");
        Tick();Tick();Check(Count==250&&player.Buffs.GetCustomVar("rbStarter_"+origin)==1,"multi-stack grant completes without truncation");
        Setup();Offer();var savedUi=LocalPlayerUI.Current;LocalPlayerUI.Current=null;Tick();
        Check(savedUi.xui.PlayerInventory.Calls==0,"UI not yet created postpones delivery");
        LocalPlayerUI.Current=savedUi;Tick();Check(Count==7,"delivery resumes when inventory UI becomes available");
        Setup();Inventory.Space=3;Offer();Tick();RebirthBackgroundStarterItems.Reset();
        Inventory.Space=4;Offer();Tick();Check(Count==7,"partial receipts survive transient reset and reoffer");
        Setup();RebirthWorldCharacterService.Record.IsComplete=false;Offer();Tick();
        Check(Count==0,"unfinished character cannot receive background kit");
        Setup();Offer();Tick();
        RebirthBackgroundStarterItems.Receive(world,17,Guid.Parse(origin).ToString("D"),"chef","same");Tick();
        Check(Count==7,"alternate GUID formatting uses the same receipt identity");
        Setup();RebirthBackgroundStarterItems.Receive(world,17,origin,"chef","different");Tick();
        Offer();Tick();Check(Count==7,"corrected definition offer resumes previously withheld kit");
        Setup();RebirthSurvivorDefinitionRegistry.Kit.StartingItems.Clear();Offer();Tick();
        Check(player.Buffs.GetCustomVar("rbStarter_"+origin)==1&&Inventory.Calls==0,"empty authored kit completes without inventory calls");
        Setup();RebirthSurvivorDefinitionRegistry.Kit.StartingItems.Insert(0,new(){ItemId="missing",Count=1});Offer();Tick();
        Check(Count==7&&player.Buffs.GetCustomVar("rbStarter_"+origin)==0,"unresolved first entry does not starve later valid starter items");
        Console.WriteLine("RESULT: "+checks+" passed; native boundaries simulated, not a game test.");
    }
}
