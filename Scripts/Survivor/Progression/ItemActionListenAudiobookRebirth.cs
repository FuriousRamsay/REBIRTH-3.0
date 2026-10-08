#nullable disable

/// <summary>
/// Inventory and held-item action assigned to audiobook cassettes in survivor items.xml.
/// Transfers to the authoritative Walkman audiobook slot; successful custody commit starts pending listening.
/// Does not consume the tape; ownership moves from carried inventory to its stored slot.
/// </summary>
public sealed class ItemActionListenAudiobookRebirth : ItemActionEat
{
    public override void ReadFrom(DynamicProperties properties)
    {
        base.ReadFrom(properties);
        // Animation completion can call the inherited consume method directly.
        // Listening starts a reusable session; it must never eat the cassette.
        Consume=false;
        UseAnimation=false;
    }

    public override bool ExecuteInstantAction(EntityAlive ent, ItemStack stack, bool isHeldItem, XUiC_ItemStack stackController)
    {
        EntityPlayer player=ent as EntityPlayer;
        if(RebirthBackpackLibraryReservation.IsHeld(player as EntityPlayerLocal))return false;
        if(player==null||stack==null||stack.IsEmpty()||stack.itemValue==null)return false;
        Dispatch(player,stack.itemValue);
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
        var local=player as EntityPlayerLocal;
        if(local?.world==null||itemValue==null)return;
        if(RebirthBackpackLibraryReservation.IsHeld(local))
        {RebirthSurvivorSupportUiFeedback.Receive(false,Localization.Get("xuiRebirthLibraryTransferPending"));return;}
        if(!RebirthAudiobookLibraryClient.RequestUse(local,itemValue))
        {
            RebirthMusicLibraryClient.Dispatch(local,0);
            RebirthSurvivorSupportUiFeedback.Receive(false,Localization.Get("xuiRebirthMusicRefresh"));
        }
    }
}