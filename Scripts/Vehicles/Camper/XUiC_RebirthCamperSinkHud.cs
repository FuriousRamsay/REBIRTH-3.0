using System.Globalization;
using UnityEngine;
using UnityEngine.Scripting;

[Preserve]
public sealed class XUiC_RebirthCamperSinkHud:XUiController
{
    private XUiView card;
    private float next;
    private int amount=-2;
    private bool closed,visible;
    public override void Init(){base.Init();card=GetChildById("camperSinkCard")?.ViewComponent;if(card!=null)card.IsVisible=false;}
    public override void OnOpen(){base.OnOpen();closed=false;next=0;}
    public override void OnClose(){closed=true;if(card!=null)card.IsVisible=false;base.OnClose();}
    public override void Update(float dt)
    {
        base.Update(dt);
        var player=xui?.playerUI?.entityPlayer;var windows=xui?.playerUI?.windowManager;
        if(closed||player==null||windows==null||windows.IsCursorWindowOpen()||windows.IsModalWindowOpen()
            ||RebirthConsoleInputGuardRuntime.BlocksGameplayInput())
        {if(card!=null)card.IsVisible=false;visible=false;next=0;return;}
        if(Time.realtimeSinceStartup<next)return;next=Time.realtimeSinceStartup+.2f;
        bool shown=RebirthCamperSink.ObserveFocused(player,out int current);
        if(card!=null)card.IsVisible=shown;
        if(shown!=visible||current!=amount){visible=shown;amount=current;RefreshBindings();}
    }
    public override bool GetBindingValueInternal(ref string value,string bindingName)
    {
        switch(bindingName)
        {
            case "campersinkcaption":value=string.Format(Localization.Get("xuiRebirthCamperSinkGaugeTitle"),amount<0?"…":(amount/1000f).ToString("0.#",CultureInfo.InvariantCulture),"30");return true;
            case "campersinkfill":value=(amount<0?0f:Mathf.Clamp01((float)amount/RebirthCamperSink.CapacityMl)).ToString("0.####",CultureInfo.InvariantCulture);return true;
        }
        return base.GetBindingValueInternal(ref value,bindingName);
    }
}
