using System;
class ItemStack {public int count;public bool IsEmpty(){return count<=0;}}
class Bag {public ItemStack[] Slots;public int Writes;public ItemStack[] GetSlots(){return Slots;}public void SetSlot(int i,ItemStack s){Slots[i]=s;Writes++;}}
class EntityPlayer {public Bag bag;}
class Check {
// METHODS
 static void Main(){
  var source=new ItemStack{count=7};
  if(AddToBackpack(null,source))throw new Exception("Missing player falsely accepted");
  var p=new EntityPlayer();if(AddToBackpack(p,source))throw new Exception("Missing bag falsely accepted");
  p.bag=new Bag();if(AddToBackpack(p,source))throw new Exception("Missing slots falsely accepted");
  p.bag.Slots=new[]{new ItemStack{count=2}};if(AddToBackpack(p,source)||p.bag.Writes!=0||source.count!=7)throw new Exception("Full bag altered");
  p.bag.Slots=new ItemStack[1];if(!AddToBackpack(p,source)||p.bag.Slots[0]!=source||p.bag.Writes!=1||source.count!=7)throw new Exception("Empty slot delivery");
  if(!AddToBackpack(null,null)||!AddToBackpack(null,new ItemStack()))throw new Exception("Empty no-op");
  Console.WriteLine("PASS: actual recovery destination helper rejects missing/full destinations and preserves full stack on accepted placement. Native bag/drop not exercised.");
 }
}
