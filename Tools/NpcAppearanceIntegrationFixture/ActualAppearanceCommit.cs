using System; using System.IO; using System.Collections.Generic; partial class AppearanceCommitStore {    internal static bool TryCommitResolvedAppearance(RebirthNpcStableId id, uint expectedRevision,
        RebirthHumanNpcAppearanceDescriptor original, RebirthHumanNpcAppearanceDescriptor proposed,
        out RebirthHumanNpcAppearanceDescriptor committed, out string error)
    {
        committed=original;error=string.Empty;
        if(!original.IsValid||!proposed.IsValid||proposed.Resolved==null||
            !original.TryWithResolved(proposed.Resolved,out var bound)||!bound.Equals(proposed))
        {error="resolved-appearance-binding-invalid";return false;}
        EnsureLoaded();
        var scope=RebirthNpcSaveScope.ObserveCurrent();
        lock(Sync)
        {
            if(!loaded||string.IsNullOrEmpty(scope.SaveDirectory)||
                !string.Equals(loadedSaveDirectory,scope.SaveDirectory,StringComparison.OrdinalIgnoreCase)||
                !string.Equals(loadedSaveScope,scope.Fingerprint??string.Empty,StringComparison.OrdinalIgnoreCase))
            {error="resolved-appearance-save-unavailable-or-changed";return false;}
            if(!Records.TryGetValue(id,out var previous))
            {error="resolved-appearance-existing-person-required";return false;}
            // Retry/concurrent resolution returns the durably owned value, never the new proposal.
            if(previous.HumanAppearance.HasValue&&previous.HumanAppearance.Value.Resolved!=null)
            {committed=previous.HumanAppearance.Value;return true;}
            if(previous.AggregateRevision!=expectedRevision||
                (previous.HumanAppearance.HasValue&&!previous.HumanAppearance.Value.Equals(original)))
            {error="resolved-appearance-original-revision-changed";return false;}
            var candidate=RebirthNpcAggregateRecordCopy.Copy(previous);
            candidate.HumanAppearance=proposed;
            candidate.AggregateRevision=Math.Max(1U,unchecked(previous.AggregateRevision+1U));
            if(!ValidatePublication(candidate,id,out error))return false;
            candidate.AggregateChecksum=ComputeRecordChecksum(candidate);
            var merged=new Dictionary<RebirthNpcStableId,RebirthNpcPersistentRecord>(Records);
            merged[id]=candidate;
            var ordered=new List<RebirthNpcPersistentRecord>(merged.Values);
            ordered.Sort((x,y)=>string.CompareOrdinal(x.Identity.StableNpcId.ToString(),y.Identity.StableNpcId.ToString()));
            bool backupPending;
            try {backupPending=WriteRecordsLocked(ordered,loadedSaveDirectory,loadedSaveScope);}
            catch(Exception ex){error="resolved-appearance-publication-failed:"+ex.GetType().Name+":"+ex.Message;return false;}
            Records=merged;InvalidateAllReadViewsLocked();dirty=backupPending;
            provenanceState=backupPending?"scoped-current-backup-pending":"scoped-current";
            saves++;committed=proposed;RecordWriteTelemetry(loadedSaveDirectory);return true;
        }
    }
}