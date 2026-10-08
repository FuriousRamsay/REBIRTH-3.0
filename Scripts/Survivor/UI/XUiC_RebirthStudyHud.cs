using UnityEngine;

#nullable disable

/// <summary>
/// Compact read/listen HUD shown only while a timed study session is active.
/// It is presentation-only; all progress remains owned by the authoritative study services.
/// </summary>
public sealed class XUiC_RebirthStudyHud : XUiController
{
    private const float RefreshInterval=0.10f;

    private EntityPlayerLocal player;
    private float accumulator;
    private string title=string.Empty;
    private string status=string.Empty;
    private string time=string.Empty;
    private string icon="ui_game_symbol_book";
    private string accent="181,140,255,255";
    private string position="0,-10000";
    private string fill="0";

    public override void OnOpen()
    {
        base.OnOpen();
        player=xui!=null&&xui.playerUI!=null?xui.playerUI.entityPlayer:null;
        RefreshState();
        IsDirty=true;
        RefreshBindings();
    }

    public override void Update(float dt)
    {
        accumulator+=dt;
        if(accumulator<RefreshInterval)return;
        accumulator=0f;
        base.Update(dt);
        // The local entity may be replaced while this HUD controller survives a respawn.
        player=xui!=null&&xui.playerUI!=null?xui.playerUI.entityPlayer:null;
        string oldTitle=title,oldStatus=status,oldTime=time,oldIcon=icon;
        string oldAccent=accent,oldPosition=position,oldFill=fill;
        RefreshState();
        if(title!=oldTitle||status!=oldStatus||time!=oldTime||icon!=oldIcon
            ||accent!=oldAccent||position!=oldPosition||fill!=oldFill)
            RefreshBindings();
    }

    private void RefreshState()
    {
        title=string.Empty;
        status=string.Empty;
        time=string.Empty;
        icon="ui_game_symbol_book";
        accent="181,140,255,255";
        position="0,-10000";
        fill="0";

        if(player==null)return;

        ConnectionManager connection=SingletonMonoBehaviour<ConnectionManager>.Instance;
        bool remoteClient=connection!=null&&connection.IsClient&&!connection.IsServer;

        if(remoteClient)
        {
            RebirthStudyHudMode mode;
            string itemId;
            float progress,remaining;
            bool slow;
            if(!RebirthStudyHudClientState.TryGet(out mode,out itemId,out progress,out remaining,out slow))
                return;

            if(mode==RebirthStudyHudMode.Reading)
                ApplyReading(itemId,progress,remaining,slow);
            else if(mode==RebirthStudyHudMode.Audiobook)
                ApplyAudiobook(itemId,progress,remaining);
            return;
        }

        string physicalItemId;
        float physicalProgress,physicalRemaining;
        bool physicalSlow;
        if(RebirthLiteratureStudySessionService.TryGetUiState(player,out physicalItemId,out physicalProgress,out physicalRemaining,out physicalSlow))
        {
            ApplyReading(physicalItemId,physicalProgress,physicalRemaining,physicalSlow);
            return;
        }

        string audioItemId,sourceId;
        float audioProgress,audioRemaining;
        if(RebirthAudiobookListeningSessionService.TryGetUiState(player,out audioItemId,out sourceId,out audioProgress,out audioRemaining))
            ApplyAudiobook(sourceId,audioProgress,audioRemaining);
    }

    private void ApplyReading(string itemId,float progress,float remaining,bool slow)
    {
        title=Friendly(itemId);
        status=Localization.Get("xuiRebirthStudyHudReading")+"  •  "
            +(slow?Localization.Get("xuiRebirthStudyHudHalfAttention"):Localization.Get("xuiRebirthStudyHudFullAttention"));
        time=FormatTime(remaining)+"  •  "+Mathf.RoundToInt(progress*100f)+"%";
        icon="ui_game_symbol_book";
        accent=slow?"214,201,120,255":"181,140,255,255";
        position="0,0";
        fill=Mathf.Clamp01(progress).ToString("0.###",System.Globalization.CultureInfo.InvariantCulture);
    }

    private void ApplyAudiobook(string sourceId,float progress,float remaining)
    {
        title=Friendly(sourceId);
        status=Localization.Get("xuiRebirthStudyHudListening")+"  •  "+Localization.Get("xuiRebirthStudyHudHandsFree");
        time=FormatTime(remaining)+"  •  "+Mathf.RoundToInt(progress*100f)+"%";
        icon="ui_game_symbol_speaker";
        accent="181,140,255,255";
        position="0,0";
        fill=Mathf.Clamp01(progress).ToString("0.###",System.Globalization.CultureInfo.InvariantCulture);
    }

    public override bool GetBindingValueInternal(ref string value,string bindingName)
    {
        switch(bindingName)
        {
            case "rebirth_study_title": value=title; return true;
            case "rebirth_study_status": value=status; return true;
            case "rebirth_study_time": value=time; return true;
            case "rebirth_study_icon": value=icon; return true;
            case "rebirth_study_accent": value=accent; return true;
            case "rebirth_study_position": value=position; return true;
            case "rebirth_study_fill": value=fill; return true;
            default: return base.GetBindingValueInternal(ref value,bindingName);
        }
    }

    private static string Friendly(string itemId)
    {
        if(string.IsNullOrEmpty(itemId))return string.Empty;
        string localized=Localization.Get(itemId);
        return !string.IsNullOrEmpty(localized)&&!string.Equals(localized,itemId,System.StringComparison.OrdinalIgnoreCase)
            ? localized : itemId;
    }

    private static string FormatTime(float seconds)
    {
        int total=Mathf.Max(0,Mathf.CeilToInt(seconds));
        return (total/60).ToString("00")+":"+(total%60).ToString("00");
    }
}
