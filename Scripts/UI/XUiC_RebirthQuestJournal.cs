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
    public override void Update(float dt) {
        base.Update(dt);
        var q=CurrentQuest;
        if(q==lastQuest && Time.realtimeSinceStartup<nextRefresh)return;
        lastQuest=q;nextRefresh=Time.realtimeSinceStartup+.25f;
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
