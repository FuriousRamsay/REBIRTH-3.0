$ErrorActionPreference='Stop'
$s=Get-Content -Raw (Join-Path $PSScriptRoot '../../Scripts/Survivor/Support/RebirthBackpackLibraryCursorGuard.cs')
$a=$s.IndexOf('    public static bool Prefix(Inventory __instance,ItemStack _itemStack,int _startSlot')
$b=$s.IndexOf('[HarmonyPatch(typeof(Inventory),nameof(Inventory.TryStackItem))]',$a)
$body=$s.Substring($a,$b-$a).Trim()
Add-Type -TypeDefinition (@'
using System;
public class EntityPlayer {public bool bPlayerStatsChanged,isEntityRemote;}
public class ItemValue {public int type;}
public class ItemStack {public ItemValue itemValue=new ItemValue();public int count;public bool Note;public bool IsEmpty(){return count==0;}public bool CanMoveTo(object o){return true;}public bool CanStackWith(ItemStack s){return count>0&&itemValue.type==s.itemValue.type&&count+s.count<=10;}}
public static class XUiC_ItemStack {public static class StackLocationTypes {public static object ToolBelt;}}
public class Inventory {public EntityPlayer entity=new EntityPlayer();public int Length=20;public ItemStack[] cells=new ItemStack[20];public Inventory(){for(int i=0;i<20;i++)cells[i]=new ItemStack();}public ItemStack GetStackAt(int n){return cells[n];}public bool CanMoveToSlot(ItemStack s,int n){return !s.Note&&n<4;}public void SetItem(int n,ItemValue v,int c){cells[n].itemValue=v;cells[n].count=c;}public void CallOnToolbeltChangedInternal(){}}
public static class RebirthBackpackLibraryReservation {public static bool Held=true;public static int Reserved=1;public static bool TryGetReservedSlot(object o,bool b,out int n){n=Reserved;return Held;}}
public static class RebirthToolbeltCapacity {public static int GetOwnedSlotCount(EntityPlayer p,int n){return Math.Min(4,n);}}
public static class Actual {
'@ + $body + @'
public class Checks {
static void A(bool c,string n){if(!c)throw new Exception(n);}
public static void Run(){
 var i=new Inventory();var s=new ItemStack{count=2,itemValue=new ItemValue{type=9}};int slot=8;bool result=true;
 A(!Actual.Prefix(i,s,1,1,ref slot,ref result)&&!result&&slot==-1&&i.cells[1].count==0,"reserved direct range mutated");
 A(!Actual.Prefix(i,s,2,1,ref slot,ref result)&&result&&slot==2&&i.cells[0].count==0,"range ignored");
 i=new Inventory();A(!Actual.Prefix(i,s,4,int.MaxValue,ref slot,ref result)&&!result&&slot==-1,"locked or overflow admitted");
 i=new Inventory();s.Note=true;A(!Actual.Prefix(i,s,0,20,ref slot,ref result)&&!result&&i.cells[0].count==0,"rejected item falsely accepted");
 s.Note=false;i.cells[1]=new ItemStack{count=3,itemValue=s.itemValue};i.cells[3]=new ItemStack{count=3,itemValue=s.itemValue};
 A(!Actual.Prefix(i,s,0,20,ref slot,ref result)&&result&&slot==3&&i.cells[1].count==3&&i.cells[3].count==5&&s.count==2,"merge custody/remainder changed");
 RebirthBackpackLibraryReservation.Held=false;A(Actual.Prefix(i,s,1,1,ref slot,ref result),"unreserved native path intercepted");
 Console.WriteLine("PASS actual ranged belt reservation: direct UI range protected, requested bounds preserved, reserved/locked/overflow/rejected cells excluded, merge custody and native unreserved path preserved; native adapters doubled.");
}}
'@)
[Checks]::Run()
