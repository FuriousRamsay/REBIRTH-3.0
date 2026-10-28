using System;
using System.Collections.Generic;
using System.Linq;

// Explicit native doubles. Dispatch invokes linked actual guard methods; this
// does not install Harmony or qualify reflection targets/native game APIs.
namespace HarmonyLib {
    [AttributeUsage(AttributeTargets.Class)] public sealed class HarmonyPatch:Attribute {
        public HarmonyPatch(Type t,string m){} public HarmonyPatch(Type t,string m,Type[] p){} }
    [AttributeUsage(AttributeTargets.Method)] public sealed class HarmonyPrefix:Attribute{}
    [AttributeUsage(AttributeTargets.Method)] public sealed class HarmonyPostfix:Attribute{}
}
public class ItemClass { public int MaxCount=10; }
public class ItemClassQuest:ItemClass{}
public class ItemValue {
    public int type,Meta; public ushort Seed; public ItemClass ItemClass;
    public Dictionary<string,int> Metadata=new();
    public ItemValue Clone()=>new(){type=type,Meta=Meta,Seed=Seed,ItemClass=ItemClass,Metadata=new(Metadata)};
    public void SetMetadata(string key,int value)=>Metadata[key]=value;
    public bool TryGetMetadata(string key,out int value)=>Metadata.TryGetValue(key,out value);
    public string Snapshot()=>type+"/"+Seed+"/"+Meta+"/"+string.Join(",",Metadata.OrderBy(p=>p.Key).Select(p=>p.Key+"="+p.Value));
}
public class ItemStack {
    public static bool Guards=true;
    public static ItemStack Empty=new(new ItemValue{type=0,ItemClass=new ItemClass()},0);
    public ItemValue itemValue; public int count;
    public ItemStack(ItemValue value,int n){itemValue=value;count=n;}
    public ItemStack Clone()=>new(itemValue.Clone(),count);
    public bool IsEmpty()=>count<=0||itemValue.type==0;
    public string Snapshot()=>count+":"+itemValue.Snapshot();
    public bool CanStack(int n){bool result=false;if(Guards&&!RebirthFetchFullStackGuard.Prefix(this,ref result))return result;return itemValue.type==0||itemValue.ItemClass.MaxCount>=n+count;}
    public bool CanStackPartly(ref int n){bool result=false;if(Guards&&!RebirthFetchPartialStackGuard.Prefix(this,ref result))return result;if(itemValue.type==0)return false;n=Math.Min(itemValue.ItemClass.MaxCount-count,n);return n>0;}
    public int StackTransferCount(ItemStack incoming){int result=0;if(Guards&&!RebirthFetchTransferCountGuard.Prefix(this,incoming,ref result))return result;return itemValue.type==incoming.itemValue.type?Math.Min(itemValue.ItemClass.MaxCount-count,incoming.count):0;}
    public bool CanStackWith(ItemStack incoming,bool partial=false){int n=incoming.count;bool result=itemValue.type==incoming.itemValue.type&&(partial?CanStackPartly(ref n):CanStack(n));if(Guards)RebirthFetchStackGuard.Postfix(this,incoming,ref result);return result;}
}
public class ItemStackGrid {
    public ItemStack[] items;
    public ItemStackGrid(params ItemStack[] cells){items=cells;}
    // Exact inspected positive branch: direct partial capacity, then count add.
    public int ChangeCount(int i,int delta){var s=items[i];if(delta==0||s.IsEmpty())return 0;if(delta>0){int n=delta;if(!s.CanStackPartly(ref n))return 0;s.count+=n;return n;}int removed=-Math.Min(-delta,s.count);s.count+=removed;return removed;}
}
public class Bag {
    public ItemStackGrid ItemGrid;
    public Bag(params ItemStack[] cells){ItemGrid=new(cells);}
    public void SetSlot(int i,ItemStack s)=>ItemGrid.items[i]=s;
    // Native Bag.TryStackItem's inspected type/partial-capacity branch.
    public (bool anyMoved,bool allMoved) TryStackItem(int start,ItemStack incoming){bool any=false;for(int i=start;i<ItemGrid.items.Length;i++){var dst=ItemGrid.items[i];int n=incoming.count;if(incoming.itemValue.type==dst.itemValue.type&&dst.CanStackPartly(ref n)){dst.count+=n;incoming.count-=n;any=true;if(incoming.count==0)return(true,true);}}return(any,false);}
}
public class Inventory:Bag { public Inventory(params ItemStack[] cells):base(cells){} public void SetItem(int i,ItemStack s)=>SetSlot(i,s); }
public static class XUiM_PlayerInventory {
    public static bool TryStackItem(int start,ItemStack incoming,ItemStack[] cells){for(int i=start;i<cells.Length;i++){int n=incoming.count;if(incoming.itemValue.type==cells[i].itemValue.type&&cells[i].CanStackPartly(ref n)){cells[i].count+=n;incoming.count-=n;if(incoming.count==0)return true;}}return false;}
}
public class EntityPlayerLocal {public Bag bag;public Inventory inventory;}
public class QuestJournal {public EntityPlayerLocal OwnerPlayer;}
public class Quest {public int QuestCode;public QuestJournal OwnerJournal;}
public class ObjectiveBaseFetchContainer {public int CurrentValue=3;public ItemClass expectedItemClass;public ItemValue expectedItem;public Quest OwnerQuest;public void SetupExpectedItem()=>throw new Exception("fixture expects initialized native objective");}
public class CollectedItemList {public List<ItemStack> Removed=new();public void AddRemoveItemQueueEntry(ItemStack s)=>Removed.Add(s);}
public class XUi {public CollectedItemList CollectedItemList=new();}
public class LocalPlayerUI {public XUi xui=new();public static LocalPlayerUI Current=new();public static LocalPlayerUI GetUIForPlayer(EntityPlayerLocal p)=>Current;}

