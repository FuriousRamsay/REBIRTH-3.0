// AUTHORED_NOT_RUN. Actual shared Insert and PlanWithOverflow bodies.
using System;
using System.Collections.Generic;
using System.Linq;
class ItemClass {internal int MaxCount=10;}
class ItemValue {internal string Rich="plain";internal ItemClass ItemClass=new ItemClass();internal ItemValue Clone()=>new ItemValue{Rich=Rich,ItemClass=ItemClass};}
class ItemStack {
    internal ItemValue itemValue;internal int count;
    internal ItemStack(ItemValue value,int n){itemValue=value;count=n;}
    internal bool IsEmpty()=>count<1||itemValue.Rich=="native-none";internal bool CanStackWith(ItemStack other)=>count>0&&other.count>0&&itemValue.Rich==other.itemValue.Rich;
    internal ItemStack Clone()=>new ItemStack(itemValue.Clone(),count);internal static ItemStack[] Clone(ItemStack[] items)=>items.Select(i=>i.Clone()).ToArray();
}
static class RebirthRecyclerOwnerDelivery {
// ACTUAL_PLANNER
}
class Program {
    static int assertions;
    static ItemStack Item(int n,string rich="plain")=>new ItemStack(new ItemValue{Rich=rich},n);
    static void Need(bool value,string label){assertions++;if(!value)throw new Exception(label);}
    static Dictionary<string,long> Totals(IEnumerable<ItemStack> items)=>items.Where(i=>!i.IsEmpty()).GroupBy(i=>i.itemValue.Rich).ToDictionary(g=>g.Key,g=>g.Sum(i=>(long)i.count));
    static void Main(){
        for(int variant=0;variant<5;variant++){
            ItemStack[] bag=new[]{Item(8),Item(0)},belt=new[]{Item(7),Item(0),Item(0)};var offer=new[]{Item(19),Item(4,"rich-variant")};
            bool[] bagLocks={false,variant==1},beltLocks={variant==2,false,false};int limit=variant==3?0:2;
            if(variant==4){bag[1]=Item(10,"incompatible");belt[1]=Item(10,"incompatible");}
            var original=Totals(bag.Concat(belt).Concat(offer));
            Need(RebirthRecyclerOwnerDelivery.PlanWithOverflow(bag,belt,bagLocks,beltLocks,limit,offer,out var afterBag,out var afterBelt,out var overflow),"valid rich original refused");
            var result=Totals(afterBag.Concat(afterBelt).Concat(overflow));Need(original.Count==result.Count&&original.All(pair=>result[pair.Key]==pair.Value),"rich/count conservation across partial destination and remainder "+variant);
            Need(bag[0].count==8&&belt[0].count==7&&offer[0].count==19,"detached planning mutated live/source images");
            Need(afterBelt[2].count==0,"unowned belt capacity used");if(bagLocks[1])Need(afterBag[1].count==0,"locked bag slot used");if(beltLocks[0])Need(afterBelt[0].count==7,"locked belt slot used");
            if(limit==0)Need(afterBelt[0].count==7&&afterBelt[1].count==0,"zero native belt capacity ignored");
            Need(afterBag.Concat(afterBelt).All(i=>i.count<=10),"stack capacity exceeded");
        }
        Need(RebirthRecyclerOwnerDelivery.PlanWithOverflow(new[]{Item(10)},new[]{Item(10)},null,null,1,new[]{Item(3,"rich")},out var b,out var t,out var r)&&r.Length==1&&r[0].count==3&&r[0].itemValue.Rich=="rich","full owner must retain exact native overflow");
        Need(RebirthRecyclerOwnerDelivery.PlanWithOverflow(new[]{Item(1,"native-none")},new ItemStack[0],null,null,0,new[]{Item(10)},out b,out t,out r)&&b[0].count==10&&r.Length==0,"native type0/count1 empty must expose its full capacity without count inflation");
        Need(RebirthRecyclerOwnerDelivery.PlanWithOverflow(new[]{Item(1,"native-none")},new ItemStack[0],null,null,0,new[]{Item(11)},out b,out t,out r)&&b[0].count==10&&r.Length==1&&r[0].count==1,"native empty placeholder cannot change credited plus overflow conservation");
        Need(!RebirthRecyclerOwnerDelivery.PlanWithOverflow(new ItemStack[]{null},new ItemStack[0],null,null,0,new[]{Item(1)},out b,out t,out r),"malformed destination accepted");
        var invalid=Item(1);invalid.itemValue.ItemClass.MaxCount=0;
        Need(!RebirthRecyclerOwnerDelivery.PlanWithOverflow(new[]{Item(0)},new ItemStack[0],null,null,0,new[]{invalid},out b,out t,out r),"invalid native stack bound became legitimate overflow");
        foreach(int size in new[]{13,64,255}){
            var offers=Enumerable.Range(0,size).Select(i=>Item(i%9+1,"output-"+i)).ToArray();
            var before=Totals(offers);
            Need(RebirthRecyclerOwnerDelivery.PlanWithOverflow(new[]{Item(0)},new[]{Item(0)},null,null,1,offers,out b,out t,out r),"configured output grid refused "+size);
            var after=Totals(b.Concat(t).Concat(r));
            Need(before.Count==after.Count&&before.All(pair=>after[pair.Key]==pair.Value),"configured output grid lost rich remainder "+size);
            Need(offers.Select((item,index)=>item.count==index%9+1&&item.itemValue.Rich=="output-"+index).All(value=>value),"configured output source mutated "+size);
        }
        Need(!RebirthRecyclerOwnerDelivery.PlanWithOverflow(new[]{Item(0)},new ItemStack[0],null,null,0,Enumerable.Range(0,256).Select(i=>Item(1)).ToArray(),out b,out t,out r),"over-limit native output grid accepted");
        Console.WriteLine("MP042 actual partial refund planner assertions="+assertions+"; native stack API is an adapter");
    }
}
