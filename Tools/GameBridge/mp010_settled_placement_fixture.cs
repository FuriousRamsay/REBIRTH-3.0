using System;using System.Collections.Generic;
class ItemStack{}
class EntityPlayerLocal{internal bool Remote;}
static class RebirthSurvivorUiText{internal static string L(string key,string text)=>text;}
static class RebirthCookingGridCustody
{internal static bool Accept;internal static int Calls;internal static object Seen;internal static bool Pull(object workspace,EntityPlayerLocal player,List<ItemStack> needs,ItemStack[] ghosts){Calls++;Seen=needs;return Accept;}}
class Workspace
{
 ItemStack[] ghosts=new ItemStack[12];string status="unchanged";int renders;
 void Render(){renders++;}
 // ACTUAL_SETTLED_PLACEMENT
 static void Need(bool value,string reason){if(!value)throw new Exception(reason);}
 static void Main(){foreach(bool remote in new[]{false,true})foreach(bool accept in new[]{false,true}){var w=new Workspace();var needs=new List<ItemStack>{new ItemStack()};RebirthCookingGridCustody.Calls=0;RebirthCookingGridCustody.Accept=accept;w.PlaceSettledPulledIngredients(new EntityPlayerLocal{Remote=remote},needs);Need(RebirthCookingGridCustody.Calls==1&&ReferenceEquals(needs,RebirthCookingGridCustody.Seen),"same original admission in both modes");Need(w.renders==(accept?0:1)&&w.status==(accept?"unchanged":"Not enough usable ingredients. Drink containers must be full."),"refusal preserves owner custody; accepted original owns projection");}Console.WriteLine("MP010 settled placement: 8 assertions; actual caller, no direct inventory/debit adapter supplied.");}
}
