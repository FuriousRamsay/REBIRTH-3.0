using System;using System.IO;using System.Linq;using System.Xml.Linq;
static partial class RebirthWorldCharacterRepository {
    // Candidate supporting immutable mixed tuple: prospective clone only, no authority minted by DATA.
    internal static bool ValidateStationMixedProspective(RebirthWorldCharacterRecord owner,MixedCompletionRecord data)
    {
        if(!serverAuthority||owner==null||data==null||!IsCurrentCachedRecord(owner)||owner.Progression.StationCompletionPublications.ContainsKey(data.Job)||owner.Progression.StationCompletionExpectationProjections.ContainsKey(data.Job))return false;
        try{
            var clone=owner.Progression.Clone();
            if(clone.StationMixedCompletionRecords.TryGetValue(data.Job,out var existing)){if(!XNode.DeepEquals(existing.Write(),data.Write()))return false;}
            else clone.StationMixedCompletionRecords.Add(data.Job,data.Clone());
            var node=SerializeProgression(clone,owner.StablePlayerKey);
            return MixedCompletionRecord.TryReadAllData(node,out _)&&serverAuthority&&IsCurrentCachedRecord(owner);
        }catch{return false;}
    }
    internal static bool HasSavedStationMixedOriginal(RebirthStablePlayerIdentity identity,RebirthWorldCharacterRecord owner,MixedLiveRecordCandidate original)
    {
        if(!serverAuthority||identity==null||owner==null||original==null||!IsCurrentCachedRecord(owner)||!original.RevalidatePublished()||
            identity.StorageKey!=owner.StablePlayerKey||identity.CanonicalId!=owner.StablePlayerId||
            !owner.Progression.StationMixedCompletionRecords.TryGetValue(original.Data.Job,out var retained)||!ReferenceEquals(retained,original.Data))return false;
        string path=GetPath(identity.StorageKey);if(string.IsNullOrEmpty(path))return false;
        lock(GetWriteLock(identity.StorageKey)){
            try{
                if(!serverAuthority||!TryLoadValidatedRecord(path,identity,out var saved,out var migrated,out _,out _)||migrated||
                    saved?.Progression==null||!RebirthSurvivorRequestScope.Matches(owner.Origin?.CreationId,saved.Origin?.CreationId)||
                    !saved.Progression.StationMixedCompletionRecords.TryGetValue(retained.Job,out var final)||!XNode.DeepEquals(final.Write(),retained.Write())||
                    !saved.Progression.StationPreparations.TryGetValue(retained.Job,out var a)||
                    !saved.Progression.StationTerminalIntents.TryGetValue(retained.Job,out var i)||
                    !saved.Progression.StationPublications.TryGetValue(retained.Job,out var q)||
                    !MixedNativePublicationCandidate.TryRead(final.Write().Element("completed").Elements().Single(),a,i,q,out var publication)||
                    !XNode.DeepEquals(publication.Write(),original.Publication.Write()))return false;
                return serverAuthority&&path==GetPath(identity.StorageKey)&&IsCurrentCachedRecord(owner)&&
                    owner.Progression.StationMixedCompletionRecords.TryGetValue(retained.Job,out var current)&&ReferenceEquals(current,retained)&&original.RevalidatePublished();
            }catch{return false;}
        }
    }

}