using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;

// Alternate branch of the existing queue recovery request/watch. Applied native
// transfer receipts retain the carrier; no ingredient debit or new journal occurs.
internal sealed class RebirthCamperStationRelocationRecovery : IDisposable
{
    private readonly RebirthCamperStationQueuedAuthority.Binding scope;
    private readonly object lease;
    private readonly RebirthStationGridAdmission retainedAdmission,parentAdmission;
    private readonly RebirthCamperStationPublicationRecord retainedPublication,parentPublication;
    private readonly RebirthCamperTransferJournal journal;
    private readonly Guid removal,installation;
    private readonly ItemStack carrier;
    private RebirthCamperStationSaveEvidence.Request request;
    private RebirthCamperStationSaveEvidence.Publication proof;
    private RebirthStationGridAdmission moved;
    private RebirthCamperStationPublicationRecord receipt;
    private bool closed;
    private RebirthCamperStationRelocationRecovery(RebirthCamperStationQueuedAuthority.Binding scope,object lease,
        RebirthStationGridAdmission retained,RebirthCamperStationPublicationRecord published,
        RebirthStationGridAdmission parent,RebirthCamperStationPublicationRecord parentPublished,
        RebirthCamperTransferJournal journal,Guid removal,Guid installation,ItemStack carrier)
    {
        this.scope=scope;this.lease=lease;retainedAdmission=retained.Clone();retainedPublication=published.Clone();
        parentAdmission=parent.Clone();parentPublication=parentPublished.Clone();this.journal=journal;
        this.removal=removal;this.installation=installation;this.carrier=carrier.Clone();
    }
    internal static bool TryBegin(EntityPlayer player,RebirthWorldCharacterRecord owner,RebirthStablePlayerIdentity identity,
        RebirthCamperStationState station,RebirthCamperStationAddress destination,object lease,
        RebirthStationGridAdmission retained,RebirthCamperStationPublicationRecord published,
        out RebirthCamperStationRelocationRecovery operation)
    {
        operation=null;
        try
        {
            var parent=retained;var parentPublished=published;
            if(retained.TryGetMobileAddress(out var current)&&current.Equals(destination))
            {
                var wrapper=retained.Write();
                if((string)wrapper.Attribute("version")!="7"||
                    !RebirthStationGridAdmission.TryReadStored(wrapper.Element("parentAdmission")?.Elements().SingleOrDefault(),out parent)||
                    !RebirthCamperStationPublicationRecord.TryRead(wrapper.Element("parentPublication")?.Elements().SingleOrDefault(),parent,out parentPublished))return false;
            }
            if(!parent.TryGetMobileAddress(out var source)||source.Vehicle==destination.Vehicle||source.Installation!=destination.Installation||
                !RebirthWorldCharacterRepository.HasSavedCamperStationPublication(identity,parent,parentPublished))return false;
            string root=Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH","CamperTransfers");
            if(!Directory.Exists(root))return false;
            var journal=new RebirthCamperTransferJournal(root);
            if(!TryFind(journal,source,destination,parent.CreationId,out var removal,out var installation,out var carrier))return false;
            var scope=new RebirthCamperStationQueuedAuthority.Binding(player,owner,identity,station,destination,parent,parentPublished);
            var candidate=new RebirthCamperStationRelocationRecovery(scope,lease,retained,published,parent,parentPublished,journal,removal,installation,carrier);
            if(!candidate.Current())return false;
            operation=candidate; // Retain before native request creation can acquire save custody.
            candidate.request=RebirthCamperStationSaveEvidence.BeginRecovery(player,station,destination,candidate.Current);
            return candidate.request!=null;
        }
        catch{return false;}
    }
    private static bool TryFind(RebirthCamperTransferJournal journal,RebirthCamperStationAddress source,
        RebirthCamperStationAddress destination,string creation,out Guid removal,out Guid installation,out ItemStack carrier)
    {
        removal=installation=Guid.Empty;carrier=null;
        foreach(var installed in journal.FindApplied(destination.Vehicle,creation))
        {
            var before=RebirthCamperTransferSnapshot.Restore(destination.Vehicle,installed.Before);
            var after=RebirthCamperTransferSnapshot.Restore(destination.Vehicle,installed.After);
            var item=before.Cursor;
            if(before.Stations.Find(destination.Installation)!=null||after.Stations.Find(destination.Installation)==null||
                !after.Cursor.IsEmpty()||!RebirthCamperPortableStation.TryGetSourceAddress(item,out var retained)||!retained.Equals(source)||
                !RebirthCamperPortableStation.TryRestore(item,out var portable)||portable.InstallationId!=source.Installation)continue;
            byte[] exact=RebirthCamperInventorySnapshot.Capture(new[]{item});
            foreach(var removed in journal.FindApplied(source.Vehicle,creation))
            {
                var original=RebirthCamperTransferSnapshot.Restore(source.Vehicle,removed.Before);
                var detached=RebirthCamperTransferSnapshot.Restore(source.Vehicle,removed.After);
                if(original.Stations.Find(source.Installation)==null||detached.Stations.Find(source.Installation)!=null||
                    !exact.SequenceEqual(RebirthCamperInventorySnapshot.Capture(new[]{detached.Cursor})))continue;
                if(carrier!=null)return false; // Ambiguous history cannot authorize a guessed source.
                removal=removed.Operation;installation=installed.Operation;carrier=item.Clone();
            }
        }
        return carrier!=null;
    }
    private bool Pair(RebirthStationGridAdmission admission,RebirthCamperStationPublicationRecord publication)
        =>admission!=null&&publication!=null&&scope.Progression.StationPreparations.TryGetValue(admission.JobId,out var a)&&a!=null&&
            scope.Progression.CamperStationPublications.TryGetValue(admission.JobId,out var p)&&p!=null&&
            XNode.DeepEquals(a.Write(),admission.Write())&&XNode.DeepEquals(p.Write(),publication.Write());
    private bool Current()
    {
        try
        {
            return !closed&&RebirthStationLiveAccess.TryResolveMobile(scope.Player,scope.Address,parentAdmission.CreationId,out var live,out var owner)&&
                ReferenceEquals(live,scope.Station)&&ReferenceEquals(owner,scope.Owner)&&ReferenceEquals(owner.Progression,scope.Progression)&&
                RebirthStationLiveAccess.TryGetMobileLease(scope.Player,scope.Address,parentAdmission.CreationId,out var state,out var currentLease)&&
                ReferenceEquals(state,scope.Station)&&ReferenceEquals(currentLease,lease)&&
                RebirthWorldCharacterService.TryGetIdentity(scope.Player,out var identity)&&identity!=null&&
                identity.StorageKey==scope.Identity.StorageKey&&identity.CanonicalId==scope.Identity.CanonicalId&&
                !RebirthCamperStationCompletionCallsite.IsHeld(state)&&
                !scope.Progression.StationPublications.ContainsKey(parentAdmission.JobId)&&!scope.Progression.StationTerminalIntents.ContainsKey(parentAdmission.JobId)&&
                !scope.Progression.StationCompletionPublications.ContainsKey(parentAdmission.JobId)&&!scope.Progression.StationCancellationAttempts.ContainsKey(parentAdmission.JobId)&&
                !scope.Progression.StationCancellationPublications.ContainsKey(parentAdmission.JobId)&&!scope.Progression.CamperStationCompletions.ContainsKey(parentAdmission.JobId)&&
                !scope.Progression.CamperStationTrainingPlans.ContainsKey(parentAdmission.JobId)&&!scope.Progression.CamperStationCancellations.ContainsKey(parentAdmission.JobId)&&
                !scope.Progression.CamperStationRefundDeliveries.ContainsKey(parentAdmission.JobId)&&!scope.Progression.StationCancellationRefunds.ContainsKey(parentAdmission.JobId)&&
                !scope.Progression.StationRefundDeliveries.ContainsKey(parentAdmission.JobId)&&!scope.Progression.StationRefundArchives.ContainsKey(parentAdmission.JobId)&&
                (Pair(retainedAdmission,retainedPublication)||Pair(moved,receipt))&&
                (RebirthWorldCharacterRepository.HasSavedCamperStationPublication(identity,parentAdmission,parentPublication)||
                    moved!=null&&RebirthWorldCharacterRepository.HasSavedCamperStationPublication(identity,moved,receipt))&&
                parentAdmission.TryGetMobileAddress(out var source)&&
                RebirthCamperQueuedPublicationMatch.MatchesRestoredStation(state,source,parentAdmission,scope.QueuedOwner);
        }
        catch{return false;}
    }
    internal bool CanContinue=>Current()&&(proof!=null||request!=null&&!request.Closed);
    internal bool TryRecover()
    {
        if(!Current())return false;
        if(proof==null){if(!RebirthCamperStationSaveEvidence.TryObserve(request,out var observed))return false;proof=observed;}
        if(!RebirthCamperStationRelocationCorrelation.TryCreate(journal,removal,installation,carrier,parentAdmission,parentPublication,proof,out var correlation)||
            !parentAdmission.TryRelocate(correlation,out var candidate)||
            !RebirthCamperStationPublicationRecord.TryCreateRelocated(correlation,candidate,out var published))return false;
        moved=candidate;receipt=published;
        // Reconnect retries preserve the exact previously staged proof receipt.
        if((string)retainedAdmission.Write().Attribute("version")=="7"&&XNode.DeepEquals(retainedAdmission.Write(),moved.Write()))
        {moved=retainedAdmission.Clone();receipt=retainedPublication.Clone();}
        bool saved=Pair(retainedAdmission,retainedPublication)&&(string)retainedAdmission.Write().Attribute("version")=="7"
            ?RebirthCamperStationRelocationService.TryFlushRecorded(scope.Player,moved,receipt,proof)
            :RebirthCamperStationRelocationService.TryRecord(scope.Player,correlation);
        return saved&&Current()&&RebirthCamperStationQueuedAuthority.TryRegisterRecovered(scope.Player,moved,receipt,proof);
    }
    public void Dispose()
    {
        if(!ThreadManager.IsMainThread())throw new InvalidOperationException("Release relocation recovery on game thread.");
        if(closed)return;request?.Dispose();closed=true;
    }
}
