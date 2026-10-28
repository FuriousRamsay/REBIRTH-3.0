// NOT RUN. Invoke in an initialized disposable fixture with actual stackable,
// non-liquid same-type plain/general/electrical items. Uses production predicates
// and the installed native sort path; no substituted sorting implementation.
using System;
using System.Linq;
internal static class RebirthProvenanceMergeFixture
{
    internal static void Verify(ItemValue plain,ItemValue general,ItemValue otherGeneral,ItemValue electrical,ItemValue otherElectrical)
    {
        var values=new[]{plain,general,otherGeneral,electrical,otherElectrical};
        Need(values.All(x=>x!=null&&!x.IsEmpty()&&x.type==plain.type&&!RebirthCookingItemStats.IsLiquid(x)
            &&!x.ItemClass.HasQuality&&x.ItemClass.MaxCount>=4),"Actual same-type stackable fixture required");
        Need(RebirthItemProvenanceAdapter.HasProvenance(general)&&RebirthItemProvenanceAdapter.HasProvenance(otherGeneral),"General stamps required");
        Need(!RebirthItemProvenanceAdapter.AreStackCompatible(general,otherGeneral),"Distinct general stamps required");
        Need(!RebirthElectricalItemProvenance.AreStackCompatible(electrical,otherElectrical),"Distinct electrical stamps required");
        foreach(var pair in new[]{new[]{plain,general},new[]{general,otherGeneral},new[]{plain,electrical},new[]{electrical,otherElectrical}})
        {
            Need(!RebirthCookingItemStats.Compatible(pair[0],pair[1])&&!RebirthCookingItemStats.Compatible(pair[1],pair[0]),"Partial merge lost provenance");
            Need(RebirthCookingItemStats.NeedsStateAwareStacking(pair[0])||RebirthCookingItemStats.NeedsStateAwareStacking(pair[1]),"Sort skipped provenance");
            var input=new[]{new ItemStack(pair[0].Clone(),1),new ItemStack(pair[1].Clone(),2),ItemStack.Empty.Clone()};
            var sorted=StackSortUtil.CombineAndSortStacks(input);
            Need(input[0].count==1&&input[1].count==2&&input[2].IsEmpty(),"Sort modified caller stacks");
            Need(sorted.Where(x=>!x.IsEmpty()).Count()==2&&sorted.Where(x=>!x.IsEmpty()).Sum(x=>x.count)==3,"Sort merged incompatible identities");
        }
        foreach(var item in values)
        {
            Need(RebirthCookingItemStats.Compatible(item,item.Clone()),"Equivalent item refused");
            var input=new[]{new ItemStack(item.Clone(),1),new ItemStack(item.Clone(),2),ItemStack.Empty.Clone()};
            var sorted=StackSortUtil.CombineAndSortStacks(input);
            Need(input[0].count==1&&input[1].count==2,"Compatible sort modified caller stacks");
            Need(sorted.Count(x=>!x.IsEmpty())==1&&sorted.Single(x=>!x.IsEmpty()).count==3,"Equivalent item did not merge");
        }
    }
    private static void Need(bool value,string reason){if(!value)throw new InvalidOperationException(reason);}
}
