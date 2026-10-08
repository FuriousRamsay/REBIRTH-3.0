param([switch]$Run)
$ErrorActionPreference='Stop'
$root=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
function ExtractMethod([string]$source,[string]$signature){
 $start=$source.IndexOf($signature,[StringComparison]::Ordinal)
 if($start -lt 0){throw "Missing actual method: $signature"}
 $brace=$source.IndexOf('{',$start);$depth=1;$end=$brace+1
 while($depth -gt 0 -and $end -lt $source.Length){if($source[$end] -eq '{'){$depth++};if($source[$end] -eq '}'){$depth--};$end++}
 if($depth -ne 0){throw 'Unbalanced actual source'}
 return $source.Substring($start,$end-$start).Replace('private static','public static')
}
$installer=Get-Content (Join-Path $root 'Scripts/Survivor/UI/RebirthSurvivorUiInstaller.cs') -Raw
$navigation=Get-Content (Join-Path $root 'Scripts/Crafting/UI/PersonalCrafting/RebirthCraftingNavigationService.cs') -Raw
$helper=Get-Content (Join-Path $root 'Scripts/Survivor/UI/RebirthProgressionWindowRouting.cs') -Raw
$shortcut=ExtractMethod $installer '    private static bool Prefix(XUiC_WindowSelector __instance, string _selectedPage)'
$selected=ExtractMethod $installer '    private static bool Prefix(XUiC_WindowSelector __instance)'
$character=ExtractMethod $navigation '    private static bool OpenCharacter('
$skills=ExtractMethod $navigation '    private static bool OpenSkills('
if(!$Run){Write-Output 'Prepared ACTUAL helper + two Prefix + OpenCharacter/OpenSkills fixture. NOT executed. Expected hardening failures remain before integration. -Run requires isolated-compilation authorization/fresh HAV3N check.';exit 0}
$adapters=@"
using System;
using System.Collections.Generic;
public enum RebirthPlayerProgressionMode{Native,Rebirth}
public enum RebirthSurvivorOwnerCreationState{Pending,Ready}
public static class RebirthSurvivorMode{public static RebirthPlayerProgressionMode ConfiguredMode;}
public struct RebirthSurvivorOwnerHeader : IEquatable<RebirthSurvivorOwnerHeader>{public bool Available,RebirthModeEnabled,HasCharacter;public int Generation;public RebirthSurvivorOwnerCreationState CreationState;public bool Equals(RebirthSurvivorOwnerHeader h){return Available==h.Available&&RebirthModeEnabled==h.RebirthModeEnabled&&HasCharacter==h.HasCharacter&&CreationState==h.CreationState&&Generation==h.Generation;}}
public class Snapshot{public string CreationId;public bool DefinitionsCompatible=true;}
public static class RebirthSurvivorClientState{public static RebirthSurvivorOwnerHeader Header;public static Snapshot State;public static int Reads;public static Action OnRead;public static RebirthSurvivorOwnerHeader GetOwnerHeader(){Reads++;var action=OnRead;OnRead=null;if(action!=null)action();return Header;}public static Snapshot GetOwnerStateSnapshot(out RebirthSurvivorOwnerHeader header){header=GetOwnerHeader();return State==null?null:new Snapshot{CreationId=State.CreationId,DefinitionsCompatible=State.DefinitionsCompatible};}}
public class GUIWindowManager{public HashSet<string> Opened=new HashSet<string>();public int Opens,Closes;public bool Fail;public Action OnClose,OnOpen,OnIsOpen;public bool IsWindowOpen(string id){var action=OnIsOpen;OnIsOpen=null;if(action!=null)action();return Opened.Contains(id);}public void Open(string id,bool modal){Opens++;if(Fail)throw new Exception("missing window");Opened.Add(id);var action=OnOpen;OnOpen=null;if(action!=null)action();}public void Close(string id){Closes++;Opened.Remove(id);var action=OnClose;OnClose=null;if(action!=null)action();}}
public class XUiController{public XUi xui;public XUiC_RebirthSurvivorCharacter Character;public T GetChildByType<T>() where T:class{return Character as T;}}
public class XUi{public UI playerUI;public XUiController Group;public XUiController FindWindowGroupByName(string id){return Group;}}
public class UI{public XUi xui;public EntityPlayerLocal entityPlayer;public GUIWindowManager windowManager=new GUIWindowManager();public Inputs playerInput=new Inputs();}
public class Inputs{public Permanent PermanentActions=new Permanent();}public class Permanent{public InputAction Character=new InputAction();}public class InputAction{public bool Pressed;public Action OnRead;public bool WasPressed{get{var action=OnRead;OnRead=null;if(action!=null)action();return Pressed;}set{Pressed=value;}}}
public class EntityPlayerLocal{public World world;public int entityId;public UI PlayerUI;public bool Dead,Spawned=true;public bool IsDead(){return Dead;}public bool IsSpawned(){return Spawned;}}
public class WorldState{public string Guid="world-a";}
public class World{public WorldState worldState=new WorldState();public EntityPlayerLocal Primary,Entity;public Action OnGetEntity;public EntityPlayerLocal GetPrimaryPlayer(){return Primary;}public EntityPlayerLocal GetEntity(int id){var action=OnGetEntity;OnGetEntity=null;if(action!=null)action();return Entity;}}
public class GameManager{public static GameManager Instance;public World World;}
public class View{public bool IsVisible=true,Enabled=true;}
public class Button{public View ViewComponent=new View();}
public class XUiC_WindowSelector:XUiController{public string Name;public int NameReads;public Action OnNameRead;public string SelectedName{get{NameReads++;if(OnNameRead!=null)OnNameRead();return Name;}set{Name=value;}}public Button SelectedButton=new Button();}
public static class PlayerInputManager{public enum InputStyle{Keyboard,Controller}}
public class PlatformInput{public PlayerInputManager.InputStyle Style;public Action OnRead;public PlayerInputManager.InputStyle CurrentInputStyle{get{var action=OnRead;OnRead=null;if(action!=null)action();return Style;}set{Style=value;}}}public class Native{public PlatformInput Input=new PlatformInput();}
public static class PlatformManager{public static Native NativePlatform=new Native();}
public static class RebirthConsoleInputGuardRuntime{public static bool Blocked;public static Action OnCheck;public static bool BlocksGameplayInput(){var action=OnCheck;OnCheck=null;if(action!=null)action();return Blocked;}}
public class XUiC_RebirthSurvivorCharacter:XUiController{public const string WindowGroupId="rebirthCharacter";public int Skills;public void ShowSkillsPage(){Skills++;}}
public static class Log{public static void Warning(string s){}}
public static class RebirthCharacterCreationHoldService{public static bool Held;public static bool IsHeld(EntityPlayerLocal player){return Held;}}
public static class RebirthCraftingNavigationService{
 public enum Destination{Character,Skills,Journal,PersonalCrafting}
 public static int Calls,Native;public static Destination Last;public static bool Navigate(XUiController source,Destination destination){Calls++;Last=destination;return true;}
 public static bool OpenNativePagingRoute(EntityPlayerLocal player,string route){Native++;return true;}
 // CHARACTER
 // SKILLS
}
public static class Shortcut{
 // SHORTCUT
}
public static class Selected{
 // SELECTED
}
public static class RouteFixture{
 static int checks;static XUiC_WindowSelector source;static World world;static EntityPlayerLocal player;static GUIWindowManager manager;
 static void Check(bool condition,string message){if(!condition)throw new Exception(message);checks++;}
 static void Reset(){RebirthSurvivorMode.ConfiguredMode=RebirthPlayerProgressionMode.Rebirth;RebirthSurvivorClientState.Reads=0;RebirthSurvivorClientState.OnRead=null;RebirthSurvivorClientState.Header=new RebirthSurvivorOwnerHeader{Available=true,RebirthModeEnabled=true,HasCharacter=true,CreationState=RebirthSurvivorOwnerCreationState.Ready};RebirthSurvivorClientState.State=new Snapshot{CreationId="creation-a"};world=new World();player=new EntityPlayerLocal{world=world,entityId=1};var ui=new XUi();var owner=new UI{xui=ui,entityPlayer=player};ui.playerUI=owner;player.PlayerUI=owner;world.Primary=world.Entity=player;GameManager.Instance=new GameManager{World=world};source=new XUiC_WindowSelector{xui=ui,SelectedName="character"};ui.Group=new XUiController{Character=new XUiC_RebirthSurvivorCharacter()};manager=owner.windowManager;RebirthCraftingNavigationService.Calls=RebirthCraftingNavigationService.Native=0;RebirthConsoleInputGuardRuntime.Blocked=false;RebirthConsoleInputGuardRuntime.OnCheck=null;PlatformManager.NativePlatform.Input.OnRead=null;PlatformManager.NativePlatform.Input.CurrentInputStyle=PlayerInputManager.InputStyle.Keyboard;RebirthCharacterCreationHoldService.Held=false;}
 static void Blocked(string name){Check(!Shortcut.Prefix(source,"character"),name+" shortcut suppressed");Check(RebirthCraftingNavigationService.Calls==0&&manager.Opens==0&&manager.Closes==0,name+" no UI mutation");Check(!Selected.Prefix(source),name+" selected suppressed");Check(!RebirthCraftingNavigationService.OpenCharacter(source,player,manager),name+" direct character refused");Check(!RebirthCraftingNavigationService.OpenSkills(source,player,manager),name+" direct skills refused");Check(manager.Opens==0&&RebirthCraftingNavigationService.Native==0,name+" no fallback/mutation");}
 public static int Run(){
  Reset();RebirthSurvivorMode.ConfiguredMode=RebirthPlayerProgressionMode.Native;Check(Shortcut.Prefix(source,"character"),"disabled shortcut native");Check(Selected.Prefix(source),"disabled selected native");Check(RebirthCraftingNavigationService.OpenCharacter(source,player,manager)&&manager.Opens==0,"disabled direct character native");Check(RebirthCraftingNavigationService.OpenSkills(source,player,manager)&&manager.Opens==0,"disabled direct skills native");
  Reset();source.xui.playerUI.playerInput.PermanentActions.Character.WasPressed=true;Check(!Shortcut.Prefix(source,"character")&&RebirthCraftingNavigationService.Last==RebirthCraftingNavigationService.Destination.PersonalCrafting,"B recipes exception");
  Reset();PlatformManager.NativePlatform.Input.CurrentInputStyle=PlayerInputManager.InputStyle.Controller;source.xui.playerUI.playerInput.PermanentActions.Character.WasPressed=true;Check(!Shortcut.Prefix(source,"character")&&RebirthCraftingNavigationService.Last==RebirthCraftingNavigationService.Destination.Character,"radial character");
  Reset();Check(!Shortcut.Prefix(source,"skills")&&RebirthCraftingNavigationService.Last==RebirthCraftingNavigationService.Destination.Skills,"N/skills route");
  Reset();source.SelectedName="skills";Check(!Selected.Prefix(source)&&RebirthCraftingNavigationService.Last==RebirthCraftingNavigationService.Destination.Skills,"selected skills");
  Reset();Check(RebirthCraftingNavigationService.OpenCharacter(source,player,manager)&&manager.Opened.Contains(XUiC_RebirthSurvivorCharacter.WindowGroupId),"direct Character tab");
  Reset();Check(RebirthCraftingNavigationService.OpenSkills(source,player,manager)&&source.xui.Group.Character.Skills==1,"direct Skills tab");
  Reset();manager.Fail=true;Check(!RebirthCraftingNavigationService.OpenCharacter(source,player,manager)&&RebirthCraftingNavigationService.Native==0&&manager.Opens==1,"failed character no recursive fallback");
  Reset();RebirthConsoleInputGuardRuntime.Blocked=true;Check(!Shortcut.Prefix(source,"character")&&!Selected.Prefix(source)&&RebirthCraftingNavigationService.Calls==0,"console blocks prefixes");
  Reset();source.SelectedButton.ViewComponent.Enabled=false;Check(Selected.Prefix(source)&&RebirthCraftingNavigationService.Calls==0,"disabled selected preserves native category advance");
  Reset();source.SelectedButton.ViewComponent.IsVisible=false;Check(Selected.Prefix(source)&&RebirthCraftingNavigationService.Calls==0,"hidden selected preserves native category advance");
  Reset();player.Dead=true;Blocked("death");Reset();player.Spawned=false;Blocked("spawn pending");Reset();world.Primary=new EntityPlayerLocal();Blocked("foreign owner");Reset();world.Entity=new EntityPlayerLocal();Blocked("retired entity");Reset();player.world=new World();Blocked("retired world");Reset();player.PlayerUI=new UI{xui=new XUi()};Blocked("foreign xui");
  Reset();world.OnGetEntity=()=>GameManager.Instance=new GameManager{World=world};Check(RebirthProgressionWindowRouting.Resolve(source)==RebirthProgressionWindowRouting.Admission.Blocked,"manager replaced during native getter");
  Reset();world.OnGetEntity=()=>GameManager.Instance.World=new World();Check(RebirthProgressionWindowRouting.Resolve(source)==RebirthProgressionWindowRouting.Admission.Blocked,"world replaced during native getter");
  Reset();world.OnGetEntity=()=>world.worldState=new WorldState();Check(RebirthProgressionWindowRouting.Resolve(source)==RebirthProgressionWindowRouting.Admission.Blocked,"worldState replaced during native getter");
  Reset();world.OnGetEntity=()=>world.worldState.Guid="world-b";Check(RebirthProgressionWindowRouting.Resolve(source)==RebirthProgressionWindowRouting.Admission.Blocked,"world Guid changed during native getter");
  Reset();world.OnGetEntity=()=>RebirthSurvivorClientState.State.CreationId="creation-b";Check(RebirthProgressionWindowRouting.Resolve(source)==RebirthProgressionWindowRouting.Admission.Blocked,"creation changed without generation bump");
  Reset();world.OnGetEntity=()=>{var h=RebirthSurvivorClientState.Header;h.Generation++;RebirthSurvivorClientState.Header=h;};Check(RebirthProgressionWindowRouting.Resolve(source)==RebirthProgressionWindowRouting.Admission.Blocked,"header generation changed");
  Reset();RebirthCharacterCreationHoldService.Held=true;Blocked("creation held");
  Reset();Check(!RebirthCraftingNavigationService.OpenCharacter(source,new EntityPlayerLocal(),manager)&&!RebirthCraftingNavigationService.OpenSkills(source,new EntityPlayerLocal(),manager)&&manager.Opens==0,"direct foreign argument");
  Reset();var foreignManager=new GUIWindowManager();Check(!RebirthCraftingNavigationService.OpenCharacter(source,player,foreignManager)&&!RebirthCraftingNavigationService.OpenSkills(source,player,foreignManager)&&foreignManager.Opens==0,"direct foreign manager");
  Reset();world.OnGetEntity=()=>source.xui.playerUI.windowManager=new GUIWindowManager();Check(RebirthProgressionWindowRouting.Resolve(source)==RebirthProgressionWindowRouting.Admission.Blocked,"window manager changed during getter");
  Reset();manager.Opened.Add("crafting");manager.Opened.Add("windowpaging");manager.OnClose=()=>RebirthSurvivorClientState.State.CreationId="creation-b";Check(!RebirthCraftingNavigationService.OpenCharacter(source,player,manager)&&manager.Opens==0&&manager.Closes==1,"close callback creation replacement refuses further close/open");
  Reset();manager.Opened.Add("crafting");manager.OnClose=()=>GameManager.Instance.World=new World();Check(!RebirthCraftingNavigationService.OpenCharacter(source,player,manager)&&manager.Opens==0,"close callback world replacement refuses open");
  Reset();manager.OnOpen=()=>RebirthSurvivorClientState.State.CreationId="creation-b";Check(!RebirthCraftingNavigationService.OpenSkills(source,player,manager)&&source.xui.Group.Character.Skills==0,"open callback replacement refuses skills mutation");
  Reset();manager.OnIsOpen=()=>RebirthSurvivorClientState.State.CreationId="creation-b";Check(!Shortcut.Prefix(source,"character")&&RebirthCraftingNavigationService.Calls==0&&manager.Closes==0,"shortcut IsWindowOpen replacement blocks navigate");
  Reset();manager.Opened.Add(XUiC_RebirthSurvivorCharacter.WindowGroupId);manager.OnIsOpen=()=>source.xui.playerUI.windowManager=new GUIWindowManager();Check(!Shortcut.Prefix(source,"skills")&&manager.Closes==0,"shortcut target getter replacement blocks close");
  Reset();source.xui.playerUI.playerInput.PermanentActions.Character.WasPressed=true;manager.OnIsOpen=()=>RebirthSurvivorClientState.State.CreationId="creation-b";Check(!Shortcut.Prefix(source,"character")&&RebirthCraftingNavigationService.Calls==0,"B getter replacement blocks navigate");
  Reset();source.xui.playerUI.playerInput.PermanentActions.Character.WasPressed=true;manager.Opened.Add("crafting");manager.OnIsOpen=()=>GameManager.Instance.World=new World();Check(!Shortcut.Prefix(source,"character")&&manager.Closes==0,"B getter replacement blocks close");
  Reset();RebirthConsoleInputGuardRuntime.OnCheck=()=>RebirthSurvivorClientState.State.CreationId="creation-b";Check(!Shortcut.Prefix(source,"skills")&&RebirthCraftingNavigationService.Calls==0,"console getter replacement blocks navigate");
  Reset();PlatformManager.NativePlatform.Input.OnRead=()=>RebirthSurvivorClientState.State.CreationId="creation-b";Check(!Shortcut.Prefix(source,"character")&&RebirthCraftingNavigationService.Calls==0,"input style getter replacement blocks navigate");
  Reset();source.xui.playerUI.playerInput.PermanentActions.Character.OnRead=()=>RebirthSurvivorClientState.State.CreationId="creation-b";Check(!Shortcut.Prefix(source,"character")&&RebirthCraftingNavigationService.Calls==0,"WasPressed getter replacement blocks navigate");
  Reset();source.OnNameRead=()=>{if(source.NameReads==2)RebirthSurvivorClientState.State.CreationId="creation-b";};Check(!Selected.Prefix(source)&&RebirthCraftingNavigationService.Calls==0,"selected name replacement blocks navigate");
  Reset();RebirthConsoleInputGuardRuntime.OnCheck=()=>source.xui.playerUI.windowManager=new GUIWindowManager();Check(!Selected.Prefix(source)&&RebirthCraftingNavigationService.Calls==0,"selected console getter manager replacement blocks navigate");
  Reset();source.SelectedName="map";world.Entity=new EntityPlayerLocal();Check(Selected.Prefix(source),"unrelated selected native unchanged");
  Reset();RebirthSurvivorClientState.Header=new RebirthSurvivorOwnerHeader();Check(Shortcut.Prefix(source,"character")&&Selected.Prefix(source),"load unavailable native");
  return checks;
 }
}
"@
$code=$adapters.Replace('// CHARACTER',$character).Replace('// SKILLS',$skills).Replace('// SHORTCUT',$shortcut).Replace('// SELECTED',$selected)+[Environment]::NewLine+$helper.Replace('#nullable disable','').Replace('using System;','')
Add-Type -TypeDefinition $code
Write-Output ('PASS actual helper/Prefix/direct methods: '+[RouteFixture]::Run()+' checks. Native input/render/network/world/owner adapters doubled; NOT native route qualification.')
