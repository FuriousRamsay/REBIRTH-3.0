using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;

internal enum RebirthPoiClearanceState { Discovered = 0, Cleared = 1, ResetPending = 2 }
internal enum RebirthPoiResetDisposition { None = 0, NoMutation = 1, Completed = 2 }

// This is an adapter assertion, not a native completion detector. Only the authoritative
// producer may construct it after resolving the complete authored combat-volume manifest.
internal sealed class RebirthPoiClearEvidence
{
    public readonly Guid ProofId;
    public readonly int RequiredVolumes, VerifiedVolumes, SpawnedParticipants;
    public readonly ulong WorldTime;
    public RebirthPoiClearEvidence(Guid proofId, int requiredVolumes, int verifiedVolumes,
        int spawnedParticipants, int pendingSpawns, int liveParticipants, bool isSpawning, ulong worldTime)
    {
        if (proofId == Guid.Empty || requiredVolumes < 1 || requiredVolumes > 4096 ||
            requiredVolumes != verifiedVolumes || spawnedParticipants < 1 || spawnedParticipants > 1000000 ||
            pendingSpawns != 0 || liveParticipants != 0 || isSpawning)
            throw new ArgumentException("Incomplete POI clear evidence.");
        ProofId=proofId; RequiredVolumes=requiredVolumes; VerifiedVolumes=verifiedVolumes;
        SpawnedParticipants=spawnedParticipants; WorldTime=worldTime;
    }
}

internal sealed class RebirthPoiRepopulationEvidence
{
    public readonly Guid GenerationId;
    public readonly int LiveNativeParticipants;
    public readonly ulong WorldTime;
    public RebirthPoiRepopulationEvidence(Guid generationId,int liveNativeParticipants,ulong worldTime)
    {
        if(generationId==Guid.Empty || liveNativeParticipants<1 || liveNativeParticipants>1000000)
            throw new ArgumentException("Repopulation must have verified live native participants.");
        GenerationId=generationId; LiveNativeParticipants=liveNativeParticipants; WorldTime=worldTime;
    }
}

