using System;
using System.Collections.Generic;
class ItemClass {public string Name="bat";public string GetItemName(){return Name;}}
class ItemValue {public ItemClass ItemClass=new ItemClass();public string Key="equipment-A";}
class ItemStack {public ItemValue itemValue=new ItemValue();public int count=1;public bool IsEmpty(){return count<=0;}}
class Grid {public ItemStack[] items;}
class Inventory {public Grid ItemGrid=new Grid();}
class EntityPlayerLocal {public Inventory inventory=new Inventory();}
static class RebirthGameBridgeNeeds {public static int ToolbeltSize(EntityPlayerLocal player){return 10;}}
class Storage {
 const string CrateName="cntWoodWritableCrate";
 private readonly HashSet<string> retainedSupplyKeys=new HashSet<string>(StringComparer.Ordinal);
 public string Failure {get;private set;}
 static string RestoreKey(ItemStack stack){return stack==null||stack.IsEmpty()?null:stack.itemValue.Key;}
 // PRODUCTION_CLASS
 public void Remember(ItemStack before,ItemStack after){var p=new EntityPlayerLocal();p.inventory.ItemGrid.items=new[]{after};RememberDisplacedSupplies(new[]{before},p);}
 public bool Retained(ItemStack stack){return RetainSupply(stack);}
}
class Checks {
 static void A(bool value,string message){if(!value)throw new Exception(message);}
 static ItemStack Item(string key,string name="bat"){return new ItemStack{itemValue=new ItemValue{Key=key,ItemClass=new ItemClass{Name=name}}};}
 static void Main(){
 var s=new Storage();s.Remember(Item("equipment-A"),Item("crate", "cntWoodWritableCrate"));A(s.Retained(Item("equipment-A")),"exact displaced equipment retained");A(!s.Retained(Item("equipment-B")),"same-type different quality/mods remains cargo");
 s=new Storage();s.Remember(Item("equipment-A"),Item("equipment-B"));A(s.Retained(Item("equipment-A")),"same-type belt replacement detected");A(!s.Retained(Item("equipment-B")),"replacement identity not excluded");
 s=new Storage();s.Remember(Item("equipment-A"),Item("equipment-A"));A(!s.Retained(Item("equipment-A")),"unchanged belt item creates no retained bag exclusion");
 s=new Storage();s.Remember(Item("type:ammo","ammo"),Item("crate","cntWoodWritableCrate"));A(s.Retained(Item("type:ammo","ammo")),"plain stacking supply retains type semantics");
 s=new Storage();s.Remember(Item(null),Item("crate","cntWoodWritableCrate"));A(s.Failure!=null,"unserializable displaced supply fails explicitly");
 s=new Storage();A(s.Retained(Item(null))&&s.Failure!=null,"unserializable cargo refuses deposit");A(!s.Retained(null),"empty supply harmless");
 Console.WriteLine("PASS 9 actual retention method checks: exact displaced/changed same-type equipment, alternate variant cargo, unchanged belt, plain supplies and fingerprint refusal. RestoreKey/native serialization doubled; identity serialization separately covered by existing ledger fixture.");
 }
}