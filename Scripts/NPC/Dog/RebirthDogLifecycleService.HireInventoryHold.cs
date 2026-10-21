using System;
using System.Linq;
using System.Reflection;

public static partial class RebirthDogLifecycleService
{
    private sealed class HireInventoryPermit
    {internal HireSettlement Original;internal object Inventory;internal int Slot;internal ItemStack Stack;internal bool Used;}
    [ThreadStatic]private static HireInventoryPermit LocalHireInventoryPermit;
    private static bool CompetingHireCustody(EntityPlayerLocal player)=>player==null||RebirthCookingOutputDelivery.IsHeld(player)||RebirthLogisticsOwnerReceipt.IsHeld(player)||
        AdvancedFarmingHarvestOwnerDelta.IsHeld(player)||AdvancedFarmingWaterExchangeOriginal.IsHeld(player)||RebirthGearOwnerReservation.IsHeld(player)||RebirthBackpackLibraryReservation.IsHeld(player)||
        RemoteResourceClientTransactionCoordinator.HasPendingInventoryOperation(player)||RebirthCamperEnergyOwnerHold.Blocks(player.bag)||RebirthCamperEnergyOwnerHold.Blocks(player.inventory)||
        RebirthCamperStationNativeActorPayment.Blocks(player.bag)||RebirthCamperStationNativeActorPayment.Blocks(player.inventory)||RebirthPlacedOriginal.IsHeld(player);
    private static bool LocalHireInventoryCurrent(HireSettlement original)
    {
        if(original.Terminal||original.Player is not EntityPlayerLocal actor||!ReferenceEquals(original.World,GameManager.Instance?.World)||original.World.IsRemote()||!ReferenceEquals(original.World.GetPrimaryPlayer(),actor)||
            !ReferenceEquals(actor.world,original.World)||!ReferenceEquals(original.World.GetEntity(actor.entityId),actor)||actor.IsDead()||actor.Died!=original.Deaths||original.Dog==null||original.Dog.IsDead()||
            !ReferenceEquals(actor.inventory,original.Inventory)||!ReferenceEquals(actor.bag,original.Bag))return false;
        string creation=RebirthCamperCharacterAccess.CurrentCreation(actor);return creation==null||creation==original.Creation;
    }
    private static bool LocalHireBlocksInventory(object inventory)=>HireSettlements.Count!=0&&
        HireSettlements.Values.Any(original=>LocalHireInventoryCurrent(original)&&(ReferenceEquals(original.Inventory,inventory)||ReferenceEquals(original.Bag,inventory)));
    private static bool RemoteHireInventoryCurrent(RemoteHireRequest original)
    {
        var actor=original?.Actor;
        if(actor==null||original.Dog==null||!ReferenceEquals(original.World,GameManager.Instance?.World)||!ReferenceEquals(original.World.GetPrimaryPlayer(),actor)||
            !ReferenceEquals(actor.world,original.World)||!ReferenceEquals(original.World.GetEntity(actor.entityId),actor)||actor.IsDead()||actor.Died!=original.Deaths||original.Dog.IsDead())return false;
        string creation=RebirthCamperCharacterAccess.CurrentCreation(actor);return creation==null||creation==original.Creation;
    }
    internal static bool IsHireInventoryHeld(EntityPlayerLocal player)=>player!=null&&(LocalHireBlocksInventory(player.inventory)||LocalHireBlocksInventory(player.bag)||
        RemoteHireInventoryCurrent(RemoteHireClient)&&ReferenceEquals(RemoteHireClient.Actor,player));
    private static bool BeforeLocalHireInventoryMutation(object inventory,MethodBase method,object[] args)
    {
        if(!LocalHireBlocksInventory(inventory))return true;
        var permit=LocalHireInventoryPermit;
        if(permit==null||permit.Used||!ReferenceEquals(permit.Inventory,inventory)||!ReferenceEquals(CurrentHireMutation,permit.Original)||!permit.Original.Busy||!HireCurrent(permit.Original)||
            !HireSettlements.TryGetValue(permit.Original.State.StableId,out var registered)||!ReferenceEquals(registered,permit.Original)||args==null||args.Length<2||!(args[0] is int slot)||slot!=permit.Slot)return false;
        bool exact=method.Name=="SetSlot"&&ReferenceEquals(args[1],permit.Stack)||method.Name=="SetItem"&&args.Length==3&&ReferenceEquals(args[1],permit.Stack.itemValue)&&args[2] is int count&&count==permit.Stack.count;
        if(exact)permit.Used=true;return exact;
    }
    private static bool ApplyLocalHireSlot(HireSettlement original,HireDebitSource source,ItemStack stack)
    {
        if(LocalHireInventoryPermit!=null||!ReferenceEquals(CurrentHireMutation,original)||!original.Busy||!HireCurrent(original))return false;
        var permit=new HireInventoryPermit{Original=original,Inventory=source.Belt?original.Inventory:original.Bag,Slot=source.Slot,Stack=stack};LocalHireInventoryPermit=permit;
        try
        {
            if(source.Belt)original.Player.inventory.SetItem(source.Slot,stack.itemValue,stack.count);else original.Player.bag.SetSlot(source.Slot,stack);
            return permit.Used&&HireCurrent(original)&&RebirthStationGridIngredients.IsSameStackSnapshot(source.Grid[source.Slot],stack);
        }
        finally{LocalHireInventoryPermit=null;}
    }
}
