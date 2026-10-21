using System;
namespace HarmonyLib { [AttributeUsage(AttributeTargets.Class)] public class HarmonyPatch:Attribute { public HarmonyPatch(Type t,string m){} } }
public class EntityPlayer {}
public class EntityPlayerLocal:EntityPlayer {}
public class MinEventActionPinToolbeltMessage { public string messageKey; public void Execute(){} }
public static class RebirthToolbeltCapacity { public static bool Enabled; }
public static class GameManager { public static int Removed; public static string Key; public static void RemovePinnedTooltip(EntityPlayerLocal p,string key){Removed++;Key=key;} }
class Program
{
 static int checks;
 static void Check(bool value,string name){checks++;if(!value)throw new Exception(name);}
 static void Main()
 {
  foreach(bool rebirth in new[]{false,true})
  {
   RebirthToolbeltCapacity.Enabled=rebirth;
   foreach(string key in new[]{"buffDukeNoteToolTip","buffStatusHungry03Tooltip","buffStatusThirsty03Tooltip","BUFFDUKENOTETOOLTIP","",null})
   {
    bool expected=rebirth&&key=="buffDukeNoteToolTip";
    Check(RebirthDukeNoteNotification.ShouldSuppress(key)==expected,"exact mode/key policy");
    Check(RebirthDukeNotePinPatch.Prefix(new MinEventActionPinToolbeltMessage{messageKey=key})==!expected,"native action gate");
   }
   GameManager.Removed=0;
   RebirthDukeNoteNotification.Clear(null);RebirthDukeNoteNotification.Clear(new EntityPlayer());
   Check(GameManager.Removed==0,"no remote or null UI calls");
   RebirthDukeNoteNotification.Clear(new EntityPlayerLocal());
   Check(GameManager.Removed==(rebirth?1:0),"orphan pin cleared only local REBIRTH");
   if(rebirth)Check(GameManager.Key=="buffDukeNoteToolTip","exact removal key");
  }
  Console.WriteLine("PASS "+checks+" actual-source notification policy assertions");
 }
}
