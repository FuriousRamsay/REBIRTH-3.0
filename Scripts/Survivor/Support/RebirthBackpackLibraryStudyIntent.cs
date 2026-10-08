using System;

// Transient UI intent only. Never grants custody, applies inventory, or awards learning.
public sealed class RebirthBackpackLibraryStudyIntent
{
    private readonly EntityPlayerLocal owner;
    private readonly World world;
    private readonly string creation;
    private readonly long revision;
    private readonly int librarySlot;
    private readonly ItemStack expected;
    private RebirthBackpackLibraryReceipt receipt;
    public int ToolbeltSlot { get; private set; }
    public string TransactionId => receipt?.TransactionId;

    private RebirthBackpackLibraryStudyIntent(EntityPlayerLocal player,RebirthBackpackLibraryView view,
        int source,int destination,ItemStack book)
    {
        owner=player;world=player.world;creation=view.CreationId;revision=view.GearRevision;
        librarySlot=source;ToolbeltSlot=destination;expected=book.Clone();expected.count=1;
    }

    public static bool TryCreate(EntityPlayerLocal player,RebirthBackpackLibraryView view,
        int librarySlot,int toolbeltSlot,out RebirthBackpackLibraryStudyIntent intent)
    {
        intent=null;
        if(player?.world==null||view==null||view.TransferPending||view.GearRevision<0||
            view.GearRevision==long.MaxValue||player.IsDead()||!player.IsSpawned()||
            RebirthCharacterCreationHoldService.IsHeld(player)||RebirthBackpackLibraryReservation.IsHeld(player)||RebirthGearOwnerReservation.IsHeld(player)||
            !RebirthSurvivorRequestScope.Matches(view.CreationId,RebirthSurvivorClientState.GetProjectedCreationId(player))||
            !view.TryGetSlot(librarySlot,out var book)||book==null||book.IsEmpty()||
            book.itemValue?.ItemClass==null)return false;
        RebirthLiteratureDefinition definition;
        if(!RebirthProgressionRuntimeConfig.TryGetLiterature(book.itemValue.ItemClass.GetItemName(),out definition)||definition==null)return false;
        var slots=player.inventory?.ItemGrid.items;
        int owned=slots==null?0:RebirthToolbeltCapacity.GetOwnedSlotCount(player,slots.Length);
        if(toolbeltSlot<0||toolbeltSlot>=owned||slots[toolbeltSlot]==null||!slots[toolbeltSlot].IsEmpty())return false;
        intent=new RebirthBackpackLibraryStudyIntent(player,view,librarySlot,toolbeltSlot,book);return true;
    }

    private bool IsCurrent(EntityPlayerLocal player)
        => ReferenceEquals(owner,player)&&ReferenceEquals(world,player?.world)&&
            player.IsSpawned()&&!player.IsDead()&&!RebirthCharacterCreationHoldService.IsHeld(player)&&
            RebirthSurvivorRequestScope.Matches(creation,RebirthSurvivorClientState.GetProjectedCreationId(player));

    // Receipt is supplied by the authenticated owner-offer API, never a display snapshot.
    public bool TryBind(EntityPlayerLocal player,RebirthBackpackLibraryReceipt offer)
    {
        if(!IsCurrent(player)||offer==null||offer.IsSellStash||offer.CreationId!=creation||offer.ExpectedGearRevision!=revision||
            offer.Deposit||offer.IsBag||offer.IsCursor||offer.Quantity!=1||offer.LibrarySlot!=librarySlot||
            offer.InventorySlot!=ToolbeltSlot||receipt!=null&&receipt.TransactionId!=offer.TransactionId||
            !offer.TryGetImages(out var beforePack,out var afterPack,out var before,out var after)||
            before==null||!before.IsEmpty()||!RebirthStationGridIngredients.IsSameStackSnapshot(after,expected))return false;
        receipt=offer;return true;
    }

    // Caller must still select the held slot and invoke ordinary literature admission.
    public bool TryGetSettledBook(EntityPlayerLocal player,RebirthBackpackLibrarySettlement settlement,out ItemStack book)
    {
        book=null;
        if(!IsCurrent(player)||receipt==null||settlement==null||!settlement.Applied||
            settlement.CreationId!=creation||settlement.TransactionId!=receipt.TransactionId||
            settlement.GearRevision!=revision+1||RebirthBackpackLibraryReservation.IsHeld(player)||RebirthGearOwnerReservation.IsHeld(player))return false;
        var slots=player.inventory?.ItemGrid.items;
        int owned=slots==null?0:RebirthToolbeltCapacity.GetOwnedSlotCount(player,slots.Length);
        if(ToolbeltSlot<0||ToolbeltSlot>=owned||
            !RebirthStationGridIngredients.IsSameStackSnapshot(slots[ToolbeltSlot],expected))return false;
        book=expected.Clone();return true;
    }
}