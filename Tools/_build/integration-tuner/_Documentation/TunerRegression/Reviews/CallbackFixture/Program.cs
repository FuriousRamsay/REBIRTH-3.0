using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
class RebirthVehicleTuningSession {
 public bool Valid=true;
 public class Control {
  public bool Numeric=true,Editable=true,Boolean,Proposal;
  public float Minimum=10,Maximum=30,LastValue;
  public string LastText;
  public int ApplyCalls,ApplyTextCalls;
  public bool Throw;
  public void Apply(float value){if(Throw)throw new InvalidOperationException("stub apply rejected");ApplyCalls++;LastValue=value;}
  public void ApplyText(string text){if(Throw)throw new InvalidOperationException("stub text rejected");ApplyTextCalls++;LastText=text;}
 }
}
static class RebirthVehicleTuningUi {public enum State {Off,Edit,Driving,Hidden} public static bool Transitioning;public static State Mode=State.Edit;public static RebirthVehicleTuningSession Session;}
static class Localization {public static string Get(string key)=>key;}
static class Mathf {public static float Lerp(float a,float b,float t)=>a+(b-a)*Math.Clamp(t,0,1);public static float Round(float f)=>MathF.Round(f);}
static class Input {public static bool Escape;public static bool GetKeyDown(KeyCode code)=>Escape;}
enum KeyCode {Escape}
static class Time {public static int frameCount=100;}
class Press {public bool WasPressed;}
class Actions {public Press Cancel=new();}
class PlayerActions {public Actions PermanentActions=new(),GUIActions=new();}
class PlayerUi {public PlayerActions playerInput=new();}
class Xui {public PlayerUi playerUI=new();}
class NativeBar {public float value;public int SetCalls,NotifyCalls;public bool? LastNotify;public Action Changed;public void Set(float v,bool notify=true){SetCalls++;LastNotify=notify;value=Math.Clamp(v,0,1);if(notify){NotifyCalls++;Changed?.Invoke();}}}
class XUiV_ScrollBar {public float onOpenValue;public int openFrame;public NativeBar ScrollBar=new();public void SimulateNativeUpdate(){if(Time.frameCount<=openFrame+1&&Math.Abs(onOpenValue-ScrollBar.value)>1e-6)ScrollBar.Set(onOpenValue);}}
class XUiC_RebirthVehicleTuning {
 RebirthVehicleTuningSession session;
 readonly RebirthVehicleTuningSession.Control[] displayed=new RebirthVehicleTuningSession.Control[4],editing=new RebirthVehicleTuningSession.Control[4];
 readonly int[] editEpoch=new int[4];
 readonly XUiV_ScrollBar[] sliderViews=new XUiV_ScrollBar[4];
 int epoch=7,ShowCalls;bool refreshing;string status="";Xui xui=new();
 // Disclosed seam: real Show is not under test. It advances selection epoch on rebinding.
 void Show(){ShowCalls++;epoch++;}


    private void NeedSession()
    {if(session==null||!session.Valid||!ReferenceEquals(session,RebirthVehicleTuningUi.Session))throw new InvalidOperationException(Localization.Get("xuiRbVehicleTune6"));}

    private void Submit(int slot,string text)
    {
        // Closing/escape must never commit unsubmitted text as focus is removed.
        var actions=xui?.playerUI?.playerInput;
        if(Input.GetKeyDown(KeyCode.Escape)||(actions!=null&&(actions.PermanentActions.Cancel.WasPressed||actions.GUIActions.Cancel.WasPressed)))return;
        // Selection epoch and owner must still match this synchronous submit.
        if(refreshing||RebirthVehicleTuningUi.Transitioning||RebirthVehicleTuningUi.Mode!=RebirthVehicleTuningUi.State.Edit||editEpoch[slot]!=epoch||
            !ReferenceEquals(editing[slot],displayed[slot])||displayed[slot]==null||!ReferenceEquals(session,RebirthVehicleTuningUi.Session))return;
        try{NeedSession();displayed[slot].ApplyText(text);status=Localization.Get(displayed[slot].Proposal?"xuiRbVehicleTune11":"xuiRbVehicleTune12");Show();}
        catch(Exception error){status=error.Message;Show();}
    }

    private void Slider(int slot)
    {
        if(refreshing||RebirthVehicleTuningUi.Transitioning||RebirthVehicleTuningUi.Mode!=RebirthVehicleTuningUi.State.Edit)return;
        var control=displayed[slot];if(control==null||!control.Numeric||!control.Editable)return;
        try
        {
            NeedSession();float value=Mathf.Lerp(control.Minimum,control.Maximum,sliderViews[slot].ScrollBar.value);
            if(control.Boolean)value=Mathf.Round(value);
            control.Apply(value);status=Localization.Get(control.Proposal?"xuiRbVehicleTune11":"xuiRbVehicleTune12");Show();
        }
        catch(Exception error){status=error.Message;Show();}
    }

