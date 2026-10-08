$ErrorActionPreference='Stop'
$root=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$source=[IO.File]::ReadAllText((Join-Path $root 'Scripts/Survivor/Support/RebirthNativeBackpackEquipmentPatches.cs'))
$stubs=@"
namespace HarmonyLib { [System.AttributeUsage(System.AttributeTargets.All,AllowMultiple=true)] public class HarmonyPatch:System.Attribute { public HarmonyPatch(){} public HarmonyPatch(System.Type t,string s){} } public class HarmonyPrefix:System.Attribute{} public class HarmonyPostfix:System.Attribute{} }
public enum EquipmentSlots{Head,Backpack}
public class ItemClass{public string GetItemDescriptionKey(){return "native";}}
public class ItemClassArmor:ItemClass{public EquipmentSlots EquipSlot;}
public class ItemValue{public ItemClass ItemClass;}
public class ItemStack{public ItemValue itemValue;public bool IsEmpty(){return itemValue==null;}}
public class XUiM_PlayerEquipment{public ItemStack EquipItem(ItemStack s){return s;}}
public class Drag{private ItemStack current;public int Writes;public ItemStack CurrentStack{get{return current==null?null:new ItemStack{itemValue=current.itemValue};}set{current=value;Writes++;}}}
public class XUi{public Drag DragAndDropWindow;}
public class XUiC_EquipmentStack{public EquipmentSlots EquipSlot;public XUi xui;}
public static class RebirthSurvivorMode{public static bool Enabled;public static bool IsEnabledForCurrentWorld(){return Enabled;}}
public static class Fixture{
static void Check(bool v,string name){if(!v)throw new System.Exception(name);}
public static void Run(){
var offered=new ItemStack{itemValue=new ItemValue{ItemClass=new ItemClassArmor{EquipSlot=EquipmentSlots.Backpack}}};
ItemStack result=null;
var slot=new XUiC_EquipmentStack{EquipSlot=EquipmentSlots.Backpack,xui=new XUi{DragAndDropWindow=new Drag{CurrentStack=offered}}};
Check(RebirthNativeBackpackEquipmentPatches.EquipItemPrefix(offered,ref result),"disabled equip passthrough");
Check(RebirthNativeBackpackEquipmentPatches.SwapItemPrefix(slot),"disabled swap passthrough");
RebirthSurvivorMode.Enabled=true;
Check(!RebirthNativeBackpackEquipmentPatches.EquipItemPrefix(offered,ref result)&&object.ReferenceEquals(result,offered),"refusal returns exact offered stack");
Check(!RebirthNativeBackpackEquipmentPatches.SwapItemPrefix(slot)&&slot.xui.DragAndDropWindow.CurrentStack.itemValue==offered.itemValue&&slot.xui.DragAndDropWindow.Writes==1,"cursor retained");
slot.xui.DragAndDropWindow.CurrentStack=new ItemStack();
Check(RebirthNativeBackpackEquipmentPatches.SwapItemPrefix(slot),"existing item can be removed");
slot.EquipSlot=EquipmentSlots.Head;slot.xui.DragAndDropWindow.CurrentStack=offered;
Check(RebirthNativeBackpackEquipmentPatches.SwapItemPrefix(slot),"other equipment untouched");
var ordinary=new ItemStack{itemValue=new ItemValue{ItemClass=new ItemClassArmor{EquipSlot=EquipmentSlots.Head}}};
Check(RebirthNativeBackpackEquipmentPatches.EquipItemPrefix(ordinary,ref result),"ordinary armor untouched");
Check(RebirthNativeBackpackEquipmentPatches.EquipItemPrefix(null,ref result),"null retains native contract");
string description="nativeBackpack";RebirthNativeBackpackEquipmentPatches.DescriptionKeyPostfix(offered.itemValue.ItemClass,ref description);Check(description=="xuiRebirthNativeBackpackDescription","REBIRTH native backpack description not overridden");
description="nativeArmor";RebirthNativeBackpackEquipmentPatches.DescriptionKeyPostfix(ordinary.itemValue.ItemClass,ref description);Check(description=="nativeArmor","ordinary armor description changed");
RebirthSurvivorMode.Enabled=false;description="nativeBackpack";RebirthNativeBackpackEquipmentPatches.DescriptionKeyPostfix(offered.itemValue.ItemClass,ref description);Check(description=="nativeBackpack","mode switch retained REBIRTH description");
}
}
"@
Add-Type -TypeDefinition ($source+[Environment]::NewLine+$stubs)
[Fixture]::Run()
'PASS actual backpack equipment prefixes with native/Harmony adapters doubled; not native execution.'