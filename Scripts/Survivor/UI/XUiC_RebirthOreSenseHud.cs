using UnityEngine;

#nullable disable

public sealed class XUiC_RebirthOreSenseHud : XUiController
{
    private const float RefreshInterval=.10f;private float timer;private string position="0,-10000",status=string.Empty,detail=string.Empty,accent="194,157,83,255";
    public override void Update(float dt)
    {
        timer+=dt;if(timer<RefreshInterval)return;timer=0f;base.Update(dt);
        string previousPosition=position,previousStatus=status,previousDetail=detail,previousAccent=accent;
        Refresh();
        if(IsDirty||position!=previousPosition||status!=previousStatus||detail!=previousDetail||accent!=previousAccent)
            RefreshBindings();
    }
    public override void OnOpen(){base.OnOpen();Refresh();IsDirty=true;RefreshBindings();}
    private void Refresh(){if(!RebirthOreSenseService.Active){position="0,-10000";status=string.Empty;detail=string.Empty;return;}position="0,0";status=Localization.Get("xuiRebirthOreSenseHudTitle");if(string.IsNullOrEmpty(status)||status=="xuiRebirthOreSenseHudTitle")status="ORE SENSE";detail=string.IsNullOrEmpty(RebirthOreSenseService.NearestType)?"Scanning...  "+RebirthOreSenseService.CachedCount+" signatures":RebirthOreSenseService.NearestType+"  "+RebirthOreSenseService.NearestDistance.ToString("0.0")+"m";}
    public override bool GetBindingValueInternal(ref string value,string bindingName){switch(bindingName){case "rebirth_ore_position":value=position;return true;case "rebirth_ore_status":value=status;return true;case "rebirth_ore_detail":value=detail;return true;case "rebirth_ore_accent":value=accent;return true;default:return base.GetBindingValueInternal(ref value,bindingName);}}
}
