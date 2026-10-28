// AUTHORED_NOT_RUN. Whole production predictor; native containers/events are explicit doubles.
using System;using System.Collections.Generic;using System.Linq;
class XUiC_ItemStack{internal enum StackLocationTypes{Backpack,ToolBelt}}
class ItemClass
{
 internal int MaxCount=10;internal object Stacknumber=new object();internal bool Move=true;internal int OnlySlot=-1;
 internal bool CanMoveToSlot(XUiC_ItemStack.StackLocationTypes location,int slot)=>OnlySlot<0||OnlySlot==slot;
}
class ItemClassArmor:ItemClass{internal bool AutoEquip,AllowUnEquip;internal int EquipSlot;}
class ItemValue
{
 internal int type;internal string Metadata="";internal ItemClass ItemClass;
 internal bool IsEmpty()=>type==0;
 internal ItemValue Clone()=>new ItemValue{type=type,Metadata=Metadata,ItemClass=ItemClass};
}
class ItemStack
{
 internal ItemValue itemValue;internal int count;
 internal ItemStack(ItemValue value,int quantity){itemValue=value;count=quantity;}
 internal bool IsEmpty()=>count==0||itemValue.type==0;
 internal ItemStack Clone()=>new ItemStack(itemValue.Clone(),count);
 internal static ItemStack[] Clone(ItemStack[] cells)=>cells.Select(x=>x.Clone()).ToArray();
 internal bool CanMoveTo(XUiC_ItemStack.StackLocationTypes location)=>itemValue.ItemClass==null||itemValue.ItemClass.Move;
 internal bool CanStackWith(ItemStack other)=>!IsEmpty()&&itemValue.type==other.itemValue.type&&itemValue.Metadata==other.itemValue.Metadata&&count+other.count<=itemValue.ItemClass.MaxCount;
 internal bool CanStackPartlyWith(ItemStack other,out int quantity)
 {quantity=other.count;return !IsEmpty()&&itemValue.type==other.itemValue.type&&itemValue.Metadata==other.itemValue.Metadata&&CanStackPartly(ref quantity);}
 internal bool CanStackPartly(ref int quantity)
 {if(IsEmpty())return false;quantity=Math.Min(quantity,itemValue.ItemClass.MaxCount-count);return quantity>0;}
}
class Grid{internal ItemStack[] items;}
class Bag{internal Grid ItemGrid=new Grid();}
class Inventory{internal Grid ItemGrid=new Grid();internal int[] preferredItemTypes;internal int Length;}
class Equipment
{
 internal ItemValue[] Cells;internal int[] preferredItemSlots;
 internal int GetSlotCount()=>Cells.Length;internal ItemValue GetSlotItem(int slot)=>Cells[slot];
}
class EntityPlayerLocal{internal Bag bag=new Bag();internal Inventory inventory=new Inventory();internal Equipment equipment=new Equipment();}
static class RebirthCookingPartialMergeScope{internal static ItemValue Incoming;}
static class RebirthCookingItemStats{internal static bool Compatible(ItemValue left,ItemValue right)=>left.Metadata==right.Metadata;}
// PRODUCTION_PREDICTOR
class Cases
{
 static readonly ItemClass normal=new ItemClass();
 static ItemStack S(int type,int count,string metadata="",ItemClass definition=null)=>new ItemStack(new ItemValue{type=type,Metadata=metadata,ItemClass=definition??normal},count);
 static EntityPlayerLocal Actor(ItemStack[] bag,ItemStack[] belt,int[] preferred=null)=>new EntityPlayerLocal
 {bag=new Bag{ItemGrid=new Grid{items=bag}},inventory=new Inventory{ItemGrid=new Grid{items=belt},Length=belt.Length,preferredItemTypes=preferred??new int[belt.Length]},
 equipment=new Equipment{Cells=new[]{new ItemValue(),new ItemValue()},preferredItemSlots=new int[2]}};
 static void Check(bool value,string label){if(!value)throw new Exception(label);}
 static bool Plan(EntityPlayerLocal actor,ItemStack[] outputs,out ItemStack[] bag,out ItemStack[] belt)=>RebirthRecipeOutputInventoryPrediction.TryPlan(actor,outputs,out bag,out belt);
 static void Main()
 {
  var actor=Actor(new[]{S(1,8)},new[]{S(1,8)});
  Check(!Plan(actor,new[]{S(1,4)},out _,out _),"split partial capacity does not satisfy native AddItemNoPartial admission");
  actor=Actor(new[]{S(1,8),S(0,0)},new[]{S(1,8)});
  Check(Plan(actor,new[]{S(1,4)},out var bag,out var belt)&&bag[0].count==10&&bag[1].count==2&&belt[0].count==8,"bag partial then bag empty before belt");
  Check(actor.bag.ItemGrid.items[0].count==8&&actor.bag.ItemGrid.items[1].IsEmpty(),"prediction has no native writes");
  actor=Actor(new[]{S(2,10)},new[]{S(0,0),S(0,0)},new[]{0,1});
  Check(Plan(actor,new[]{S(1,2)},out bag,out belt)&&belt[0].IsEmpty()&&belt[1].count==2,"native preferred belt precedes first empty fallback");
  actor=Actor(new[]{S(0,0)},new[]{S(0,0)});
  Check(Plan(actor,new[]{S(1,10),S(2,10)},out bag,out belt)&&bag[0].itemValue.type==1&&belt[0].itemValue.type==2,"all outputs consume detached prior output capacity");
  Check(!Plan(actor,new[]{S(1,10),S(2,10),S(3,1)},out _,out _),"later output capacity refusal returns no partial plan");
  actor=Actor(new[]{S(1,8,"original")},new[]{S(2,10)});
  Check(!Plan(actor,new[]{S(1,1,"foreign")},out _,out _),"metadata-incompatible native stack cannot admit");
  var armor=new ItemClassArmor{AutoEquip=true,AllowUnEquip=true,EquipSlot=0,MaxCount=1};
  actor=Actor(new[]{S(0,0)},new[]{S(9,10)});actor.equipment.Cells[0]=S(8,1).itemValue;
  Check(Plan(actor,new[]{S(7,1,"rich",armor)},out bag,out belt)&&bag[0].itemValue.type==8&&actor.equipment.Cells[0].type==8,"auto-equip predicts old equipment return without native setter");
  actor=Actor(new[]{S(0,0)},new[]{S(0,0)});
  var denied=new ItemClass{Move=false};Check(!Plan(actor,new[]{S(1,1,"",denied)},out _,out _),"native destination restriction refuses");
  actor=Actor(new[]{S(9,10)},new[]{S(0,0),S(0,0)},new[]{0,1});actor.inventory.Length=1;
  Check(Plan(actor,new[]{S(1,1)},out bag,out belt)&&belt[0].count==1&&belt[1].IsEmpty(),"latent belt slots are never native destinations");
  var beforeBag=new[]{S(0,0)};var beforeBelt=new[]{S(9,10)};
  var beforeEquipment=Enumerable.Range(0,13).Select(_=>new ItemValue()).ToArray();beforeEquipment[0]=S(8,1,"old-rich").itemValue;
  var beltPreferences=new[]{9};var equipmentPreferences=new int[13];equipmentPreferences[0]=8;
  Check(RebirthRecipeOutputInventoryPrediction.TryPlan(beforeBag,beforeBelt,1,beltPreferences,beforeEquipment,equipmentPreferences,
   new[]{S(7,1,"new-rich",armor)},out bag,out belt,out var equipment)&&bag[0].itemValue.type==8&&bag[0].itemValue.Metadata=="old-rich"&&
   equipment[0].type==7&&equipment[0].Metadata=="new-rich","detached saved-owner inputs predict auto-equip and rich old armor return");
  Check(beforeBag[0].IsEmpty()&&beforeEquipment[0].type==8&&equipmentPreferences[0]==8&&beltPreferences[0]==9,"remote prediction preserves all original inputs");
  Check(!RebirthRecipeOutputInventoryPrediction.TryPlan(beforeBag,beforeBelt,1,new int[0],beforeEquipment,equipmentPreferences,
   new[]{S(7,1,"new-rich",armor)},out _,out _,out _),"missing preferred slots cannot select a remote insertion path");
  Console.WriteLine("PASS additional-output native inventory prediction: 13 scenarios");
 }
}