public static class Program {
    static int checks;
    static void C(bool condition,string reason){checks++;if(!condition)throw new Exception(reason);}
    static ItemStack Package(int code,int count=1)=>new(new ItemValue{type=17,Seed=4,Meta=code,ItemClass=new ItemClassQuest(),Metadata=new(){["RebirthFetchQuestCode"]=code,["unrelated"]=81}},count);
    static ItemStack Ordinary(int count=1)=>new(new ItemValue{type=8,Seed=2,Meta=9,ItemClass=new ItemClass(),Metadata=new(){["unrelated"]=93}},count);
    public static void Main(){
        // Demonstrate the exact bypass exists in these explicitly disclosed
        // doubles without guards, then the actual linked guards refuse it.
        ItemStack.Guards=false;var baseline=Package(100);var incoming=Package(200);
        C(new Bag(baseline).TryStackItem(0,incoming)==(true,true)&&baseline.count==2&&incoming.count==0,"unguarded native-like type-only partial bypass baseline");
        ItemStack.Guards=true;
        foreach(string route in new[]{"bag","inventory","player-inventory","grid"}){
            var destination=Package(100);var source=Package(200,3);string before=destination.Snapshot(),sourceBefore=source.Snapshot();
            if(route=="bag")C(new Bag(destination).TryStackItem(0,source)==(false,false),"bag partial refusal");
            if(route=="inventory")C(new Inventory(destination).TryStackItem(0,source)==(false,false),"inventory partial refusal");
            if(route=="player-inventory")C(!XUiM_PlayerInventory.TryStackItem(0,source,new[]{destination}),"player-inventory partial refusal");
            if(route=="grid")C(new ItemStackGrid(destination).ChangeCount(0,3)==0,"positive grid increment refusal");
            C(destination.Snapshot()==before&&source.Snapshot()==sourceBefore,route+" exact source/destination count and original metadata preservation");
        }
        var quest=Package(100);var other=Package(200);int requested=7;string snapshot=quest.Snapshot();
        C(!quest.CanStack(1)&&!quest.CanStackPartly(ref requested)&&requested==7,"direct full/partial refusal preserves ref request");
        C(quest.StackTransferCount(other)==0&&!quest.CanStackWith(other)&&!quest.CanStackWith(other,true),"direct transfer/full/partial pair refusal");
        C(quest.Snapshot()==snapshot,"direct guard exact metadata preservation");
        bool result=true;C(!RebirthFetchPartialStackGuard.Prefix(quest,ref result)&&!result,"actual partial prefix refusal dispatch");
        int transfer=19;C(!RebirthFetchTransferCountGuard.Prefix(Ordinary(),quest,ref transfer)&&transfer==0,"incoming quest transfer-count refusal");
        bool pair=true;RebirthFetchStackGuard.Postfix(Ordinary(),quest,ref pair);C(!pair,"incoming quest pair postfix refusal");
        var ordinary=Ordinary();var ordinaryIncoming=Ordinary(3);string ordinaryMetadata=ordinary.itemValue.Snapshot();
        C(ordinary.CanStack(3)&&ordinary.StackTransferCount(ordinaryIncoming)==3,"ordinary full/transfer positive control");
        int n=3;C(ordinary.CanStackPartly(ref n)&&n==3,"ordinary partial positive control");
        C(new Bag(ordinary).TryStackItem(0,ordinaryIncoming)==(true,true)&&ordinary.count==4&&ordinaryIncoming.count==0,"ordinary partial native-like transfer proceeds");
        C(ordinary.itemValue.Snapshot()==ordinaryMetadata,"ordinary original metadata preserved");
        C(!Ordinary(10).CanStack(1),"ordinary full capacity rejection remains native");
        C(ItemStack.Empty.Clone().CanStack(1),"empty capacity acceptance remains native");
        var wrong=Package(200);var owned=Package(100,2);var regular=Ordinary(5);
        var player=new EntityPlayerLocal{bag=new Bag(wrong,regular),inventory=new Inventory(owned)};
        var objective=new ObjectiveBaseFetchContainer{expectedItem=Package(100).itemValue,expectedItemClass=new ItemClassQuest(),OwnerQuest=new Quest{QuestCode=100,OwnerJournal=new QuestJournal{OwnerPlayer=player}}};
        string wrongBefore=wrong.Snapshot(),regularBefore=regular.Snapshot(),ownedPayload=owned.itemValue.Snapshot();
        C(RebirthFetchRemovalIdentity.Find(player.bag.ItemGrid.items,objective)==-1,"actual identity finder refuses wrong quest package");
        C(!RebirthFetchRemovalIdentity.Prefix(objective),"actual exact turn-in replacement dispatch");
        C(player.inventory.ItemGrid.items[0].count==1&&player.inventory.ItemGrid.items[0].itemValue.Snapshot()==ownedPayload,"one exact package removed; legacy remainder metadata retained");
        C(wrong.Snapshot()==wrongBefore&&regular.Snapshot()==regularBefore,"other package/unrelated item preserved");
        C(LocalPlayerUI.Current.xui.CollectedItemList.Removed.Count==1&&LocalPlayerUI.Current.xui.CollectedItemList.Removed[0].itemValue.Snapshot()==ownedPayload,"actual removed identity notification");
        var fallback=Package(100).itemValue;fallback.Meta=0;C(RebirthFetchQuestIdentity.Matches(fallback,objective),"actual custom metadata fallback");
        fallback.Seed++;C(!RebirthFetchQuestIdentity.Matches(fallback,objective),"actual Seed discrimination");
        C(RebirthFetchQuestIdentity.Count(new[]{wrong,player.inventory.ItemGrid.items[0]},objective)==1,"actual identity count excludes other quest");
        Console.WriteLine("PASS "+checks+" linked production guard/removal/Bind/Matches/Count assertions; explicit native/Harmony/UI doubles, no native runtime qualification");
    }
}
