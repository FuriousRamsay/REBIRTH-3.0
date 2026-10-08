using System;
public static class Mathf {public static int Clamp(int v,int min,int max)=>Math.Clamp(v,min,max);}
public class ItemStack {public int count;public static ItemStack Empty=new();public bool IsEmpty()=>count==0;public ItemStack Clone()=>new(){count=count};public static ItemStack[] CreateArray(int n){var a=new ItemStack[n];for(int i=0;i<n;i++)a[i]=new();return a;}}
public class PackedBoolArray {public int Length;}
public class Grid {public ItemStack[] items=ItemStack.CreateArray(52);}
public class Bag {public Grid ItemGrid=new();public PackedBoolArray LockedSlots=new(){Length=52};public bool Refuse;public int Calls;public void SetSlots(ItemStack[] a){Calls++;if(!Refuse)ItemGrid.items=a;}}
public class EntityPlayer {public int entityId=7;public Bag bag=new();}public class EntityPlayerLocal:EntityPlayer {}
public static class RebirthGearOwnerReservation {public static bool Held;public static bool IsHeld(EntityPlayerLocal p)=>Held;}
public static class RebirthBackpackLibraryReservation {public static bool Held;public static bool IsHeld(EntityPlayerLocal p)=>Held;}
public static class RebirthLogSettings {public static bool AutomaticLoggingDefault=false;}public static class Log{public static void Out(string s){}}
static class Program {
 static int passed;static void Check(bool v,string s){if(!v)throw new Exception(s);passed++;}
 static void Main(){
 var p=new EntityPlayerLocal();RebirthGearOwnerReservation.Held=true;
 Check(!RebirthSurvivorGearService.ReconcilePhysicalBagCapacity(p,62,false)&&p.bag.Calls==0&&p.bag.LockedSlots.Length==52&&p.bag.ItemGrid.items.Length==52,"gear custody preserves backing and locks");
 Check(RebirthSurvivorGearService.ReconcilePhysicalBagCapacity(p,52,false)&&p.bag.Calls==0,"already correct backing needs no custody mutation");
 RebirthGearOwnerReservation.Held=false;RebirthBackpackLibraryReservation.Held=true;
 Check(!RebirthSurvivorGearService.ReconcilePhysicalBagCapacity(p,62,false)&&p.bag.Calls==0&&p.bag.LockedSlots.Length==52,"library custody preserves backing and locks");
 RebirthBackpackLibraryReservation.Held=false;p.bag.Refuse=true;
 Check(!RebirthSurvivorGearService.ReconcilePhysicalBagCapacity(p,62,false)&&p.bag.LockedSlots.Length==52,"silent native setter refusal cannot resize locks or claim success");
 p.bag.Refuse=false;p.bag.ItemGrid.items[3].count=2;
 Check(RebirthSurvivorGearService.ReconcilePhysicalBagCapacity(p,62,false)&&p.bag.LockedSlots.Length==62&&p.bag.ItemGrid.items[3].count==2,"ordinary resize preserves contents and aligns locks");
 p.bag.ItemGrid.items[61].count=1;
 Check(!RebirthSurvivorGearService.ReconcilePhysicalBagCapacity(p,52,false)&&p.bag.LockedSlots.Length==62,"occupied tail prevents downgrade");
 Console.WriteLine($"PASS {passed} actual extracted capacity method checks; native inventory and reservation boundaries doubled");
 }
}