// Actual production gesture/cursor prefix bodies are extracted by the runner.
// Native UI and custody permit doubles below do not prove Harmony/native ordering.
namespace HarmonyLib {
 [AttributeUsage(AttributeTargets.Class)]public sealed class HarmonyPatch:Attribute{public HarmonyPatch(){}public HarmonyPatch(Type t,string n){}}
 [AttributeUsage(AttributeTargets.Method)]public sealed class HarmonyPriority:Attribute{public HarmonyPriority(int p){}}
 public static class Priority{public const int First=800;}
 public static class AccessTools{
  public static MethodInfo DeclaredMethod(Type t,string name,Type[] args)=>t.GetMethod(name,BindingFlags.Public|BindingFlags.Instance|BindingFlags.DeclaredOnly,null,args,null);
  public static MethodInfo[] GetDeclaredMethods(Type t)=>t.GetMethods(BindingFlags.Public|BindingFlags.Instance|BindingFlags.DeclaredOnly);
 }
}
public sealed class ItemStack{}
public sealed class EntityPlayerLocal{}
// Camper owner cursor permission is independently qualified and denied here.
public static class RebirthCamperStationContentsDelivery{public static bool AllowOwnerCursor(XUiC_DragAndDropWindow window,ItemStack value)=>false;}
public sealed class Ui{public PlayerUi playerUI=new PlayerUi();}
public sealed class PlayerUi{public EntityPlayerLocal entityPlayer;}
public class XUiC_ItemStack {
 public Ui xui=new Ui();
 public void HandleStackSwap(){}public void HandlePartialStackPickup(){}public void HandleDropOne(){}public void SwapItem(){}public void HandleMoveToPreferredLocation(){}
}
public class XUiC_DragAndDropWindow {
 public Ui xui=new Ui();public void SetCurrentStack(ItemStack stack,bool playPickupSound=true){}
 public void DropCurrentItem(){}public void DropCurrentItem(int count){}public void PlaceItemBackInInventory(){}
}
public static class RebirthCookingOutputDelivery {
 public static EntityPlayerLocal Held;public static bool IsHeld(EntityPlayerLocal player)=>player!=null&&ReferenceEquals(player,Held);
}
public static class RebirthCookingGridCustody {
 public static XUiC_DragAndDropWindow Window;public static ItemStack Value;public static bool Used;
 public static bool IsExecutingSlot(XUiC_ItemStack slot)=>false;
 public static bool AllowCursor(XUiC_DragAndDropWindow window,ItemStack value){if(Used||!ReferenceEquals(window,Window)||!ReferenceEquals(value,Value))return false;Used=true;return true;}
}
public static class MpACookingCursorHoldFixture {
 static bool Call(Type t,params object[] args)=>(bool)t.GetMethod("Prefix",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,args);
 static void Check(bool value,string message){if(!value)throw new Exception(message);}
 public static void Main(){
  var owner=new EntityPlayerLocal();var other=new EntityPlayerLocal();var slot=new XUiC_ItemStack();slot.xui.playerUI.entityPlayer=owner;
  var cursor=new XUiC_DragAndDropWindow();cursor.xui.playerUI.entityPlayer=owner;var value=new ItemStack();RebirthCookingOutputDelivery.Held=owner;
  int cursorWrites=0,slotWrites=0;
  if(Call(typeof(RebirthCookingDeliverySlotGesture),slot)){cursorWrites++;slotWrites++;}
  Check(cursorWrites==0&&slotWrites==0,"held gesture stops before cursor-first swap effects");
  Check(!Call(typeof(RebirthCookingDeliveryCursorGesture),cursor),"native drop/return fallback cannot drop held cursor");
  Check(!Call(typeof(RebirthCookingDeliveryCursorSet),cursor,value),"owner delta has no cursor permit");
  RebirthCookingGridCustody.Window=cursor;RebirthCookingGridCustody.Value=value;
  Check(!Call(typeof(RebirthCookingDeliveryCursorSet),cursor,new ItemStack()),"equivalent but nonoriginal value refuses");
  Check(Call(typeof(RebirthCookingDeliveryCursorSet),cursor,value),"exact planned cursor setter permits once");
  Check(!Call(typeof(RebirthCookingDeliveryCursorSet),cursor,value),"consumed cursor setter refuses replay");
  slot.xui.playerUI.entityPlayer=other;cursor.xui.playerUI.entityPlayer=other;
  Check(Call(typeof(RebirthCookingDeliverySlotGesture),slot)&&Call(typeof(RebirthCookingDeliveryCursorGesture),cursor)&&Call(typeof(RebirthCookingDeliveryCursorSet),cursor,value),"other owner native UI remains available");
  int targets=0;foreach(var target in (IEnumerable<MethodBase>)typeof(RebirthCookingDeliveryCursorGesture).GetMethod("TargetMethods",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,null))targets++;
  Check(targets==3,"both native drop overloads and return-to-inventory boundary targeted");
  Console.WriteLine("PASS actual cooking cursor hold prefix methods with disclosed UI/permit doubles");
 }
}
