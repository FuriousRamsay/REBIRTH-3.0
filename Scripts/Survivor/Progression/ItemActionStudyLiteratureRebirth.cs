#nullable disable

/// <summary>
/// Inventory action for reusable books/manuals/schematics/recipe cards. It intentionally does not
/// call ItemActionEat's consumption path. Completion only asks the authoritative server to apply
/// the literature's authored learning effect; the physical ItemStack remains unchanged.
/// </summary>
public sealed class ItemActionStudyLiteratureRebirth : ItemActionEat
{
    public override void ReadFrom(DynamicProperties properties)
    {
        base.ReadFrom(properties);
        // Native animation completion can call ItemActionEat.Completed directly.
        // Reusable literature must never enter that consumption path.
        Consume=false;
        UseAnimation=false;
    }

    public override bool ExecuteInstantAction(EntityAlive ent, ItemStack stack, bool isHeldItem, XUiC_ItemStack stackController)
    {
        EntityPlayer player=ent as EntityPlayer;
        if(RebirthBackpackLibraryReservation.IsHeld(player as EntityPlayerLocal))return false;
        if(player==null||stack==null||stack.IsEmpty()||stack.itemValue==null)return false;
        if(stackController is XUiC_Creative2Stack&&
            !RebirthLiteratureService.HasMatchingInventoryItem(player,stack.itemValue.type,stack.itemValue.Seed))
        {
            // Creative's catalogue entry is not an owned stack. Keep one reusable copy,
            // exactly as Take does, before requesting the normal validated read.
            var copy=new ItemStack(stack.itemValue.Clone(),1);
            if(!stackController.xui.PlayerInventory.AddItem(copy,true))
            {RebirthSurvivorSupportUiFeedback.Receive(false,"Make room in your backpack for the reusable literature item.");return true;}
            if(player is EntityPlayerLocal local&&player.world.IsRemote())GameManager.Instance.doSendLocalInventory(local);
        }
        Dispatch(player,stack.itemValue);
        if(stackController!=null)stackController.IsDirty=true;
        return true;
    }

    public override void OnHoldingUpdate(ItemActionData actionData)
    {
        if(actionData==null||actionData.invData==null||actionData.invData.itemStack==null)return;
        ItemActionEat.MyInventoryData data=actionData as ItemActionEat.MyInventoryData;
        if(data==null||!data.bEatingStarted)return;
        data.bEatingStarted=false;
        EntityPlayer player=actionData.invData.holdingEntity as EntityPlayer;
        if(player!=null)Dispatch(player,actionData.invData.itemStack.itemValue);
    }

    public override void StopHolding(ItemActionData data)
    {
        ItemActionEat.MyInventoryData eat = data as ItemActionEat.MyInventoryData;
        if (eat == null) return;
        // Cancel pending use even when teardown has already detached the holder.
        eat.bEatingStarted = false;
        if (data.invData == null || data.invData.holdingEntity == null) return;
        base.StopHolding(data);
    }

    internal static void Dispatch(EntityPlayer player,ItemValue itemValue)
    {
        if(player==null||itemValue==null)return;
        if(RebirthBackpackLibraryReservation.IsHeld(player as EntityPlayerLocal))
        {RebirthSurvivorSupportUiFeedback.Receive(false,Localization.Get("xuiRebirthLibraryTransferPending"));return;}
        ConnectionManager c=SingletonMonoBehaviour<ConnectionManager>.Instance;
        if(player.world==null)
        {RebirthSurvivorSupportUiFeedback.Receive(false,Localization.Get("xuiRebirthStudyUnavailable"));return;}
        if(!player.world.IsRemote())
        {
            if(c==null||!c.IsServer)
            {RebirthSurvivorSupportUiFeedback.Receive(false,Localization.Get("xuiRebirthStudyUnavailable"));return;}
            RebirthLiteratureStudySessionService.BeginClientTracking(player,itemValue);
            string message;bool ok=RebirthLiteratureService.TryReadMatchingInventoryItem(player,itemValue.type,itemValue.Seed,out message);
            RebirthSurvivorSupportUiFeedback.Receive(ok,message);
            return;
        }
        try
        {
        var local=player as EntityPlayerLocal;
        var inventory=NetPackageManager.GetPackage<NetPackagePlayerInventory>();
        var request=NetPackageManager.GetPackage<NetPackageRebirthSurvivorSupportActionRequest>();
        if(local==null||!RebirthMusicLibraryClient.CanSend(c,inventory)||!RebirthMusicLibraryClient.CanSend(c,request))
        {RebirthSurvivorSupportUiFeedback.Receive(false,Localization.Get("xuiRebirthStudyUnavailable"));return;}
        RebirthLiteratureStudySessionService.BeginClientTracking(player,itemValue);
        c.SendToServer(inventory.Setup(local,true,true,false,false));
        c.SendToServer(request.Setup(player.entityId,RebirthSurvivorSupportAction.StudyLiterature,itemValue,string.Empty));
        }
        catch
        {RebirthSurvivorSupportUiFeedback.Receive(false,Localization.Get("xuiRebirthStudyUnavailable"));}
    }
}
