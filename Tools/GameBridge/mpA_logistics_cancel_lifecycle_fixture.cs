using System;
using System.Reflection;

// PREPARED, NOT RUN. Harness issues actual UI Cancel, observes real client
// ticks/reconnect, and waits for authenticated host rejection. No mode/phase
// fields are changed by this fixture and no no-effect receipt is invented.
internal sealed class mpA_logistics_cancel_lifecycle_fixture
{
    private readonly Guid operation;private readonly ItemStack[] bag,belt;
    internal mpA_logistics_cancel_lifecycle_fixture(EntityPlayerLocal actual)
    {
        if(actual?.world?.IsRemote()!=true||!RebirthLogisticsOwnerReceipt.TryOperation(actual.Buffs,out operation,out int phase)||phase>1||
            !RebirthLogisticsOwnerReceipt.HasPendingOriginal(actual))throw new Exception("Requires actual unpaid original before native UI Cancel.");
        bag=ItemStack.Clone(actual.bag.ItemGrid.items);belt=ItemStack.Clone(actual.inventory.ItemGrid.items);
    }
    internal void BeforeActualCanonicalRejection(EntityPlayerLocal actual)
    {
        var type=typeof(RebirthLogisticsOriginalTransport);
        bool requested=(bool)type.GetField("cancelRequested",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
        object[] args={actual,operation,false};bool known=(bool)type.GetMethod("TryLoadCancellation",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,args);
        if(!requested||!known||!(bool)args[2])throw new Exception("Actual same-original cancel intent is not durably retained after UI/reconnect.");
        if(!RebirthLogisticsOwnerReceipt.TryOperation(actual.Buffs,out var current,out int phase)||current!=operation||phase>1||
            !RebirthLogisticsDeathPartition.Same(bag,actual.bag.ItemGrid.items)||!RebirthLogisticsDeathPartition.Same(belt,actual.inventory.ItemGrid.items))
            throw new Exception("Fresh owner debit occurred while canonical cancellation was pending.");
    }
    // After host rejection, lifecycle observer RequireUnpaidCancellationPrepared
    // then RequireSettledOutcome(3) require actual saved native phase5/full
    // Before and zero attempted sources. This intent marker proves none of it.
}
