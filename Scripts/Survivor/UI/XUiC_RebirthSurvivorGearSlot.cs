using System;
using UnityEngine;
using UnityEngine.Scripting;

// Uses the existing authoritative gear transaction; inventory/cursor transfers stay native.
[Preserve]
public sealed class XUiC_RebirthSurvivorGearSlot : XUiController
{
    public string EquippedItemId = "";
    private string slotId, pendingPickup, pendingCreationId, pendingPickupTransaction;
    private ItemValue pendingEquip;
    private int pendingEquipIndex=-1;
    private EntityPlayer pendingOwner; private World pendingWorld; private object pendingSession;
    private float equipAt, pickupUntil;
    private float nextTooltipRefresh;
    private string tooltipItemId;
    public override bool ParseAttribute(string name, string value)
    {
        if(name=="gear_slot"){slotId=value;return true;}
        return base.ParseAttribute(name,value);
    }
    public override void Init()
    {
        base.Init();
        OnPress += (sender,button)=> { if(button!=0 && button!=-1)return; Interact(false); };
        OnDrag += (sender,type,delta)=> { if(type==EDragType.DragStart)Interact(true); };
    }
    private void Interact(bool dragging)
    {
        if(RebirthConsoleInputGuardRuntime.BlocksGameplayInput() || pendingEquip!=null || pendingPickup!=null)return;
        var cursor=xui?.DragAndDropWindow; var player=xui?.playerUI?.entityPlayer;
        if(cursor==null || player==null)return;
        string creation=ReadCreationId(player);
        if(!RebirthSurvivorRequestScope.Matches(creation,creation))return;
        var stack=cursor.CurrentStack;
        if(!stack.IsEmpty())
        {
            RebirthTraitSupportProfileDefinition profile;
            if(!(slotId==RebirthSurvivorGearService.SupportSlotId&&RebirthOverviewWaterRequest.Accepts(stack.itemValue))&&
                (!RebirthSurvivorDefinitionRegistry.TryGetSupportByGearItem(stack.itemValue.ItemClass.GetItemName(),out profile)||profile.GearSlotId!=slotId))return;
            // Put the exact cursor item back in a real owned slot before the server validates it.
            // Never manufacture an item from a drag payload or clear a cursor after a failed transfer.
            bool externalGear=player.world!=null&&player.world.IsRemote()&&
                !(slotId==RebirthSurvivorGearService.SupportSlotId&&RebirthOverviewWaterRequest.Accepts(stack.itemValue));
            RebirthGearInventorySnapshot before=null;
            var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;
            object peer=manager?.connectionToServer!=null&&manager.connectionToServer.Length>0?manager.connectionToServer[0]:null;
            var originalWorld=player.world;
            if(externalGear&&(!(player is EntityPlayerLocal)||peer==null||
                !RebirthGearInventorySnapshot.TryCapture(player.bag?.ItemGrid?.items,player.inventory?.ItemGrid?.items,
                    RebirthToolbeltCapacity.GetOwnedSlotCount(player,player.inventory?.ItemGrid?.items?.Length??0),out before)))return;
            var copy=stack.Clone();
            bool deposited=xui.PlayerInventory.AddItemToBackpack(copy);
            // Native AddItemToBackpack returns false when TryStackItem consumed
            // everything, and may also consume only part before failing to find
            // an empty slot. Its boolean alone does not describe what moved.
            if(!deposited && copy.count>0)
            {
                if(copy.count<stack.count)cursor.SetCurrentStack(copy, false);
                return;
            }
            cursor.SetCurrentStack(ItemStack.Empty.Clone(), false);
            int depositedIndex=-1;
            if(externalGear)
            {
                var currentManager=SingletonMonoBehaviour<ConnectionManager>.Instance;
                if(!ReferenceEquals(player,xui?.playerUI?.entityPlayer)||!ReferenceEquals(originalWorld,player.world)||
                    !ReferenceEquals(manager,currentManager)||currentManager?.connectionToServer==null||
                    currentManager.connectionToServer.Length==0||!ReferenceEquals(peer,currentManager.connectionToServer[0])||
                    !RebirthSurvivorRequestScope.Matches(creation,ReadCreationId(player))||
                    !RebirthGearInventorySnapshot.TryCapture(player.bag?.ItemGrid?.items,player.inventory?.ItemGrid?.items,
                        RebirthToolbeltCapacity.GetOwnedSlotCount(player,player.inventory?.ItemGrid?.items?.Length??0),out var after)||
                    !RebirthGearCursorDepositWitness.TryLocate(before,after,RebirthNativeItemCodec.Encode(stack.itemValue),stack.count,out depositedIndex))return;
            }
            pendingCreationId=creation;pendingOwner=player;pendingWorld=originalWorld;pendingSession=peer;
            pendingEquipIndex=depositedIndex;pendingEquip=stack.itemValue.Clone();
            equipAt=Time.realtimeSinceStartup+0.25f;
            return;
        }
        // Presentation refresh is throttled; never decide ownership from its cached label.
        EquippedItemId = ReadEquippedItem(player);
        bool quick=InputUtils.ShiftKeyPressed;
        if((!dragging&&!quick) || string.IsNullOrEmpty(EquippedItemId))return;
        if(slotId==RebirthSurvivorGearService.SupportSlotId&&!string.IsNullOrEmpty(RebirthOverviewWaterRequest.EquippedItem(player)))
        { RebirthOverviewWaterRequest.Dispatch(player,null,true);return; }
        if(!quick){pendingCreationId=creation;pendingPickup=EquippedItemId;pickupUntil=Time.realtimeSinceStartup+5f;}
        var connection=SingletonMonoBehaviour<ConnectionManager>.Instance;
        if(player.world==null || (!player.world.IsRemote() && (connection==null || !connection.IsServer)))
        {
            pendingPickup=null;
            RebirthSurvivorSupportUiFeedback.Receive(false,Localization.Get("xuiRebirthSupportUnavailable"));
            return;
        }
        if(!player.world.IsRemote())
        {
            string message;bool ok=RebirthSurvivorGearService.TryUnequip(player,slotId,out message,
                quick ? (Action<ItemStack>)null : returned=>cursor.SetCurrentStack(returned, false));
            pendingPickup=null;
            RebirthSurvivorSupportUiFeedback.Receive(ok,message);
        }
        else
        {
            if(!(player is EntityPlayerLocal local)||!RebirthGearInitialEquipDispatcher.TryBeginUnequip(local,slotId,out var transaction))
            {
                pendingPickup=null;
                RebirthSurvivorSupportUiFeedback.Receive(false,Localization.Get("xuiRebirthSupportUnavailable"));
            }
            else if(!quick){pendingPickupTransaction=transaction.ToString("N");pendingOwner=player;pendingWorld=player.world;
                pendingSession=connection?.connectionToServer!=null&&connection.connectionToServer.Length>0?connection.connectionToServer[0]:null;}
        }
    }
    private string ReadEquippedItem(EntityPlayer player)
    {
        if(slotId==RebirthSurvivorGearService.SupportSlotId){string water=RebirthOverviewWaterRequest.EquippedItem(player);if(!string.IsNullOrEmpty(water))return water;}
        if (RebirthWorldCharacterRepository.IsServerAuthority)
        {
            RebirthWorldCharacterRecord record; string item;
            if (RebirthWorldCharacterService.TryGet(player, out record) && record?.Support != null)
                return record.Support.EquippedGearBySlot.TryGetValue(slotId, out item) ? item : string.Empty;
        }
        else
            return RebirthSurvivorClientState.GetProjectedGearItem(player, slotId);
        return string.Empty;
    }
    private static string ReadCreationId(EntityPlayer player)
    {
        if(player==null)return string.Empty;
        if(!RebirthWorldCharacterRepository.IsServerAuthority)
            return RebirthSurvivorClientState.GetProjectedCreationId(player);
        RebirthWorldCharacterRecord record;
        return RebirthWorldCharacterService.TryGet(player,out record)&&record!=null&&record.IsComplete
            ? record.Origin?.CreationId : string.Empty;
    }
    public override void Update(float dt)
    {
        base.Update(dt);
        // Ownership changes update immediately; otherwise avoid constructing the same
        // localized tooltip every frame. Periodic refresh still permits language changes.
        if (tooltipItemId != EquippedItemId || Time.realtimeSinceStartup >= nextTooltipRefresh)
        {
            tooltipItemId = EquippedItemId;
            nextTooltipRefresh = Time.realtimeSinceStartup + .25f;
            bool support=slotId==RebirthSurvivorGearService.SupportSlotId;
            bool water=support&&!string.IsNullOrEmpty(RebirthOverviewWaterRequest.EquippedItem(xui?.playerUI?.entityPlayer));
            ViewComponent.ToolTip=string.IsNullOrEmpty(EquippedItemId)?Localization.Get(support?"xuiRebirthSupportDropHere":"xuiRebirthGearDropHere"):
                Localization.Get(EquippedItemId)+"\n"+Localization.Get(water?"xuiRebirthWaterSupportMoveHint":"xuiRebirthGearMoveHint");
        }
        if((pendingEquip!=null||pendingPickup!=null)
            && (!RebirthSurvivorRequestScope.Matches(pendingCreationId,ReadCreationId(xui?.playerUI?.entityPlayer))||
                ((pendingEquip!=null||pendingPickupTransaction!=null)&&(!ReferenceEquals(pendingOwner,xui?.playerUI?.entityPlayer)||!ReferenceEquals(pendingWorld,pendingOwner?.world)||
                    (pendingWorld!=null&&pendingWorld.IsRemote()&&!SamePendingPeer())))))
        {
            pendingEquip=null;pendingPickup=null;pendingCreationId=null;pendingPickupTransaction=null;
            return; // A staged cursor item has already been safely returned to the bag.
        }
        if(pendingEquip!=null && Time.realtimeSinceStartup>=equipAt)
        {var item=pendingEquip;pendingEquip=null;if(slotId==RebirthSurvivorGearService.SupportSlotId&&RebirthOverviewWaterRequest.Accepts(item))RebirthOverviewWaterRequest.Dispatch(xui.playerUI.entityPlayer,item);else if(pendingEquipIndex>=0)ItemActionEquipSurvivorGearRebirth.DispatchSelected(xui.playerUI.entityPlayer,item,true,pendingEquipIndex);else ItemActionEquipSurvivorGearRebirth.Dispatch(xui.playerUI.entityPlayer,item);}
        if(pendingPickup==null)return;
        var player=xui?.playerUI?.entityPlayer;
        if(player==null||!xui.DragAndDropWindow.CurrentStack.IsEmpty()||pendingPickupTransaction==null)
        {pendingPickup=null;pendingPickupTransaction=null;return;}
        if(RebirthGearOwnerReservation.IsHeld(player)||RebirthBackpackLibraryReservation.IsHeld(player))return;
        if(!RebirthGearOfferClient.TryGetSettledOriginal(player.world,player.entityId,pendingPickupTransaction,out var original))return;
        if(original.SlotId!=slotId||!original.TryGetPlan(out var plan)||
            !RebirthGearUnequipReturnSlot.TryFind(plan,out var returnedIndex,out var returnedData))
        {pendingPickup=null;pendingPickupTransaction=null;return;} // Exact gear may be in original recovery instead.
        var physical=player.bag?.ItemGrid?.items;
        if(physical==null||returnedIndex<0||returnedIndex>=physical.Length||physical[returnedIndex]==null||
            physical[returnedIndex].count!=1||RebirthNativeItemCodec.Encode(physical[returnedIndex].itemValue)!=returnedData)
        {pendingPickup=null;pendingPickupTransaction=null;return;} // Never search for a replacement by name.
        var owner=GetParentByType<XUiC_RebirthSurvivorCharacter>();
        var bag=owner?.GetChildByType<XUiC_RebirthCharacterBackpack>();
        if(bag==null)return;
        foreach(var cell in bag.GetItemStackControllers())
        {
            if(cell.SlotNumber!=returnedIndex||cell.ItemStack.IsEmpty()||cell.ItemStack.count!=1||cell.IsLocked||cell.StackLock||
                RebirthNativeItemCodec.Encode(cell.ItemStack.itemValue)!=returnedData)continue;
            cell.HandleStackSwap();pendingPickup=null;pendingPickupTransaction=null;break;
        }
    }
    private bool SamePendingPeer()
    {
        var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;
        return manager!=null&&!manager.IsServer&&manager.connectionToServer!=null&&manager.connectionToServer.Length>0&&
            ReferenceEquals(pendingSession,manager.connectionToServer[0])&&manager.connectionToServer[0]!=null&&
            !manager.connectionToServer[0].IsDisconnected();
    }
    public override void OnClose()
    {
        // A staged item remains safely in the backpack if the screen closes early.
        pendingEquip=null;pendingPickup=null;pendingCreationId=null;pendingPickupTransaction=null;base.OnClose();
    }
}

[HarmonyLib.HarmonyPatch(typeof(XUiC_ItemStack),nameof(XUiC_ItemStack.HandleMoveToPreferredLocation))]
public static class RebirthSurvivorGearQuickEquip
{
    [HarmonyLib.HarmonyPriority(HarmonyLib.Priority.First)]
    public static bool Prefix(XUiC_ItemStack __instance)
    {
        var owner=__instance.GetParentByType<XUiC_RebirthSurvivorCharacter>();
        if(owner==null || !owner.IsCharacterWindowOpen || __instance.StackLocation!=XUiC_ItemStack.StackLocationTypes.Backpack || __instance.ItemStack.IsEmpty())return true;
        RebirthTraitSupportProfileDefinition profile;
        if(!RebirthSurvivorDefinitionRegistry.TryGetSupportByGearItem(__instance.ItemStack.itemValue.ItemClass.GetItemName(),out profile) || profile.Kind!="survivor_gear")return true;
        if(!__instance.IsLocked && !__instance.StackLock)
            ItemActionEquipSurvivorGearRebirth.DispatchSelected(__instance.xui.playerUI.entityPlayer,__instance.ItemStack.itemValue,true,__instance.SlotNumber);
        return false;
    }
}
