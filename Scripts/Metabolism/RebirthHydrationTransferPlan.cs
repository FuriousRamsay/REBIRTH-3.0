using System;
using Stack=RebirthGearInventoryPlan.Stack;

// Pure, detached native-item delta. GearBefore/After name the external custody
// cell in the shared conservation model; here that cell is hydration storage.
// No profile change, capacity projection, overflow, native write or authority.
internal static class RebirthHydrationTransferPlan
{
    internal static bool TryCreate(Stack[] bag,Stack[] belt,bool[] lockedBag,int ownedBelt,
        Stack hydration,bool sourceIsBag,int sourceIndex,out RebirthGearInventoryPlan plan)
    {
        plan=null;
        if(sourceIndex < -1||hydration==null||hydration.Count<0||hydration.Count>1||
            !RebirthGearEncodedSnapshot.TryCopy(bag,belt,ownedBelt,out var beforeBag,out var beforeBelt)||
            lockedBag!=null&&lockedBag.Length!=beforeBag.Length)return false;
        foreach(var cell in beforeBag)if(cell.Count>ushort.MaxValue)return false;
        foreach(var cell in beforeBelt)if(cell.Count>ushort.MaxValue)return false;
        var next=new RebirthGearInventoryPlan{
            BagSlotsBefore=beforeBag.Length,BagSlotsAfter=beforeBag.Length,
            BeltSlotsBefore=beforeBelt.Length,BeltSlotsAfter=beforeBelt.Length,
            GearBefore=Copy(hydration),GearAfter=new Stack()
        };
        if(sourceIndex>=0)
        {
            if(hydration.Count!=0||hydration.ItemData!=string.Empty||
                sourceIndex>=(sourceIsBag?beforeBag.Length:ownedBelt)||
                sourceIsBag&&lockedBag!=null&&lockedBag[sourceIndex])return false;
            var source=(sourceIsBag?beforeBag:beforeBelt)[sourceIndex];
            if(source.Count<1)return false;
            next.GearAfter=new Stack{Count=1,ItemData=source.ItemData};
            next.Changes.Add(new RebirthGearInventoryPlan.Change{
                IsBag=sourceIsBag,Index=sourceIndex,Before=Copy(source),
                After=source.Count==1?new Stack():new Stack{Count=source.Count-1,ItemData=source.ItemData}
            });
        }
        else
        {
            if(hydration.Count!=1)return false;
            int destination=-1;
            for(int index=0;index<beforeBag.Length;index++)
                if(beforeBag[index].Count==0&&(lockedBag==null||!lockedBag[index])){destination=index;break;}
            if(destination<0)return false; // retain hydration custody; no implicit drop
            next.Changes.Add(new RebirthGearInventoryPlan.Change{
                IsBag=true,Index=destination,Before=Copy(beforeBag[destination]),After=Copy(hydration)
            });
        }
        if(next.Recovery.Count!=0||next.Changes.Count!=1||!next.IsConserved()||
            !next.MatchesBefore(beforeBag,beforeBelt,hydration,beforeBag.Length,beforeBelt.Length))return false;
        // Reuse the strict existing codec to freeze an independently owned plan.
        return RebirthGearInventoryPlanCodec.TryRead(RebirthGearInventoryPlanCodec.Write(next),out plan);
    }
    private static Stack Copy(Stack value)=>new Stack{Count=value.Count,ItemData=value.ItemData};
}
