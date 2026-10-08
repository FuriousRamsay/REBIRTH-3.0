using System;
using HarmonyLib;

#nullable disable

/// <summary>
/// PE-09 read-only bridge from REBIRTH-owned lock explanations into the Progression Explorer.
/// It never grants Knowledge/Skill and never changes Capability evaluation. Callers provide the
/// blocked target and the exact missing requirement already produced by authoritative runtime logic.
/// </summary>
public static class RebirthProgressionLockCrossLinkService
{
    public static string RequirementFocusId(RebirthCapabilityRequirementEvaluation requirement,string fallbackId)
    {
        if(requirement!=null&&!string.IsNullOrEmpty(requirement.Id))
        {
            if(string.Equals(requirement.Kind,RebirthCapabilityKinds.Knowledge,StringComparison.OrdinalIgnoreCase))return requirement.Id;
            if(string.Equals(requirement.Kind,RebirthCapabilityKinds.Skill,StringComparison.OrdinalIgnoreCase))return requirement.Id;
        }
        return fallbackId??string.Empty;
    }

    public static bool TryGetRecipeLock(EntityPlayer player,string recipeName,out string recipeNodeId,out string focusId,out string reason)
    {
        recipeName=(recipeName??string.Empty).Trim();recipeNodeId=RebirthProgressionGraphRegistry.RecipeNodeId(recipeName);focusId=recipeNodeId;reason=string.Empty;
        if(player==null||recipeName.Length==0||!RebirthSurvivorMode.IsEnabledForCurrentWorld())return false;
        RebirthCapabilityEvaluation evaluation=RebirthCapabilityService.EvaluateRecipe(player,recipeName);
        if(evaluation==null||evaluation.IsAllowed)return false;
        RebirthCapabilityRequirementEvaluation missing=evaluation.MissingHardRequirements!=null&&evaluation.MissingHardRequirements.Count>0?evaluation.MissingHardRequirements[0]:null;
        focusId=RequirementFocusId(missing,recipeNodeId);reason=missing!=null&&!string.IsNullOrEmpty(missing.Message)?missing.Message:evaluation.FirstMissingReason;
        if(string.IsNullOrEmpty(reason))reason="Missing progression requirement";
        return true;
    }

    public static bool Open(XUi xui,string focusId,string launchReason,string callerWindowGroupId,string returnToken,string returnLabel,out string error)
    {
        error=string.Empty;if(xui==null){error="XUi unavailable.";return false;}
        var context=new RebirthProgressionExplorerReturnContext(callerWindowGroupId,returnToken,returnLabel);
        var request=new RebirthProgressionExplorerLaunchRequest(focusId,RebirthProgressionExplorerMode.LiveCharacter,launchReason,context);
        return RebirthProgressionExplorerUiService.Open(xui,request,out error);
    }
}

/// <summary>
/// PE-09 child controller appended to the native craftingInfoPanel. It inspects the recipe already
/// selected by XUiC_CraftingInfoWindow and only becomes visible when REBIRTH Capability logic is the
/// reason that recipe is locked. The native crafting controller remains untouched.
/// </summary>
public sealed class XUiC_RebirthRecipeProgressionCrossLink : XUiController
{
    private static readonly System.Reflection.FieldInfo RecipeField=AccessTools.Field(typeof(XUiC_CraftingInfoWindow),"recipe");
    private XUiV_Label reasonLabel;
    private XUiController exploreButton;
    private XUiC_CraftingInfoWindow craftingInfo;
    private string recipeName=string.Empty,focusId=string.Empty,reason=string.Empty;
    private float nextRefresh;

    public override void Init()
    {
        base.Init();reasonLabel=GetChildById("rebirthRecipeProgressionLockReason")?.ViewComponent as XUiV_Label;exploreButton=GetChildById("btnRebirthRecipeProgressionExplore");
        if(exploreButton!=null)exploreButton.OnPress+=Explore_OnPressed;
        XUiController p=Parent;while(p!=null&&craftingInfo==null){craftingInfo=p as XUiC_CraftingInfoWindow;p=p.Parent;}
        SetVisible(false);
    }

    public override void Update(float _dt)
    {
        base.Update(_dt);if(Time.time<nextRefresh)return;nextRefresh=Time.time+0.12f;RefreshLockState();
    }

    private void RefreshLockState()
    {
        EntityPlayerLocal player=xui!=null&&xui.playerUI!=null?xui.playerUI.entityPlayer:null;
        Recipe recipe=null;try{if(craftingInfo!=null&&RecipeField!=null)recipe=RecipeField.GetValue(craftingInfo) as Recipe;}catch{}
        if(player==null||recipe==null){Clear();return;}
        string name=string.Empty;try{name=recipe.GetName()??string.Empty;}catch{}
        string recipeNode,newFocus,newReason;
        if(!RebirthProgressionLockCrossLinkService.TryGetRecipeLock(player,name,out recipeNode,out newFocus,out newReason)){Clear();return;}
        recipeName=name;focusId=newFocus;reason=newReason;
        if(reasonLabel!=null)reasonLabel.Text=Localization.Get("xuiRebirthProgressionRecipeLockedBy")+" "+reason;
        SetVisible(true);
    }

    private void Explore_OnPressed(XUiController sender,int mouseButton)
    {
        if(string.IsNullOrEmpty(focusId)||xui==null||xui.playerUI==null||xui.playerUI.windowManager==null)return;
        string token="recipe-lock:"+Guid.NewGuid().ToString("N");
        string launch=Localization.Get("xuiRebirthProgressionRecipeLaunchReason")+" "+recipeName+" — "+reason;
        // Keep the crafting window open underneath the Explorer. XUiWindowGroup does not expose
        // a public group ID in this runtime, and retaining the caller is both safer and preserves
        // the exact selected recipe/workstation context automatically on return.
        string error;if(!RebirthProgressionLockCrossLinkService.Open(xui,focusId,launch,string.Empty,token,Localization.Get("xuiRebirthProgressionExplorerReturnCrafting"),out error))
        {
            Log.Error("[REBIRTH Progression Explorer][PE-09] recipe cross-link launch failed recipe="+recipeName+" error="+error);
        }
    }

    private void Clear(){recipeName=string.Empty;focusId=string.Empty;reason=string.Empty;if(reasonLabel!=null)reasonLabel.Text=string.Empty;SetVisible(false);}
    private void SetVisible(bool value){if(ViewComponent!=null)ViewComponent.IsVisible=value;}
}
