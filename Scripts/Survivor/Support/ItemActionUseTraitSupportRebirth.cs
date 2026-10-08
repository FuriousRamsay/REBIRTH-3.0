#nullable disable

/// <summary>
/// Uses ItemActionEat's input surface but commits support consumption only on the
/// authoritative server. This avoids client/server double-consumption while preserving the
/// item's authored onSelfPrimaryActionEnd effects on the server.
/// </summary>
public sealed class ItemActionUseTraitSupportRebirth : ItemActionEat
{
    public override void ReadFrom(DynamicProperties properties)
    {
        base.ReadFrom(properties);
        // The REBIRTH service owns item transfer/consumption. Native animation
        // completion must not independently consume or apply this item's effects.
        Consume=false;
        UseAnimation=false;
    }

    public override bool ExecuteInstantAction(EntityAlive ent, ItemStack stack, bool isHeldItem, XUiC_ItemStack stackController)
    {
        EntityPlayer player=ent as EntityPlayer;
        if(RebirthBackpackLibraryReservation.IsHeld(player as EntityPlayerLocal))return false;
        if(player==null||stack==null||stack.IsEmpty()) return false;
        Dispatch(player,stack.itemValue);
        return true;
    }

    public override void OnHoldingUpdate(ItemActionData actionData)
    {
        if(actionData==null||actionData.invData==null||actionData.invData.itemStack==null)return;
        if(PercentDone(actionData)<1f)return;
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

    private static void Dispatch(EntityPlayer player,ItemValue itemValue)
    {
        if(player==null||itemValue==null)return;
        if(RebirthBackpackLibraryReservation.IsHeld(player as EntityPlayerLocal))
        {RebirthSurvivorSupportUiFeedback.Receive(false,Localization.Get("xuiRebirthLibraryTransferPending"));return;}
        ConnectionManager c=SingletonMonoBehaviour<ConnectionManager>.Instance;
        if(player.world==null)
        {RebirthSurvivorSupportUiFeedback.Receive(false,Localization.Get("xuiRebirthSupportUnavailable"));return;}
        if(!player.world.IsRemote())
        {
            if(c==null||!c.IsServer)
            {RebirthSurvivorSupportUiFeedback.Receive(false,Localization.Get("xuiRebirthSupportUnavailable"));return;}
            string message;bool ok=RebirthTraitSupportService.ConsumeAndApplyMatchingInventoryItem(player,itemValue.type,itemValue.Seed,out message);
            RebirthSurvivorSupportUiFeedback.Receive(ok,message);
            return;
        }
        var request=NetPackageManager.GetPackage<NetPackageRebirthSurvivorSupportActionRequest>();
        if(!(player is EntityPlayerLocal)||!RebirthMusicLibraryClient.CanSend(c,request))
        {RebirthSurvivorSupportUiFeedback.Receive(false,Localization.Get("xuiRebirthSupportUnavailable"));return;}
        c.SendToServer(request.Setup(player.entityId,RebirthSurvivorSupportAction.ConsumeSupportItem,itemValue,string.Empty));
    }
}
