using System;
using System.Runtime.CompilerServices;

#nullable disable

public static class RebirthRecipeCapabilityIntegration
{
    private sealed class HeldQueue { public RecipeQueueItem Item; public string Reason; }
    private static readonly ConditionalWeakTable<TileEntityWorkstation,HeldQueue> HeldQueues =
        new ConditionalWeakTable<TileEntityWorkstation,HeldQueue>();
    public static void ApplyPresentationGate(Recipe recipe,EntityPlayer player,ref bool unlocked)
    {
        // Recipe.IsUnlocked is the native learnable flag only. Ready, explicitly owned
        // REBIRTH policy replaces that flag; native transaction/environment guards remain.
        bool resolved;
        if (RebirthOwnedRecipeUnlockResolution.TryResolve(recipe, player, out resolved)) unlocked = resolved;
    }

    /// <summary>
    /// Server-authoritative REBIRTH crafting-policy gate for workstation recipes. UNIVERSAL and
    /// external compatibility recipes pass natively; GATED/DISABLED recipes use the same unified
    /// evaluator as personal crafting. Personal/inventory admission is validated separately by
    /// RebirthPersonalCraftAuthorizationService at the native ItemActionEntryCraft.OnActivated
    /// transaction boundary.
    /// </summary>
    public static bool AuthorizeActiveQueue(TileEntityWorkstation workstation)
    {
        if(workstation==null)return true;
        if(!RebirthWorldCharacterRepository.IsServerAuthority||!RebirthSurvivorMode.IsEnabledForCurrentWorld()){HeldQueues.Remove(workstation);return true;}
        RecipeQueueItem[] queue=workstation.Queue;
        if(queue==null||queue.Length==0){HeldQueues.Remove(workstation);return true;}
        RecipeQueueItem item=queue[queue.Length-1];
        if(item==null||item.Recipe==null||item.Multiplier<=0){HeldQueues.Remove(workstation);return true;}
        if(!RebirthStationGridQueue.HasValidMultiplier(item.Recipe,item.Multiplier))
        {HoldActiveQueue(workstation,item,item.Recipe.GetName(),"station-batch-multiplier-invalid");return false;}
        if(!RebirthStationGridQueue.HasResolvedDefinition(item.Recipe))
        {HoldActiveQueue(workstation,item,item.Recipe.GetName(),"station-definition-unresolved");return false;}
        // Cooking cards are guidance. A concrete ingredient-grid batch does not require the card.
        if(RebirthCookingBatch.IsBatch(item.Recipe)){HeldQueues.Remove(workstation);return true;}
        World world=GameManager.Instance!=null?GameManager.Instance.World:null;
        string recipeName=item.Recipe.GetName();
        EntityPlayer player=world!=null?world.GetEntity(item.StartingEntityId) as EntityPlayer:null;
        if(player==null){HoldActiveQueue(workstation,item,recipeName,"crafter-unavailable");return false;}
        if(RebirthStationQueuedDiscoveryAuthorization.TryEvaluate(workstation,item,player,out var discoveryAllowed,out var discoveryReason))
        {
            if(discoveryAllowed){HeldQueues.Remove(workstation);return true;}
            HoldActiveQueue(workstation,item,recipeName,discoveryReason??"station-discovery-queued-authority");return false;
        }
        RebirthCapabilityEvaluation evaluation=RebirthCapabilityService.EvaluateRecipe(player,recipeName);
        if(evaluation.IsAllowed){HeldQueues.Remove(workstation);return true;}
        HoldActiveQueue(workstation,item,recipeName,"crafting-policy:"+evaluation.FirstMissingReason);
        return false;
    }

