using System;

// Full bounded cold observation, not actor/custody recovery or permission.
// Keep this lease alive while using its detached source/artifact graphs.
internal static class RebirthRemoteResourcePlacedStationColdRead
{
    internal sealed class Original:IDisposable
    {
        private RebirthRemoteResourcePlacedStationPreparation.ColdRead allocation;
        private RebirthRemoteResourceDurableJournal.Record source;
        private RebirthRemoteResourcePlacedStationArtifactReader.Image artifact;
        internal readonly string ArtifactDigest;
        internal Original(RebirthRemoteResourcePlacedStationPreparation.ColdRead budget,RebirthRemoteResourceDurableJournal.Record root,
            RebirthRemoteResourcePlacedStationArtifactReader.Image image,string digest)
        {allocation=budget;source=root;artifact=image;ArtifactDigest=digest;}
        internal bool Current=>allocation?.Current()==true;
        internal RebirthRemoteResourceDurableJournal.Record Source=>Current?source:null;
        internal RebirthRemoteResourcePlacedStationArtifactReader.Image Artifact=>Current?artifact:null;
        internal RebirthRemoteResourcePlacedStationPreparation.ColdRead Allocation=>Current?allocation:null;
        public void Dispose(){source=null;artifact=null;allocation?.Dispose();allocation=null;}
    }
    internal static bool TryOpen(EntityPlayer actor,Guid operation,out Original original,out bool unknown)
    {
        original=null;unknown=true;RebirthRemoteResourcePlacedStationPreparation.ColdRead allocation=null;
        try
        {
            if(!ThreadManager.IsMainThread()||actor?.world?.IsRemote()!=false||!actor.IsSpawned()||
                !ReferenceEquals(GameManager.Instance?.World,actor.world)||!ReferenceEquals(actor.world.GetEntity(actor.entityId),actor)||
                !RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var owner)||
                !RemoteResourceWorldIdentity.TryGet(actor.world,out string world)||
                !RebirthRemoteResourcePlacedStationArtifactStore.TryReadHeader(operation,out var header)||
                !RebirthRemoteResourcePlacedStationPreparation.TryReserveColdRead(header,out allocation))return false;
            // The bounded immutable header precedes the FIRST whole source
            // read. No prior unrestricted current-root hydration is required.
            if(!RebirthRemoteResourcePlacedStationAllocation.TryBeginScratch(allocation,RebirthRemoteResourcePlacedStationAllocation.ReadPhase.HistoricalSource,out var scratch))return false;
            RebirthRemoteResourceDurableJournal.Record root;
            using(scratch)
            {
                if(!RebirthRemoteResourceDurableJournal.TryReadPlacedHistoryBounded(owner.StorageKey,operation,header,out root,out bool corrupt)||corrupt||root==null)return false;
            }
            if(root.Owner!=owner.StorageKey||root.World!=world||!RebirthOriginalActionScope.Matches(actor,root.Owner,root.Creation)||
                !RebirthRemoteResourcePlacedStationOriginal.IsMappedRoot(root)||
                !RebirthRemoteResourcePlacedStationPrepared.TryRead(root,out var full,out string digest,null,allocation)||!full.HasCapturedAllocation||
                full.Original.Owner!=owner.CanonicalId||full.Original.World!=actor.world.worldState.Guid||full.Original.Creation!=root.Creation||
                !allocation.Current())return false;
            original=new Original(allocation,root,full,digest);allocation=null;unknown=false;return true;
        }
        catch{original=null;unknown=true;return false;}
        finally{allocation?.Dispose();}
    }
}
