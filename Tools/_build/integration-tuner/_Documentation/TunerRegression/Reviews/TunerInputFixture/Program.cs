using System;
using System.Linq;
using UnityEngine;
static class Program {
 static int passed; static LocalPlayerUI ui;static XUiWindowGroup edit,preview;static RebirthVehicleTuningSession session;
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
 static void Tick(params KeyCode[] keys){Time.frameCount++;Input.Held.Clear();Input.Down.Clear();foreach(var k in keys){Input.Held.Add(k);Input.Down.Add(k);}ModEvents.GameUpdate.Fire();Input.Down.Clear();}
 static void Start(bool fps=true,bool screenshot=true,XUiWindowGroup.EHasActionSetFor previewFlag=XUiWindowGroup.EHasActionSetFor.None){
  Input.Held.Clear();Input.Down.Clear();GUIWindowConsole.Open=false;if(ui!=null){ui.windowManager.CloseFailureBudget=0;ui.windowManager.PopFailureBudget=0;}RebirthVehicleTuningUi.End();for(int i=0;i<4;i++)Tick();
  RebirthNativeControls.Actions=new();PlayerActionsGlobal.Instance=new();PlayerActionsGlobal.Instance.ShowFPS.Enabled=fps;PlayerActionsGlobal.Instance.Screenshot.Enabled=screenshot;
  RebirthVehicleTuningSession.Dm=true;ui=new();GameManager.Instance.World=new(){Player=ui.entityPlayer};session=new();
  edit=new("rebirthVehicleTuning",XUiWindowGroup.EHasActionSetFor.Both);preview=new("rebirthVehicleTuningPreview",previewFlag);
  edit.Controller.Child=new XUiC_RebirthVehicleTuning();edit.Controller.Close=()=>RebirthVehicleTuningUi.NativeClosed(RebirthVehicleTuningUi.State.Edit);preview.Controller.Close=()=>RebirthVehicleTuningUi.NativeClosed(RebirthVehicleTuningUi.State.Driving);
  ui.windowManager.Windows.Add(edit.Id,edit);ui.windowManager.Windows.Add(preview.Id,preview);
  RebirthVehicleTuningUi.Begin(ui,session);ui.windowManager.FlushOpen();Tick();
 }
 static void Test(string name,Action a){try{a();passed++;Console.WriteLine("PASS "+name);}catch(Exception e){Console.WriteLine("FAIL "+name+": "+e);Environment.ExitCode=1;throw;}}
 static void Main(){
 Test("edit is modal/cursor/actionset; suppress exactly two native actions",()=>{Start();Check(edit.isModal&&edit.alwaysUsesMouseCursor&&edit.bActionSetEnabled,"edit contract");Check(!PlayerActionsGlobal.Instance.ShowFPS.Enabled&&!PlayerActionsGlobal.Instance.Screenshot.Enabled,"two suppressed");Check(PlayerActionsGlobal.Instance.Console.Enabled&&PlayerActionsGlobal.Instance.Other.Enabled,"other action mutated");});
 Test("native preview HasActionSet default true does not prevent first drive",()=>{Start();Check(preview.HasActionSet(),"fixture must model native preopen default");RebirthVehicleTuningUi.Drive();ui.windowManager.FlushOpen();Check(RebirthVehicleTuningUi.Mode==RebirthVehicleTuningUi.State.Driving&&!preview.HasActionSet()&&!preview.bActionSetEnabled&&!preview.alwaysUsesMouseCursor&&!preview.isModal,"passive preview contract");});
 Test("edit close and pop precede preview open",()=>{Start();ui.windowManager.Events.Clear();RebirthVehicleTuningUi.Drive();var e=ui.windowManager.Events;Check(e.IndexOf("onclose:"+edit.Id)>=0&&e.IndexOf("pop:"+edit.Id)<e.IndexOf("open:"+preview.Id),"native pop ordered before preview");Check(!ui.windowManager.IsWindowOpen(edit),"edit remains open");});
 Test("hide then reopen edit then drive",()=>{Start();RebirthVehicleTuningUi.Drive();ui.windowManager.FlushOpen();RebirthVehicleTuningUi.Hide();Check(RebirthVehicleTuningUi.Mode==RebirthVehicleTuningUi.State.Hidden&&!ui.windowManager.IsWindowOpen(edit)&&!ui.windowManager.IsWindowOpen(preview),"hide contract");RebirthVehicleTuningUi.OpenEdit();ui.windowManager.FlushOpen();Check(RebirthVehicleTuningUi.Mode==RebirthVehicleTuningUi.State.Edit&&edit.bActionSetEnabled,"reopen edit");RebirthVehicleTuningUi.Drive();Check(!edit.bActionSetEnabled&&ui.windowManager.IsWindowOpen(preview),"drive after reopen");});
 Test("Escape native controller close ends and pops",()=>{Start();ui.windowManager.Close(edit,true);Check(!RebirthVehicleTuningUi.Active&&!edit.bActionSetEnabled&&!ui.windowManager.IsWindowOpen(preview),"Escape leaked context");});
 Test("invalid session ends on update",()=>{Start();session.Available=false;Tick();Check(!RebirthVehicleTuningUi.Active&&!edit.bActionSetEnabled,"invalid entity leak");});
 Test("debug off ends on update",()=>{Start();RebirthVehicleTuningSession.Dm=false;Tick();Check(!RebirthVehicleTuningUi.Active&&!edit.bActionSetEnabled,"dmoff leak");});
 Test("world primary identity replacement ends",()=>{Start();GameManager.Instance.World.Player=new();Tick();Check(!RebirthVehicleTuningUi.Active&&!edit.bActionSetEnabled,"world owner identity leak");});
 Test("emergency close works with debug disabled",()=>{Start();RebirthVehicleTuningSession.Dm=false;RebirthVehicleTuningUi.EmergencyClose(ui);Check(!RebirthVehicleTuningUi.Active&&!edit.bActionSetEnabled,"emergency gated by dm");});
 Test("prior native flags restore after release gate",()=>{Start(false,true);Tick(KeyCode.F9);RebirthVehicleTuningUi.End();Time.frameCount+=4;Input.Down.Clear();ModEvents.GameUpdate.Fire();Check(!PlayerActionsGlobal.Instance.Screenshot.Enabled,"held F9 premature restore");Tick();Check(!PlayerActionsGlobal.Instance.ShowFPS.Enabled&&PlayerActionsGlobal.Instance.Screenshot.Enabled,"prior exact flags lost");Check(PlayerActionsGlobal.Instance.Console.Enabled,"unrelated native flag");});
 Test("end restoration waits more than one frame",()=>{Start();RebirthVehicleTuningUi.End();Tick();Check(!PlayerActionsGlobal.Instance.ShowFPS.Enabled,"restore too soon");Tick();Check(PlayerActionsGlobal.Instance.ShowFPS.Enabled&&PlayerActionsGlobal.Instance.Screenshot.Enabled,"restore never completed");});
 Test("Begin while restore pending retains original flags",()=>{Start(false,true);RebirthVehicleTuningUi.End();var next=new RebirthVehicleTuningSession();RebirthVehicleTuningUi.Begin(ui,next);Check(!PlayerActionsGlobal.Instance.Screenshot.Enabled,"pending begin unsuppressed");RebirthVehicleTuningUi.End();Tick();Tick();Check(!PlayerActionsGlobal.Instance.ShowFPS.Enabled&&PlayerActionsGlobal.Instance.Screenshot.Enabled,"recaptured suppressed flags");});
 Test("failed native Close retries twice and refuses preview",()=>{Start();ui.windowManager.Events.Clear();ui.windowManager.CloseFailureBudget=100;bool threw=false;try{RebirthVehicleTuningUi.Drive();}catch(InvalidOperationException){threw=true;}Check(threw&&!ui.windowManager.IsWindowOpen(preview),"failed close still opened drive");Check(ui.windowManager.Events.Count(x=>x=="close:"+edit.Id)==4,"expected two transition retries plus two End retries");Check(RebirthVehicleTuningUi.Active&&edit.bActionSetEnabled,"failure falsely reported Off");ui.windowManager.CloseFailureBudget=0;RebirthVehicleTuningUi.End();Check(!RebirthVehicleTuningUi.Active&&!edit.bActionSetEnabled,"retry cleanup failed");});
 Test("throwing OnClose hides then second Close completes native pop",()=>{Start();edit.Controller.Close=()=>throw new Exception("Injected OnClose failure");ui.windowManager.Events.Clear();RebirthVehicleTuningUi.Drive();Check(!edit.bActionSetEnabled&&ui.windowManager.IsWindowOpen(preview),"retry did not finish pop");Check(ui.windowManager.Events.Count(x=>x=="onclose:"+edit.Id)==1,"OnClose callback unexpectedly repeated");});
 Test("one Pop failure retries cleanup before drive",()=>{Start();ui.windowManager.PopFailureBudget=1;RebirthVehicleTuningUi.Drive();Check(!edit.bActionSetEnabled&&ui.windowManager.IsWindowOpen(preview),"Pop retry failed");});
 Test("preview actionset forbidden before first open",()=>{Start(previewFlag:XUiWindowGroup.EHasActionSetFor.Both);bool threw=false;try{RebirthVehicleTuningUi.Drive();}catch(InvalidOperationException){threw=true;}Check(threw&&!ui.windowManager.IsWindowOpen(preview),"unsafe preview admitted");});
 Test("console and unrelated modal ignore tuner function keys",()=>{Start();GUIWindowConsole.Open=true;Tick(KeyCode.F8);Check(RebirthVehicleTuningUi.Mode==RebirthVehicleTuningUi.State.Edit,"console F8 changed tuner");GUIWindowConsole.Open=false;Tick();var modal=new GUIWindow{Id="other",isModal=true,isShowing=true};ui.windowManager.Windows.Add(modal.Id,modal); // edit was inserted first; hide it to expose unrelated modal
 RebirthVehicleTuningUi.Hide();Tick(KeyCode.F8);Check(RebirthVehicleTuningUi.Mode==RebirthVehicleTuningUi.State.Hidden,"unrelated modal changed tuner");modal.isShowing=false;});
 Test("F8 edit-drive and F9 hide recover only after key release",()=>{Start();Tick(KeyCode.F8);Check(RebirthVehicleTuningUi.Mode==RebirthVehicleTuningUi.State.Driving,"F8 drive missing");Check(!PlayerActionsGlobal.Instance.ShowFPS.WasPressed&&!PlayerActionsGlobal.Instance.Screenshot.WasPressed,"native action escaped suppression");Tick(KeyCode.F8);Check(RebirthVehicleTuningUi.Mode==RebirthVehicleTuningUi.State.Driving,"held key toggled repeatedly");Tick();Tick(KeyCode.F9);Check(RebirthVehicleTuningUi.Mode==RebirthVehicleTuningUi.State.Hidden,"F9 hide missing");Tick();Tick(KeyCode.F8);Check(RebirthVehicleTuningUi.Mode==RebirthVehicleTuningUi.State.Edit&&edit.bActionSetEnabled,"F8 reopen missing");});
 Test("reenter edit performs close/pop/reopen rather than native already-open return",()=>{Start();ui.windowManager.Events.Clear();RebirthVehicleTuningUi.OpenEdit(true);var e=ui.windowManager.Events;Check(e.Contains("pop:"+edit.Id)&&e.Contains("open:"+edit.Id)&&!e.Contains("alreadyopen:"+edit.Id)&&edit.bActionSetEnabled,"reenter skipped native lifecycle");});
 Test("other bare F8/F9 binding in each native set refuses before action ownership",()=>{
  for(int setIndex=0;setIndex<6;setIndex++)foreach(string key in new[]{" F8 ","f9"}){
   Start(true,false);RebirthVehicleTuningUi.End();Tick();Tick();
   var sets=new InControl.PlayerActionSet[]{ui.playerInput,ui.playerInput.GUIActions,ui.playerInput.VehicleActions,ui.playerInput.PermanentActions,RebirthNativeControls.Actions,PlayerActionsGlobal.Instance};
   var conflict=new InControl.PlayerAction{Name="custom-"+setIndex,Enabled=false};conflict.Bindings.Add(new(key));sets[setIndex].Actions.Add(conflict);
   bool threw=false;try{RebirthVehicleTuningUi.Begin(ui,new());}catch(InvalidOperationException e){threw=e.Message.Contains("xuiRbTuneKeyConflict");}
   Check(threw&&!RebirthVehicleTuningUi.Active&&RebirthVehicleTuningUi.Session==null&&RebirthVehicleTuningUi.Mode==RebirthVehicleTuningUi.State.Off,"conflict admitted/retained session");
   Check(PlayerActionsGlobal.Instance.ShowFPS.Enabled&&!PlayerActionsGlobal.Instance.Screenshot.Enabled&&!conflict.Enabled&&PlayerActionsGlobal.Instance.Other.Enabled,"conflict changed action flags");
   Check(!edit.bActionSetEnabled&&!ui.windowManager.IsWindowOpen(edit)&&!ui.windowManager.IsWindowOpen(preview),"conflict opened input context");
   sets[setIndex].Actions.Remove(conflict);
  }
 });
 Test("modifier chords allowed by scan and ignored by tuner",()=>{
  Start();RebirthVehicleTuningUi.End();Tick();Tick();var chord=new InControl.PlayerAction{Name="CtrlF8"};chord.Bindings.Add(new("Ctrl+F8"));ui.playerInput.Actions.Add(chord);
  RebirthVehicleTuningUi.Begin(ui,new());ui.windowManager.FlushOpen();Tick();
  foreach(var modifier in new[]{KeyCode.LeftControl,KeyCode.RightControl,KeyCode.LeftAlt,KeyCode.RightAlt,KeyCode.LeftShift,KeyCode.RightShift}){
   Tick();Tick(modifier,KeyCode.F8);Check(RebirthVehicleTuningUi.Mode==RebirthVehicleTuningUi.State.Edit&&chord.Enabled,"F8 chord changed tuner/other action");
   Tick();Tick(modifier,KeyCode.F9);Check(RebirthVehicleTuningUi.Mode==RebirthVehicleTuningUi.State.Edit&&chord.Enabled,"F9 chord changed tuner/other action");
  }
 });
 Test("native pending-open close is cleaned before preview",()=>{Start();RebirthVehicleTuningUi.Hide();RebirthVehicleTuningUi.OpenEdit(true);Check(edit.Pending&&!edit.isShowing&&edit.bActionSetEnabled,"pending native model");RebirthVehicleTuningUi.Drive();Check(!edit.Pending&&!edit.bActionSetEnabled&&preview.Pending,"pending edit leaked");});
 Test("world shutdown closes tuner; later update restores actions",()=>{Start();ModEvents.WorldShuttingDown.Fire();Check(!RebirthVehicleTuningUi.Active&&!edit.bActionSetEnabled,"world shutdown leak");Tick();Tick();Check(PlayerActionsGlobal.Instance.ShowFPS.Enabled,"world restore leak");});
 Test("final game shutdown restores captured flags immediately",()=>{Start(false,true);ModEvents.GameShutdown.Fire();Check(!RebirthVehicleTuningUi.Active&&!PlayerActionsGlobal.Instance.ShowFPS.Enabled&&PlayerActionsGlobal.Instance.Screenshot.Enabled,"final game shutdown flags");});
 Console.WriteLine($"RESULT {passed} meaningful actual-service controls PASS; native API behavior modeled, not in-game acceptance.");
 }
}
