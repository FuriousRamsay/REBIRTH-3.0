#nullable disable

public sealed class ItemActionPlayMusicCassetteRebirth : ItemActionEat
{
    public override void ReadFrom(DynamicProperties properties)
    {
        base.ReadFrom(properties);
        // The REBIRTH service owns item transfer/consumption. Native animation
        // completion must not independently consume or apply this item's effects.
        Consume=false;
        UseAnimation=false;
    }

    public override bool ExecuteInstantAction(EntityAlive ent,ItemStack stack,bool isHeldItem,XUiC_ItemStack stackController)
    {
        EntityPlayerLocal player=ent as EntityPlayerLocal;
        if(player==null||stack==null||stack.IsEmpty()||stack.itemValue==null)return false;
        return RebirthMusicLibraryClient.RequestUse(player,stack.itemValue);
    }

    public override void OnHoldingUpdate(ItemActionData actionData)
    {
        if(actionData==null||actionData.invData==null||actionData.invData.itemStack==null)return;
        ItemActionEat.MyInventoryData data=actionData as ItemActionEat.MyInventoryData;
        if(data==null||!data.bEatingStarted)return;
        data.bEatingStarted=false;
        EntityPlayerLocal player=actionData.invData.holdingEntity as EntityPlayerLocal;
        if(player==null)return;
        RebirthMusicLibraryClient.RequestUse(player,actionData.invData.itemStack.itemValue);
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
}
