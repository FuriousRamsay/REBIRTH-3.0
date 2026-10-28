using System;
// AUTHORED_NOT_RUN. Actual pure planner; native item policy/stacking are explicit
// adapters, not real quote transport, saved owner or NPC mutation proof.
enum RebirthNpcTradeDirection {BuyFromNpc,SellToNpc}
class RebirthNpcTradeQuote {public Guid QuoteId=Guid.NewGuid();public int Quantity=2,UnitPrice=3,TotalPrice=6;public string ItemKey="goods",CurrencyItemKey="currency";public RebirthNpcTradeDirection Direction;public RebirthNpcStableId NpcId=new RebirthNpcStableId();public uint ExpectedNpcInventoryRevision=1;public ulong RecoveryGeneration=1;public string ActorId="actor",EndpointId="player:1";public long ExpiresClockTicks=DateTime.UtcNow.AddSeconds(20).Ticks;}
class RebirthNpcStableId {public override string ToString()=>"npc";public static bool TryParse(string value,out RebirthNpcStableId id){id=value=="npc"?new RebirthNpcStableId():null;return id!=null;}}
class ItemValue {public int type;public string Rich="";public bool IsEmpty()=>type==0;public ItemValue Clone()=>new ItemValue{type=type,Rich=Rich};}
class ItemClass {public int MaxCount=100;public static ItemValue GetItem(string key,bool ignored)=>new ItemValue{type=key=="goods"?1:key=="currency"?2:0};public static ItemClass GetForId(int type)=>type>0?new ItemClass():null;}
class XUiC_ItemStack {public enum StackLocationTypes {Backpack,ToolBelt}}
class ItemStack {public ItemValue itemValue;public int count;public ItemStack(ItemValue value,int n){itemValue=value;count=n;}public static ItemStack Empty=>new ItemStack(new ItemValue(),0);public bool IsEmpty()=>count==0||itemValue.type==0;public ItemStack Clone()=>new ItemStack(itemValue.Clone(),count);public bool CanMoveTo(XUiC_ItemStack.StackLocationTypes area)=>true;public bool CanStackPartlyWith(ItemStack other,out int capacity){capacity=100-count;return !IsEmpty()&&itemValue.type==other.itemValue.type&&capacity>0;}}
class RebirthNpcPlayerInventoryEndpoint {public static bool IsFungible(ItemValue value)=>value!=null&&!value.IsEmpty()&&value.Rich.Length==0;}
// ACTUAL_OWNER_PLAN
class Fixture
{
 static ItemStack S(int type,int count,string rich="")=>new ItemStack(new ItemValue{type=type,Rich=rich},count);
 static void Need(bool value,string message){if(!value)throw new Exception(message);}
 static void Main()
 {
  var quote=new RebirthNpcTradeQuote();var bag=new[]{S(2,6),S(3,100)};var belt=new[]{S(3,100),S(2,100)};
  Need(RebirthNpcTradeOwnerPlan.TryCreate(quote,bag,belt,1,(area,slot)=>false,out var plan),"freed owner debit cell did not admit credit");
  Need(bag[0].itemValue.type==2&&bag[0].count==6&&plan.BeforeBag[0].count==6&&plan.AfterBag[0].itemValue.type==1&&plan.AfterBag[0].count==2,"pure owner exchange mutated input or lost conservation");
  Need(plan.AfterBelt[1].count==100,"unowned belt changed");
  Need(!RebirthNpcTradeOwnerPlan.TryCreate(quote,new[]{S(3,100)},belt,1,(a,i)=>false,out _),"unowned belt currency funded trade");
  Need(!RebirthNpcTradeOwnerPlan.TryCreate(quote,bag,belt,1,(a,i)=>a&&i==0,out _),"locked native currency funded trade");
  Need(!RebirthNpcTradeOwnerPlan.TryCreate(quote,new[]{S(2,6,"unique metadata")},new ItemStack[0],0,(a,i)=>false,out _),"abstract trade erased rich currency");
  Need(!RebirthNpcTradeOwnerPlan.TryCreate(quote,new[]{S(2,7)},new[]{S(3,100)},1,(a,i)=>false,out _),"full owner output capacity invented");
  quote.Direction=RebirthNpcTradeDirection.SellToNpc;Need(RebirthNpcTradeOwnerPlan.TryCreate(quote,new[]{S(1,2)},new ItemStack[0],0,(a,i)=>false,out var sell)&&sell.AfterBag[0].itemValue.type==2&&sell.AfterBag[0].count==6,"sell native item/currency directions reversed");
  quote.TotalPrice=5;Need(!RebirthNpcTradeOwnerPlan.TryCreate(quote,new[]{S(1,2)},new ItemStack[0],0,(a,i)=>false,out _),"altered quote arithmetic admitted");quote.TotalPrice=6;
  var cosmeticEmpty=S(0,1);Need(RebirthNpcTradeOwnerPlan.TryCreate(quote,new[]{S(1,2),cosmeticEmpty},new ItemStack[0],0,(a,i)=>false,out var empty)&&empty.BeforeBag[1].count==1&&empty.AfterBag[1].count==1,"native cosmetic empty was normalized in original frame");
  Need(!RebirthNpcTradeOwnerPlan.TryCreate(quote,new[]{S(1,2)},new ItemStack[0],1,(a,i)=>false,out _),"invalid native owned belt width admitted");
  Need(!RebirthNpcTradeOwnerPlan.TryCreate(quote,new[]{S(1,2)},new ItemStack[0],0,(a,i)=>{throw new Exception("unknown lock");},out _),"unknown native lock policy admitted");
  Console.WriteLine("PASS actual pure trade owner exchange planner; native/persistence/network/NPC adapters NOT certified");
 }
}
