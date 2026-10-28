using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
// AUTHORED_NOT_RUN. Exact production Frame class injected; native codec/UI
// adapters explicitly do not establish native gesture or persistence evidence.
internal static partial class RebirthProductiveEarnedPendingToolGestures {
// ACTUAL_PENDING_TOOL_FRAME_SOURCE
}
internal sealed class ItemStack {internal int Id;internal ItemStack(int id){Id=id;}}
internal sealed class Grid {internal ItemStack[] items=new[]{new ItemStack(0)};}
internal sealed class Container {internal Grid ItemGrid=new Grid();}
internal sealed class EntityPlayerLocal {internal Container bag=new Container(),inventory=new Container();}
internal sealed class DragWindow {internal ItemStack CurrentStack=new ItemStack(0);}
internal sealed class XUi {internal DragWindow DragAndDropWindow=new DragWindow();}
internal sealed class XUiC_WorkstationToolGrid {internal XUi xui=new XUi();internal ItemStack[] Slots=new[]{new ItemStack(7)};internal ItemStack[] GetSlots()=>Slots;}
internal static class RebirthNativeItemConformanceReader {
 internal static bool TryEncodeStackArrayV1(ItemStack[] a,int bytes,int chars,out string s){s=null;if(a==null||a.Any(v=>v==null))return false;s=Convert.ToBase64String(a.Select(v=>(byte)v.Id).ToArray());return true;}
 internal static bool TryDecodeStackArrayV1(string s,int count,int bytes,int chars,out ItemStack[] a){a=Convert.FromBase64String(s).Select(v=>new ItemStack(v)).ToArray();return a.Length==count;}
}
internal static class Program {
 static void Need(bool b,string m){if(!b)throw new Exception(m);}
 public static void Main(){
  var actor=new EntityPlayerLocal();var tools=new XUiC_WorkstationToolGrid();
  var first=RebirthProductiveEarnedPendingToolGestures.Frame.Capture(actor,tools);
  Need(first!=null&&first.Counts.SequenceEqual(new[]{1,1,1,1}),"four native vectors missing");
  Need(first.Decode(2,out var decoded)&&decoded[0].Id==7,"tool vector decode changed");
  Need(first.Same(RebirthProductiveEarnedPendingToolGestures.Frame.Capture(actor,tools)),"unchanged frame differs");
  tools.xui.DragAndDropWindow.CurrentStack=tools.Slots[0];tools.Slots[0]=new ItemStack(0);
  var next=RebirthProductiveEarnedPendingToolGestures.Frame.Capture(actor,tools);
  Need(!first.Same(next)&&next.Decode(3,out decoded)&&decoded[0].Id==7,"native cursor omitted from frame");
  Need(first.Decode(2,out decoded)&&decoded[0].Id==7,"capture retained mutable native items");
  tools.xui.DragAndDropWindow.CurrentStack=null;Need(RebirthProductiveEarnedPendingToolGestures.Frame.Capture(actor,tools)==null,"missing cursor accepted");
  tools.xui.DragAndDropWindow.CurrentStack=new ItemStack(0);
  actor.bag.ItemGrid.items=new ItemStack[170];Need(RebirthProductiveEarnedPendingToolGestures.Frame.Capture(actor,tools)==null,"oversize bag accepted");
  actor.bag.ItemGrid.items=new[]{new ItemStack(0)};actor.inventory.ItemGrid.items=new ItemStack[21];Need(RebirthProductiveEarnedPendingToolGestures.Frame.Capture(actor,tools)==null,"oversize belt accepted");
  actor.inventory.ItemGrid.items=new[]{new ItemStack(0)};tools.Slots=new ItemStack[256];Need(RebirthProductiveEarnedPendingToolGestures.Frame.Capture(actor,tools)==null,"oversize tools accepted");
  tools.Slots=new ItemStack[0];Need(RebirthProductiveEarnedPendingToolGestures.Frame.Capture(actor,tools)==null,"empty vector accepted");
  Console.WriteLine("PASS exact production gesture Frame capture with explicit codec/UI adapters; callback/persistence coverage outstanding");
 }
}
