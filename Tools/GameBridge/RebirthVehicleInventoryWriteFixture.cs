// NOT RUN. Actual write verifier class is compiled with these explicit native
// actor/inventory/guard doubles. No vehicle commit, disk or remote-owner proof.
using System;
class ItemValue {internal int type=1;internal string Metadata="original";internal bool IsEmpty()=>type==0;}
static class ItemClass {internal static ItemValue GetItem(string name,bool create)=>new ItemValue();}
class ItemStack
{
    internal ItemValue itemValue=new ItemValue();internal int count;
    internal static ItemStack Empty=>new ItemStack{count=0};
    internal bool IsEmpty()=>count<=0;
    internal ItemStack Clone()=>new ItemStack{count=count,itemValue=new ItemValue{type=itemValue.type,Metadata=itemValue.Metadata}};
    internal bool CanStackWith(ItemStack other,bool ignore)=>itemValue.type==other.itemValue.type&&itemValue.Metadata==other.itemValue.Metadata;
}
class Grid {internal ItemStack[] items={new ItemStack{count=3},ItemStack.Empty};}
class Bag
{
    internal Grid ItemGrid=new Grid();internal bool[] LockedSlots=new bool[2];internal int Mode,Writes;
    internal void SetSlot(int slot,ItemStack value){Writes++;if(Mode==1)return;ItemGrid.items[slot]=value;if(Mode==2)ItemGrid.items[slot].itemValue.Metadata="unknown";if(Mode==5)ItemGrid.items[1]=new ItemStack{count=1};if(Mode==3)throw new Exception("after native write");}
    internal bool AddItem(ItemStack incoming)
    {
        if(Mode==1)return false;if(Mode==4)return true;
        for(int i=0;i<ItemGrid.items.Length;i++)
        {
            if(ItemGrid.items[i].IsEmpty()){ItemGrid.items[i]=incoming.Clone();incoming.count=0;return true;}
        }
        return false;
    }
}
class Inventory:Bag
{
    internal int Length=>ItemGrid.items.Length;
    internal ItemStack GetItemStack(int slot)=>ItemGrid.items[slot];
    internal void SetItem(int slot,ItemStack value)=>SetSlot(slot,value);
}
class World {internal EntityPlayer Player;internal EntityPlayer GetEntity(int id)=>Player;}
class EntityPlayer {internal World world;internal int entityId=1;internal Bag bag=new Bag();internal Inventory inventory=new Inventory();internal bool IsDead()=>false;}
class EntityPlayerLocal:EntityPlayer {}
class GameManager {internal static GameManager Instance=new GameManager();internal World World;}
static class ThreadManager {internal static bool IsMainThread()=>true;}
static class RebirthCamperStationNativeActorPayment {internal static bool Held;internal static bool Blocks(object target)=>Held;}
static class RebirthGearOwnerReservation {internal static bool BlocksInventory(object target,bool bag)=>false;}
// This older direct-write cohort never acquires the new native vehicle grant.
static class RebirthVehicleOwnerCustody {internal static bool NativeCanEnter(EntityPlayer player,bool bag)=>false;}
static class RebirthRemoteResourceOwnerAction {internal static bool IsHeld(EntityPlayerLocal player)=>false;}
static class RebirthToolbeltCapacity {internal static int Owned=2;internal static int GetOwnedSlotCount(EntityPlayer player,int count)=>Math.Min(Owned,count);}
static class RebirthCookingItemStats {internal static bool Compatible(ItemValue a,ItemValue b)=>a.type==b.type&&a.Metadata==b.Metadata;}
static class RebirthStationGridIngredients
{
    internal static bool IsSameStackSnapshot(ItemStack a,ItemStack b)=>a!=null&&b!=null&&a.count==b.count&&a.itemValue.type==b.itemValue.type&&a.itemValue.Metadata==b.itemValue.Metadata;
}
static class RebirthVehicleRefundDropWitness {internal static bool TryDrop(EntityPlayer player,ItemStack stack)=>false;}
static class RebirthVehicleDirectInventoryFixture
{
    // PRODUCTION_DIRECT_VEHICLE_METHODS
    internal static bool Credit(EntityPlayer player,ItemStack stack)=>TryGiveStackNoDrop(player,stack);
}
static class RebirthVehicleInventoryWriteFixture
{
    static void Need(bool value,string reason){if(!value)throw new Exception(reason);}
    static EntityPlayerLocal Owner(){var p=new EntityPlayerLocal();p.world=new World{Player=p};GameManager.Instance.World=p.world;return p;}
    public static void Main()
    {
        foreach(bool bag in new[]{false,true})
        {
            var p=Owner();var target=bag?p.bag:p.inventory;var before=target.ItemGrid.items[0].Clone();var after=before.Clone();after.count--;
            target.Mode=1;Need(!RebirthVehicleInventoryWrite.TryDebit(p,bag,0,before,after)&&target.ItemGrid.items[0].count==3,"Suppressed debit accepted");
            target.Mode=0;Need(RebirthVehicleInventoryWrite.TryDebit(p,bag,0,before,after)&&target.ItemGrid.items[0].count==2,"Exact debit refused");
            target.ItemGrid.items[0]=before.Clone();target.Mode=2;bool unknown=false;
            try{RebirthVehicleInventoryWrite.TryDebit(p,bag,0,before,after);}catch(InvalidOperationException){unknown=true;}
            Need(unknown,"Unknown postimage classified as safe refusal");
            target.ItemGrid.items[0]=before.Clone();target.Mode=5;unknown=false;
            try{RebirthVehicleInventoryWrite.TryDebit(p,bag,0,before,after);}catch(InvalidOperationException){unknown=true;}
            Need(unknown,"Unrelated native callback edit accepted as exact debit");target.ItemGrid.items[1]=ItemStack.Empty;
            target.ItemGrid.items[0]=before.Clone();target.Mode=0;RebirthCamperStationNativeActorPayment.Held=true;int writes=target.Writes;
            Need(!RebirthVehicleInventoryWrite.TryDebit(p,bag,0,before,after)&&target.Writes==writes,"Foreign hold bypassed");RebirthCamperStationNativeActorPayment.Held=false;
        }
        var old=new[]{new ItemStack{count=3},ItemStack.Empty};var next=new[]{new ItemStack{count=5},ItemStack.Empty};var none=new ItemStack[0];
        Need(RebirthVehicleInventoryWrite.CreditMatches(old,none,next,none,new ItemStack{count=2},2),"Exact credit rejected");
        Need(!RebirthVehicleInventoryWrite.CreditMatches(old,none,next,none,new ItemStack{count=2},1),"Wrong credit amount accepted");
        next[0].itemValue.Metadata="lost";Need(!RebirthVehicleInventoryWrite.CreditMatches(old,none,next,none,new ItemStack{count=2},2),"Credit erased metadata");
        Need(!RebirthVehicleInventoryWrite.SameSlots(old,next),"Full image mismatch accepted");
        var debitBefore=new[]{new ItemStack{count=3},new ItemStack{count=5}};
        var debitAfter=new[]{new ItemStack{count=1},new ItemStack{count=5}};
        int consumed;
        Need(RebirthVehicleInventoryWrite.TryMeasureDebit(debitBefore,none,debitAfter,none,1,2,out consumed)&&consumed==2,"Full native fuel debit refused");
        Need(!RebirthVehicleInventoryWrite.TryMeasureDebit(debitBefore,none,debitAfter,none,2,2,out consumed),"Wrong configured fuel type accepted");
        Need(!RebirthVehicleInventoryWrite.TryMeasureDebit(debitBefore,none,debitAfter,none,1,1,out consumed),"Over-request native debit accepted");
        debitAfter[1].itemValue.Metadata="unrelated";
        Need(!RebirthVehicleInventoryWrite.TryMeasureDebit(debitBefore,none,debitAfter,none,1,2,out consumed),"Unrelated fuel inventory edit accepted");
        debitAfter[1]=debitBefore[1].Clone();debitAfter[0]=ItemStack.Empty;
        Need(RebirthVehicleInventoryWrite.TryMeasureDebit(debitBefore,none,debitAfter,none,1,3,out consumed)&&consumed==3,"Native exhausted fuel cell refused");
        debitAfter[0]=new ItemStack{count=4};
        Need(!RebirthVehicleInventoryWrite.TryMeasureDebit(debitBefore,none,debitAfter,none,1,3,out consumed),"Fuel credit mistaken for native debit");
        Need(RebirthVehicleInventoryWrite.FuelMatches(10f,40f,11f,25,true),"Native25 items/unit refused");
        Need(RebirthVehicleInventoryWrite.FuelMatches(39.99f,40f,40f,1,true),"Native ceiling/clamp refused");
        Need(RebirthVehicleInventoryWrite.FuelMatches(10f,40f,10f,0,false),"Unchanged native refusal rejected");
        Need(!RebirthVehicleInventoryWrite.FuelMatches(10f,40f,11f,0,true),"Unpaid fuel accepted");
        Need(!RebirthVehicleInventoryWrite.FuelMatches(10f,40f,10f,25,true),"Debit without fuel accepted");
        Need(!RebirthVehicleInventoryWrite.FuelMatches(10f,40f,11f,25,false),"False native result after debit accepted");
        Need(!RebirthVehicleInventoryWrite.FuelMatches(10f,40f,float.NaN,25,true),"Nonfinite native fuel accepted");
        var bulk=Owner();Need(RebirthVehicleDirectInventoryFixture.TryTakeSimpleItems(bulk,"gas",4)&&bulk.bag.ItemGrid.items[0].IsEmpty()&&bulk.inventory.ItemGrid.items[0].count==2,"Actual bulk debit did not verify both areas");
        bulk=Owner();bulk.bag.Mode=1;Need(!RebirthVehicleDirectInventoryFixture.TryTakeSimpleItems(bulk,"gas",2)&&bulk.bag.ItemGrid.items[0].count==3,"Actual bulk accepted suppressed setter");
        bulk=Owner();bulk.bag.LockedSlots[0]=true;Need(!RebirthVehicleDirectInventoryFixture.TryTakeSimpleItems(bulk,"gas",4)&&bulk.inventory.ItemGrid.items[0].count==3,"Actual bulk consumed locked source or lost rollback");
        bulk=Owner();Need(RebirthVehicleDirectInventoryFixture.Credit(bulk,new ItemStack{count=2})&&bulk.bag.ItemGrid.items[1].count==2,"Actual credit failed verified empty fallback");
        bulk=Owner();bulk.bag.Mode=4;bool creditUnknown=false;try{RebirthVehicleDirectInventoryFixture.Credit(bulk,new ItemStack{count=2});}catch(InvalidOperationException){creditUnknown=true;}
        Need(creditUnknown,"Actual credit accepted false native success");
    }
}
