using System;
class PackedBoolArray {bool[] data;public PackedBoolArray(int n){data=new bool[n];}public int Length{get{return data.Length;}set{Array.Resize(ref data,value);}}public bool this[int i]{get{return data[i];}set{data[i]=value;}}}
class Bag {public object[] Slots=new object[8];public PackedBoolArray LockedSlots;public object[] GetSlots(){return Slots;}}
class XUi {public Bag Bag=new Bag();}
class XUiC_ItemStack {public bool UserLockedSlot;}
class Check {
static Bag GetBag(XUi ui){return ui==null?null:ui.Bag;}
// METHODS
static void Main(){var ui=new XUi();var locks=GetLockedSlots(ui,2);if(locks.Length!=8||!object.ReferenceEquals(ui.Bag.LockedSlots,locks))throw new Exception("Actual capacity not stored");locks[7]=true;locks[1]=true;
PersistLockedSlots(ui,new[]{new XUiC_ItemStack{UserLockedSlot=true},null},2);if(!locks[7]||!locks[1]||!locks[0]||locks.Length!=8)throw new Exception("Partial view lost locks");
PersistLockedSlots(ui,new[]{new XUiC_ItemStack{UserLockedSlot=false}},100);if(locks[0]||!locks[7])throw new Exception("Wrong represented range");
ui.Bag.Slots=new object[10];GetLockedSlots(ui,1);if(locks.Length!=10||!locks[7])throw new Exception("Expansion loses state");
PersistLockedSlots(ui,new XUiC_ItemStack[12],12);if(!locks[7])throw new Exception("Null controllers clear locks");
ui.Bag.Slots=new object[4];GetLockedSlots(ui,100);if(locks.Length!=4)throw new Exception("Real bag shrink not respected");
Console.WriteLine("PASS: actual lock storage methods preserve unseen/null-controller slots, clamp writes to owned capacity, store new mask, preserve expansion and follow real bag shrink. Native bag/bitset substituted.");}}
