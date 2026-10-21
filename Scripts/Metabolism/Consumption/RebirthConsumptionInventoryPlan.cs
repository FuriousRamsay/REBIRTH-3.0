using System;
using System.Linq;

// Complete native inventory postimage for one exact consumed source. Detached
// planning owns no setter, ingestion, drop, effect or publication permission.
internal sealed class RebirthConsumptionInventoryPlan
{
    internal readonly RebirthMetabolismSourceKind Kind;internal readonly int Slot,BeltCount;
    internal readonly ItemStack[] BeforeBag,BeforeBelt,AfterBag,AfterBelt;
    internal readonly bool[] BagLocks,BeltLocks;
    internal readonly struct Command
    {
        internal readonly bool Bag;internal readonly int Slot;internal readonly ItemStack Before,After;
        internal Command(bool bag,int slot,ItemStack before,ItemStack after){Bag=bag;Slot=slot;Before=before.Clone();After=after.Clone();}
    }
    internal Command[] Commands()
    {
        var result=new System.Collections.Generic.List<Command>();
        void Add(bool bag,int slot)
        {
            var before=bag?BeforeBag:BeforeBelt;var after=bag?AfterBag:AfterBelt;
            if(!RebirthStationGridIngredients.IsSameStackSnapshot(before[slot],after[slot]))result.Add(new Command(bag,slot,before[slot],after[slot]));
        }
        bool sourceBag=Kind==RebirthMetabolismSourceKind.Backpack;Add(sourceBag,Slot);
        for(int index=0;index<BeforeBag.Length;index++)if(!sourceBag||index!=Slot)Add(true,index);
        for(int index=0;index<BeltCount;index++)if(sourceBag||index!=Slot)Add(false,index);
        return result.ToArray();
    }
    internal bool MatchesPrefix(ItemStack[] bag,ItemStack[] belt,int completed)
    {
        var commands=Commands();if(completed<0||completed>commands.Length)return false;
        var expectedBag=ItemStack.Clone(BeforeBag);var expectedBelt=ItemStack.Clone(BeforeBelt);
        for(int index=0;index<completed;index++)(commands[index].Bag?expectedBag:expectedBelt)[commands[index].Slot]=commands[index].After.Clone();
        return RebirthVehicleInventoryWrite.SameSlots(expectedBag,bag)&&RebirthVehicleInventoryWrite.SameSlots(expectedBelt,belt);
    }
    private RebirthConsumptionInventoryPlan(RebirthMetabolismSourceKind kind,int slot,int beltCount,ItemStack[] bag,ItemStack[] belt,
        ItemStack[] afterBag,ItemStack[] afterBelt,bool[] bagLocks,bool[] beltLocks)
    {Kind=kind;Slot=slot;BeltCount=beltCount;BeforeBag=ItemStack.Clone(bag);BeforeBelt=ItemStack.Clone(belt);AfterBag=afterBag;AfterBelt=afterBelt;
        BagLocks=(bool[])bagLocks.Clone();BeltLocks=(bool[])beltLocks.Clone();}
    internal static bool TryCreate(RebirthMetabolismSourceKind kind,int slot,int beltCount,ItemStack[] bag,ItemStack[] belt,bool[] bagLocks,bool[] beltLocks,
        ItemStack source,ItemStack replacement,ItemStack siblings,out RebirthConsumptionInventoryPlan plan)
    {
        plan=null;
        try
        {
            if(bag==null||belt==null||bag.Length<1||bag.Length>169||belt.Length>20||beltCount<0||beltCount>belt.Length||
                bagLocks?.Length!=bag.Length||beltLocks?.Length!=belt.Length||source==null||source.IsEmpty()||source.count<1||
                bag.Any(item=>item==null)||belt.Any(item=>item==null)||replacement==null||replacement.itemValue==null||replacement.count<0||
                replacement.count>source.count||siblings!=null&&(siblings.itemValue==null||siblings.count<0))return false;
            var selected=kind==RebirthMetabolismSourceKind.Backpack?bag:kind==RebirthMetabolismSourceKind.HeldToolbelt?belt:null;
            if(selected==null||slot<0||slot>=selected.Length||kind==RebirthMetabolismSourceKind.HeldToolbelt&&slot>=beltCount||
                !RebirthStationGridIngredients.IsSameStackSnapshot(selected[slot],source))return false;
            var nextBag=ItemStack.Clone(bag);var nextBelt=ItemStack.Clone(belt);
            (kind==RebirthMetabolismSourceKind.Backpack?nextBag:nextBelt)[slot]=replacement.Clone();
            if(siblings!=null&&!siblings.IsEmpty())
            {
                // The used container occupies the original source cell; rich
                // untouched stacked siblings must fit wholly before admission.
                if(!RebirthRecyclerOwnerDelivery.Plan(nextBag,nextBelt,bagLocks,beltLocks,beltCount,new[]{siblings.Clone()},out nextBag,out nextBelt))return false;
            }
            plan=new RebirthConsumptionInventoryPlan(kind,slot,beltCount,bag,belt,nextBag,nextBelt,bagLocks,beltLocks);
            if(plan.Commands().Length==0){plan=null;return false;}return true;
        }
        catch{plan=null;return false;}
    }
    internal bool MatchesBefore(ItemStack[] bag,ItemStack[] belt)=>RebirthVehicleInventoryWrite.SameSlots(BeforeBag,bag)&&RebirthVehicleInventoryWrite.SameSlots(BeforeBelt,belt);
    internal bool MatchesLocks(EntityPlayer actor)
    {
        var bag=RebirthCamperStationNativeActorPayment.Locks(actor?.bag?.LockedSlots);var belt=RebirthCamperStationNativeActorPayment.Locks(actor?.inventory?.ItemGrid?.SlotLocks);
        return bag!=null&&belt!=null&&BagLocks.SequenceEqual(bag)&&BeltLocks.SequenceEqual(belt);
    }
    internal bool MatchesAfter(ItemStack[] bag,ItemStack[] belt)=>RebirthVehicleInventoryWrite.SameSlots(AfterBag,bag)&&RebirthVehicleInventoryWrite.SameSlots(AfterBelt,belt);
}
