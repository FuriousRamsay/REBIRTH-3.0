using System;
using System.IO;
using System.Linq;

// PREPARED NOT RUN. Harness supplies actual native published frames and drives
// the real server notice/owner processing; fixture never spawns or sets cells.
internal sealed class mpA_logistics_loaded_reference_fixture
{
    private readonly EntityBackpack body;private readonly EntityPlayerLocal owner;
    private readonly ItemStack[] bodyItems,bag,belt;private readonly bool[] locks;
    private readonly NavObject[] foreign;
    internal mpA_logistics_loaded_reference_fixture(EntityBackpack actual,EntityPlayerLocal actor)
    {
        body=actual;owner=actor;bodyItems=ItemStack.Clone(actual.bag.ItemGrid.items);bag=ItemStack.Clone(actor.bag.ItemGrid.items);belt=ItemStack.Clone(actor.inventory.items);
        locks=Enumerable.Range(0,actual.bag.ItemGrid.Length).Select(i=>actual.bag.ItemGrid.IsLocked(i)).ToArray();
        foreign=NavObjectManager.Instance.NavObjectList.Where(n=>!ReferenceEquals(n,actual.NavObject)).ToArray();
    }
    internal static void RequirePureCanonicalInverse(byte[] actualBefore,int oldOwner,int nextOwner)
    {
        var frozen=(byte[])actualBefore.Clone();var projected=RebirthLogisticsLoadedBodyNotice.Project(actualBefore,oldOwner,nextOwner);
        if(!actualBefore.SequenceEqual(frozen)||!RebirthLogisticsLoadedBodyNotice.Project(projected,nextOwner,oldOwner).SequenceEqual(frozen))throw new Exception("Canonical metadata projection changed more than its exact original reference.");
    }
    internal static void RequireRichNativeEnvelope(byte[] actualAllocated,int oldOwner,int nextOwner)
    {
        if(actualAllocated.Length<=131072||actualAllocated.Length>RebirthLogisticsLoadedBodyNotice.MaximumFrame)throw new Exception("Actual rich native boundary beyond former whole-ECD limit required.");
        RebirthLogisticsNativeBodyPreflight.Validate(actualAllocated,true);RequirePureCanonicalInverse(actualAllocated,oldOwner,nextOwner);
        var malformed=(byte[])actualAllocated.Clone();Buffer.BlockCopy(BitConverter.GetBytes(int.MaxValue),0,malformed,54,4);
        RequirePreflightRefusal(malformed,true);malformed=(byte[])actualAllocated.Clone();malformed[50]=1;RequirePreflightRefusal(malformed,true);
        RequirePreflightRefusal(new byte[RebirthLogisticsLoadedBodyNotice.MaximumFrame+1],true);
    }
    internal static void RequirePreflightRefusal(byte[] actualMalformed,bool allocated)
    {
        try{RebirthLogisticsNativeBodyPreflight.Validate(actualMalformed,allocated);}catch(InvalidDataException){return;}catch(EndOfStreamException){return;}
        throw new Exception("Malformed native envelope reached variable native allocation admission.");
    }
    internal static void RequireOnlyLifetimeNormalization(byte[] actualCanonical)
    {
        var frozen=(byte[])actualCanonical.Clone();var lifetime=(byte[])actualCanonical.Clone();float old=BitConverter.ToSingle(lifetime,9);
        if(float.IsNaN(old)||float.IsInfinity(old)||old<0)throw new Exception("Actual finite valid native lifetime required.");
        Buffer.BlockCopy(BitConverter.GetBytes(old==0f?1f:0f),0,lifetime,9,4);
        if(!RebirthLogisticsLoadedBodyNotice.CompareLifetimeOnly(actualCanonical,lifetime)||!actualCanonical.SequenceEqual(frozen))throw new Exception("Lifetime comparison mutated original or rejected valid sole volatile difference.");
        var pose=(byte[])actualCanonical.Clone();float x=BitConverter.ToSingle(pose,13);Buffer.BlockCopy(BitConverter.GetBytes(x==0f?1f:0f),0,pose,13,4);
        if(RebirthLogisticsLoadedBodyNotice.CompareLifetimeOnly(actualCanonical,pose))throw new Exception("Pose was normalized beyond the approved lifetime field.");
        foreach(float bad in new[]{float.NaN,float.PositiveInfinity,-1f})
        {
            var invalid=(byte[])actualCanonical.Clone();Buffer.BlockCopy(BitConverter.GetBytes(bad),0,invalid,9,4);bool accepted=false;
            try{accepted=RebirthLogisticsLoadedBodyNotice.CompareLifetimeOnly(actualCanonical,invalid);}catch(InvalidDataException){}
            if(accepted)throw new Exception("Invalid lifetime accepted as metadata identity.");
        }
    }
    internal static void RequireSuppressionModeBinding(RebirthLogisticsLoadedBodyNotice.Notice actual)
    {
        bool mode=actual.Suppressed;string policy=actual.PolicyWitness;
        string before=RebirthLogisticsLoadedBodyNoticeJournal.Digest(actual);
        try
        {
            actual.Suppressed=!mode;actual.PolicyWitness=actual.Suppressed?new string('a',64):string.Empty;
            if(RebirthLogisticsLoadedBodyNoticeJournal.Digest(actual)==before)throw new Exception("Normal and suppressed readiness share a digest.");
        }
        finally{actual.Suppressed=mode;actual.PolicyWitness=policy;}
        if(RebirthLogisticsLoadedBodyNoticeJournal.Digest(actual)!=before)throw new Exception("Fixture failed to restore immutable witness.");
    }
    internal static void RequireActualReadinessRevalidation(RebirthLogisticsTransferJournal.Record original,RebirthLogisticsLoadedBodyNotice.Notice notice,bool expectedCurrent)
    {
        string frozen=RebirthLogisticsTransferJournal.Encode(original);
        bool current=RebirthLogisticsLoadedBodyNotice.ServerCurrent(notice,out _);
        if(current!=expectedCurrent)throw new Exception("Actual current policy/tracking/full native body readiness differs from harness expectation.");
        if(!RebirthLogisticsTransferJournal.TryRead(original.Owner,original.Operation,out var retained)||RebirthLogisticsTransferJournal.Encode(retained)!=frozen)throw new Exception("Readiness revalidation changed item settlement.");
    }
    internal void RequireNativeSuppression()
    {
        RequireMaterialUnchanged();
        if(GamePrefs.GetInt(EnumGamePrefs.DeathPenalty)!=3||owner.GetDroppedBackpackPositions().Count!=0||body.RefPlayerId!=-1||body.NavObject!=null||!RebirthLogisticsLoadedBodyNotice.CanRefreshOwnedNav(body,out _))throw new Exception("Actual native suppression/readback expected; no fake current reference/nav.");
    }
    internal static void RequireDurableMetadataState(RebirthLogisticsTransferJournal.Record actual,Guid incarnation,Guid nonce,int life,string digest,int expected)
    {
        string original=RebirthLogisticsTransferJournal.Encode(actual);
        if(!RebirthLogisticsLoadedBodyNoticeJournal.TryLoad(actual,incarnation,nonce,life,out var notice,out int state,digest))throw new Exception("Actual persisted readiness expected.");
        using(notice)if(state!=expected||notice.Digest!=RebirthLogisticsLoadedBodyNoticeJournal.Digest(notice)||!RebirthLogisticsLoadedBodyNotice.Project(notice.Before,notice.Old,notice.Next).SequenceEqual(notice.After))throw new Exception("Durable full witness/readiness mismatch.");
        if(!RebirthLogisticsTransferJournal.TryRead(actual.Owner,actual.Operation,out var retained)||RebirthLogisticsTransferJournal.Encode(retained)!=original)throw new Exception("Metadata receipt changed original item/source/payment settlement.");
    }
    internal void RequireMaterialUnchanged()
    {
        if(!ReferenceEquals(owner.world.GetEntity(body.entityId),body)||!RebirthLogisticsDeathPartition.Same(bodyItems,body.bag.ItemGrid.items)||!RebirthLogisticsDeathPartition.Same(bag,owner.bag.ItemGrid.items)||!RebirthLogisticsDeathPartition.Same(belt,owner.inventory.items)||
            Enumerable.Range(0,locks.Length).Any(i=>body.bag.ItemGrid.IsLocked(i)!=locks[i])||foreign.Any(n=>!NavObjectManager.Instance.NavObjectList.Contains(n)))throw new Exception("Metadata notice replaced a body, changed material/locks, or removed a foreign nav.");
    }
    internal void RequireNativeNav(int expectedReference)
    {
        RequireMaterialUnchanged();if(body.RefPlayerId!=expectedReference||!RebirthLogisticsLoadedBodyNotice.CanRefreshOwnedNav(body,out _))throw new Exception("Expected exact native object-owned reference/nav.");
    }
    internal static void RequireKnownForeignNavRefusal(EntityBackpack actual)
    {if(RebirthLogisticsLoadedBodyNotice.CanRefreshOwnedNav(actual,out _))throw new Exception("Harness foreign/mixed nav should be refused before mutation.");}
    internal static void RequireCorruptFrameRefusal(byte[] malformed,int oldOwner,int nextOwner)
    {try{RebirthLogisticsLoadedBodyNotice.Project(malformed,oldOwner,nextOwner);}catch(InvalidDataException){return;}catch(EndOfStreamException){return;}throw new Exception("Malformed/truncated canonical ECD accepted.");}
}

