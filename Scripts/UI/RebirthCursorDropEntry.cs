using System;

// Keep the native Drop label/icon/shortcut and native cursor transaction. Its
// display ItemStackControl mirrors the cursor grid; clearing that mirror through
// ordinary ItemActionEntryDrop would leave the owned cursor stack behind.
internal sealed class RebirthCursorDropEntry:ItemActionEntryDrop
{
    internal RebirthCursorDropEntry(XUiController source):base(source){ }
    public override void RefreshEnabled()
    {
        base.RefreshEnabled();
        var player=ItemController?.xui?.playerUI?.entityPlayer;
        var cursor=ItemController?.xui?.DragAndDropWindow;
        Enabled=Enabled&&player!=null&&!player.IsDead()&&cursor!=null&&!cursor.IsEmpty()
            &&ReferenceEquals(player.world,GameManager.Instance?.World)
            &&ReferenceEquals(player.world?.GetPrimaryPlayer(),player)
            &&ReferenceEquals(player.world?.GetEntity(player.entityId),player)
            &&ReferenceEquals(ItemController,cursor.ItemStackControl)
            &&!RebirthConsoleInputGuardRuntime.BlocksGameplayInput()
            &&!RebirthBackpackLibraryReservation.BlocksResourceUse(player)
            &&!RemoteResourceClientTransactionCoordinator.HasPendingInventoryOperation(player)
            &&!RebirthCamperEnergyOwnerHold.Blocks(player.bag)&&!RebirthCamperEnergyOwnerHold.Blocks(player.inventory)
            &&!RebirthCamperEnergyOwnerHold.Blocks(cursor)
            &&!RebirthCamperStationNativeActorPayment.Blocks(player.bag)&&!RebirthCamperStationNativeActorPayment.Blocks(player.inventory);
    }
    public override void OnActivated()
    {
        RefreshEnabled();if(!Enabled)return;
        var cursor=ItemController.xui.DragAndDropWindow;
        if(!RebirthStationGridIngredients.IsSameStackSnapshot(cursor.CurrentStack,((XUiC_ItemStack)ItemController).ItemStack))return;
        cursor.DropCurrentItem();
    }
}
