using System;
using System.Collections.Generic;
public class ItemValue{public Dictionary<string,object> Metadata;}
public class ItemStack{public ItemValue itemValue;}
public class CraftCompleteData{public ItemStack CraftedItemStack;}
class Test {
 internal const string Prefix="rebirth.station.receipt.";
// SOURCE
 static int passed;
 static void Check(bool ok,string name){if(!ok)throw new Exception(name);passed++;}
 static CraftCompleteData Make(string key){
  var metadata=new Dictionary<string,object>();if(key!=null)metadata[key]="invalid";
  return new CraftCompleteData{CraftedItemStack=new ItemStack{itemValue=new ItemValue{Metadata=metadata}}};
 }
 static void Main(){
  Check(CanUseNativeRewards(null),"null native batch passes");
  Check(CanUseNativeRewards(new List<CraftCompleteData>()),"empty native batch passes");
  var ordinary=Make("other");Check(CanUseNativeRewards(new[]{ordinary}),"ordinary metadata passes");
  var malformed=Make(Prefix+"job");Check(!CanUseNativeRewards(new[]{malformed}),"malformed generic marker holds");
  Check(!CanUseNativeRewards(new[]{Make(Prefix+"unknown")}),"unknown reserved key holds");
  var mixed=new[]{ordinary,malformed};Check(!CanUseNativeRewards(mixed),"mixed batch holds");
  Check(ReferenceEquals(mixed[0],ordinary)&&ReferenceEquals(mixed[1],malformed)&&mixed.Length==2,"held batch remains intact");
  Check(CanUseNativeRewards(new[]{Make("rebirth.station.receiptElsewhere")}),"similar unrelated prefix passes");
  Console.WriteLine("PASS "+passed+" actual reserved receipt hold checks; native receipt/item types doubled.");
 }
}