// Deferred actual scenario matrix (no execution):
// 1 loaded old native body + actual saved host reference3 -> same body/new ref,
//   native transform-owned nav only; original owner inventory/body cells stable.
// 2 duplicate notice and already-exact After -> idempotent native refresh;
//   conflicting same nonce/proof with changed ECD/header must retain.
// 3 native penalty3+zero actual positions on both peers + host full untracked
//   proof -> typed suppression ACK after native HandleNavObject sets ref=-1;
//   native positions/policy change invalidates this digest readiness.
// 4 body absent/streaming, emodel pending, changed full Before, reused ID, wrong
//   owner/world/creation/current ref or later unrelated command -> retain.
// 5 own native nav, no nav, mixed/foreign class and foreign transform cases;
//   never unregister foreign nav, add tracker or evict native3-body rows.
// 6 native pressure,8jobs/16MiB client allowance, late write/world unload ->
//   retained original unchanged; no item/spawn/payment authority from notice.
// 7 reconnect cold completed accepted/root/parcel3 -> fresh actual same-snapshot
//   publication before same immutable notice; observed-before5 never sends CAS.

// Deferred durable matrix: crash after fsynced Prepared1/tmp before first send;
// lost notice/client missing body keeps GUID; native later load + exact full
// Before/After + own nav => ACK2; lost ACK retries same digest; stale sender,
// wrong native life/creation/world/GUID/nonce/digest never ACK; crash after ACK2
// before GUID cleanup reuses exact saved readiness; item Phase4 stays4 through
// reconnect terminal metadata CAS and native before/preflight/after publication.
// Root/607-parcel retirement independent per exact ACK, no body/marker recreate;
// native chunk still contains old GUID after cold metadata archive is driven
// ONLY by actual loaded GUID + typed original/full positive body proof.

