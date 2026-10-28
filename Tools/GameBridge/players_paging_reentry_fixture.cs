using System;
class XUiC_WindowSelector { public string Selected="crafting"; public void SetSelected(string name){Selected=name;} }
class XUiController { public XUiC_WindowSelector Selector; public T GetChildByType<T>() where T:class{return Selector as T;} }
class EntityPlayerLocal {}
class LocalPlayerUI {public EntityPlayerLocal entityPlayer=new EntityPlayerLocal();}
class XUi {public LocalPlayerUI playerUI=new LocalPlayerUI();public XUiController Group=new XUiController{Selector=new XUiC_WindowSelector()};public XUiController FindWindowGroupByName(string id){return Group;} }
class XUiC_PlayersList {
 public XUi xui=new XUi();public bool HostOpen,ReenteredCrafting;public int NativeOpens;
 public virtual void OnOpen(){NativeOpens++;if(!HostOpen){HostOpen=true;ReenteredCrafting=xui.Group.Selector.Selected=="crafting";}xui.Group.Selector.SetSelected("players");}
}
static class RebirthPartyIdentityClientState {public static event Action Changed;}
static class RebirthPartyIdentityService {public static int Requests;public static void RequestSnapshot(EntityPlayerLocal p,bool force,string reason){Requests++;}}
class ActualPlayers : XUiC_PlayersList {
 public int Rebinds; void OnGroupIdentityChanged(){} void RebindSelection(bool selectFirstIfMissing){if(selectFirstIfMissing)Rebinds++;}
 // ACTUAL_ON_OPEN
}
public static class PlayersPagingFixture {
 public static string Run(){
  var p=new ActualPlayers();p.OnOpen();if(p.ReenteredCrafting||p.NativeOpens!=1||p.Rebinds!=1||RebirthPartyIdentityService.Requests!=1)throw new Exception("closed host reopened remembered crafting or skipped native/player initialization");
  p.HostOpen=false;p.xui.Group.Selector.Selected="crafting";p.OnOpen();if(p.ReenteredCrafting||p.NativeOpens!=2||p.Rebinds!=2)throw new Exception("reopen lost ordering");
  var already=new ActualPlayers{HostOpen=true};already.xui.Group.Selector.Selected="players";already.OnOpen();if(already.ReenteredCrafting||already.NativeOpens!=1)throw new Exception("already-selected native route changed");
  var native=new XUiC_PlayersList();native.OnOpen();if(!native.ReenteredCrafting)throw new Exception("adapter did not expose original native ordering failure");
  return "PASS actual Players.OnOpen: initial/reopened/already-open paging, original native ordering counterexample; explicit UI/native adapters, gameplay NOT RUN";
 }
}
