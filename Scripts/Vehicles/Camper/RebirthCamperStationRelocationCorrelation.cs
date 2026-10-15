using System;
using System.Security.Cryptography;
using System.Xml.Linq;

// Detached correlation only. No inventory, queue, claim or owner-record writes.
// Applied native transfer journals and queued-save evidence remain independent
// prerequisites; this object alone never grants destination processing.
internal sealed class RebirthCamperStationRelocationCorrelation
{
    internal readonly RebirthCamperStationAddress Source, Destination;
    internal readonly Guid RemovalOperation, InstallationOperation;
    internal readonly string RemovalAfterHash, InstallationAfterHash;
    internal readonly RebirthStationGridAdmission OriginalAdmission;
    internal readonly RebirthCamperStationPublicationRecord OriginalPublication;
    internal readonly RebirthCamperStationSaveEvidence.Publication DestinationProof;
    private RebirthCamperStationRelocationCorrelation(RebirthCamperStationAddress source,
        RebirthCamperStationAddress destination,RebirthCamperTransferJournal.Record removal,
        RebirthCamperTransferJournal.Record installation,RebirthStationGridAdmission admission,
        RebirthCamperStationPublicationRecord publication,RebirthCamperStationSaveEvidence.Publication proof)
    {
        Source=source;Destination=destination;RemovalOperation=removal.Operation;InstallationOperation=installation.Operation;
        RemovalAfterHash=Hash(removal.After);InstallationAfterHash=Hash(installation.After);
        OriginalAdmission=admission.Clone();OriginalPublication=publication.Clone();DestinationProof=proof;
    }
    internal static string Hash(byte[] bytes)
    {using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","");}
    private static bool SameItem(ItemStack left,ItemStack right)
    {
        if(left==null||right==null)return false;
        // A confirmed removal may have delivered its separate tools/fuel/output
        // parcel before this carrier is installed again. Compare the retained
        // station image exactly, excluding only that consumed delivery envelope.
        if(left.itemValue?.Metadata?.ContainsKey(RebirthCamperStationRemovalContents.ContentsKey)==true)
        {RebirthCamperStationRemovalContents.TryRead(left,out _);left=RebirthCamperStationRemovalContents.WithoutContents(left);}
        if(right.itemValue?.Metadata?.ContainsKey(RebirthCamperStationRemovalContents.ContentsKey)==true)
        {RebirthCamperStationRemovalContents.TryRead(right,out _);right=RebirthCamperStationRemovalContents.WithoutContents(right);}
        return Hash(RebirthCamperInventorySnapshot.Capture(new[]{left}))==Hash(RebirthCamperInventorySnapshot.Capture(new[]{right}));
    }
    private static RebirthCamperStationState Find(RebirthCamperStationInstallations stations,Guid installation)
    {
        RebirthCamperStationState result=null;
        for(int i=0;i<RebirthCamperCraftingStation.ModuleSlots;i++)
        {
            var state=stations.GetSlot(i)?.State;
            if(state==null||state.InstallationId!=installation)continue;
            if(result!=null)throw new InvalidOperationException("Ambiguous relocated installation.");
            result=state;
        }
        return result;
    }
    internal static bool TryCreate(RebirthCamperTransferJournal journal,Guid removalOperation,Guid installationOperation,
        ItemStack carrier,RebirthStationGridAdmission original,RebirthCamperStationPublicationRecord publication,
        RebirthCamperStationSaveEvidence.Publication proof,out RebirthCamperStationRelocationCorrelation correlation)
    {
        correlation=null;
        try
        {
            if(!ThreadManager.IsMainThread()||journal==null||removalOperation==Guid.Empty||installationOperation==Guid.Empty||
                removalOperation==installationOperation||original==null||publication==null||proof==null||
                !original.IsPublicationAttempted||!original.TryGetMobileAddress(out var source)||
                !RebirthCamperPortableStation.TryGetSourceAddress(carrier,out var retained)||!source.Equals(retained)||
                !RebirthCamperPortableStation.TryRestore(carrier,out var portable)||
                !proof.Address.IsValid||proof.Address.Installation!=source.Installation||proof.Address.Vehicle==source.Vehicle||
                !RebirthCamperStationPublicationRecord.TryRead(publication.Write(),original,out var parsed)||
                !parsed.MatchesSaveScope(GameIO.GetSaveGameDir())||
                !parsed.MatchesSaveScope(proof.SaveDirectory))return false;
            var removal=journal.Read(removalOperation);var installation=journal.Read(installationOperation);
            if(removal==null||installation==null||removal.State!=RebirthCamperTransferJournal.Phase.Applied||
                installation.State!=RebirthCamperTransferJournal.Phase.Applied||removal.Vehicle!=source.Vehicle||
                installation.Vehicle!=proof.Address.Vehicle||removal.OwnerCreation!=original.CreationId||
                installation.OwnerCreation!=original.CreationId)return false;
            var sourceBefore=RebirthCamperTransferSnapshot.Restore(removal.Vehicle,removal.Before);
            var sourceAfter=RebirthCamperTransferSnapshot.Restore(removal.Vehicle,removal.After);
            var destinationBefore=RebirthCamperTransferSnapshot.Restore(installation.Vehicle,installation.Before);
            var destinationAfter=RebirthCamperTransferSnapshot.Restore(installation.Vehicle,installation.After);
            var sourceState=Find(sourceBefore.Stations,source.Installation);
            var destinationState=Find(destinationAfter.Stations,source.Installation);
            int queuedOwner=(int)parsed.Write().Attribute("queuedOwner");
            if(sourceState==null||destinationState==null||Find(sourceAfter.Stations,source.Installation)!=null||
                Find(destinationBefore.Stations,source.Installation)!=null||!destinationAfter.Cursor.IsEmpty()||
                !SameItem(sourceAfter.Cursor,carrier)||!SameItem(destinationBefore.Cursor,carrier)||
                !RebirthCamperQueuedPublicationMatch.MatchesRestoredStation(sourceState,source,original,queuedOwner)||
                !RebirthCamperQueuedPublicationMatch.MatchesRestoredStation(portable,source,original,queuedOwner)||
                !RebirthCamperQueuedPublicationMatch.MatchesRestoredStation(destinationState,source,original,queuedOwner)||
                !RebirthCamperQueuedPublicationMatch.MatchesRestoredStation(proof.RestoreStationSnapshot(),source,original,queuedOwner))return false;
            correlation=new RebirthCamperStationRelocationCorrelation(source,proof.Address,removal,installation,original,parsed,proof);
            return true;
        }
        catch{return false;}
    }
}
