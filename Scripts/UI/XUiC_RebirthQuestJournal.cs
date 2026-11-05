using UnityEngine;
using UnityEngine.Scripting;

[Preserve] public sealed class XUiC_RebirthQuestChrome : XUiC_RebirthScreenChrome {
 protected override RebirthCraftingNavigationService.Destination Destination => RebirthCraftingNavigationService.Destination.Quests;
}

[Preserve]
public sealed class XUiC_RebirthQuestBriefing : XUiC_QuestDescriptionWindow
{
    private float nextRefresh;
    private Quest lastQuest;
    private XUiV_Texture poiPreview;
    private Texture poiCropped;
    private Quest poiQuest;
    private string poiUri=string.Empty,poiBoundUri=string.Empty,poiPlaceholder="rb_preview_missing";
    public override void Init() {
        base.Init();
        poiPreview=GetChildById("questPoiPreview")?.ViewComponent as XUiV_Texture;
    }
    public override void Cleanup() {
        XUiC_RebirthTraderJobCard.DetachPreview(poiPreview);
        base.Cleanup();
    }
    private void ResolvePoiPreview() {
        var q=CurrentQuest;
        if(ReferenceEquals(poiQuest,q)&&poiUri.Length>0)return;
        poiQuest=q;
        poiPlaceholder="rb_preview_missing";
        poiUri=q!=null?XUiC_RebirthTraderJobCard.PoiPreviewUri(q,out poiPlaceholder):string.Empty;
    }
    public override bool GetBindingValueInternal(ref string value,string bindingName) {
        switch(bindingName) {
            case "questpoipreviewuri":
                ResolvePoiPreview();
                // The binding owns loading, but this window owns disposing the replaced JPEG.
                if(poiBoundUri!=poiUri){poiBoundUri=poiUri;poiCropped=null;XUiC_RebirthTraderJobCard.DetachPreview(poiPreview);}
                value=poiUri;return true;
            case "questpoipreviewvisible":ResolvePoiPreview();value=(poiUri.Length>0).ToString();return true;
            case "questpoiplaceholdervisible":ResolvePoiPreview();value=(poiUri.Length==0).ToString();return true;
            case "questpoiplaceholdersprite":ResolvePoiPreview();value=poiPlaceholder;return true;
        }
        return base.GetBindingValueInternal(ref value,bindingName);
    }
    public override void Update(float dt) {
        base.Update(dt);
        var q=CurrentQuest;
        var loaded=poiPreview?.Texture;
        if(loaded!=null&&!ReferenceEquals(poiCropped,loaded)){poiCropped=loaded;XUiC_RebirthTraderJobCard.CropPreview(poiPreview,loaded);}
        if(q==lastQuest && Time.realtimeSinceStartup<nextRefresh)return;
        lastQuest=q;nextRefresh=Time.realtimeSinceStartup+.25f;
        // A quest's location can arrive after it is first shown.
        if(q!=null&&poiUri.Length==0){string missing=poiUri;ResolvePoiPreview();if(poiUri!=missing)RefreshBindings();}
        string state="Select a quest", tier="—", distance="—", tracking="—";
        if(q!=null){
            switch(q.CurrentState){
                case Quest.QuestState.NotStarted:state="Not started";break;
                case Quest.QuestState.InProgress:state="In progress";break;
                case Quest.QuestState.ReadyForTurnIn:state="Ready to turn in";break;
                case Quest.QuestState.Completed:state="Completed";break;
                case Quest.QuestState.Failed:state="Failed";break;
            }
            tier=q.QuestClass.DifficultyTier.ToString();
            tracking=q.Tracked ? "Tracked on HUD" : "Not tracked";
            if(q.HasPosition){var delta=q.Position-xui.playerUI.entityPlayer.GetPosition();delta.y=0;
                distance=delta.magnitude>=1000 ? (delta.magnitude/1000).ToString("0.0")+" km" : Mathf.RoundToInt(delta.magnitude)+" m";}
            else distance="No location";
        }
        Set("questSummaryStatus",state);Set("questSummaryTier",tier);Set("questSummaryDistance",distance);Set("questSummaryTracking",tracking);
    }
    private void Set(string id,string value)=>(GetChildById(id)?.ViewComponent as XUiV_Label)?.SetTextImmediately(value);
}
