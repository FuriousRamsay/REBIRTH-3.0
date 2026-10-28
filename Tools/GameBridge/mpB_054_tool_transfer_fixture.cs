using System;
using System.Linq;
using System.Collections.Generic;
// AUTHORED_NOT_RUN. Actual tool transfer adapter plus existing native conservation method.
// Native item codec and PlayerDataFile reader are explicit doubles, not owner/save authentication.
// ACTUAL_TOOL_TRANSFER_SOURCE
internal sealed class ItemClass {}
internal sealed class ItemValue {internal ItemClass ItemClass=new ItemClass();internal string Image;}
internal sealed class ItemStack
{internal int count;internal ItemValue itemValue=new ItemValue();internal bool IsEmpty()=>count==0;internal static ItemStack Empty=>new ItemStack();}
internal static class RebirthNativeItemCodec {internal static string Encode(ItemValue value)=>value.Image;}
internal sealed class PlayerDataFile {internal ItemStack[] Bag,Belt;}
internal static class RebirthGearPlayerDataInventory
{internal static bool Throw;internal static ItemStack[] ReadSlots(PlayerDataFile data,bool bag){if(Throw)throw new Exception("unavailable native blob");return bag?data.Bag:data.Belt;}}
internal static class RebirthLogisticsDeathPartition
{
 // ACTUAL_NATIVE_CONSERVATION_SOURCE
}
internal static class Program
{
 private static ItemStack Stack(string image,int count=1)=>new ItemStack{count=count,itemValue=new ItemValue{Image=image}};
 private static ItemStack Empty()=>new ItemStack();
 private static void Need(bool value,string message){if(!value)throw new Exception(message);}
 public static void Main()
 {
  var bag=new[]{Stack("new-tool:q6:mods:stats:texture"),Stack("unrelated",4),Empty()};var belt=new[]{Stack("belt",2)};
  var tools=new[]{Stack("worn-tool:q2:wear99"),Empty()};
  var afterBag=new[]{Stack("worn-tool:q2:wear99"),Stack("unrelated",4),Empty()};var afterBelt=new[]{Stack("belt",2)};
  var afterTools=new[]{Stack("new-tool:q6:mods:stats:texture"),Empty()};
  Func<bool> valid=()=>RebirthProductiveEarnedPendingToolTransfer.Conserved(bag,belt,tools,afterBag,afterBelt,afterTools);
  Need(valid()&&bag[0].itemValue.Image=="new-tool:q6:mods:stats:texture"&&tools[0].itemValue.Image=="worn-tool:q2:wear99","native exact swap not conserved or source mutated");
  afterBag[0]=Empty();Need(!valid(),"old station tool lost");afterBag[0]=Stack("worn-tool:q2:wear99");
  afterBag[2]=Stack("new-tool:q6:mods:stats:texture");Need(!valid(),"new station tool duplicated in owner bag");afterBag[2]=Empty();
  afterTools[0]=Stack("new-tool:q1");Need(!valid(),"tool quality/native metadata changed");afterTools[0]=Stack("new-tool:q6:mods:stats:texture");
  afterBag[1].count=3;Need(!valid(),"unrelated owner material lost");afterBag[1].count=4;
  afterBelt[0].count=3;Need(!valid(),"unrelated belt material created");afterBelt[0].count=2;
  afterTools[1]=null;Need(!valid(),"malformed null native slot accepted");afterTools[1]=Empty();
  afterTools[0].count=ushort.MaxValue+1;Need(!valid(),"out-of-native-range stack accepted");afterTools[0].count=1;
  Need(!RebirthProductiveEarnedPendingToolTransfer.Conserved(new ItemStack[170],belt,tools,new ItemStack[170],afterBelt,afterTools),"oversized owner grid accepted");
  Need(!RebirthProductiveEarnedPendingToolTransfer.Conserved(bag,belt,tools,afterBag,afterBelt,new[]{Stack("new-tool:q6:mods:stats:texture")}),"station slot layout changed");
  var beforeFile=new PlayerDataFile{Bag=bag,Belt=belt};var afterFile=new PlayerDataFile{Bag=afterBag,Belt=afterBelt};
  Need(RebirthProductiveEarnedPendingToolTransfer.ConservedNativeOwnerFiles(beforeFile,afterFile,tools,afterTools),"canonical owner file projection not composed");
  afterFile.Bag=null;Need(!RebirthProductiveEarnedPendingToolTransfer.ConservedNativeOwnerFiles(beforeFile,afterFile,tools,afterTools),"missing native owner file grid accepted");
  afterFile.Bag=afterBag;RebirthGearPlayerDataInventory.Throw=true;Need(!RebirthProductiveEarnedPendingToolTransfer.ConservedNativeOwnerFiles(beforeFile,afterFile,tools,afterTools),"unavailable native blob escaped comparison refusal");
  RebirthGearPlayerDataInventory.Throw=false;
  var cursorBefore=Stack("new-tool:q6:mods:stats:texture");var cursorAfter=Stack("worn-tool:q2:wear99");var gestureBag=new[]{Empty(),Stack("unrelated",4),Empty()};
  Need(RebirthProductiveEarnedPendingToolTransfer.ConservedGesture(gestureBag,belt,tools,cursorBefore,gestureBag,afterBelt,afterTools,cursorAfter),"native cursor-to-tool swap rejected");
  Need(!RebirthProductiveEarnedPendingToolTransfer.Conserved(gestureBag,belt,tools,gestureBag,afterBelt,afterTools),"nonempty cursor silently omitted from settled comparison");
  Need(!RebirthProductiveEarnedPendingToolTransfer.ConservedGesture(gestureBag,belt,tools,cursorBefore,gestureBag,afterBelt,afterTools,Empty()),"old tool vanished from native cursor");
  Need(!RebirthProductiveEarnedPendingToolTransfer.ConservedGesture(gestureBag,belt,tools,cursorBefore,gestureBag,afterBelt,afterTools,Stack("new-tool:q6:mods:stats:texture")),"new tool duplicated on cursor");
  Need(!RebirthProductiveEarnedPendingToolTransfer.ConservedGesture(gestureBag,belt,tools,null,gestureBag,afterBelt,afterTools,cursorAfter),"missing cursor image accepted as empty");
  Console.WriteLine("PASS extracted pending tool transfer conservation and existing native-item balance; disclosed codec/file adapters, no transfer authority");
 }
}
