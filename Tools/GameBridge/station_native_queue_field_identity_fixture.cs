// Actual field matcher and complete rewrite are injected. Native field metadata
// is inspected separately; these types model reflection identity only.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
public class TileEntity { public bool bUserAccessing; }
public class TileEntityWorkstation:TileEntity { public bool isBurning;public bool hasRecipeInQueue()=>true; }
public class RecipeQueueItem { public float CraftingTimeLeft; }
public class ForeignFieldOwner { public bool bUserAccessing; }
internal static class RebirthRecyclerNativeLoop {
 internal static void SetTime(RecipeQueueItem row,float value,TileEntityWorkstation station){}
 internal static bool Continue(TileEntityWorkstation station)=>true;
 internal static bool UserAccessing(TileEntityWorkstation station)=>false;
 internal static bool BurningForPaidCompletion(TileEntityWorkstation station)=>false;
 // ACTUAL_FIELD_IDENTITY
 // ACTUAL_REWRITE
}
public static class QueueFieldIdentityFixture {
 private static void Need(bool value,string name){if(!value)throw new Exception(name);}
 private static List<CodeInstruction> Shape(FieldInfo access){
  var timer=typeof(RecipeQueueItem).GetField("CraftingTimeLeft");
  return new List<CodeInstruction>{new CodeInstruction(OpCodes.Ldfld,access),new CodeInstruction(OpCodes.Ldfld,typeof(TileEntityWorkstation).GetField("isBurning")),new CodeInstruction(OpCodes.Stfld,timer),new CodeInstruction(OpCodes.Stfld,timer),new CodeInstruction(OpCodes.Stfld,timer),new CodeInstruction(OpCodes.Call,typeof(TileEntityWorkstation).GetMethod("hasRecipeInQueue"))};
 }
 private static bool Refuses(List<CodeInstruction> shape){try{RebirthRecyclerNativeLoop.Rewrite(shape).ToArray();return false;}catch(InvalidOperationException){return true;}}
 public static string Run(){
  var reflected=typeof(TileEntityWorkstation).GetField("bUserAccessing");
  var declared=typeof(TileEntity).GetField("bUserAccessing");
  Need(!reflected.Equals(declared)&&reflected.MetadataToken==declared.MetadataToken,"fixture must reproduce unequal reflected wrappers of one native field");
  var source=Shape(declared);source[0].labels.Add(default(Label));
  var rewritten=RebirthRecyclerNativeLoop.Rewrite(source).ToArray();
  Need(rewritten.Count(x=>x.operand is MethodInfo m&&m.Name=="UserAccessing")==1,"inherited owner gate missing");
  Need(rewritten.Count(x=>x.operand is MethodInfo m&&m.Name=="BurningForPaidCompletion")==1,"burning gate missing");
  Need(rewritten.Count(x=>x.operand is MethodInfo m&&m.Name=="SetTime")==3,"timer writes missing");
  Need(rewritten[0].labels.Count==1&&source[0].opcode==OpCodes.Ldfld,"rewrite dropped branch labels or mutated source instructions");
  Need(Refuses(Shape(typeof(ForeignFieldOwner).GetField("bUserAccessing"))),"same-name foreign field accepted");
  var missing=Shape(declared);missing.RemoveAt(1);Need(Refuses(missing),"missing burning gate accepted");
  var duplicate=Shape(declared);duplicate.Add(duplicate[2]);Need(Refuses(duplicate),"extra timer write accepted");
  return "PASS actual native queue rewrite: inherited field identity, all exact gates, branch labels, foreign-field/missing/duplicate refusal";
 }
}
