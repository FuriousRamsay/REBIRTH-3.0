#nullable disable
public sealed class ItemActionUseRageCapsuleRebirth : ItemActionEat
{
    public override void ReadFrom(DynamicProperties properties)
    {
        base.ReadFrom(properties);
        // The REBIRTH service owns item transfer/consumption. Native animation
        // completion must not independently consume or apply this item's effects.
        Consume=false;
        UseAnimation=false;
    }

    public override bool ExecuteInstantAction(EntityAlive ent,ItemStack stack,bool isHeldItem,XUiC_ItemStack stackController){EntityPlayer p=ent as EntityPlayer;if(p==null||stack==null||stack.IsEmpty())return false;Dispatch(p,stack.itemValue);return true;}
    public override void OnHoldingUpdate(ItemActionData actionData){if(actionData==null||actionData.invData==null||actionData.invData.itemStack==null)return;if(PercentDone(actionData)<1f)return;ItemActionEat.MyInventoryData d=actionData as ItemActionEat.MyInventoryData;if(d==null||!d.bEatingStarted)return;d.bEatingStarted=false;EntityPlayer p=actionData.invData.holdingEntity as EntityPlayer;if(p!=null)Dispatch(p,actionData.invData.itemStack.itemValue);}
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
        ConnectionManager c=SingletonMonoBehaviour<ConnectionManager>.Instance;
        if(player.world==null)
        {RebirthSurvivorSupportUiFeedback.Receive(false,Localization.Get("xuiRebirthRageCapsuleUnavailable"));return;}
        if(!player.world.IsRemote())
        {
            if(c==null||!c.IsServer)
            {RebirthSurvivorSupportUiFeedback.Receive(false,Localization.Get("xuiRebirthRageCapsuleUnavailable"));return;}
            string message;bool ok=RebirthRageCapsuleService.Consume(player,itemValue.type,itemValue.Seed,out message);
            RebirthSurvivorSupportUiFeedback.Receive(ok,message);
            return;
        }
        var request=NetPackageManager.GetPackage<NetPackageRebirthSurvivorSupportActionRequest>();
        if(!(player is EntityPlayerLocal)||!RebirthMusicLibraryClient.CanSend(c,request))
        {RebirthSurvivorSupportUiFeedback.Receive(false,Localization.Get("xuiRebirthRageCapsuleUnavailable"));return;}
        c.SendToServer(request.Setup(player.entityId,RebirthSurvivorSupportAction.UseRageCapsule,itemValue,string.Empty));
    }
}
