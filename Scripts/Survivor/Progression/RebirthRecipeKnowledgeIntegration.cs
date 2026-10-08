using System;

#nullable disable

public static class RebirthRecipeKnowledgeIntegration
{
    public static void ApplyPresentationGate(Recipe recipe,EntityPlayer player,ref bool unlocked)
    {
        if(recipe==null || player==null || !RebirthSurvivorMode.IsEnabledForCurrentWorld())return;
        string required;
        if(!RebirthKnowledgeService.CanCraft(player,recipe.GetName(),out required))unlocked=false;
    }

    /// <summary>
    /// Definitive workstation-side gate. A modified client can inject a queue entry, but the server
    /// refuses to advance/output any mapped recipe unless StartingEntityId owns the required Knowledge.
    /// </summary>
    [Obsolete("Dormant legacy queue gate. Live workstation authority is RebirthRecipeCapabilityIntegration.AuthorizeActiveQueue; do not reuse without conservation/offline-owner review.")]
    public static bool AuthorizeActiveQueue(TileEntityWorkstation workstation)
    {
        if(workstation==null || !RebirthWorldCharacterRepository.IsServerAuthority || !RebirthSurvivorMode.IsEnabledForCurrentWorld())return true;
        RecipeQueueItem[] queue=workstation.Queue;
        if(queue==null || queue.Length==0)return true;
        RecipeQueueItem item=queue[queue.Length-1];
        if(item==null || item.Recipe==null || item.Multiplier<=0)return true;
        World world=GameManager.Instance!=null?GameManager.Instance.World:null;
        string recipeName=item.Recipe.GetName();
        EntityPlayer player=world!=null?world.GetEntity(item.StartingEntityId) as EntityPlayer:null;
        if(player==null)
        {
            RejectActiveQueue(workstation,item,recipeName,"invalid-crafter");
            return false;
        }
        string required;
        if(RebirthKnowledgeService.CanCraft(player,recipeName,out required))return true;

        // Fail closed and remove only the active unauthorized entry. We do not refund resources for
        // maliciously injected queues; legitimate clients were blocked before queueing.
        RejectActiveQueue(workstation,item,recipeName,"missing-knowledge:"+required);
        return false;
    }

    private static void RejectActiveQueue(TileEntityWorkstation workstation,RecipeQueueItem item,string recipeName,string reason)
    {
        int entityId=item!=null?item.StartingEntityId:-1;
        if(item!=null)
        {
            item.Multiplier=0;
            item.IsCrafting=false;
            item.CraftingTimeLeft=0f;
        }
        workstation.cycleRecipeQueue();
        workstation.setModified();
        Log.Warning("[REBIRTH Survivor] blocked workstation recipe='"+(recipeName??string.Empty)+"' entity="+entityId+" reason="+(reason??string.Empty));
    }

    public static void OnWorkstationCraftComplete(int crafterEntityId,string recipeName,int craftedCount)
    {
        if(!RebirthWorldCharacterRepository.IsServerAuthority || !RebirthSurvivorMode.IsEnabledForCurrentWorld())return;
        World world=GameManager.Instance!=null?GameManager.Instance.World:null;
        EntityPlayer player=world!=null?world.GetEntity(crafterEntityId) as EntityPlayer:null;
        if(player==null)return;
        RebirthSkillEventRouter.OnCraftOutputCompleted(player,recipeName,craftedCount);
    }
}