    private static void SetBar(XUiV_ScrollBar view,float value)
    {
        // Recycled rows must not rewind to a previous native on-open value and issue a spurious write.
        view.onOpenValue=value;view.openFrame=Time.frameCount-2;view.ScrollBar.Set(value,false);
    }
static int checks;static readonly List<string> passed=new();
static void Check(bool ok,string label){checks++;if(!ok)throw new Exception("FAIL "+label);passed.Add(label);}
static XUiC_RebirthVehicleTuning Fresh(){var f=new XUiC_RebirthVehicleTuning();f.session=new();RebirthVehicleTuningUi.Session=f.session;RebirthVehicleTuningUi.Mode=RebirthVehicleTuningUi.State.Edit;RebirthVehicleTuningUi.Transitioning=false;Input.Escape=false;for(int i=0;i<4;i++){f.displayed[i]=new();f.editing[i]=f.displayed[i];f.editEpoch[i]=f.epoch;f.sliderViews[i]=new();f.sliderViews[i].ScrollBar.value=.25f;}return f;}
int Writes=>displayed.Where(c=>c!=null).Sum(c=>c.ApplyCalls+c.ApplyTextCalls);
static void SubmitNoWrite(string label,Action<XUiC_RebirthVehicleTuning> arrange){var f=Fresh();var all=f.displayed.ToArray();arrange(f);f.Submit(2,"22");Check(all.Sum(c=>c.ApplyCalls+c.ApplyTextCalls)==0,label);}
static void SliderNoWrite(string label,Action<XUiC_RebirthVehicleTuning> arrange){var f=Fresh();var all=f.displayed.ToArray();arrange(f);f.Slider(2);Check(all.Sum(c=>c.ApplyCalls+c.ApplyTextCalls)==0,label);}
static void Main(){
 SubmitNoWrite("submit refreshing",f=>f.refreshing=true);
 SubmitNoWrite("submit transitioning",f=>RebirthVehicleTuningUi.Transitioning=true);
 foreach(var mode in new[]{RebirthVehicleTuningUi.State.Off,RebirthVehicleTuningUi.State.Driving,RebirthVehicleTuningUi.State.Hidden})SubmitNoWrite("submit mode "+mode,f=>RebirthVehicleTuningUi.Mode=mode);
 SubmitNoWrite("submit invalid session",f=>f.session.Valid=false);
 SubmitNoWrite("submit null session",f=>f.session=null);
 SubmitNoWrite("submit epoch mismatch",f=>f.editEpoch[2]--);
 SubmitNoWrite("submit recycled control",f=>f.displayed[2]=new());
 SubmitNoWrite("submit null displayed",f=>f.displayed[2]=null);
 SubmitNoWrite("submit changed session",f=>RebirthVehicleTuningUi.Session=new());
 SubmitNoWrite("submit physical escape",f=>Input.Escape=true);
 SubmitNoWrite("submit permanent cancel",f=>f.xui.playerUI.playerInput.PermanentActions.Cancel.WasPressed=true);
 SubmitNoWrite("submit gui cancel",f=>f.xui.playerUI.playerInput.GUIActions.Cancel.WasPressed=true);
 SubmitNoWrite("submit physical escape without xui",f=>{f.xui=null;Input.Escape=true;});
 SubmitNoWrite("submit apply exception no effect",f=>f.displayed[2].Throw=true);
 var f=Fresh();var target=f.displayed[2];f.Submit(2,"22.75");Check(target.ApplyTextCalls==1&&target.LastText=="22.75"&&f.displayed.Where((c,i)=>i!=2).All(c=>c.ApplyTextCalls==0),"submit exact selected control once");Check(f.ShowCalls==1,"valid submit refreshes once");f.Submit(2,"99");Check(target.ApplyTextCalls==1,"duplicate submit stale epoch rejected");f.editEpoch[2]=f.epoch;f.Submit(2,"23");Check(target.ApplyTextCalls==2&&target.LastText=="23","new selection epoch accepted");
 SliderNoWrite("slider refreshing",s=>s.refreshing=true);
 SliderNoWrite("slider transitioning",s=>RebirthVehicleTuningUi.Transitioning=true);
 foreach(var mode in new[]{RebirthVehicleTuningUi.State.Off,RebirthVehicleTuningUi.State.Driving,RebirthVehicleTuningUi.State.Hidden})SliderNoWrite("slider mode "+mode,s=>RebirthVehicleTuningUi.Mode=mode);
 SliderNoWrite("slider invalid session",s=>s.session.Valid=false);
 SliderNoWrite("slider null session",s=>s.session=null);
 SliderNoWrite("slider changed session",s=>RebirthVehicleTuningUi.Session=new());
 SliderNoWrite("slider null control",s=>s.displayed[2]=null);
 SliderNoWrite("slider nonnumeric",s=>s.displayed[2].Numeric=false);
 SliderNoWrite("slider readonly",s=>s.displayed[2].Editable=false);
 SliderNoWrite("slider apply exception no effect",s=>s.displayed[2].Throw=true);
 f=Fresh();target=f.displayed[2];f.Slider(2);Check(target.ApplyCalls==1&&Math.Abs(target.LastValue-15)<1e-5&&f.Writes==1,"slider correct display range and selected control once");
 f=Fresh();target=f.displayed[2];target.Minimum=0;target.Maximum=1;target.Boolean=true;f.sliderViews[2].ScrollBar.value=.8f;f.Slider(2);Check(target.ApplyCalls==1&&target.LastValue==1,"boolean slider rounds native display value");
 foreach(float v in new[]{0f,.33f,1f}){f=Fresh();var view=f.sliderViews[2];view.openFrame=Time.frameCount;view.onOpenValue=.1f;view.ScrollBar.Changed=()=>f.Slider(2);SetBar(view,v);Check(view.ScrollBar.value==v&&view.ScrollBar.LastNotify==false&&view.ScrollBar.NotifyCalls==0&&f.Writes==0,"SetBar silent "+v);Check(view.onOpenValue==v&&view.openFrame==Time.frameCount-2,"SetBar native open hold disabled "+v);view.SimulateNativeUpdate();Check(view.ScrollBar.value==v&&view.ScrollBar.SetCalls==1&&f.Writes==0,"SetBar no native open rewind "+v);}
 Console.WriteLine("PASS "+checks+" actual-source UI callback checks");foreach(var label in passed)Console.WriteLine("PASS "+label);
 }
}
