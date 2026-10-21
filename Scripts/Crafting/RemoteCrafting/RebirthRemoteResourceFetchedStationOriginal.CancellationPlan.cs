using System;
using System.Collections.Generic;
using System.Linq;

internal static partial class RebirthRemoteResourceFetchedStationOriginal
{
    internal static bool IsCancellationCandidate(Recipe recipe)=>recipe?.ingredients?.Any(fragment=>fragment?.itemValue!=null&&
        (fragment.itemValue.HasMetadata(RebirthRemoteResourceFetchedStationPlan.SourceKey)||fragment.itemValue.HasMetadata(RebirthRemoteResourceFetchedStationPlan.BindingKey)))==true;
    internal sealed class CancellationPlan
    {
        internal readonly Guid Job,Source;internal readonly string Binding;internal readonly int Slot;
        private readonly ItemStack[] refunds;
        internal CancellationPlan(RebirthRemoteResourceFetchedStationPlan.Plan original,int slot,List<ItemStack> remaining)
        {Job=original.Job;Source=original.Source;Binding=original.Binding;Slot=slot;refunds=ItemStack.Clone(remaining.ToArray());}
        internal ItemStack[] CopyRefunds()=>ItemStack.Clone(refunds);
    }
    // Pure rich-fragment plan only. Candidate failures must never fall through
    // to ordinary actor-source cancellation, which has no fetch-origin proof.
    internal static bool TryPlanCancellation(EntityPlayer actor,RebirthCamperStationState station,RebirthCamperStationAddress address,string creation,Guid job,out CancellationPlan cancellation)
    {
        cancellation=null;
        try
        {
            if(!ThreadManager.IsMainThread()||actor?.world?.IsRemote()!=false||actor.IsDead()||!actor.IsSpawned()||!RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var owner)||
                !RebirthOriginalActionScope.Matches(actor,owner.StorageKey,creation)||!RebirthStationLiveAccess.TryGetMobileLease(actor,address,creation,out var actual,out _)||
                !ReferenceEquals(actual,station)||RebirthRemoteResourceSourceCustody.BlocksStation(station)||RebirthCamperStationGridCustody.IsHeld(station))return false;
            var found=station.Queue.Select((row,index)=>new{row,index}).Where(value=>RebirthCamperStationNativeCrafting.TryJob(value.row?.Recipe,out var id)&&id==job).ToArray();
            if(found.Length!=1||!IsCancellationCandidate(found[0].row.Recipe))return false;
            var row=found[0].row;var marker=row.Recipe.ingredients[0].itemValue;
            if(!marker.TryGetMetadata(RebirthRemoteResourceFetchedStationPlan.SourceKey,out string sourceText)||!Guid.TryParseExact(sourceText,"N",out var source)||
                !marker.TryGetMetadata(RebirthRemoteResourceFetchedStationPlan.BindingKey,out string binding)||
                !RebirthRemoteResourceDurableJournal.TryRead(owner.StorageKey,source,out var root,out bool unknown)||unknown||root?.Phase!=4||root.Binding!=binding||
                (string)root.Copy().Attribute("disposition")!="used"||!TryReadPlan(root,out var plan)||plan.Job!=job||plan.Address!=address.ToString()||plan.Creation!=creation||
                plan.Deaths!=actor.Died||plan.CanonicalOwner!=owner.CanonicalId||!MatchesNativePermission(root,plan)||
                !TryObservePublication(actor.world,root,out var publication)||(string)root.Copy().Attribute("ownerProof")!=publication)return false;
            var original=plan.RestoreAfterStation().Queue[plan.QueueSlot];
            if(row.RepairItem!=null||row.Multiplier<1||row.Multiplier>original.Multiplier||row.Quality!=original.Quality||
                RebirthRemoteResourceOwnerAction.RecipeImage(row.Recipe)!=RebirthRemoteResourceOwnerAction.RecipeImage(original.Recipe)||
                !RebirthCamperStationNativeCrafting.TryRemainingRefund(row,out var remaining))return false;
            foreach(var fragment in remaining)
            {
                if(!fragment.itemValue.TryGetMetadata("rebirth.camper.native.actor.owner",out string origin)||origin!=owner.CanonicalId||
                    !fragment.itemValue.TryGetMetadata("rebirth.camper.native.actor.source",out string kind)||(kind!="bag"&&kind!="belt"&&kind!="fetch")||
                    !fragment.itemValue.TryGetMetadata("rebirth.camper.native.actor.slot",out int slot)||kind=="fetch"&&slot!=-1||kind!="fetch"&&slot<0)return false;
                fragment.itemValue.Metadata.Remove("rebirth.camper.native.actor.owner");fragment.itemValue.Metadata.Remove("rebirth.camper.native.actor.source");fragment.itemValue.Metadata.Remove("rebirth.camper.native.actor.slot");
                // The complete original native image was restored by the native
                // refund planner; do not reconstruct a type-only item or regrant
                // consumed batches. No source rollback is part of cancellation.
            }
            cancellation=new CancellationPlan(plan,found[0].index,remaining);return true;
        }
        catch{cancellation=null;return false;}
    }
}
