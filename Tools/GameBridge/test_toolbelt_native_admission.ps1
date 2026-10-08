$ErrorActionPreference='Stop'
$source=Get-Content -Raw (Join-Path $PSScriptRoot '../../Scripts/UI/RebirthToolbeltCapacity.cs')
function Body([string]$text,[string]$signature){
 $a=$text.IndexOf($signature);if($a -lt 0){throw "Missing actual source $signature"};$b=$text.IndexOf('{',$a);$depth=1;$i=$b+1
 while($depth -gt 0 -and $i -lt $text.Length){if($text[$i] -eq '{'){$depth++};if($text[$i] -eq '}'){$depth--};$i++}
 return $text.Substring($a,$i-$a)
}
$code=@'
using System;
public class EntityAlive {}
public class EntityPlayer:EntityAlive {public int Capacity=4;public Inventory inventory;}
public class ItemStack {public bool Note;}
public class Inventory {public EntityAlive entity;public int Length;}
public class XUiC_Toolbelt {public XUi xui;} public class XUi {public PlayerUI playerUI;} public class PlayerUI {public EntityPlayer entityPlayer;}
public class Hand {public EntityAlive entity;public Inventory toolbelt;}
static class RebirthToolbeltCapacity {
 public static bool Enabled=true;
 public const int BackingPublicSlots=20;
 public static EntityAlive ResolveOwner(Inventory i){return i.entity;}
 public static bool IsPlayerInventory(Inventory i){return i.entity is EntityPlayer;}
 public static bool IsRejectedIncoming(ItemStack s){return Enabled&&s!=null&&s.Note;}
 public static int GetSlotsForPlayer(EntityPlayer p){return p.Capacity;}
 // RANGE
 // LOCKED
}
'@
$code=$code.Replace('// RANGE',(Body $source '    public static int GetOwnedSlotCount(')).Replace('// LOCKED',(Body $source '    public static bool IsLockedPlayerSlot('))
foreach($name in @('RebirthToolbeltInventoryConstructorPatch','RebirthToolbeltCanMoveToSlotPatch','RebirthToolbeltAddItemAtSlotAuthorityPatch','RebirthToolbeltAddItemAuthorityPatch','RebirthToolbeltSelectedSlotPatch','RebirthToolbeltHandSelectPatch','RebirthToolbeltShortcutPatch','RebirthToolbeltFocusWrapPatch')){
 $class=Body $source ('public static class '+$name)
 if($name -eq 'RebirthToolbeltAddItemAuthorityPatch'){$start=$class.IndexOf('    public static MethodBase');$end=$class.IndexOf('    public static bool Prefix');$class=$class.Substring(0,$start)+$class.Substring($end)}
 $code+=$class
}
$code+=@'
public class Checks {
 static void Assert(bool value,string why){if(!value)throw new Exception(why);}
 public static void Run(){
  var p=new EntityPlayer();var i=new Inventory{entity=p,Length=20};var item=new ItemStack();
  int slots=10;RebirthToolbeltInventoryConstructorPatch.Prefix(p,ref slots);Assert(slots==20,"player capacity not allocated before bind");
  slots=24;RebirthToolbeltInventoryConstructorPatch.Prefix(p,ref slots);Assert(slots==24,"larger constructor capacity shrunk");
  slots=10;RebirthToolbeltInventoryConstructorPatch.Prefix(new EntityAlive(),ref slots);Assert(slots==10,"NPC expanded");
  foreach(int capacity in new[]{4,6,18}){
   p.Capacity=capacity;
   int start=0,count=20,output=-1;bool result=false;
   Assert(RebirthToolbeltAddItemAuthorityPatch.Prefix(i,item,ref start,ref count,ref output,ref result)&&count==capacity,"whole-grid add not bounded");
   start=capacity;count=20;RebirthToolbeltAddItemAuthorityPatch.Prefix(i,item,ref start,ref count,ref output,ref result);Assert(count==0,"locked-only add admitted");
   start=capacity-1;count=int.MaxValue;RebirthToolbeltAddItemAuthorityPatch.Prefix(i,item,ref start,ref count,ref output,ref result);Assert(count==1,"overflow or last owned slot dropped");
   result=true;Assert(!RebirthToolbeltAddItemAtSlotAuthorityPatch.Prefix(i,item,capacity,ref result)&&!result,"locked transfer reported success");
   result=true;Assert(!RebirthToolbeltCanMoveToSlotPatch.Prefix(i,item,capacity,ref result)&&!result,"locked destination accepted");
   result=false;Assert(RebirthToolbeltAddItemAtSlotAuthorityPatch.Prefix(i,item,capacity-1,ref result),"last owned slot refused");
   int selected=capacity;RebirthToolbeltSelectedSlotPatch.Prefix(i,ref selected);Assert(selected==0,"restored locked selection retained");
   var hand=new Hand{entity=p,toolbelt=i};Assert(!RebirthToolbeltHandSelectPatch.Prefix(hand,capacity),"locked hand action allowed");Assert(RebirthToolbeltHandSelectPatch.Prefix(hand,capacity-1),"last owned hand action refused");
  }
  int a=0,b=20,c=9;bool success=true;Assert(!RebirthToolbeltAddItemAuthorityPatch.Prefix(i,new ItemStack{Note=true},ref a,ref b,ref c,ref success)&&!success&&c==-1,"rejected note falsely transferred");
  p.inventory=i;p.Capacity=4;var ui=new XUiC_Toolbelt{xui=new XUi{playerUI=new PlayerUI{entityPlayer=p}}};int focus=-1;
  Assert(!RebirthToolbeltFocusWrapPatch.Prefix(ui,3,1,ref focus)&&focus==0,"forward focus entered locked slot");
  Assert(!RebirthToolbeltFocusWrapPatch.Prefix(ui,0,-1,ref focus)&&focus==3,"negative focus wrap failed");
  int shortcut=-1;RebirthToolbeltShortcutPatch.Postfix(7,true,ref shortcut);Assert(shortcut==17,"shift bank wrong");
  RebirthToolbeltShortcutPatch.Postfix(7,false,ref shortcut);Assert(shortcut==7,"base bank wrong");
  RebirthToolbeltCapacity.Enabled=false;shortcut=42;RebirthToolbeltShortcutPatch.Postfix(7,true,ref shortcut);Assert(shortcut==42,"native shortcut changed");
  Assert(RebirthToolbeltFocusWrapPatch.Prefix(ui,3,1,ref focus),"native focus intercepted");
  RebirthToolbeltCapacity.Enabled=false;slots=10;RebirthToolbeltInventoryConstructorPatch.Prefix(p,ref slots);Assert(slots==10,"native construction changed");
  a=0;b=20;c=-1;success=false;Assert(RebirthToolbeltAddItemAuthorityPatch.Prefix(i,item,ref a,ref b,ref c,ref success)&&b==20,"native addition changed");
  success=false;Assert(RebirthToolbeltAddItemAtSlotAuthorityPatch.Prefix(i,item,19,ref success),"native explicit slot blocked");
  Console.WriteLine("PASS actual toolbelt constructor/admission/selection prefixes: Rebirth20 before binding, NPC/native untouched,4/6/18 usable, last owned slot retained, locked ranges/overflow refused before transfer, note false-success prevented; native operations/capacity provider doubled.");
 }
}
'@
Add-Type -TypeDefinition $code
[Checks]::Run()
