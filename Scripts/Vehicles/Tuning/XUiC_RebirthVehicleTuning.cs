#if REBIRTH_DEBUG
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.Scripting;

[Preserve]
public sealed class XUiC_RebirthVehicleTuning:XUiController
{
    private RebirthVehicleTuningSession session;
    private readonly List<RebirthVehicleTuningSession.Control> filtered=new List<RebirthVehicleTuningSession.Control>();
    private readonly RebirthVehicleTuningSession.Control[] displayed=new RebirthVehicleTuningSession.Control[4];
    private readonly RebirthVehicleTuningSession.Control[] editing=new RebirthVehicleTuningSession.Control[4];
    private readonly int[] editEpoch=new int[4];
    private readonly XUiC_TextInput[] inputs=new XUiC_TextInput[4];
    private readonly XUiController[] sliderTracks=new XUiController[4];
    private readonly XUiV_Sprite[] thumbs=new XUiV_Sprite[4];
    private int heldSlider=-1;
    private RebirthVehicleTuningSession.Control heldControl;
    private float heldNumber;
    private readonly XUiV_Sprite[] fills=new XUiV_Sprite[4],markers=new XUiV_Sprite[4];
    private readonly XUiV_Label[] originalLabels=new XUiV_Label[4];
    private XUiV_ScrollBar listScroll;
    private int category,first,epoch;
    private bool refreshing,initialized;
    private Vector2i layoutScreen=new Vector2i(-1,-1);
    private string status="";
    internal void Bind(RebirthVehicleTuningSession value)
    {if(!ReferenceEquals(session,value)){category=0;first=0;}session=value;Filter();}
    public override void Init()
    {
        base.Init();refreshing=true;
        try
        {
            Button("close",()=>RebirthVehicleTuningUi.End());
            Button("drive",()=>RebirthVehicleTuningUi.Drive());
            Button("reset",()=>{NeedSession();session.Reset();status=Localization.Get("xuiRbVehicleTune13");Show();});
            Button("export",()=>{NeedSession();session.Export();status=Localization.Get("xuiRbVehicleTune14");RefreshBindings();});
            for(int i=0;i<7;i++){int index=i;Button("category"+i,()=>{category=index;first=0;Filter();Show();});}
            for(int i=0;i<4;i++)
            {
                int slot=i;
                var row=GetChildById("row"+i);row.OnScroll+=Scroll;
                inputs[i]=GetChildById("row"+i+"Value") as XUiC_TextInput;
                if(inputs[i]==null)throw new InvalidOperationException("Tuner numeric input missing: "+i);
                inputs[i].VkPropagatesEvents=false;
                inputs[i].OnInputSelectedHandler+=(sender,selected)=>{if(selected){editing[slot]=displayed[slot];editEpoch[slot]=epoch;}};
                // Native OnSubmit only removes focus when no delayed handler is registered.
                // Commit in the same native UIInput submit event, never to a recycled row next frame.
                EventDelegate.Add(inputs[i].UIInput.onSubmit,()=>Submit(slot,inputs[slot].Text));
                sliderTracks[i]=GetChildById("row"+i+"Slider");
                sliderTracks[i].OnScroll+=Scroll;
                thumbs[i]=GetChildById("row"+i+"SliderThumb").ViewComponent as XUiV_Sprite;
                sliderTracks[i].OnMouseUpDown+=(sender,pressed)=>
                {
                    if(pressed)
                    {
                        var control=displayed[slot];if(control==null||!control.Numeric||!control.Editable)return;
                        heldSlider=slot;heldControl=control;heldNumber=control.Number;Slider(slot);
                    }
                    else if(heldSlider==slot)FinishSlider(slot);
                };
                sliderTracks[i].OnDrag+=(sender,type,delta)=>
                {
                    if(heldSlider!=slot)return;
                    if(type==EDragType.Dragging)Slider(slot);
                    else if(type==EDragType.DragEnd)FinishSlider(slot);
                };
                fills[i]=GetChildById("row"+i+"Fill").ViewComponent as XUiV_Sprite;
                markers[i]=GetChildById("row"+i+"OriginalMarker").ViewComponent as XUiV_Sprite;
                originalLabels[i]=GetChildById("row"+i+"OriginalLabel").ViewComponent as XUiV_Label;
            }
            listScroll=GetChildById("settingsScroll").ViewComponent as XUiV_ScrollBar;
            ConfigureBar(listScroll,false,ListChanged);
            initialized=true;
        }
        finally{refreshing=false;}
    }
    private void ConfigureBar(XUiV_ScrollBar view,bool horizontal,EventDelegate.Callback changed)
    {
        if(view?.ScrollBar==null)throw new InvalidOperationException("Tuner native scrollbar unavailable.");
        var bar=view.ScrollBar;
        bar.fillDirection=horizontal?UIProgressBar.FillDirection.LeftToRight:UIProgressBar.FillDirection.TopToBottom;
        bar.numberOfSteps=0;bar.barSize=horizontal?.035f:1f;
        view.Connect(Scroll,changed);
    }
    private void Button(string id,Action action)
    {
        var button=GetChildById(id);if(button==null)throw new InvalidOperationException("Tuner control missing: "+id);
        button.OnPress+=(sender,key)=>{if(key!=0&&key!=-1)return;try{if(heldSlider>=0){ReleaseSlider();Show();}action();}catch(Exception error){status=error.Message;RefreshBindings();}};
    }
    private void NeedSession()
    {if(session==null||!session.Valid||!ReferenceEquals(session,RebirthVehicleTuningUi.Session))throw new InvalidOperationException(Localization.Get("xuiRbVehicleTune6"));}
    private void Filter()
    {
        ReleaseSlider();
        filtered.Clear();
        if(session!=null)foreach(var control in session.Controls)if(control.Category==category)filtered.Add(control);
        first=Math.Max(0,Math.Min(first,Math.Max(0,filtered.Count-4)));
    }
    private void Scroll(XUiController sender,float delta)
    {
        if(refreshing||RebirthVehicleTuningUi.Transitioning||delta==0||RebirthVehicleTuningUi.Mode!=RebirthVehicleTuningUi.State.Edit)return;
        ReleaseSlider();
        first=Math.Max(0,Math.Min(Math.Max(0,filtered.Count-4),first+(delta>0?-1:1)));Show();
    }
    private void ListChanged()
    {
        if(refreshing||RebirthVehicleTuningUi.Transitioning||listScroll==null)return;
        int next=Mathf.RoundToInt(listScroll.ScrollBar.value*Math.Max(0,filtered.Count-4));
        if(next!=first){ReleaseSlider();first=next;Show();}
    }
    private void Slider(int slot,bool commit=false)
    {
        if(refreshing||heldSlider!=slot||RebirthConsoleInputGuardRuntime.BlocksGameplayInput()||RebirthVehicleTuningUi.Transitioning||RebirthVehicleTuningUi.Mode!=RebirthVehicleTuningUi.State.Edit)return;
        var control=displayed[slot];if(control==null||!control.Numeric||!control.Editable)return;
        if(!ReferenceEquals(control,heldControl)){ReleaseSlider();return;}
        try
        {
            NeedSession();
            // Same native pointer-to-local-plane conversion as the working Energy
            // Management POI light slider. Includes editor scale and UI pixel ratio.
            var transform=sliderTracks[slot]?.ViewComponent?.UiTransform;var camera=UICamera.currentCamera;
            if(transform==null||camera==null)return;
            Vector2 mouse=UICamera.currentTouch!=null?UICamera.currentTouch.pos:(Vector2)Input.mousePosition;
            var ray=camera.ScreenPointToRay(mouse);
            if(!new Plane(transform.forward,transform.position).Raycast(ray,out float distance))return;
            float fraction=Mathf.Clamp01(transform.InverseTransformPoint(ray.GetPoint(distance)).x/270f);
            float value=Mathf.Lerp(control.Minimum,control.Maximum,fraction);
            if(control.Boolean||control.Category==RebirthVehicleTuningSession.StorageCategory)value=Mathf.Round(value);
            heldNumber=value;
            if(commit){control.Apply(value);status=Localization.Get(control.Proposal?"xuiRbVehicleTune11":"xuiRbVehicleTune12");}
            Show();
        }
        catch(Exception error){status=error.Message;Show();}
    }
    private void ReleaseSlider(){heldSlider=-1;heldControl=null;}
    private void FinishSlider(int slot)
    {try{Slider(slot,true);}finally{ReleaseSlider();Show();}}
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
    private static void SetBar(XUiV_ScrollBar view,float value)
    {
        // Recycled rows must not rewind to a previous native on-open value and issue a spurious write.
        view.onOpenValue=value;view.openFrame=Time.frameCount-2;view.ScrollBar.Set(value,false);
    }
    private void Show()
    {
        if(!initialized)return;
        if(session==null||!session.Valid)
        {
            refreshing=true;epoch++;for(int i=0;i<4;i++)displayed[i]=null;
            status=Localization.Get("xuiRbVehicleTune6");try{RefreshBindings();}finally{refreshing=false;}return;
        }
        refreshing=true;epoch++;
        try
        {
            for(int i=0;i<4;i++)displayed[i]=first+i<filtered.Count?filtered[first+i]:null;
            IsDirty=true;RefreshBindings();
            for(int i=0;i<4;i++)
            {
                var control=displayed[i];if(control==null)continue;
                bool preview=heldSlider==i&&ReferenceEquals(heldControl,control);
                float shown=preview?heldNumber:control.Numeric?control.Number:0;
                float normalized=control.Maximum<=control.Minimum?0:Mathf.Clamp01((shown-control.Minimum)/(control.Maximum-control.Minimum));
                inputs[i].Text=preview?(control.Boolean?Localization.Get(shown>=.5f?"xuiRbTuneOn":"xuiRbTuneOff"):shown.ToString("G7",CultureInfo.InvariantCulture)):control.Text;
                GetChildById("row"+i).ViewComponent.ToolTip=control.DetailHelp+"\n"+control.Technical;
                inputs[i].ViewComponent.ToolTip=control.DetailHelp+"\n"+control.Technical+"\n"+Localization.Get("xuiRbTunerOriginal")+": "+control.OriginalText;
                sliderTracks[i].ViewComponent.ToolTip=inputs[i].ViewComponent.ToolTip;
                bool numeric=control.Numeric;
                var inputView=inputs[i].ViewComponent;
                inputView.Position=new Vector2i(numeric?588:290,-14);inputView.Size=new Vector2i(numeric?64:397,36);
                if(inputs[i].label!=null)inputs[i].label.Size=new Vector2i(numeric?60:385,32);
                foreach(string suffix in new[]{"Background","Border"})
                {
                    var frame=GetChildById("row"+i+"Value"+suffix)?.ViewComponent;
                    if(frame!=null){frame.Position=new Vector2i(numeric?586:288,-14);frame.Size=new Vector2i(numeric?101:399,36);}
                }
                if(!numeric)continue;
                sliderTracks[i].ViewComponent.Enabled=control.Editable;
                float travel=262f,half=4f;
                if(thumbs[i]!=null)thumbs[i].Position=new Vector2i(290+Mathf.RoundToInt(normalized*travel),-27);
                int originalX=290+Mathf.RoundToInt(half+control.OriginalNormalized*travel);
                if(markers[i]!=null)markers[i].Position=new Vector2i(originalX,-25);
                if(originalLabels[i]!=null)originalLabels[i].Position=new Vector2i(Math.Max(290,Math.Min(482,originalX-38)),-5);
                if(fills[i]!=null)fills[i].Size=new Vector2i(Mathf.RoundToInt(half+normalized*travel),8);
            }
            listScroll.BarRequired=filtered.Count>4;
            listScroll.ScrollBar.barSize=filtered.Count<=4?1:Mathf.Clamp01(4f/filtered.Count);
            SetBar(listScroll,filtered.Count<=4?0:(float)first/(filtered.Count-4));
        }
        finally{refreshing=false;}
    }
    public override void OnOpen()
    {
        base.OnOpen();
        if(RebirthVehicleTuningUi.Session==null){xui.playerUI.windowManager.Close(windowGroup);return;}
        Bind(RebirthVehicleTuningUi.Session);status="";Layout(true);Show();
    }
    public override void OnClose()
    {ReleaseSlider();base.OnClose();epoch++;RebirthVehicleTuningUi.NativeClosed(RebirthVehicleTuningUi.State.Edit);}
    public override void Cleanup()
    {RebirthVehicleTuningUi.End();base.Cleanup();}
    public override void Update(float dt)
    {
        // Native modal cancellation already prioritizes the console and selected
        // text field. Do not consume its Cancel press from this LateUpdate path.
        Layout(false);
        // Only the native vertical list bar remains; its visibility can rearm
        // a two-frame rewind after a row rebind.
        int before=Time.frameCount-2;
        if(listScroll!=null)listScroll.openFrame=before;
        base.Update(dt);
    }
    private void Layout(bool force)
    {
        if(ViewComponent?.UiTransform==null)return;
        var screen=xui.GetXUiScreenSize();if(!force&&screen==layoutScreen)return;layoutScreen=screen;
        float scale=Math.Max(.1f,Math.Min(1.5f,Math.Min((screen.x-32)/960f,(screen.y-32)/500f)));
        ViewComponent.Position=new Vector2i(-Mathf.RoundToInt(480*scale),Mathf.RoundToInt(250*scale));
        ViewComponent.TryUpdatePosition();ViewComponent.UiTransform.localScale=new Vector3(scale,scale,1);
    }
    public override bool GetBindingValueInternal(ref string value,string name)
    {
        switch(name)
        {
            case "titleVehicle":value=session?.DisplayName??"";return true;
            case "categoryName":value=Localization.Get("xuiRbTunerCategory"+category);return true;
            case "status":value=status;return true;
            case "settingsScrollVisible":value=(filtered.Count>4).ToString();return true;
        }
        if(name.StartsWith("category",StringComparison.Ordinal)&&name.EndsWith("Selected",StringComparison.Ordinal))
        {int number;if(int.TryParse(name.Substring(8,name.Length-16),out number)){value=(category==number).ToString();return true;}}
        if(name.StartsWith("row",StringComparison.Ordinal)&&name.Length>4&&name[3]>='0'&&name[3]<='3')
        {
            int slot=name[3]-'0';var row=displayed[slot];
            switch(name.Substring(4))
            {
                case "Visible":value=(row!=null).ToString();return true;
                case "Numeric":value=(row?.Numeric==true).ToString();return true;
                case "Editable":value=(row?.Editable==true).ToString();return true;
                case "Name":value=row?.Label??"";return true;
                case "Help":value=row?.Help??"";return true;
                case "Original":value=row==null?"":Localization.Get("xuiRbTunerOriginal")+": "+row.OriginalText;return true;
                case "Value":value=row?.Text??"";return true;
                case "Unit":value=row?.Unit??"";return true;
                case "Icon":value=row?.Icon??"rb_tuner_advanced";return true;
            }
        }
        return base.GetBindingValueInternal(ref value,name);
    }
}

