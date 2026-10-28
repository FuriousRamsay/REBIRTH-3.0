using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

// PREPARED, NOT RUN. Harness supplies actual A originals and native transition.
// No fake save selection, journal effects, custody release or fixture file edits.
internal sealed class mpA_npc_original_transition_fixture
{
    private readonly World originalWorld;private readonly string save,fingerprint;private readonly ulong generation;
    private readonly RebirthNpcOriginalEffectAdmission.TransitionSeal seal;private readonly string[] ids;
    internal mpA_npc_original_transition_fixture(IEnumerable<RebirthNpcStableId> actualWholeDomain)
    {
        originalWorld=GameManager.Instance.World;save=Path.GetFullPath(GameIO.GetSaveGameDir());
        fingerprint=RebirthNpcSaveScope.ObserveCurrent().Fingerprint;generation=RebirthNpcRecoveryGeneration.RequireCurrent();
        var actual=actualWholeDomain.ToArray();ids=actual.Select(id=>id.ToString()).OrderBy(id=>id,StringComparer.Ordinal).ToArray();
        if(!RebirthNpcOriginalEffectAdmission.TryCaptureTransitionSeal(originalWorld,actual,fingerprint,generation,out seal,out var error))throw new Exception("Actual current-A custody capture required: "+error);
        if(!seal.CopyNpcIds().SequenceEqual(ids))throw new Exception("Capture changed the whole-domain ID set.");
    }
    internal static void RequireCurrentReaderAgreement(World actual,bool expectedReadable)
    {
        bool ordinary=RebirthLogisticsTransferJournal.TryReadPendingNativeOriginals(actual,out var records);
        bool compact=RebirthLogisticsTransferJournal.TryReadTransitionPending(actual,out var pending,out var heads);
        if(ordinary!=expectedReadable||compact!=ordinary)throw new Exception("Compact transition observation changed strict ordinary parse/unknown refusal.");
        if(!ordinary)return;
        if(records.Length!=pending.Length||heads.Length!=records.Length)throw new Exception("Transition observation omitted or duplicated an original.");
        foreach(var record in records)
        {
            var matches=pending.Where(value=>value.Owner==record.Owner&&value.Operation==record.Operation).ToArray();
            if(matches.Length!=1)throw new Exception("Transition observation changed original identity.");
            var expected=RebirthNpcOriginalEffectAdmission.Logistics(record);var current=matches[0];
            if(current.Domain!=expected.Domain||current.World!=expected.World||current.Creation!=expected.Creation||current.Deaths!=expected.Deaths)throw new Exception("Transition observation rebased immutable scope.");
            var a=expected.CopyClaims();var b=current.CopyClaims();
            if(a.Length!=b.Length||a.Any(claim=>!b.Any(other=>other.Npc==claim.Npc&&other.Binding==claim.Binding&&other.Dog==claim.Dog&&other.CopyEffects().SequenceEqual(claim.CopyEffects()))))throw new Exception("Transition observation changed exact original receipt claims.");
            string prefix=record.Owner+":"+record.Operation.ToString("N")+":";
            var head=heads.Where(value=>value.StartsWith(prefix,StringComparison.Ordinal)).ToArray();
            if(head.Length!=1||head[0].Length!=prefix.Length+64||!head[0].Substring(prefix.Length).All(Uri.IsHexDigit))throw new Exception("Missing bounded full original state fingerprint.");
        }
    }
    internal void RequireUnchangedOriginal(bool expected)
    {
        if(seal.IsUnchangedOriginal(originalWorld,save,fingerprint,generation,out var error)!=expected)throw new Exception("Unexpected captured original custody validity: "+error);
        if(!seal.CopyNpcIds().SequenceEqual(ids))throw new Exception("Validation rebased captured NPC identities.");
    }
    internal void RequireWrongIdentityRefusal(World otherWorld,string otherSave,string otherFingerprint,ulong otherGeneration)
    {
        if(ReferenceEquals(otherWorld,originalWorld)&&otherSave==save&&otherFingerprint==fingerprint&&otherGeneration==generation)throw new Exception("Harness must supply a distinct actual boundary.");
        if(seal.IsUnchangedOriginal(otherWorld,otherSave,otherFingerprint,otherGeneration,out _))throw new Exception("Original seal accepted substituted boundary identity.");
    }
}
// Deferred actual vectors: unchanged A->B validates captured A without B GameIO
// parsing; pending logical NPC and dog claims remain retained; NPC omitted from
// whole-domain union refuses; missing/corrupt/conflicting/sidecar cold originals
// refuse capture; actual admission/reserve/release/bootstrap/journal phase or
// failed write invalidates before atomic detach, including same final bytes ABA.
// Original head byte/name/directory/selected terminal/native generation change,
// junction ancestor, deleted root, changed A world/fingerprint/G refuse; B-only
// path contents never establish A emptiness/payment/refund or release custody.
// Missed pretransition capture cannot be rebuilt from B; no phase/revision reset.

// Compact reader agreement vectors: no root/empty actual current domain;
// logical/dog/combined/native-only originals; pending multiple owner/same-plan
// different-phase states; exact valid predecessor backup/next tmp; malformed
// newest or duplicate-conflicting archive; changed native world/wrong old A
// argument after B must match existing strict ordinary reader refusal. Compact
// implementation may retain claims/hashes only, never aggregate native arrays.
