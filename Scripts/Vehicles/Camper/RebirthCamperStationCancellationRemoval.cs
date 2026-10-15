using System;
using System.Runtime.CompilerServices;
using System.Xml.Linq;

// One original removal. Native save confirmation proves removal only; it never
// credits the refund or releases cancellation custody.
internal sealed class RebirthCamperStationCancellationRemoval : IDisposable
{
    private static readonly ConditionalWeakTable<RebirthCamperStationState,RebirthCamperStationCancellationRemoval> holds
        =new ConditionalWeakTable<RebirthCamperStationState,RebirthCamperStationCancellationRemoval>();
    private readonly RebirthCamperStationCancellationCheckpoint checkpoint;
    private readonly RebirthCamperStationCancellationSave saved;
    private readonly string expected;
    private RebirthCamperStationSaveEvidence.Request request;
    private RebirthCamperStationSaveEvidence.Publication proof;
    private RebirthCamperStationCancellationRecord publication;
    private bool publicationSaved;
    private RebirthCamperStationRefundDelivery settledDelivery;
    private RebirthCamperStationRemoteRefundDelivery settledRemoteDelivery;
    private bool removed,closed,confirmed;
    private RebirthCamperStationCancellationRemoval(RebirthCamperStationCancellationCheckpoint checkpoint,
        RebirthCamperStationCancellationSave saved,string expected)
    {this.checkpoint=checkpoint;this.saved=saved;this.expected=expected;}
    internal static bool IsHeld(RebirthCamperStationState state)=>state!=null&&holds.TryGetValue(state,out _);
    internal static bool TryBegin(RebirthCamperStationCancellationCheckpoint checkpoint,RebirthCamperStationCancellationSave saved,
        out RebirthCamperStationCancellationRemoval operation)
    {
        operation=null;
        try
        {
            if(!ThreadManager.IsMainThread()||checkpoint==null||saved==null||!saved.IsConfirmedFor(checkpoint)||
                IsHeld(checkpoint.Binding.Station)||!checkpoint.TryBuildRemoval(out var replacement,out var digest)||
                !checkpoint.TryBuildRemovalInput(out var input))return false;
            var candidate=new RebirthCamperStationCancellationRemoval(checkpoint,saved,digest);
            operation=candidate;holds.Add(checkpoint.Binding.Station,candidate);
            // Credits molten counters and removes the paid queue in one native
            // station image. The following save acknowledgment proves both.
            checkpoint.Binding.Station.Input=input;checkpoint.Binding.Station.Queue=replacement;candidate.removed=true;
            checkpoint.Binding.Station.SetModified();
            candidate.request=RebirthCamperStationSaveEvidence.BeginRetirement(checkpoint.Binding.Station,checkpoint.Binding.Address,candidate.IsCurrent);
            return true;
        }
        catch{return false;}
    }
    private bool IsCurrent()
    {
        try
        {
            var state=checkpoint.Binding.Station;
            return !closed&&ThreadManager.IsMainThread()&&removed&&holds.TryGetValue(state,out var held)&&ReferenceEquals(held,this)&&
                saved.IsSavedForRemoval(checkpoint)&&checkpoint.MatchesStagedOwner()&&checkpoint.MatchesPlannedQueue(state.Queue)&&
                checkpoint.StationDigest(state)==expected;
        }
        catch{return false;}
    }
    internal bool TryConfirm()
    {
        try
        {
            if(!IsCurrent())return false;
            if(proof==null)
            {
                if(request==null)request=RebirthCamperStationSaveEvidence.BeginRetirement(checkpoint.Binding.Station,checkpoint.Binding.Address,IsCurrent);
                if(!RebirthCamperStationSaveEvidence.TryObserve(request,out var observed))return false;
                proof=observed;
            }
            if(!proof.Address.Equals(checkpoint.Binding.Address)||proof.RecoveryDigest()!=expected||!IsCurrent())return false;
            confirmed=true;return true;
        }
        catch{return false;}
    }
    internal bool IsConfirmedFor(RebirthCamperStationCancellationCheckpoint expectedCheckpoint)
        =>confirmed&&ReferenceEquals(checkpoint,expectedCheckpoint)&&IsCurrent();
    internal bool TryGetPublication(RebirthCamperStationCancellationCheckpoint expectedCheckpoint,
        out RebirthCamperStationSaveEvidence.Publication publication)
    {
        publication=null;
        if(!IsConfirmedFor(expectedCheckpoint)||proof==null)return false;
        publication=proof;return true;
    }
    internal bool TrySavePublication()
    {
        try
        {
            if(!IsConfirmedFor(checkpoint))return false;
            var binding=checkpoint.Binding;var records=binding.Progression.CamperStationCancellations;string job=binding.Admission.JobId;
            if(publication==null)
            {
                if(records.ContainsKey(job)||records.Count>=64||
                    !RebirthCamperStationCancellationRecord.TryCreate(checkpoint,this,out var proposed))return false;
                publication=proposed;records.Add(job,proposed.Clone());
            }
            if(!MatchesPublication())return false;
            if(!publicationSaved)
            {
                RebirthWorldCharacterService.MarkDirty(binding.Owner,"camper-cancellation-publication");
                if(!RebirthWorldCharacterService.FlushPlayer(binding.Player,"camper-cancellation-publication")||!MatchesPublication())return false;
            }
            if(!HasSavedPublication())return false;
            publicationSaved=true;return true;
        }
        catch{return false;}
    }
    private bool MatchesPublication()=>publication!=null&&IsConfirmedFor(checkpoint)&&
        checkpoint.Binding.Progression.CamperStationCancellations.TryGetValue(checkpoint.Binding.Admission.JobId,out var current)&&current!=null&&
        XNode.DeepEquals(current.Write(),publication.Write());
    private bool HasSavedPublication()
    {
        var binding=checkpoint.Binding;
        return MatchesPublication()&&RebirthWorldCharacterRepository.HasSavedCamperStationCancellation(binding.Identity,binding.Admission,
            binding.Publication,checkpoint.CopyIntent(),checkpoint.CopyRefund(),checkpoint.CopyAttempt(),publication)&&MatchesPublication();
    }
    internal bool TryGetCancellationRecord(out RebirthCamperStationCancellationRecord record)
    {
        record=null;
        if(!HasSavedPublication())return false;
        record=publication.Clone();return true;
    }
    // Live original custody only, after the saved publication was confirmed.
    // Delivery preparation/attempts separately recheck their primary record.
    internal bool MatchesRefundCustody(RebirthCamperStationCancellationCheckpoint expectedCheckpoint,
        RebirthCamperStationCancellationRecord record)
        =>publicationSaved&&ReferenceEquals(checkpoint,expectedCheckpoint)&&record!=null&&MatchesPublication()&&
            XNode.DeepEquals(record.Write(),publication.Write());
    // Detached destination proposal only. Inventory application still requires
    // a saved delivery attempt and its one-use native application permit.
    internal bool TryPrepareRefund(out RebirthStationRefundBackpackPlan destination)
    {
        destination=null;
        try
        {
            if(!HasSavedPublication())return false;
            if(!(checkpoint.Binding.Player is EntityPlayerLocal))
            {
                var b=checkpoint.Binding;
                if(!RebirthCamperRemoteStationTransport.TryResolve(b.Player,b.Address,b.Admission.CreationId,out var station,out _)||
                    !ReferenceEquals(station,b.Station)||!RebirthStationRefundPlayerFileWitness.TryCopySavedBag(b.Identity,out var savedBag))return false;
                var savedSlots=savedBag.ItemGrid.items;
                if(!RebirthStationRefundBackpackPlan.TryCreate(savedSlots,savedSlots.Length,
                    slot=>savedBag.LockedSlots!=null&&slot<savedBag.LockedSlots.Length&&savedBag.LockedSlots[slot],checkpoint.CopyRefund(),out var remoteProposed)||
                    !HasSavedPublication())return false;
                destination=remoteProposed;return true;
            }
            if(!(checkpoint.Binding.Player is EntityPlayerLocal player)||
                !ReferenceEquals(player.world?.GetPrimaryPlayer(),player))return false;
            var bag=player.bag;var grid=bag?.ItemGrid;var slots=grid?.items;
            if(slots==null)return false;
            Func<int,bool> locked=slot=>bag.LockedSlots!=null&&slot<bag.LockedSlots.Length&&bag.LockedSlots[slot];
            if(!RebirthStationRefundBackpackPlan.TryCreate(slots,slots.Length,locked,checkpoint.CopyRefund(),out var proposed)||
                !HasSavedPublication()||!ReferenceEquals(player.bag,bag)||!ReferenceEquals(bag.ItemGrid,grid)||
                !ReferenceEquals(grid.items,slots)||!ReferenceEquals(player.world?.GetPrimaryPlayer(),player)||
                !proposed.MatchesBefore(slots,slots.Length,locked))return false;
            destination=proposed;return true;
        }
        catch{return false;}
    }
    internal bool TryReleaseSettled(RebirthCamperStationRemoteRefundDelivery delivery)
    {
        try
        {
            if(!ThreadManager.IsMainThread()||delivery==null)return false;
            if(closed)return ReferenceEquals(settledRemoteDelivery,delivery);
            if(!HasSavedPublication()||!delivery.IsConfirmedFor(checkpoint,this)||!IsCurrent())return false;
            request?.Dispose();
            if(!IsCurrent()||!holds.Remove(checkpoint.Binding.Station))return false;
            settledRemoteDelivery=delivery;closed=true;return true;
        }
        catch{return false;}
    }
    internal bool TryReleaseSettled(RebirthCamperStationRefundDelivery delivery)
    {
        try
        {
            if(!ThreadManager.IsMainThread()||delivery==null)return false;
            if(closed)return ReferenceEquals(settledDelivery,delivery);
            if(!HasSavedPublication()||!delivery.IsConfirmedFor(checkpoint,this)||!IsCurrent())return false;
            request?.Dispose();
            if(!IsCurrent()||!holds.Remove(checkpoint.Binding.Station))return false;
            settledDelivery=delivery;closed=true;return true;
        }
        catch{return false;}
    }
    public void Dispose()
    {
        if(!ThreadManager.IsMainThread())throw new InvalidOperationException("Release cancellation save on game thread.");
        if(closed)return;request?.Dispose();closed=true;
        // Ordinary cleanup never releases custody; only a confirmed original
        // refund delivery can use TryReleaseSettled.
    }
}
