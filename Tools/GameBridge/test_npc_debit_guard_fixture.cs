using System;
using System.Collections.Generic;
public struct TextureFullArray {public bool Changed;public bool IsDefault {get{return !Changed;}}}
public class ItemValue {public int type=1,Meta,Quality,Seed;public byte Flags,SelectedAmmoTypeIndex;public float UseTimes;public int[] Stats;public TextureFullArray TextureFullArray;public Dictionary<string,int> Metadata;public ItemValue[] modifications,cosmeticMods;public bool IsEmpty(){return type==0;}}
public class ItemStack {public ItemValue itemValue;public int count;public bool IsEmpty(){return count==0;}public ItemStack Clone(){return new ItemStack{itemValue=itemValue,count=count};}public static ItemStack Empty {get{return new ItemStack();}}}
public class Store {public ItemStack[] slots;public Store ItemGrid {get{return this;}}public ItemStack[] items {get{return slots;}}public int Writes;public ItemStack[] GetSlots(){return slots;}public void SetSlot(int i,ItemStack s){Writes++;slots[i]=s;}public void SetItem(int i,ItemStack s){SetSlot(i,s);}}
public class EntityPlayer {public Store bag=new Store(),inventory=new Store();}
public class DebitClaim {public bool Backpack;public int Slot,Quantity,ItemFingerprint;}
public class Service {
 public static object ReservationSync=new object();public static Dictionary<string,int> DebitSlotReservations=new Dictionary<string,int>();
 static string SlotKey(bool bag,int slot){return bag+":"+slot;}
 static int ItemFingerprint(ItemValue v){return 123;} // Intentionally unchanged for omitted flag fields.
// METHODS
}
public static class Checks {public static void Main(){
 for(int changed=0;changed<2;changed++){
 var p=new EntityPlayer();p.bag.slots=new[]{new ItemStack{itemValue=new ItemValue(),count=5},new ItemStack{itemValue=new ItemValue{Flags=(byte)changed},count=5}};p.inventory.slots=new ItemStack[0];
 var claims=new[]{new DebitClaim{Backpack=true,Slot=0,Quantity=2,ItemFingerprint=123},new DebitClaim{Backpack=true,Slot=1,Quantity=2,ItemFingerprint=123}};
 Service.DebitSlotReservations["True:0"]=2;Service.DebitSlotReservations["True:1"]=2;
 bool ok=new Service().RemoveExactClaims(p,1,4,claims);
 if(changed==0){if(!ok||p.bag.slots[0].count!=3||p.bag.slots[1].count!=3)throw new Exception("valid debit");}
 else if(ok||p.bag.Writes!=0||p.bag.slots[0].count!=5||p.bag.slots[1].count!=5)throw new Exception("partial debit after attribute change");
 }
 Console.WriteLine("PASS: production debit validates all claims before writes; changed flags reject despite identical stub fingerprint; plain items debit correctly");
}}
