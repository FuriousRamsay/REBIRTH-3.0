using System;
using System.Linq;
using System.Reflection;

// PREPARED, NOT RUN. Driver for a future real-native disposable-save harness.
// Requires an already admitted original and actual accepted native lease;
// never constructs receipt/proof fields or permits a native item setter.
internal sealed class mpA_logistics_rich_lifecycle_fixture:IDisposable
{
    private readonly RebirthLogisticsDeathSettlement.Lease actual;
    private readonly RebirthLogisticsParcelCoordinator coordinator;
    private bool previouslyApproved;
    internal mpA_logistics_rich_lifecycle_fixture(RebirthLogisticsDeathSettlement.Lease actual)
    {this.actual=actual??throw new ArgumentNullException(nameof(actual));if(!actual.Current()||actual.Death.State!=2)throw new Exception("Requires actual canonical prepared State2 native plan.");coordinator=new RebirthLogisticsParcelCoordinator(actual);}
    internal bool StepFrozenPreview()
    {
        var world=actual.Accepted.World;
        var originals=actual.Death.Parcels.Where(parcel=>parcel.State==0).Select(parcel=>parcel.EntityId).ToArray();
        var root=ItemStack.Clone(actual.Accepted.Backpack.bag.ItemGrid.items);
        bool ready=coordinator.TryFrozen();
        if(originals.Any(id=>world.GetEntity(id)!=null))throw new Exception("Preview spawned a native parcel.");
        if(!RebirthLogisticsDeathPartition.Same(root,actual.Accepted.Backpack.bag.ItemGrid.items))throw new Exception("Preview changed actual material.");
        using(var account=RebirthLogisticsSnapshotReservation.TryOpenRetainedRead(actual.Original,actual.Current))
            if(account!=null&&(account.WorkspaceBytes>192L*1024*1024||account.RetainedBytes>72L*1024*1024))throw new Exception("Actual original proof limits exceeded.");
        if(ready)
        {
            var field=typeof(RebirthLogisticsParcelCoordinator).GetField("frames",BindingFlags.Instance|BindingFlags.NonPublic);
            if(field.GetValue(coordinator)!=null)throw new Exception("Materialized stage graph remained during native confirmation.");
        }
        previouslyApproved=ready;return ready;
    }
    internal void AfterActualBaselineChange()
    {
        if(!previouslyApproved)throw new Exception("Requires actual earlier positive frozen approval before testing invalidation.");
        // Caller first makes a real unrelated native block/entity change in a
        // used chunk through the future scenario. No fake current proof here.
        if(StepFrozenPreview())throw new Exception("Stale approved baseline was reused after actual chunk change.");
    }
    public void Dispose(){coordinator.Dispose();}
}
