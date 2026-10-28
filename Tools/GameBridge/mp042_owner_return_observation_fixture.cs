// AUTHORED_NOT_RUN. Actual native-postimage observer; codecs/world are adapters.
using System;
using System.Linq;
using System.Collections.Generic;
// ACTUAL_OBSERVER
class ItemValue {internal string Tag="plain";internal ItemValue Clone()=>new ItemValue{Tag=Tag};}
class ItemStack {
 internal ItemValue itemValue;internal int count;internal ItemStack(ItemValue value,int quantity){itemValue=value;count=quantity;}
 internal bool IsEmpty()=>count==0||itemValue==null;internal ItemStack Clone()=>new ItemStack(itemValue?.Clone(),count);
}
class ItemStackGrid {internal ItemStack[] items=new[]{new ItemStack(new ItemValue(),0)};}
class Carrier {internal ItemStackGrid ItemGrid=new ItemStackGrid();}
class EquipmentSlots {internal ItemValue[] Slots=new ItemValue[1];internal int GetSlotCount()=>Slots.Length;internal ItemValue GetSlotItem(int slot)=>Slots[slot];}
class EntityPlayerLocal {
 internal World world;internal int entityId=1,Died;internal bool Dead;internal string Creation="original";
 internal Carrier bag=new Carrier(),inventory=new Carrier();internal EquipmentSlots equipment=new EquipmentSlots();
 internal bool IsSpawned()=>true;internal bool IsDead()=>Dead;
}
class World {internal EntityPlayerLocal Player;internal EntityPlayerLocal GetPrimaryPlayer()=>Player;internal object GetEntity(int id)=>Player;}
class GameManager {internal static GameManager Instance=new GameManager();internal World World;}
static class ThreadManager {internal static bool IsMainThread()=>true;}
static class RebirthCamperCharacterAccess {internal static string CurrentCreation(EntityPlayerLocal player)=>player.Creation;}
static class RebirthStationGridIngredients {
 internal static bool IsSameStackSnapshot(ItemStack a,ItemStack b)=>a!=null&&b!=null&&(a.IsEmpty()&&b.IsEmpty()||a.count==b.count&&a.itemValue?.Tag==b.itemValue?.Tag);
}
static class RebirthProductiveEarnedObservationBudget {
 sealed class Lease:IDisposable {public void Dispose(){}}
 internal static bool Available=true;internal static bool TryRetainDetached(long size,out IDisposable lease){lease=new Lease();return Available;}
 internal static bool TryResizeDetached(IDisposable lease,long size)=>Available;
}
static class RemoteResourceTransactions {
 internal static long MeasureOriginalNativeFrame(byte[] image)=>1024;
 internal static byte[] CaptureOriginalNativeFrame(ItemStack[] slots,ref long remaining)=>RebirthCamperInventorySnapshot.Capture(slots);
}
static class RebirthCamperInventorySnapshot {
 static int next;static Dictionary<int,ItemStack[]> frames=new Dictionary<int,ItemStack[]>();
 internal static byte[] Capture(ItemStack[] slots){int id=++next;frames[id]=slots.Select(s=>s.Clone()).ToArray();return BitConverter.GetBytes(id);}
 internal static ItemStack[] Restore(byte[] image)=>frames[BitConverter.ToInt32(image,0)].Select(s=>s.Clone()).ToArray();
}
static class Program {
 static ItemStack Stack(int count,string tag="plain")=>new ItemStack(new ItemValue{Tag=tag},count);
 static void Need(bool ok,string why){if(!ok)throw new Exception(why);}
 static EntityPlayerLocal Setup(){var player=new EntityPlayerLocal();player.world=new World{Player=player};GameManager.Instance.World=player.world;return player;}
 public static void Main(){
  var player=Setup();var offer=Stack(7);int count;
  using(var original=RebirthCookingOwnerReturnObservation.Begin(player,offer)){
   player.bag.ItemGrid.items[0]=Stack(3);original.Argument.count=4;
   Need(original.Observe(original.Argument,out count)&&count==3&&offer.count==7,"partial true native delivery lost retained remainder");
  }
  player=Setup();using(var original=RebirthCookingOwnerReturnObservation.Begin(player,Stack(3))){
   original.Argument.count=0;Need(!original.Observe(original.Argument,out count),"suppressed native add accepted");
  }
  player=Setup();using(var original=RebirthCookingOwnerReturnObservation.Begin(player,Stack(3))){
   player.bag.ItemGrid.items[0]=Stack(2);original.Argument.count=0;Need(!original.Observe(original.Argument,out count),"under-delivery accepted");
  }
  player=Setup();using(var original=RebirthCookingOwnerReturnObservation.Begin(player,Stack(3))){
   player.bag.ItemGrid.items[0]=Stack(4);original.Argument.count=0;Need(!original.Observe(original.Argument,out count),"over-delivery accepted");
  }
  player=Setup();using(var original=RebirthCookingOwnerReturnObservation.Begin(player,Stack(3))){
   player.bag.ItemGrid.items[0]=Stack(3,"foreign");original.Argument.count=0;Need(!original.Observe(original.Argument,out count),"wrong rich item accepted");
  }
  player=Setup();player.bag.ItemGrid.items[0]=Stack(4);
  using(var original=RebirthCookingOwnerReturnObservation.Begin(player,Stack(3))){
   player.bag.ItemGrid.items[0].count=7;original.Argument.count=0;
   Need(original.Observe(original.Argument,out count)&&count==3,"independent before image changed with native slot");
  }
  player=Setup();using(var original=RebirthCookingOwnerReturnObservation.Begin(player,Stack(1))){
   player.equipment.Slots[0]=new ItemValue();original.Argument.count=0;Need(original.Observe(original.Argument,out count)&&count==1,"native equipment insertion lost");
  }
  player=Setup();using(var original=RebirthCookingOwnerReturnObservation.Begin(player,Stack(3))){
   player.bag.ItemGrid.items[0]=Stack(3);original.Argument.count=0;player.Died++;
   Need(!original.Observe(original.Argument,out count),"new life inherited delivery");
  }
  player=Setup();using(var original=RebirthCookingOwnerReturnObservation.Begin(player,Stack(3))){
   player.bag.ItemGrid.items[0]=Stack(3);original.Argument.count=0;original.Argument.itemValue.Tag="mutated";
   player.bag.ItemGrid.items[0].itemValue=original.Argument.itemValue;
   Need(!original.Observe(original.Argument,out count),"mutable native argument changed expected rich item");
  }
  player=Setup();using(var original=RebirthCookingOwnerReturnObservation.Begin(player,Stack(3))){Need(original.Observe(original.Argument,out count)&&count==0,"unchanged refusal lost");}
  RebirthProductiveEarnedObservationBudget.Available=false;Need(RebirthCookingOwnerReturnObservation.Begin(player,Stack(3))==null,"budget refusal ignored");
  Console.WriteLine("PASS actual cooking owner AddItem postimage observer; native codecs and API calls are adapters; persistence/gameplay NOT RUN");
 }
}