// Deferred suppression matrix (actual native harness, never simulated authority):
// >3 parcels: nonzero native owner positions retain normal native nav even
// for an untracked body; zero positions require all-row body absence before
// typed suppression. No artificial tracker limit or insertion is introduced.
// Last native tracked bag looted: both actual native position views reach zero;
// reobserve same full body under a new mode/policy digest, never reuse normal ACK.
// Duplicate suppression notice: exact ref=-1/native no-nav is idempotent; no
// tracker insertion, timestamp, items, source effects or foreign-nav destruction.
// Delayed normal ACK after policy3/zero, delayed suppressed ACK after native
// positions become nonzero, changed all-row tracking fingerprint, stale native
// life/owner/world/creation/digest: readiness refused, GUID retained/reobserved.
// Policy changes during native nav callback or before host ACK fsync: current
// policy/positions/full witness must still match; unknown client body waits load.

// Deferred suppressed->normal transition: client holds exact native-produced
// Ref=-1/full canonical body; host publishes fresh normal-mode digest/current
// normal policy. Only exact derived -1 frame of that SAME body may reobserve;
// native owned nav refresh produces full After/refNext then NEW normal ACK.
// One changed bag/pose/birth/GUID/life/command or other reference refuses.

// Deferred near-ushort: genuine native EntityData65535 limit, outer ECD just
// above131072 but below scoped262144, exact bag/body suffix and all metadata;
// same actual publication and normal/suppressed durable ACK, no truncation.
// Declared bag intMax, stats/trader payload, truncated ushort body, wrong class,
// malformed flags or >262144 refuse BEFORE native reader allocations. Repeated
// prepare/query failures close decoded streams;8job/16MiB charge/native<2MiB
// wire remain fixed. Exact scoped wire admission precedes journal/send queue.
