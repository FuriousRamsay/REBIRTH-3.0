using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;

class Program
{
    static int checks;
    static void Check(bool condition, string label) { if (!condition) throw new Exception(label); checks++; }
    static TEFeatureStorage Storage(int width, int height, string name="dumpster") => new TEFeatureStorage { lootListName=name, ItemGrid=new ItemStackGrid(width,height) };
    static void Main()
    {
        RebirthLootCapacityParity.Load();
        LootContainer.Values["dumpster"]=new LootContainer { size=new Vector2i(8,6) };
        var storage=Storage(6,1); var original=storage.ItemGrid.items[2]; original.count=7;
        RebirthLootCapacityParity.AfterPersistentRead(storage,StreamModeRead.Persistency);
        Check(storage.ItemGrid.Length==48 && storage.ItemGrid.ContainerSize.x==8,"saved six-slot dumpster expands to48");
        Check(ReferenceEquals(original,storage.ItemGrid.items[2]) && original.count==7,"physical item retained at same slot");
        Check(storage.Modified==1,"resize marks persistence dirty");
        RebirthLootCapacityParity.AfterPersistentRead(storage,StreamModeRead.Persistency);
        Check(storage.Modified==1,"repeated equal-size load is idempotent");
        var owned=Storage(6,1);owned.ItemGrid.PlayerOwned=true;
        RebirthLootCapacityParity.AfterPersistentRead(owned,StreamModeRead.Persistency);
        Check(owned.ItemGrid.Length==6 && owned.Modified==0,"player storage not migrated");
        var remote=Storage(6,1);SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer=false;
        RebirthLootCapacityParity.AfterPersistentRead(remote,StreamModeRead.Persistency);
        Check(remote.ItemGrid.Length==6,"remote client cannot resize");
        SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer=true;
        RebirthLootCapacityParity.AfterPersistentRead(remote,StreamModeRead.FromServer);
        Check(remote.ItemGrid.Length==6,"network reads not mutated");
        var unrelated=Storage(6,1,"newUnrelatedContainer");LootContainer.Values[unrelated.lootListName]=LootContainer.Values["dumpster"];
        RebirthLootCapacityParity.AfterPersistentRead(unrelated,StreamModeRead.Persistency);
        Check(unrelated.ItemGrid.Length==6,"new containers outside legacy list not changed");
        LootContainer.Values["reinforcedChestT1"]=new LootContainer { size=new Vector2i(6,5) };
        var crate=Storage(8,9,"reinforcedChestT1");crate.ItemGrid.items[60].count=3;
        RebirthLootCapacityParity.AfterPersistentRead(crate,StreamModeRead.Persistency);
        Check(crate.ItemGrid.Length==72 && crate.ItemGrid.items[60].count==3 && crate.Modified==0,"occupied trailing slots prevent lossy shrink");
        crate.ItemGrid.items[60].count=0;var retained=crate.ItemGrid.items[4];retained.count=2;
        RebirthLootCapacityParity.AfterPersistentRead(crate,StreamModeRead.Persistency);
        Check(crate.ItemGrid.Length==30 && ReferenceEquals(retained,crate.ItemGrid.items[4]),"safe shrink preserves retained item identity");
        foreach (var named in new[] { (Name:"rebirthBrownPalletLoot",Width:6,Height:3), (Name:"rebirthWineBarrelJunk",Width:6,Height:3) })
        {
            LootContainer.Values[named.Name]=new LootContainer { size=new Vector2i(named.Width,named.Height) };
            var mapped=Storage(4,1,named.Name);
            RebirthLootCapacityParity.AfterPersistentRead(mapped,StreamModeRead.Persistency);
            Check(mapped.ItemGrid.Length==named.Width*named.Height,"verified renamed container participates in saved-grid parity: "+named.Name);
        }
        LootContainer.Values["dumpster"].size=new Vector2i(0,6);var invalid=Storage(6,1);
        RebirthLootCapacityParity.AfterPersistentRead(invalid,StreamModeRead.Persistency);
        Check(invalid.ItemGrid.Length==6,"invalid authored dimensions refused");
        LootContainer.Values["shamwayFreezer"]=new LootContainer { size=new Vector2i(6,4) };
        var ice=Storage(6,4,"shamwayFreezer");ice.Parent.TeData.Block.Properties.Values["RebirthLootContainerSize"]="6,7";
        RebirthLootCapacityParity.AfterInit(ice);
        Check(ice.ItemGrid.Length==42 && ice.Modified==0,"fresh ice machine gets legacy42 grid without changing shared loot or dirtying incomplete init");
        ice.ItemGrid.Resize(new Vector2i(6,4));
        RebirthLootCapacityParity.AfterPersistentRead(ice,StreamModeRead.Persistency);
        Check(ice.ItemGrid.Length==42 && ice.Modified==1,"saved ice machine gets same block-specific42 grid");
        var otherFreezer=Storage(6,4,"shamwayFreezer");
        RebirthLootCapacityParity.AfterInit(otherFreezer);
        Check(otherFreezer.ItemGrid.Length==24,"other freezer using same loot list retains its own legacy size");
        ice.Parent.TeData.Block.Properties.Values["RebirthLootContainerSize"]="-1,7";
        RebirthLootCapacityParity.AfterInit(ice);
        Check(ice.ItemGrid.Length==42,"malformed block override cannot resize");
        RebirthLootCapacityParity.AfterPersistentRead(null,StreamModeRead.Persistency);
        Check(Log.Errors==0,"shipped named parity loaded without errors");
        Console.WriteLine($"PASS {checks} actual-source parity checks with controlled native adapters. Native lifecycle/network/persistence not playtested.");
    }
}
public struct Vector2i { public int x,y;public Vector2i(int a,int b){x=a;y=b;} }
public enum StreamModeRead { Persistency,FromServer }
public class ItemStack {public int count;public bool IsEmpty()=>count<=0;}
public class ItemStackGrid
{
    public bool PlayerOwned;public Vector2i ContainerSize;public ItemStack[] items;public int Length=>items.Length;
    public ItemStack this[int i]=>items[i];
    public ItemStackGrid(int w,int h){ContainerSize=new Vector2i(w,h);items=Enumerable.Range(0,w*h).Select(_=>new ItemStack()).ToArray();}
    public void Resize(Vector2i size){Array.Resize(ref items,size.x*size.y);for(int i=0;i<items.Length;i++)items[i]??=new ItemStack();ContainerSize=size;}
}
public class TEFeatureStorage {public ItemStackGrid ItemGrid;public string lootListName;public int Modified;public Parent Parent=new Parent();public void SetModified()=>Modified++;}
public class Parent {public TeData TeData=new TeData();}
public class TeData {public Block Block=new Block();}
public class Block {public Properties Properties=new Properties();}
public class Properties {public Dictionary<string,string> Values=new Dictionary<string,string>();}
public class LootContainer {public Vector2i size;public static Dictionary<string,LootContainer> Values=new();public static LootContainer GetLootContainer(string name)=>Values.TryGetValue(name,out var v)?v:null;}
public class ConnectionManager {public bool IsServer=true;}
public static class SingletonMonoBehaviour<T> where T:new(){public static T Instance=new T();}
public static class RebirthSurvivorDefinitionLoader {public static string ResolveConfigRoot()=>Path.Combine(Directory.GetCurrentDirectory(),"Config","_Survivor");}
public static class Log {public static int Errors;public static void Error(string text){Errors++;Console.WriteLine(text);}}
