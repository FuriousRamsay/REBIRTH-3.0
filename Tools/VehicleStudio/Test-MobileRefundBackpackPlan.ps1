#requires -Version 7.0
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Initialize-TemporaryFiles.ps1')
$camperSource=[IO.File]::ReadAllText((Join-Path $PSScriptRoot '../../Scripts/Crafting/UI/RebirthStationRefundBackpackPlan.cs'))
$camperFixture=@'
public class ItemClass {public int MaxCount=10;}
public class ItemValue {public int type;public string Payload="";public ItemClass ItemClass=new ItemClass();public ItemValue Clone()=>new ItemValue{type=type,Payload=Payload,ItemClass=new ItemClass{MaxCount=ItemClass.MaxCount}};}
public class ItemStack {
 public ItemValue itemValue;public int count;public bool Movable=true;
 public ItemStack(ItemValue value,int count){itemValue=value;this.count=count;}
 public static ItemStack Empty=>new ItemStack(new ItemValue(),0);
 public ItemStack Clone()=>new ItemStack(itemValue.Clone(),count){Movable=Movable};public bool IsEmpty()=>count==0;
 public bool CanMoveTo(XUiC_ItemStack.StackLocationTypes location)=>Movable;
 public bool CanStackPartlyWith(ItemStack other,out int available){available=System.Math.Max(0,itemValue.ItemClass.MaxCount-count);return itemValue.type==other.itemValue.type&&itemValue.Payload==other.itemValue.Payload;}
}
public static class XUiC_ItemStack {public enum StackLocationTypes{Backpack}}
public static class RebirthNativeItemCodec {
 public static string Encode(ItemValue v)=>v.type+":"+v.ItemClass.MaxCount+":"+v.Payload;
 public static bool TryDecode(string s,out ItemValue v){v=null;var p=s.Split(':');if(p.Length!=3||!int.TryParse(p[0],out var type)||!int.TryParse(p[1],out var max))return false;v=new ItemValue{type=type,Payload=p[2],ItemClass=new ItemClass{MaxCount=max}};return true;}
}
public static class RebirthStationGridIngredients {public static bool IsSameStackSnapshot(ItemStack a,ItemStack b)=>a!=null&&b!=null&&a.count==b.count&&RebirthNativeItemCodec.Encode(a.itemValue)==RebirthNativeItemCodec.Encode(b.itemValue);}
public class RebirthStationCancellationRefund {public bool IsMaterialReturn;private readonly ItemStack[] stacks;public RebirthStationCancellationRefund(params ItemStack[] values){stacks=values;}public ItemStack[] CopyRefunds()=>IsMaterialReturn?new ItemStack[0]:System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Select(stacks,s=>s.Clone()));}
public static class RefundBackpackFixture {
 static void Check(bool b,string name){if(!b)throw new System.Exception(name);}
 static ItemStack Stack(int count,string payload="paid")=>new ItemStack(new ItemValue{type=1,Payload=payload},count);
 public static void Run(){
  var live=new[]{Stack(8),ItemStack.Empty,ItemStack.Empty};var refund=new RebirthStationCancellationRefund(Stack(5));
  Check(RebirthStationRefundBackpackPlan.TryCreate(live,3,i=>i==1,refund,out var plan),"merge and overflow fit");var after=plan.CopyAfter();
  Check(live[0].count==8&&live[2].count==0&&after[0].count==10&&after[1].count==0&&after[2].count==3,"detached exact credit and locked slot preservation");
  after[0].count=1;Check(plan.CopyAfter()[0].count==10,"returned after image cannot mutate plan");
  Check(plan.MatchesBefore(live,3,i=>i==1)&&!plan.MatchesBefore(live,3,i=>false),"locks included in before state");
  live[0].count=7;Check(!plan.MatchesBefore(live,3,i=>i==1),"destination edits invalidate proposal");live[0].count=8;
  Check(RebirthStationRefundBackpackPlan.TryRead(plan.Write(),refund,out var restored)&&restored.MatchesAfter(plan.CopyAfter()),"stored plan revalidates exact conserved credit");
  var tampered=plan.Write();tampered.Element("slot").SetAttributeValue("afterCount",9);Check(!RebirthStationRefundBackpackPlan.TryRead(tampered,refund,out _),"tampered credit rejected");
  Check(!RebirthStationRefundBackpackPlan.TryCreate(live,1,null,refund,out _),"unowned empty slots not used");
  Check(!RebirthStationRefundBackpackPlan.TryCreate(live,3,i=>i>0,refund,out _),"full destination refused without partial mutation");
  live=new[]{Stack(8,"different-quality"),ItemStack.Empty};Check(RebirthStationRefundBackpackPlan.TryCreate(live,2,null,new RebirthStationCancellationRefund(Stack(4)),out plan)&&plan.CopyAfter()[0].count==8&&plan.CopyAfter()[1].itemValue.Payload=="paid","different item payload never merged");
  live=new[]{ItemStack.Empty,ItemStack.Empty};refund=new RebirthStationCancellationRefund(Stack(7),Stack(8));Check(RebirthStationRefundBackpackPlan.TryCreate(live,2,null,refund,out plan)&&plan.CopyAfter()[0].count==10&&plan.CopyAfter()[1].count==5,"duplicate paid fragments conserve total");
  var forbidden=Stack(1);forbidden.Movable=false;Check(!RebirthStationRefundBackpackPlan.TryCreate(live,2,null,new RebirthStationCancellationRefund(forbidden),out _),"native move permission obeyed");
  Check(!RebirthStationRefundBackpackPlan.TryCreate(new ItemStack[]{null},1,null,refund,out _),"malformed destination denied");
  Check(!RebirthStationRefundBackpackPlan.TryCreate(live,2,null,new RebirthStationCancellationRefund(Stack(65536)),out _),"oversized paid fragment denied");
  var counters=new RebirthStationCancellationRefund(Stack(300000)){IsMaterialReturn=true};
  Check(RebirthStationRefundBackpackPlan.TryCreate(live,2,i=>i==1,counters,out var empty)&&empty.MatchesBefore(live,2,i=>i==1)&&empty.MatchesAfter(live)&&RebirthStationRefundBackpackPlan.TryRead(empty.Write(),counters,out _),"counter settlement injected molten items into backpack or lost exact no-credit image");
  Check(!RebirthStationRefundBackpackPlan.TryCreate(live,2,null,new RebirthStationCancellationRefund(),out _),"empty physical refund accepted");
  System.Console.WriteLine("PASS actual refund backpack planner: detached credits, exact payloads, stack limits, locked/unowned slots, insufficient capacity, conserved duplicate fragments, immutable copies and stored-plan tampering. Native item/permission/codec adapters doubled; no inventory application or gameplay proof.");
 }
}
'@
Add-Type -TypeDefinition ($camperSource+"`n"+$camperFixture)
[RefundBackpackFixture]::Run()