[Preserve]
public sealed class XUiC_RebirthVehicleTuningPreview:XUiController
{
    private float nextRefresh;
    private Vector2i layoutScreen=new Vector2i(-1,-1);
    private string speed="",limit="",acceleration="";
    public override void OnOpen(){base.OnOpen();Layout(true);Refresh();}
    public override void OnClose(){base.OnClose();RebirthVehicleTuningUi.NativeClosed(RebirthVehicleTuningUi.State.Driving);}
    public override void Cleanup(){RebirthVehicleTuningUi.End();base.Cleanup();}
    public override void Update(float dt)
    {
        if(Time.realtimeSinceStartup>=nextRefresh){nextRefresh=Time.realtimeSinceStartup+.2f;Layout(false);Refresh();}
        base.Update(dt);
    }
    private void Layout(bool force)
    {
        if(ViewComponent?.UiTransform==null)return;
        var screen=xui.GetXUiScreenSize();if(!force&&screen==layoutScreen)return;layoutScreen=screen;
        float scale=Math.Max(.1f,Math.Min(1f,Math.Min((screen.x-36)/260f,(screen.y-196)/148f)));
        ViewComponent.Position=new Vector2i(18,178+Mathf.RoundToInt(148*scale));
        ViewComponent.TryUpdatePosition();ViewComponent.UiTransform.localScale=new Vector3(scale,scale,1);
    }
    private void Refresh()
    {
        var session=RebirthVehicleTuningUi.Session;if(session==null||!session.Valid)return;
        var body=session.Entity.vehicleRB;float actual;
        if(body!=null){var v=body.velocity;actual=Mathf.Sqrt(v.x*v.x+v.z*v.z)*3.6f;}
        else actual=Mathf.Abs(session.Entity.vehicle.CurrentForwardVelocity)*3.6f;
        speed=actual.ToString("0",CultureInfo.InvariantCulture)+" km/h";
        limit=session.ForwardLimitKmh.ToString("0.#",CultureInfo.InvariantCulture)+" km/h";
        acceleration=session.AccelerationPercent.ToString("0.#",CultureInfo.InvariantCulture)+" %";
        RefreshBindings();
    }
    public override bool GetBindingValueInternal(ref string value,string name)
    {
        switch(name){case "previewSpeed":value=speed;return true;case "previewForwardLimit":value=limit;return true;case "previewAcceleration":value=acceleration;return true;}
        return base.GetBindingValueInternal(ref value,name);
    }
}
#endif
