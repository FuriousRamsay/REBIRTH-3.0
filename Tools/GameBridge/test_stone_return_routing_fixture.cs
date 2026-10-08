using System;
using HarmonyLib;
namespace HarmonyLib { public class HarmonyPrefix:Attribute{} }
public class ItemClass {public string Name;public string GetItemName()=>Name;}
public class ItemValue {public ItemClass ItemClass;}
public class ItemStack {public ItemValue itemValue; public int count;}
public class Inventory {public EntityPlayerLocal Owner;}
public class Backpack {public bool Room;public bool CanTakeItem(ItemStack s)=>Room;}
public class XUiM_PlayerInventory {public Inventory Toolbelt; public Backpack Backpack=new Backpack();}
public class XUi {public XUiM_PlayerInventory PlayerInventory;}
public class PlayerUI {public XUi xui;}
public class EntityPlayerLocal {public PlayerUI PlayerUI;}
public static class RebirthToolbeltCapacity {public static object ResolveOwner(Inventory i)=>i.Owner;}
public static class RebirthSurvivorMode {public static bool Enabled=true;public static bool IsEnabledForCurrentWorld()=>Enabled;}
// PRODUCTION_CLASS
public static class Check {
 static void A(bool ok,string name){if(!ok)throw new Exception(name);}
 public static void Main(){
 var belt=new Inventory();var bag=new Backpack{Room=true};var ui=new XUiM_PlayerInventory{Toolbelt=belt,Backpack=bag};belt.Owner=new EntityPlayerLocal{PlayerUI=new PlayerUI{xui=new XUi{PlayerInventory=ui}}};
 var stone=new ItemStack{count=3,itemValue=new ItemValue{ItemClass=new ItemClass{Name="resourceRockSmall"}}};bool result=true;
 A(!RebirthLooseStoneReturnRoutingPatch.Prefix(belt,stone,ref result)&&!result&&stone.count==3,"stone defers preferred hotbar without debit");
 bag.Room=false;A(RebirthLooseStoneReturnRoutingPatch.Prefix(belt,stone,ref result),"full backpack native fallback");bag.Room=true;
 stone.itemValue.ItemClass.Name="resourceWood";A(RebirthLooseStoneReturnRoutingPatch.Prefix(belt,stone,ref result),"other items unchanged");stone.itemValue.ItemClass.Name="resourceRockSmall";
 RebirthSurvivorMode.Enabled=false;A(RebirthLooseStoneReturnRoutingPatch.Prefix(belt,stone,ref result),"modeoff unchanged");RebirthSurvivorMode.Enabled=true;
 var other=new Inventory{Owner=belt.Owner};A(RebirthLooseStoneReturnRoutingPatch.Prefix(other,stone,ref result),"not player's toolbelt unchanged");
 A(RebirthLooseStoneReturnRoutingPatch.Prefix(new Inventory(),stone,ref result),"NPC/unknown owner unchanged");
 A(RebirthLooseStoneReturnRoutingPatch.Prefix(belt,null,ref result),"null unchanged");
 Console.WriteLine("PASS actual stone-return prefix: resource/local/mode/capacity constraints and no source debit");
 }
}
