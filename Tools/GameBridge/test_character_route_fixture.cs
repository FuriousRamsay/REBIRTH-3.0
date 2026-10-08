using System;
class XUiController {}
class EntityPlayerLocal {}
class GUIWindowManager {
 public bool Fail; public int Opens;
 public bool IsWindowOpen(string id){return false;}
 public void Close(string id){}
 public void Open(string id,bool modal){Opens++;if(Fail)throw new Exception("missing window");}
}
enum RebirthSurvivorOwnerCreationState { Ready, Pending }
class RebirthSurvivorOwnerHeader { public bool Available, RebirthModeEnabled, HasCharacter; public RebirthSurvivorOwnerCreationState CreationState; }
static class RebirthSurvivorClientState {
 public static RebirthSurvivorOwnerHeader State=new RebirthSurvivorOwnerHeader();
 public static RebirthSurvivorOwnerHeader GetOwnerHeader(){return State;}
}
static class XUiC_RebirthSurvivorCharacter {public const string WindowGroupId="rebirthCharacter";}
static class Log { public static void Warning(string text){} }
class Test {
 static int Native;
 static bool OpenNativePagingRoute(EntityPlayerLocal player,string route){Native++;throw new Exception("recursive native fallback");}
 // SOURCE
 static void Main(){
 var state=RebirthSurvivorClientState.State;state.Available=state.RebirthModeEnabled=state.HasCharacter=true;state.CreationState=RebirthSurvivorOwnerCreationState.Ready;
 var manager=new GUIWindowManager {Fail=true};
 if(OpenCharacter(new XUiController(),new EntityPlayerLocal(),manager)||Native!=0||manager.Opens!=1)throw new Exception("failure retried");
 manager.Fail=false;
 if(!OpenCharacter(new XUiController(),new EntityPlayerLocal(),manager)||Native!=0||manager.Opens!=2)throw new Exception("success route failed");
 Console.WriteLine("PASS: failed REBIRTH open does not recurse; successful open uses dedicated window");
 }
}