internal sealed class RebirthPoiClearanceRecord
{
    public readonly RebirthPoiIdentity Identity;
    public readonly long Epoch, Revision;
    public readonly RebirthPoiClearanceState State;
    public readonly RebirthPoiClearEvidence Clear;
    public readonly bool ResetOnly;
    public readonly RebirthPoiSupplyCredits SupplyCredits;
    public readonly Guid ResetId, LastResetId;
    public readonly RebirthPoiRepopulationEvidence LastRepopulation;
    public readonly RebirthPoiPartialObservation Observations;
    public readonly RebirthPoiResetPlan ResetPlan;
    public readonly RebirthPoiAuthoredResetBindings LastAuthoredReset;
    public readonly RebirthPoiResetDisposition LastResetDisposition;
    public readonly RebirthPoiClearanceState BeforeReset;
    public readonly RebirthPoiClearEvidence BeforeResetClear;
    internal RebirthPoiClearanceRecord(RebirthPoiIdentity identity, long epoch, long revision,
        RebirthPoiClearanceState state, RebirthPoiClearEvidence clear, Guid resetId,
        RebirthPoiClearanceState beforeReset, RebirthPoiClearEvidence beforeResetClear,
        Guid lastResetId, RebirthPoiResetDisposition lastResetDisposition, RebirthPoiRepopulationEvidence lastRepopulation=null,RebirthPoiPartialObservation observations=null,RebirthPoiResetPlan resetPlan=null,RebirthPoiAuthoredResetBindings lastAuthoredReset=null,bool resetOnly=false,RebirthPoiSupplyCredits supplyCredits=null)
    {
        if (identity == null || epoch < 0 || revision < 1 || !Enum.IsDefined(typeof(RebirthPoiClearanceState),state) ||
            !Enum.IsDefined(typeof(RebirthPoiResetDisposition),lastResetDisposition)) throw new ArgumentException("Invalid POI record.");
        if ((lastResetId == Guid.Empty) != (lastResetDisposition == RebirthPoiResetDisposition.None)) throw new ArgumentException("Invalid terminal receipt.");
        if (state == RebirthPoiClearanceState.ResetPending)
        {
            if (resetId == Guid.Empty || resetId == lastResetId || clear != null ||
                beforeReset == RebirthPoiClearanceState.ResetPending || !Enum.IsDefined(typeof(RebirthPoiClearanceState),beforeReset) ||
                (beforeReset == RebirthPoiClearanceState.Cleared) != (beforeResetClear != null)) throw new ArgumentException("Invalid reset intent.");
        }
        else if (resetId != Guid.Empty || beforeResetClear != null || beforeReset != RebirthPoiClearanceState.Discovered ||
            (state == RebirthPoiClearanceState.Cleared) != (clear != null)) throw new ArgumentException("Invalid clearance state.");
        if(observations!=null&&observations.Epoch!=epoch)throw new ArgumentException("Observation epoch differs.");
        if((state==RebirthPoiClearanceState.Cleared||state==RebirthPoiClearanceState.ResetPending&&beforeReset==RebirthPoiClearanceState.Cleared)&&observations!=null&&observations.Volumes.Values.Any(v=>v.Actors.Values.Any(a=>!a.Dead)))throw new ArgumentException("Cleared record contains living original participants.");
        if(resetPlan!=null&&(state!=RebirthPoiClearanceState.ResetPending||resetPlan.Transaction!=resetId))throw new ArgumentException("Reset plan does not bind original intent.");
        if(lastAuthoredReset!=null&&lastAuthoredReset.OriginalEpoch+1!=epoch)throw new ArgumentException("Authored runtime receipt differs from native generation epoch.");
        if(resetOnly&&(clear!=null||beforeResetClear!=null||observations!=null||lastRepopulation!=null||supplyCredits!=null))throw new ArgumentException("Reset-only custody cannot contain combat evidence.");
        if(supplyCredits!=null&&!supplyCredits.Matches(observations))throw new ArgumentException("Supply credit token lacks original death custody.");
        SupplyCredits=supplyCredits;ResetOnly=resetOnly;LastAuthoredReset=lastAuthoredReset;ResetPlan=resetPlan;Observations=observations;LastRepopulation=lastRepopulation; Identity=identity; Epoch=epoch; Revision=revision; State=state; Clear=clear; ResetId=resetId;
        BeforeReset=beforeReset; BeforeResetClear=beforeResetClear; LastResetId=lastResetId; LastResetDisposition=lastResetDisposition;
    }
}

