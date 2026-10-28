// Native rich-stack policy is an explicit adapter. The complete actual plan is
// injected; no NPC mutation, durable reservation or owner receipt is simulated.
class RebirthNpcInventorySnapshot
{
 public RebirthNpcStableId NpcId;public uint Revision;public Dictionary<string,int> Quantities,Reservations;
 public RebirthNpcNativeStackSet NativeStacks;public Dictionary<Guid,string> LogisticsEffects;
}
class RebirthNpcNativeStackSet
{
 public string Owner="npc",Item="goods";public uint Revision=1;public int Count;
 public int GetQuantity(string key)=>key==Item?Count:0;
 public bool FitsQuantities(Dictionary<string,int> values)=>Count==0||values.TryGetValue(Item,out var count)&&count>=Count;
 public RebirthNpcNativeStackSet WithRevision(uint value)=>new RebirthNpcNativeStackSet{Owner=Owner,Item=Item,Revision=value,Count=Count};
 public XElement Write()=>new XElement("native",new XAttribute("owner",Owner),new XAttribute("revision",Revision),new XAttribute("item",Item),new XAttribute("count",Count));
}
// ACTUAL_TRADE_NPC_PLAN
static class NpcPlanFixture
{
 static void Need(bool value,string message){if(!value)throw new Exception(message);}
 internal static void Run(RebirthNpcTradeOriginal original)
 {
  var quote=original.CopyQuote();var receipt=Guid.NewGuid();
  var snapshot=new RebirthNpcInventorySnapshot{NpcId=quote.NpcId,Revision=quote.ExpectedNpcInventoryRevision,
   Quantities=new Dictionary<string,int>{{"goods",10},{"currency",20},{"unrelated",7}},Reservations=new Dictionary<string,int>(),
   LogisticsEffects=new Dictionary<Guid,string>{{receipt,"unchanged prior effect"}}};
  Need(RebirthNpcTradeNpcPlan.TryCreate(original,snapshot,out var plan)&&plan.Matches(snapshot,false),"NPC buy plan unavailable");
  var after=plan.CopySnapshot(true);
  Need(after.Revision==snapshot.Revision+1&&after.Quantities["goods"]==8&&after.Quantities["currency"]==26&&after.Quantities["unrelated"]==7&&after.LogisticsEffects[receipt]=="unchanged prior effect","NPC two-leg conservation or unrelated state changed");
  after.Quantities["goods"]=99;after.LogisticsEffects[receipt]="mutated copy";
  Need(plan.CopySnapshot(true).Quantities["goods"]==8&&plan.CopySnapshot(true).LogisticsEffects[receipt]=="unchanged prior effect","NPC plan shares mutable dictionaries");
  snapshot.Reservations["goods"]=9;Need(!RebirthNpcTradeNpcPlan.TryCreate(original,snapshot,out _),"NPC reserved stock spent");snapshot.Reservations.Clear();
  snapshot.NativeStacks=new RebirthNpcNativeStackSet{Count=9};Need(!RebirthNpcTradeNpcPlan.TryCreate(original,snapshot,out _),"rich native NPC stack spent by abstract trade");
  snapshot.NativeStacks=new RebirthNpcNativeStackSet{Count=2,Owner="foreign"};Need(!RebirthNpcTradeNpcPlan.TryCreate(original,snapshot,out _),"foreign native stack owner accepted");
  snapshot.NativeStacks=null;snapshot.Quantities["currency"]=int.MaxValue;Need(!RebirthNpcTradeNpcPlan.TryCreate(original,snapshot,out _),"NPC credit overflow accepted");
  snapshot.Quantities["currency"]=20;snapshot.Revision++;Need(!RebirthNpcTradeNpcPlan.TryCreate(original,snapshot,out _),"stale quote revision accepted");snapshot.Revision--;
  snapshot.Quantities["goods"]=2;Need(RebirthNpcTradeNpcPlan.TryCreate(original,snapshot,out var exhausted)&&!exhausted.CopySnapshot(true).Quantities.ContainsKey("goods"),"exact depleted NPC stack not removed");
  var sell=original.CopyQuote();sell.QuoteId=Guid.NewGuid();sell.Direction=RebirthNpcTradeDirection.SellToNpc;
  var ownerBag=new[]{new ItemStack(new ItemValue{type=1},2)};var ownerBelt=new[]{new ItemStack(new ItemValue{type=0},1)};
  Need(RebirthNpcTradeOwnerPlan.TryCreate(sell,ownerBag,ownerBelt,1,(bag,slot)=>false,out var ownerPlan),"sell owner plan missing");
  var sellOriginal=RebirthNpcTradeOriginal.Freeze(sell,ownerPlan,original.Account,original.Creation,original.World,original.Deaths,original.NativeId);
  Need(RebirthNpcTradeNpcPlan.TryCreate(sellOriginal,snapshot,out var selling)&&selling.CopySnapshot(true).Quantities["goods"]==4&&selling.CopySnapshot(true).Quantities["currency"]==14,"sell direction did not invert NPC legs");
 }
}
