#nullable disable

/// <summary>Server-confirmed equip action for lightweight REBIRTH Survivor gear slots.</summary>
public sealed class ItemActionEquipSurvivorGearRebirth : ItemActionEat
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
        EntityPlayer player=ent as EntityPlayer; if(player==null||stack==null||stack.IsEmpty())return false; if(stackController!=null&&(stackController.StackLocation==XUiC_ItemStack.StackLocationTypes.Backpack||stackController.StackLocation==XUiC_ItemStack.StackLocationTypes.ToolBelt))DispatchSelected(player,stack.itemValue,stackController.StackLocation==XUiC_ItemStack.StackLocationTypes.Backpack,stackController.SlotNumber);else if(isHeldItem&&player.inventory!=null)DispatchSelected(player,stack.itemValue,false,player.inventory.holdingItemIdx);else Dispatch(player,stack.itemValue); return true;
    }
    public override void OnHoldingUpdate(ItemActionData actionData)
    {
        if(actionData==null||actionData.invData==null||actionData.invData.itemStack==null)return;
        ItemActionEat.MyInventoryData data=actionData as ItemActionEat.MyInventoryData; if(data==null||!data.bEatingStarted)return;
        data.bEatingStarted=false; EntityPlayer player=actionData.invData.holdingEntity as EntityPlayer; if(player!=null)DispatchSelected(player,actionData.invData.itemStack.itemValue,false,actionData.invData.slotIdx);
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
    internal static void DispatchSelected(EntityPlayer player,ItemValue selected,bool bag,int index)
    {
        if(player?.world==null||!player.world.IsRemote()){Dispatch(player,selected);return;}
        bool admitted=player is EntityPlayerLocal local&&
            (local.PlayerUI?.xui?.IsUsingItemActionEntryUse==true
                ? RebirthGearDeferredInitialDispatcher.TryQueue(local,selected,bag,index)
                : RebirthGearInitialEquipDispatcher.TryBegin(local,selected,bag,index));
        if(!admitted)RebirthSurvivorSupportUiFeedback.Receive(false,Localization.Get("xuiRebirthSupportUnavailable"));
    }
    public static void Dispatch(EntityPlayer player,ItemValue itemValue)
    {
        if(player==null||itemValue==null)return;
        ConnectionManager c=SingletonMonoBehaviour<ConnectionManager>.Instance;
        if(player.world==null)
        {RebirthSurvivorSupportUiFeedback.Receive(false,Localization.Get("xuiRebirthSupportUnavailable"));return;}
        if(!player.world.IsRemote())
        {
            if(c==null||!c.IsServer)
            {RebirthSurvivorSupportUiFeedback.Receive(false,Localization.Get("xuiRebirthSupportUnavailable"));return;}
            string message;bool ok=RebirthSurvivorGearService.TryEquipMatchingInventoryItem(player,itemValue.type,itemValue.Seed,out message);
            RebirthSurvivorSupportUiFeedback.Receive(ok,message);
            return;
        }
        try
        {
            var belt=player.inventory?.ItemGrid?.items;
            if(player is EntityPlayerLocal local&&belt!=null&&
                RebirthGearInventorySnapshot.TryCapture(player.bag?.ItemGrid?.items,belt,
                    RebirthToolbeltCapacity.GetOwnedSlotCount(local,belt.Length),out var snapshot)&&
                RebirthGearUniqueSource.TryFind(snapshot,RebirthNativeItemCodec.Encode(itemValue),out var bag,out var index))
            {DispatchSelected(local,itemValue,bag,index);return;}
        }
        catch { } // Missing or ambiguous original source never falls back to type/seed.
        RebirthSurvivorSupportUiFeedback.Receive(false,Localization.Get("xuiRebirthSupportUnavailable"));
    }
}