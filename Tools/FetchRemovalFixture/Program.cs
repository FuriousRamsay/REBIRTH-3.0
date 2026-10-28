using System;
class Program
{
    static ItemStack Package(int code,int count=1)=>new ItemStack(new ItemValue{type=7,Seed=1,Meta=code},count);
    static void Check(bool v,string name){if(!v)throw new Exception(name);Console.WriteLine("PASS "+name);}
    static void Main()
    {
        var p=new Player(); var q=new ObjectiveBaseFetchContainer{CurrentValue=3,expectedItemClass=new ItemClassQuest(),OwnerQuest=new Quest{QuestCode=20,OwnerJournal=new Journal{OwnerPlayer=p}}};
        p.bag.ItemGrid.items=new[]{Package(10),Package(20),Package(30)};
        Check(!RebirthFetchRemovalIdentity.Prefix(q),"native type-only removal replaced");
        Check(p.bag.ItemGrid.items[1].IsEmpty(),"matching quest removed");
        Check(p.bag.ItemGrid.items[0].count==1&&p.bag.ItemGrid.items[2].count==1,"other quests preserved");
        Check(p.bag.writes==1&&LocalPlayerUI.ui.xui.CollectedItemList.count==1,"one slot write and notification");
        RebirthFetchRemovalIdentity.Prefix(q);
        Check(p.bag.writes==1,"repeat with no matching package preserves other quests");
        p.inventory.ItemGrid.items=new[]{Package(20)};
        RebirthFetchRemovalIdentity.Prefix(q);
        Check(p.inventory.ItemGrid.items[0].IsEmpty()&&p.inventory.writes==1,"toolbelt fallback");
        p.bag.ItemGrid.items=new[]{Package(20,3)};
        RebirthFetchRemovalIdentity.Prefix(q);
        Check(p.bag.ItemGrid.items[0].count==2,"pre-existing merged remainder preserved");
        q.CurrentValue=2;Check(RebirthFetchRemovalIdentity.Prefix(q),"incomplete objective retains native path");
        q.CurrentValue=3;q.expectedItemClass=new ItemClass();Check(RebirthFetchRemovalIdentity.Prefix(q),"ordinary fetch retains native path");
        Console.WriteLine("SIMULATION ONLY: actual removal prefix with native inventory/UI and identity matcher doubles; no game, network or trader validated.");
    }
}
namespace HarmonyLib { public class HarmonyPatch:Attribute{public HarmonyPatch(Type t,string n){}} public class HarmonyPrefix:Attribute{} }
public class ItemClass{} public class ItemClassQuest:ItemClass{}
public class ItemValue {public int type,Seed,Meta;public ItemValue Clone()=>new ItemValue{type=type,Seed=Seed,Meta=Meta};}
public class ItemStack {public ItemValue itemValue;public int count;public ItemStack(ItemValue v,int n){itemValue=v;count=n;}public static ItemStack Empty=>new ItemStack(new ItemValue(),0);public bool IsEmpty()=>count<=0||itemValue.type==0;public ItemStack Clone()=>new ItemStack(itemValue.Clone(),count);}
public class Grid {public ItemStack[] items=Array.Empty<ItemStack>();}
public class Bag {public Grid ItemGrid=new Grid();public int writes;public void SetSlot(int i,ItemStack s){ItemGrid.items[i]=s;writes++;}}
public class Inventory {public Grid ItemGrid=new Grid();public int writes;public void SetItem(int i,ItemStack s){ItemGrid.items[i]=s;writes++;}}
public class Player {public Bag bag=new Bag();public Inventory inventory=new Inventory();}
public class Journal {public Player OwnerPlayer;}
public class Quest {public int QuestCode;public Journal OwnerJournal;}
public class ObjectiveBaseFetchContainer {public int CurrentValue;public ItemClass expectedItemClass;public ItemValue expectedItem=new ItemValue{type=7,Seed=1};public Quest OwnerQuest;public void SetupExpectedItem(){expectedItemClass=new ItemClassQuest();}}
public class Notifications {public int count;public void AddRemoveItemQueueEntry(ItemStack s){count++;}}
public class XUi {public Notifications CollectedItemList=new Notifications();}
public class LocalPlayerUI {public XUi xui=new XUi();public static LocalPlayerUI ui=new LocalPlayerUI();public static LocalPlayerUI GetUIForPlayer(Player p)=>ui;}
public static class RebirthFetchQuestIdentity {public static void Bind(ObjectiveBaseFetchContainer o){o.expectedItem.Meta=o.OwnerQuest.QuestCode;}public static bool Matches(ItemValue v,ObjectiveBaseFetchContainer o)=>v.type==o.expectedItem.type&&v.Seed==o.expectedItem.Seed&&v.Meta==o.OwnerQuest.QuestCode;}
