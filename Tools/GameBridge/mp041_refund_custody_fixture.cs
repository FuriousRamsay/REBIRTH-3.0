// AUTHORED_NOT_RUN. Actual workspace refund/return methods; native effects explicit adapters.
using System;
using System.Collections.Generic;
using System.Linq;
class EntityPlayer {internal object position;internal int entityId=1;}
class ItemValue {internal int type=1;internal string Tag="plain";internal ItemValue Clone()=>new ItemValue{type=type,Tag=Tag};}
class ItemStack {internal ItemValue itemValue;internal int count;internal ItemStack(ItemValue value,int number){itemValue=value;count=number;}internal bool IsEmpty()=>count<=0;internal ItemStack Clone()=>new ItemStack(itemValue.Clone(),count);internal static ItemStack Empty=>new ItemStack(new ItemValue(),0);}
static class RebirthStationGridIngredients {internal static bool IsSameStackSnapshot(ItemStack a,ItemStack b)=>a!=null&&b!=null&&a.count==b.count&&(a.IsEmpty()&&b.IsEmpty()||a.itemValue.type==b.itemValue.type&&a.itemValue.Tag==b.itemValue.Tag);}
static class RemoteResourceTransactions {
 internal sealed class ReturnReceipt {
  internal ItemStack Item;internal int Remaining,Effects;internal bool IsReturnUncertain;internal Func<ReturnReceipt,int> Effect;
  internal int Restore(EntityPlayer player,ItemStack offered){if(Remaining<=0)return 0;int count=Effect(this);if(count>0){Effects++;Remaining-=count;}return count;}
 }
}
class Inventory {
 internal int Calls,Credited,Room=int.MaxValue;internal bool Throw;
 internal bool AddItem(ItemStack stack,bool notify){Calls++;int moved=Math.Min(Room,stack.count);Credited+=moved;if(Throw)throw new Exception("injected opaque owner return");stack.count-=moved;return moved>0;}
}
class PlayerUI {internal EntityPlayer entityPlayer=new EntityPlayer();}
class Xui {internal PlayerUI playerUI=new PlayerUI();internal Inventory PlayerInventory=new Inventory();}
class Slot {
 ItemStack stack;internal bool Suppress;internal Slot(ItemStack item){stack=item;}
 internal ItemStack ItemStack{get=>stack;set{if(!Suppress)stack=value;}}
}
class GameManager {
 internal static GameManager Instance=new GameManager();internal int Drops;
 internal void ItemDropServer(ItemStack stack,object position,object velocity,int id,float time,bool option){Drops++;}
}
static class Vector3 {internal static object zero;}
// Actual native drop observation has its own source-injected cohort.
static class RebirthCookingRefundDropObservation {
 internal static int Mode;
 internal static bool TryDrop(EntityPlayer player,ItemStack stack,out bool attempted){attempted=Mode!=1;if(attempted)GameManager.Instance.ItemDropServer(stack.Clone(),null,null,player.entityId,120f,false);return Mode==0;}
}
// Native postimage observer is covered by its own actual-source fixture.
sealed class RebirthCookingOwnerReturnObservation:IDisposable {
 internal ItemStack Argument;int before;
 internal static RebirthCookingOwnerReturnObservation Begin(EntityPlayer player,ItemStack stack)=>new RebirthCookingOwnerReturnObservation{Argument=stack.Clone(),before=stack.count};
 internal bool Observe(ItemStack remainder,out int delivered){delivered=before-remainder.count;return delivered>=0&&delivered<=before;}
 public void Dispose(){}
}
static class Log {internal static void Warning(string message){}}
static class RebirthCookingRemoteAdmission {internal static bool RetainReturn(XUiC_RebirthCookingWorkspace workspace)=>false;internal static bool IsHeld(XUiC_RebirthCookingWorkspace workspace)=>workspace.HasPendingIngredientReturn;}
static class RebirthCookingGridCustody {internal static bool Handled;internal static bool Return(XUiC_RebirthCookingWorkspace workspace,EntityPlayer player)=>Handled;}
class XUiC_RebirthCookingWorkspace {
 internal readonly Xui xui=new Xui();internal Slot[] slots;
 readonly List<RemoteResourceTransactions.ReturnReceipt> ingredientOrigins=new List<RemoteResourceTransactions.ReturnReceipt>();
 readonly List<ItemStack> pendingIngredientReturns=new List<ItemStack>();bool ingredientReturnGridUnknown;string status;
// ACTUAL_PENDING_RETURN
// ACTUAL_REFUND
// ACTUAL_RETURN_GRID
// ACTUAL_RECONCILE
 internal XUiC_RebirthCookingWorkspace(params ItemStack[] items){slots=items.Select(item=>new Slot(item)).ToArray();}
 internal void Receipt(RemoteResourceTransactions.ReturnReceipt receipt)=>ingredientOrigins.Add(receipt);
 internal bool Return()=>ReturnIngredients();internal bool Orphan(ItemStack stack)=>RefundIngredient(stack,true);
 internal void Reconcile()=>ReconcileIngredientOrigins();
 internal int Pending=>pendingIngredientReturns.Count;internal bool GridUnknown=>ingredientReturnGridUnknown;
}
static class Program {
 static ItemStack Stack(int count,int type=1)=>new ItemStack(new ItemValue{type=type},count);
 static void Need(bool value,string error){if(!value)throw new Exception(error);}
 static RemoteResourceTransactions.ReturnReceipt Receipt(int count,Func<RemoteResourceTransactions.ReturnReceipt,int> effect)=>new RemoteResourceTransactions.ReturnReceipt{Item=Stack(count),Remaining=count,Effect=effect};
 public static void Main(){
  var workspace=new XUiC_RebirthCookingWorkspace(Stack(5));
  var first=Receipt(2,receipt=>2);var pending=Receipt(3,receipt=>{receipt.IsReturnUncertain=true;return 0;});workspace.Receipt(first);workspace.Receipt(pending);
  Need(!workspace.Return()&&workspace.slots[0].ItemStack.count==3&&workspace.HasPendingIngredientReturn,"positive earlier return was not reflected before later unknown");
  Need(workspace.xui.PlayerInventory.Calls==0&&GameManager.Instance.Drops==0,"unknown companion return fell through to another grant");
  pending.Effect=receipt=>{receipt.IsReturnUncertain=false;return 3;};
  Need(workspace.Return()&&workspace.slots[0].ItemStack.IsEmpty()&&first.Effects==1&&pending.Effects==1,"saved continuation replayed an earlier returned quantity");
  workspace=new XUiC_RebirthCookingWorkspace(Stack(3));var refused=Receipt(3,receipt=>0);workspace.Receipt(refused);
  Need(workspace.Return()&&workspace.xui.PlayerInventory.Credited==3&&workspace.slots[0].ItemStack.IsEmpty()&&refused.Remaining==0,"known refusal lost owner fallback or retained a spent source entitlement");
  workspace=new XUiC_RebirthCookingWorkspace(Stack(3));first=Receipt(3,receipt=>3);workspace.Receipt(first);workspace.slots[0].Suppress=true;
  Need(!workspace.Return()&&workspace.GridUnknown&&first.Effects==1&&workspace.slots[0].ItemStack.count==3,"suppressed grid setter accepted");
  Need(!workspace.Return()&&first.Effects==1&&workspace.xui.PlayerInventory.Calls==0,"unresolved grid postimage replayed a financial effect");
  workspace=new XUiC_RebirthCookingWorkspace(Stack(0));pending=Receipt(3,receipt=>{receipt.IsReturnUncertain=true;return 0;});workspace.Receipt(pending);var orphan=Stack(3);
  Need(!workspace.Orphan(orphan)&&workspace.Pending==1&&workspace.HasPendingIngredientReturn,"orphan pending return was discarded");
  pending.Effect=receipt=>{receipt.IsReturnUncertain=false;return 3;};Need(workspace.Return()&&workspace.Pending==0&&orphan.count==0&&workspace.xui.PlayerInventory.Calls==0,"orphan source continuation created another owner grant");
  workspace=new XUiC_RebirthCookingWorkspace(Stack(3));workspace.xui.PlayerInventory.Throw=true;
  Need(!workspace.Return()&&workspace.GridUnknown&&workspace.xui.PlayerInventory.Calls==1,"opaque owner failure was not retained");
  Need(!workspace.Return()&&workspace.xui.PlayerInventory.Calls==1,"opaque owner failure automatically replayed");
  int drops=GameManager.Instance.Drops;
  workspace=new XUiC_RebirthCookingWorkspace(Stack(3));workspace.xui.PlayerInventory.Room=0;
  Need(workspace.Return()&&workspace.slots[0].ItemStack.IsEmpty()&&GameManager.Instance.Drops==drops+1,"positively observed native drop did not retire exact retained grid");
  drops=GameManager.Instance.Drops;RebirthCookingRefundDropObservation.Mode=1;
  workspace=new XUiC_RebirthCookingWorkspace(Stack(3));workspace.xui.PlayerInventory.Room=0;
  Need(!workspace.Return()&&!workspace.GridUnknown&&workspace.slots[0].ItemStack.count==3&&GameManager.Instance.Drops==drops,"unavailable native drop cleared custody or attempted action");
  RebirthCookingRefundDropObservation.Mode=2;
  Need(!workspace.Return()&&workspace.GridUnknown&&workspace.slots[0].ItemStack.count==3&&GameManager.Instance.Drops==drops+1,"unknown native drop outcome cleared custody");
  Need(!workspace.Return()&&GameManager.Instance.Drops==drops+1,"unknown native drop was repeated");
  RebirthCookingRefundDropObservation.Mode=0;
  workspace=new XUiC_RebirthCookingWorkspace(Stack(3));workspace.xui.PlayerInventory.Room=0;workspace.slots[0].Suppress=true;
  drops=GameManager.Instance.Drops;Need(!workspace.Return()&&workspace.GridUnknown&&GameManager.Instance.Drops==drops+1,"drop followed by failed grid publication was not retained");
  Need(!workspace.Return()&&GameManager.Instance.Drops==drops+1,"failed drop/grid publication repeated native drop");
  RebirthCookingGridCustody.Handled=true;workspace=new XUiC_RebirthCookingWorkspace(Stack(0));Need(workspace.Return(),"empty remote grid blocked recipe selection");
  workspace=new XUiC_RebirthCookingWorkspace(Stack(3));Need(!workspace.Return(),"asynchronous remote return falsely reported an empty grid");
  var rich=Stack(3);rich.itemValue.Tag="second-source";workspace=new XUiC_RebirthCookingWorkspace(rich);
  first=Receipt(3,receipt=>0);first.Item.itemValue.Tag="first-source";var second=Receipt(3,receipt=>0);second.Item.itemValue.Tag="second-source";
  workspace.Receipt(first);workspace.Receipt(second);workspace.Reconcile();
  Need(first.Remaining==0&&second.Remaining==3&&workspace.slots[0].ItemStack.count==3&&workspace.xui.PlayerInventory.Calls==0,"same type with different rich metadata consumed the wrong original receipt");
  workspace=new XUiC_RebirthCookingWorkspace(Stack(1),Stack(3));first=Receipt(2,receipt=>0);second=Receipt(4,receipt=>0);workspace.Receipt(first);workspace.Receipt(second);workspace.Reconcile();
  Need(first.Remaining==2&&second.Remaining==2&&workspace.slots[0].ItemStack.count==1&&workspace.slots[1].ItemStack.count==3,"same rich material split across slots was overallocated or mutated");
  Console.WriteLine("PASS actual workspace refund custody and rich origin reconciliation with native/UI adapters; gameplay/persistence NOT RUN");
 }
}