// Copy-on-write state: callers save/publish the returned successor only after their durable
// store confirms the original-world write. Never publish a successor merely on method success.
internal sealed class RebirthPoiClearanceLedger
{
    public const int MaximumRecords=20000;
    public readonly Guid WorldId;
    public readonly long Revision;
    public readonly int ConservativeEncodedCharacters;
    private readonly object worldScope;
    private readonly Dictionary<string,RebirthPoiClearanceRecord> records;
    private readonly ReadOnlyDictionary<string,RebirthPoiClearanceRecord> view;
    public IReadOnlyDictionary<string,RebirthPoiClearanceRecord> Records { get { return view; } }
    public RebirthPoiClearanceLedger(Guid worldId, object scope) : this(worldId,scope,0,new Dictionary<string,RebirthPoiClearanceRecord>(StringComparer.Ordinal)) { }
    internal RebirthPoiClearanceLedger(Guid worldId, object scope, long revision, Dictionary<string,RebirthPoiClearanceRecord> values)
    {
        if(worldId==Guid.Empty || scope==null || revision<0 || values==null || values.Count>MaximumRecords) throw new ArgumentException("Invalid ledger.");
        WorldId=worldId; worldScope=scope; Revision=revision;
        records=new Dictionary<string,RebirthPoiClearanceRecord>(values,StringComparer.Ordinal);
        foreach(var p in records) if(p.Value==null || p.Key!=p.Value.Identity.Key || p.Value.Revision>revision) throw new ArgumentException("Invalid record revision/key.");
                // UTF16 names can expand to XML entities. Budget includes both current/before
        // evidence plus terminal receipts; it intentionally admits fewer large-name POIs.
        long budget=200;
        foreach(var record in records.Values) budget+=1800L+6L*(record.Identity.Prefab.Length+record.Identity.Biome.Length)+(record.SupplyCredits==null?0:record.SupplyCredits.ConservativeCharacters)+(record.Observations==null?0:record.Observations.ConservativeCharacters)+(record.ResetPlan==null?0:record.ResetPlan.ConservativeCharacters)+(record.LastAuthoredReset==null?0:record.LastAuthoredReset.ConservativeCharacters);
        if(budget>RebirthPoiClearanceCodec.MaximumCharacters) throw new ArgumentException("Ledger encoded budget exhausted.");
        ConservativeEncodedCharacters=(int)budget;
        view=new ReadOnlyDictionary<string,RebirthPoiClearanceRecord>(records);
    }
    private bool Scope(object scope,Guid worldId) { return ReferenceEquals(worldScope,scope) && WorldId==worldId; }
    private bool Find(RebirthPoiIdentity identity,out RebirthPoiClearanceRecord record)
    {
        record=null; return identity!=null && records.TryGetValue(identity.Key,out record) && record.Identity.Biome==identity.Biome;
    }
    private bool Next(RebirthPoiClearanceRecord record,out RebirthPoiClearanceLedger next)
    {
        next=null; if(Revision==long.MaxValue || record.Revision!=Revision+1) return false;
        var copy=new Dictionary<string,RebirthPoiClearanceRecord>(records,StringComparer.Ordinal); copy[record.Identity.Key]=record;
                try { next=new RebirthPoiClearanceLedger(WorldId,worldScope,Revision+1,copy); return true; }
        catch(ArgumentException) { return false; }
    }
    public bool TryDiscover(object scope,Guid worldId,long expectedRevision,RebirthPoiIdentity identity,out RebirthPoiClearanceLedger next,bool resetOnly=false)
    {
        next=null; if(!Scope(scope,worldId) || identity==null) return false;
        RebirthPoiClearanceRecord old;
        if(Find(identity,out old)) {
            if(!old.ResetOnly||resetOnly){next=this;return true;}
            if(old.State==RebirthPoiClearanceState.ResetPending||expectedRevision!=Revision||Revision==long.MaxValue)return false;
            return Next(new RebirthPoiClearanceRecord(identity,old.Epoch,Revision+1,old.State,null,Guid.Empty,RebirthPoiClearanceState.Discovered,null,old.LastResetId,old.LastResetDisposition,lastAuthoredReset:old.LastAuthoredReset,supplyCredits:old.SupplyCredits),out next);
        }
        if(records.ContainsKey(identity.Key) || expectedRevision!=Revision || Revision==long.MaxValue || records.Count>=MaximumRecords) return false;
        return Next(new RebirthPoiClearanceRecord(identity,0,Revision+1,RebirthPoiClearanceState.Discovered,null,Guid.Empty,
            RebirthPoiClearanceState.Discovered,null,Guid.Empty,RebirthPoiResetDisposition.None,resetOnly:resetOnly),out next);
    }
    public bool TryObservePartial(object scope,Guid worldId,long expectedRevision,RebirthPoiIdentity identity,long epoch,RebirthPoiPartialObservation observations,out RebirthPoiClearanceLedger next)
    {
        next=null;RebirthPoiClearanceRecord old;
        if(!Scope(scope,worldId)||observations==null||observations.Epoch!=epoch||!Find(identity,out old)||old.Epoch!=epoch||old.State!=RebirthPoiClearanceState.Discovered||old.ResetOnly)return false;
        if(old.Observations!=null&&old.Observations.Canonical==observations.Canonical){next=this;return true;}
        if(expectedRevision!=Revision||Revision==long.MaxValue||!observations.IsSuccessorOf(old.Observations))return false;
        return Next(new RebirthPoiClearanceRecord(identity,epoch,Revision+1,old.State,null,Guid.Empty,RebirthPoiClearanceState.Discovered,null,old.LastResetId,old.LastResetDisposition,old.LastRepopulation,observations,lastAuthoredReset:old.LastAuthoredReset,supplyCredits:old.SupplyCredits),out next);
    }
    public bool TryClear(object scope,Guid worldId,long expectedRevision,RebirthPoiIdentity identity,long epoch,
        RebirthPoiClearEvidence evidence,out RebirthPoiClearanceLedger next)
    {
        next=null; RebirthPoiClearanceRecord old;
        if(!Scope(scope,worldId) || evidence==null || !Find(identity,out old) || old.Epoch!=epoch || old.State==RebirthPoiClearanceState.ResetPending||old.ResetOnly) return false;
        if(old.Observations!=null&&old.Observations.Volumes.Values.Any(v=>v.Actors.Values.Any(a=>!a.Dead)))return false;
        if(old.State==RebirthPoiClearanceState.Cleared)
        {
            if(!SameEvidence(old.Clear,evidence)) return false; next=this; return true;
        }
        if(expectedRevision!=Revision || Revision==long.MaxValue) return false;
        RebirthPoiSupplyCredits earned;if(!RebirthPoiSupplyCredits.TryCredit(old.SupplyCredits,old.Observations,out earned))return false;
        return Next(new RebirthPoiClearanceRecord(identity,epoch,Revision+1,RebirthPoiClearanceState.Cleared,evidence,Guid.Empty,
            RebirthPoiClearanceState.Discovered,null,old.LastResetId,old.LastResetDisposition,old.LastRepopulation,old.Observations,lastAuthoredReset:old.LastAuthoredReset,supplyCredits:earned),out next);
    }
    internal static bool SameEvidence(RebirthPoiClearEvidence a,RebirthPoiClearEvidence b)
    {
        return a!=null && b!=null && a.ProofId==b.ProofId && a.RequiredVolumes==b.RequiredVolumes &&
            a.VerifiedVolumes==b.VerifiedVolumes && a.SpawnedParticipants==b.SpawnedParticipants && a.WorldTime==b.WorldTime;
    }
    // Native repopulation is distinct from a quest reset. Elapsed respawn time alone is
    // not evidence; the producer supplies the original observed native generation.
    public bool TryObserveRepopulation(object scope,Guid worldId,long expectedRevision,RebirthPoiIdentity identity,
        long originalEpoch,RebirthPoiRepopulationEvidence evidence,out RebirthPoiClearanceLedger next)
    {
        next=null; RebirthPoiClearanceRecord old;
        if(!Scope(scope,worldId) || evidence==null || !Find(identity,out old) || old.State==RebirthPoiClearanceState.ResetPending||old.ResetOnly) return false;
        if(old.LastRepopulation!=null && old.LastRepopulation.GenerationId==evidence.GenerationId)
        {
            if(originalEpoch==long.MaxValue || old.Epoch!=originalEpoch+1 ||
                old.LastRepopulation.LiveNativeParticipants!=evidence.LiveNativeParticipants || old.LastRepopulation.WorldTime!=evidence.WorldTime) return false;
            next=this; return true;
        }
        if(old.State!=RebirthPoiClearanceState.Cleared || old.Epoch!=originalEpoch || expectedRevision!=Revision ||
            originalEpoch==long.MaxValue || Revision==long.MaxValue) return false;
        return Next(new RebirthPoiClearanceRecord(identity,originalEpoch+1,Revision+1,RebirthPoiClearanceState.Discovered,null,Guid.Empty,
            RebirthPoiClearanceState.Discovered,null,old.LastResetId,old.LastResetDisposition,evidence,supplyCredits:RebirthPoiSupplyCredits.Retain(old.SupplyCredits,null)),out next);
    }
    // Called only for actual native reset intent. Quest offered/accepted is not a caller.
    public bool TryBeginReset(object scope,Guid worldId,long expectedRevision,RebirthPoiIdentity identity,long epoch,Guid resetId,out RebirthPoiClearanceLedger next,RebirthPoiResetPlan plan=null)
    {
        next=null; RebirthPoiClearanceRecord old;
        if(plan!=null&&plan.Transaction!=resetId)return false;
        if(!Scope(scope,worldId) || resetId==Guid.Empty || !Find(identity,out old) || old.Epoch!=epoch || old.LastResetId==resetId) return false;
        if(old.State==RebirthPoiClearanceState.ResetPending)
        { if(old.ResetId!=resetId||(old.ResetPlan==null)!=(plan==null)||plan!=null&&old.ResetPlan.Canonical!=plan.Canonical) return false; next=this; return true; }
        if(expectedRevision!=Revision || Revision==long.MaxValue) return false;
        return Next(new RebirthPoiClearanceRecord(identity,epoch,Revision+1,RebirthPoiClearanceState.ResetPending,null,resetId,
            old.State,old.Clear,old.LastResetId,old.LastResetDisposition,old.LastRepopulation,old.Observations,plan,old.LastAuthoredReset,old.ResetOnly,old.SupplyCredits),out next);
    }
    // Partial/uncertain native mutation never calls this terminal operation; keep ResetPending.
    public bool TryFinishReset(object scope,Guid worldId,long expectedRevision,RebirthPoiIdentity identity,long originalEpoch,
        Guid resetId,RebirthPoiResetDisposition disposition,out RebirthPoiClearanceLedger next,RebirthPoiPartialObservation retainedObservation=null,RebirthPoiAuthoredResetBindings authoredBindings=null)
    {
        next=null; RebirthPoiClearanceRecord old;
        if(!Scope(scope,worldId) || resetId==Guid.Empty || disposition==RebirthPoiResetDisposition.None ||
            !Enum.IsDefined(typeof(RebirthPoiResetDisposition),disposition) || !Find(identity,out old)) return false;
        if(old.LastResetId==resetId && old.State!=RebirthPoiClearanceState.ResetPending)
        {
            long terminalEpoch=disposition==RebirthPoiResetDisposition.Completed && originalEpoch<long.MaxValue ? originalEpoch+1 : originalEpoch;
            if(old.LastResetDisposition!=disposition || old.Epoch!=terminalEpoch || retainedObservation!=null&&(old.Observations==null||old.Observations.Canonical!=retainedObservation.Canonical)||authoredBindings!=null&&(old.LastAuthoredReset==null||old.LastAuthoredReset.Canonical!=authoredBindings.Canonical||old.LastAuthoredReset.OriginalEpoch!=authoredBindings.OriginalEpoch)) return false; next=this; return true;
        }
        if(old.State!=RebirthPoiClearanceState.ResetPending || old.ResetId!=resetId || old.Epoch!=originalEpoch ||
            expectedRevision!=Revision || Revision==long.MaxValue || (disposition==RebirthPoiResetDisposition.Completed && originalEpoch==long.MaxValue)) return false;
        bool completed=disposition==RebirthPoiResetDisposition.Completed;
        if(completed&&old.ResetPlan!=null&&old.ResetPlan.IsAuthored&&(authoredBindings==null||authoredBindings.OriginalEpoch!=originalEpoch||authoredBindings.OriginalPlan.Canonical!=old.ResetPlan.Canonical))return false;
        if(authoredBindings!=null&&(!completed||old.ResetPlan==null||authoredBindings.OriginalEpoch!=originalEpoch||authoredBindings.OriginalPlan.Canonical!=old.ResetPlan.Canonical))return false;
        if(completed&&retainedObservation==null&&old.Observations!=null&&old.Observations.Volumes.Values.Any(v=>v.Actors.Values.Any(a=>!a.Dead)))return false;
        if(retainedObservation!=null){var exact=completed&&old.Observations!=null?old.Observations.RollOverVerifiedReset(old.ResetPlan,authoredBindings):null;if(exact==null||exact.Canonical!=retainedObservation.Canonical)return false;}
        return Next(new RebirthPoiClearanceRecord(identity,completed?originalEpoch+1:originalEpoch,Revision+1,
            completed?RebirthPoiClearanceState.Discovered:old.BeforeReset,completed?null:old.BeforeResetClear,Guid.Empty,
            RebirthPoiClearanceState.Discovered,null,resetId,disposition,old.LastRepopulation,completed?retainedObservation:old.Observations,lastAuthoredReset:completed?authoredBindings:old.LastAuthoredReset,resetOnly:old.ResetOnly,supplyCredits:completed?RebirthPoiSupplyCredits.Retain(old.SupplyCredits,retainedObservation):old.SupplyCredits),out next);
    }
}