    private sealed class OpenQueueCheck
    {
        public Recipe Recipe; public object Viewer, World; public int Crafter;
        public float Until; public bool Allowed;
    }
    private static readonly ConditionalWeakTable<XUiC_RecipeStack,OpenQueueCheck> OpenQueueChecks =
        new ConditionalWeakTable<XUiC_RecipeStack,OpenQueueCheck>();
    public static bool AuthorizeOpenQueue(XUiC_RecipeStack stack,bool outputBoundary = false)
    {
        if (stack?.recipe == null || !(stack.windowGroup?.Controller is XUiC_WorkstationWindowGroup)) return true;
        if(!RebirthStationGridQueue.HasValidMultiplier(stack.recipe,stack.recipeCount))return false;
        if(!RebirthStationGridQueue.HasResolvedDefinition(stack.recipe))return false;
        var viewer = stack.xui?.playerUI?.entityPlayer;
        OpenQueueCheck check = OpenQueueChecks.GetValue(stack, _ => new OpenQueueCheck());
        float now = UnityEngine.Time.realtimeSinceStartup;
        // Progress presentation can wait at most a quarter second. Every output attempt,
        // including Update's inventory-full retry, always checks current authority afresh.
        if (!outputBoundary && ReferenceEquals(check.Recipe,stack.recipe) && ReferenceEquals(check.Viewer,viewer)
            && ReferenceEquals(check.World,viewer?.world) && check.Crafter == stack.StartingEntityId && now < check.Until)
            return check.Allowed;
        check.Recipe = stack.recipe; check.Viewer = viewer; check.World = viewer?.world;
        check.Crafter = stack.StartingEntityId; check.Until = now + .25f;
        check.Allowed = EvaluateOpenQueue(stack);
        return check.Allowed;
    }
    private static bool EvaluateOpenQueue(XUiC_RecipeStack stack)
    {
        if (stack?.recipe == null || !(stack.windowGroup?.Controller is XUiC_WorkstationWindowGroup)
            || !RebirthSurvivorMode.IsEnabledForCurrentWorld()) return true;
        // Cooking jobs use their existing registered-ticket/native-witness path.
        if (RebirthCookingBatch.IsBatch(stack.recipe)) return true;
        var viewer = stack.xui?.playerUI?.entityPlayer;
        if (viewer?.world == null) return false;
        EntityPlayer crafter;
        if (viewer.world.IsRemote())
        {
            // A client holds only its own authenticated progression snapshot.
            // Never substitute the station viewer's capability for another crafter's job.
            if (stack.StartingEntityId != viewer.entityId) return false;
            crafter = viewer;
        }
        else crafter = viewer.world.GetEntity(stack.StartingEntityId) as EntityPlayer;
        return crafter != null && RebirthCapabilityService.EvaluateRecipe(crafter,stack.recipe.GetName()).IsAllowed;
    }

    private static void HoldActiveQueue(TileEntityWorkstation workstation,RecipeQueueItem item,string recipeName,string reason)
    {
        // Native queue inputs are already paid. A missing online entity or current capability
        // failure does not prove malicious injection and must never destroy that custody.
        HeldQueue held = HeldQueues.GetValue(workstation, _ => new HeldQueue());
        if (ReferenceEquals(held.Item,item) && string.Equals(held.Reason,reason,StringComparison.Ordinal)) return;
        held.Item = item; held.Reason = reason;
        Log.Warning("[REBIRTH Survivor] held workstation recipe='"+(recipeName??string.Empty)+"' entity="+(item?.StartingEntityId??-1)+" reason="+(reason??string.Empty));
    }

    public static void OnWorkstationCraftComplete(int crafterEntityId,string recipeName,int craftedCount)
    {
        if(!RebirthWorldCharacterRepository.IsServerAuthority||!RebirthSurvivorMode.IsEnabledForCurrentWorld())return;
        World world=GameManager.Instance!=null?GameManager.Instance.World:null;
        EntityPlayer player=world!=null?world.GetEntity(crafterEntityId) as EntityPlayer:null;
        if(player==null)return;
        RebirthSkillEventRouter.OnCraftOutputCompleted(player,recipeName,craftedCount);
    }
}

