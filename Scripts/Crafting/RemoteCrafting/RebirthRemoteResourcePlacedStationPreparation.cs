using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;
using Newtonsoft.Json.Linq;

// Drives actual native Before observation into the immutable full artifact.
// ArtifactSaved is NOT Ready: aggregate allocation, client custody and all
// settlement/recovery qualification must separately precede source attempts.
internal interface IRebirthRemoteResourceRetainedNativeBuffers { bool IsDrained {get;} }
internal static class RebirthRemoteResourcePlacedStationPreparation
{
    private const int MaximumAttempts=32;
    private static readonly Dictionary<Guid,Attempt> attempts=new Dictionary<Guid,Attempt>();
    private static readonly Dictionary<Guid,ColdRead> coldReads=new Dictionary<Guid,ColdRead>();
    private static readonly Dictionary<Guid,UploadRead> uploadReads=new Dictionary<Guid,UploadRead>();
    private static readonly List<UploadRead> retiredNativeReads=new List<UploadRead>();
    private static readonly Dictionary<Guid,RebirthRemoteResourcePlacedStationAllocation.Admission> preparing=new Dictionary<Guid,RebirthRemoteResourcePlacedStationAllocation.Admission>();
    private static long retainedPlanningBytes;
    private static World attemptWorld;private static string attemptDirectory;
    internal static int ActiveAttemptCount=>attempts.Count;
    internal static bool HasRetainedAttempt(Guid source)=>attempts.ContainsKey(source)||preparing.ContainsKey(source)||uploadReads.ContainsKey(source);
    internal static long RetainedAllocationBytes=>retainedPlanningBytes;
    internal sealed class UploadRead:IDisposable
    {
        internal readonly Guid Source;private readonly World world;private readonly string directory;private long reserved;private bool closed,retiring;
        private IRebirthRemoteResourceRetainedNativeBuffers nativeBuffers;
        internal UploadRead(Guid source,long bytes){Source=source;reserved=bytes;world=attemptWorld;directory=attemptDirectory;}
        internal bool Current=>!closed&&!retiring&&ThreadManager.IsMainThread()&&ReferenceEquals(GameManager.Instance?.World,world)&&directory==GameIO.GetSaveGameDir()&&uploadReads.TryGetValue(Source,out var actual)&&ReferenceEquals(actual,this);
        internal long ReservedBytes=>reserved;
        internal bool Reserve(long bytes)
        {
            if(!Current||bytes<1||bytes>RebirthRemoteResourcePlacedStationArtifact.MaximumOperationWorkspace)return false;
            if(bytes<=reserved)return true;long extra=bytes-reserved;if(retainedPlanningBytes>RebirthRemoteResourcePlacedStationArtifact.MaximumOperationWorkspace-extra)return false;
            retainedPlanningBytes+=extra;reserved=bytes;return true;
        }
        internal bool RetainNativeBuffers(IRebirthRemoteResourceRetainedNativeBuffers buffers)
        {if(!Current||buffers==null||nativeBuffers!=null&&!ReferenceEquals(nativeBuffers,buffers))return false;nativeBuffers=buffers;return true;}
        public void Dispose()
        {
            if(closed)return;
            if(nativeBuffers!=null&&!nativeBuffers.IsDrained)
            {
                if(!retiring){retiring=true;if(uploadReads.TryGetValue(Source,out var value)&&ReferenceEquals(value,this))uploadReads.Remove(Source);retiredNativeReads.Add(this);}return;
            }
            closed=true;if(uploadReads.TryGetValue(Source,out var actual)&&ReferenceEquals(actual,this))uploadReads.Remove(Source);
            retiredNativeReads.Remove(this);retainedPlanningBytes-=reserved;nativeBuffers=null;
        }
    }
    internal static void RetireDrainedNativeReads(){if(ThreadManager.IsMainThread())foreach(var read in retiredNativeReads.ToArray())read.Dispose();}
    internal static bool TryReserveUploadRead(Guid source,long bytes,out UploadRead read)
    {
        read=null;if(!BindWorld()||source==Guid.Empty||bytes<1||bytes>RebirthRemoteResourcePlacedStationArtifact.MaximumOperationWorkspace||
            attempts.ContainsKey(source)||coldReads.ContainsKey(source)||uploadReads.ContainsKey(source)||preparing.ContainsKey(source)||
            attempts.Count+coldReads.Count+uploadReads.Count+preparing.Count+retiredNativeReads.Count>=MaximumAttempts||retainedPlanningBytes>RebirthRemoteResourcePlacedStationArtifact.MaximumOperationWorkspace-bytes)return false;
        read=new UploadRead(source,bytes);uploadReads.Add(source,read);retainedPlanningBytes+=bytes;return true;
    }
    internal static bool TryReserveCancellationRead(Guid operation,long bytes,out UploadRead read)
    {
        read=null;if(!BindWorldCore(false)||operation==Guid.Empty||bytes<1||bytes>RebirthRemoteResourcePlacedStationArtifact.MaximumOperationWorkspace||
            attempts.ContainsKey(operation)||coldReads.ContainsKey(operation)||uploadReads.ContainsKey(operation)||preparing.ContainsKey(operation)||
            attempts.Count+coldReads.Count+uploadReads.Count+preparing.Count+retiredNativeReads.Count>=MaximumAttempts||retainedPlanningBytes>RebirthRemoteResourcePlacedStationArtifact.MaximumOperationWorkspace-bytes)return false;
        read=new UploadRead(operation,bytes);uploadReads.Add(operation,read);retainedPlanningBytes+=bytes;return true;
    }
    internal static bool OwnsAllocation(RebirthRemoteResourcePlacedStationAllocation.Admission allocation)
        =>ThreadManager.IsMainThread()&&allocation!=null&&ReferenceEquals(GameManager.Instance?.World,attemptWorld)&&attemptDirectory==GameIO.GetSaveGameDir()&&
        (attempts.TryGetValue(allocation.Source,out var attempt)&&ReferenceEquals(attempt.Allocation,allocation)||
            preparing.TryGetValue(allocation.Source,out var original)&&ReferenceEquals(original,allocation));
    internal static bool TryReadRetained(EntityPlayer actor,RebirthRemoteResourceDurableJournal.Record root,out RebirthRemoteResourceClientOriginal.Intent original,
        out RebirthRemoteResourcePlacedStationCustody.Claim claim,out RebirthRemoteResourcePlacedStationAllocation.Admission allocation,
        out IReadOnlyDictionary<string,RebirthRemoteResourceSourceCustody.Lease> sources)
    {
        original=null;claim=null;allocation=null;sources=null;
        return ThreadManager.IsMainThread()&&root!=null&&attempts.TryGetValue(root.Operation,out var attempt)&&
            attempt.TryReadRetained(actor,root,out original,out claim,out allocation,out sources);
    }
    // Allocation/immutable-original observation only. Root4 source leases may
    // already have retired; this cannot grant a native/owner/queue setter.
    // Caller must still reparse positive full completion evidence and bind its
    // proof to the actual saved terminal before replying or releasing custody.
    internal static bool TryReadUsedAllocation(EntityPlayer actor,RebirthRemoteResourceDurableJournal.Record root,
        out RebirthRemoteResourceClientOriginal.Intent original,out RebirthRemoteResourcePlacedStationAllocation.Admission allocation)
    {
        original=null;allocation=null;
        return ThreadManager.IsMainThread()&&root!=null&&attempts.TryGetValue(root.Operation,out var attempt)&&attempt.TryReadUsedAllocation(actor,root,out original,out allocation);
    }
    internal static bool RetireTerminal(RebirthRemoteResourcePlacedStationOriginal.TerminalArchive receipt)
    {
        return receipt!=null&&receipt.Current&&attempts.TryGetValue(receipt.Root.Operation,out var attempt)&&attempt.RetireTerminal(receipt);
    }
    internal static bool RetireNeverEntered(RebirthRemoteResourcePlacedStationOriginal.NeverEnteredTerminal terminal)
    {
        if(terminal?.Current!=true)return false;
        return !attempts.TryGetValue(terminal.Root.Operation,out var attempt)||attempt.RetireNeverEntered(terminal);
    }
    internal static JObject CaptureAuditState(EntityPlayer actor)
    {
        var result=new JObject{["available"]=false,["producerActive"]=false};
        if(!ThreadManager.IsMainThread()||actor?.world?.IsRemote()!=false||!ReferenceEquals(GameManager.Instance?.World,actor.world)||
            !ReferenceEquals(actor.world.GetEntity(actor.entityId),actor)||!ReferenceEquals(attemptWorld,actor.world)||attemptDirectory!=GameIO.GetSaveGameDir()||
            !RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var owner))return result;
        var records=new JArray();foreach(var attempt in attempts.Values)
        {
            var row=attempt.CaptureAuditState(owner.StorageKey,actor.Died);if(row!=null)records.Add(row);
        }
        result["available"]=true;result["activeReadCount"]=coldReads.Count;result["retainedAllocationBytes"]=retainedPlanningBytes;
        result["maximumAllocationBytes"]=RebirthRemoteResourcePlacedStationArtifact.MaximumOperationWorkspace;result["records"]=records;return result;
    }
    // A cold read owns memory only. It cannot rebind inventory custody, a
    // target, actor permission, source effect or an original queue.
    internal sealed class ColdRead:IDisposable
    {
        internal readonly RebirthRemoteResourcePlacedStationArtifactStore.Header Header;
        private readonly World world;private readonly string directory;private long reserved;private bool closed;
        internal ColdRead(RebirthRemoteResourcePlacedStationArtifactStore.Header header,long charge)
        {Header=header;reserved=charge;world=attemptWorld;directory=attemptDirectory;}
        internal bool Current()=>!closed&&ThreadManager.IsMainThread()&&ReferenceEquals(GameManager.Instance?.World,world)&&
            directory==GameIO.GetSaveGameDir()&&coldReads.TryGetValue(Header.Source,out var actual)&&ReferenceEquals(actual,this);
        internal bool ReserveReadWorkspace(long workspace)
        {
            try
            {
                if(!Current()||workspace<1||workspace>RebirthRemoteResourcePlacedStationArtifact.MaximumOperationWorkspace)return false;
                long next=checked(Header.SourceWorkspace+workspace);if(next>RebirthRemoteResourcePlacedStationArtifact.MaximumOperationWorkspace)return false;
                if(next<=reserved)return true;long extra=next-reserved;
                if(retainedPlanningBytes>RebirthRemoteResourcePlacedStationArtifact.MaximumOperationWorkspace-extra)return false;
                retainedPlanningBytes+=extra;reserved=next;return true;
            }
            catch{return false;}
        }
        public void Dispose()
        {
            if(closed)return;closed=true;
            if(coldReads.TryGetValue(Header.Source,out var current)&&ReferenceEquals(current,this))coldReads.Remove(Header.Source);
            retainedPlanningBytes-=reserved;
        }
    }
    internal static bool TryReserveColdRead(RebirthRemoteResourcePlacedStationArtifactStore.Header header,out ColdRead read)
    {
        read=null;
        try
        {
            if(!BindWorld()||header==null||attempts.ContainsKey(header.Source)||coldReads.ContainsKey(header.Source)||uploadReads.ContainsKey(header.Source)||preparing.ContainsKey(header.Source)||
                attempts.Count+coldReads.Count+uploadReads.Count+preparing.Count+retiredNativeReads.Count>=MaximumAttempts)return false;
            long charge=checked(header.SourceWorkspace+3*header.Bytes);
            if(charge<1||charge>RebirthRemoteResourcePlacedStationArtifact.MaximumOperationWorkspace||
                retainedPlanningBytes>RebirthRemoteResourcePlacedStationArtifact.MaximumOperationWorkspace-charge)return false;
            var value=new ColdRead(header,charge);coldReads.Add(header.Source,value);retainedPlanningBytes+=charge;read=value;return true;
        }
        catch{read=null;return false;}
    }
    internal static bool TryGrowArtifactReservation(RebirthRemoteResourcePlacedStationAllocation.Admission admission,long artifactBytes)
        =>artifactBytes>=1&&artifactBytes<=RebirthRemoteResourcePlacedStationArtifact.MaximumBytes&&TryGrowReadReservation(admission,3*artifactBytes);
    internal static bool TryGrowReadReservation(RebirthRemoteResourcePlacedStationAllocation.Admission admission,long workspaceBytes)
    {
        try
        {
            if(!ThreadManager.IsMainThread()||admission==null||workspaceBytes<1||workspaceBytes>RebirthRemoteResourcePlacedStationArtifact.MaximumOperationWorkspace||
                !ReferenceEquals(GameManager.Instance?.World,attemptWorld)||attemptDirectory!=GameIO.GetSaveGameDir()||
                !attempts.TryGetValue(admission.Source,out var attempt)||!ReferenceEquals(attempt.Allocation,admission))return false;
            long next=checked(admission.PlanningBytes+workspaceBytes);
            if(next>RebirthRemoteResourcePlacedStationArtifact.MaximumOperationWorkspace)return false;
            if(next<=admission.ReservedBytes)return true;
            long delta=next-admission.ReservedBytes;
            if(retainedPlanningBytes>RebirthRemoteResourcePlacedStationArtifact.MaximumOperationWorkspace-delta)return false;
            retainedPlanningBytes+=delta;admission.ReservedBytes=next;return true;
        }
        catch{return false;}
    }
    private static bool BindWorld()=>BindWorldCore(true);
    private static bool BindWorldCore(bool authoritative)
    {
        RetireDrainedNativeReads();
        if(!ThreadManager.IsMainThread()||GameManager.Instance?.World==null||authoritative&&GameManager.Instance.World.IsRemote())return false;
        var world=GameManager.Instance.World;string directory=GameIO.GetSaveGameDir();
        if(!ReferenceEquals(world,attemptWorld)||directory!=attemptDirectory)
        {
            foreach(var value in new List<Attempt>(attempts.Values))value.DropEndedWorldTransient();
            foreach(var value in new List<ColdRead>(coldReads.Values))value.Dispose();
            foreach(var value in new List<UploadRead>(uploadReads.Values))value.Dispose();
            attempts.Clear();preparing.Clear();retainedPlanningBytes=retiredNativeReads.Sum(value=>value.ReservedBytes);attemptWorld=world;attemptDirectory=directory;
        }
        return true;
    }
    internal enum Progress{Observing,ArtifactSaved,Unknown}
    internal sealed class Attempt:IDisposable
    {
        private EntityPlayer actor;
        private RebirthRemoteResourcePlacedStationCustody.Claim claim;
        private RebirthRemoteResourceClientOriginal.Intent original;
        private RebirthRemoteResourcePlacedStationPlan.Plan plan;
        private IReadOnlyDictionary<string,RebirthRemoteResourceSourceCustody.Lease> leases;
        private RebirthRemoteResourceNativeBinding target;
        private readonly long revision;
        internal RebirthRemoteResourcePlacedStationAllocation.Admission Allocation {get;private set;}
        private RebirthRemoteResourceNativePublication.Watch watch;
        private bool closed,artifactSaved,acceptedUploadObserved;private string savedArtifactDigest;
        internal bool HasSavedArtifact=>artifactSaved;
        internal bool TryReadUsedAllocation(EntityPlayer current,RebirthRemoteResourceDurableJournal.Record root,
            out RebirthRemoteResourceClientOriginal.Intent intent,out RebirthRemoteResourcePlacedStationAllocation.Admission allocation)
        {
            intent=null;allocation=null;
            if(closed||!artifactSaved||root?.Phase!=4||(string)root.Copy().Attribute("disposition")!="used"||
                !ReferenceEquals(current,actor)||root.Operation!=claim.Source||root.Binding!=claim.Binding||root.Owner!=claim.Owner||
                current?.world!=claim.World||!ReferenceEquals(GameManager.Instance?.World,claim.World)||attemptDirectory!=GameIO.GetSaveGameDir()||
                current.IsDead()||!current.IsSpawned()||current.Died!=original.Deaths||!ReferenceEquals(claim.World.GetEntity(current.entityId),current)||
                !RebirthOriginalActionScope.Matches(current,root.Owner,original.Creation)||!claim.Matches(root,original))return false;
            intent=original;allocation=Allocation;return true;
        }
        internal bool TryReadRetained(EntityPlayer current,RebirthRemoteResourceDurableJournal.Record root,out RebirthRemoteResourceClientOriginal.Intent intent,
            out RebirthRemoteResourcePlacedStationCustody.Claim custody,out RebirthRemoteResourcePlacedStationAllocation.Admission allocation,
            out IReadOnlyDictionary<string,RebirthRemoteResourceSourceCustody.Lease> sources)
        {
            intent=null;custody=null;allocation=null;sources=null;
            if(closed||!artifactSaved||!ReferenceEquals(actor,current)||root.Revision<revision||root.Operation!=claim.Source||root.Binding!=claim.Binding||root.Owner!=claim.Owner||
                current?.world!=claim.World||current.IsDead()||!current.IsSpawned()||current.Died!=original.Deaths||!ReferenceEquals(claim.World.GetEntity(current.entityId),current)||
                !RebirthOriginalActionScope.Matches(current,root.Owner,root.Creation)||!claim.Matches(root,original)||!claim.TargetCurrent())return false;
            foreach(var lease in leases.Values)if(!lease.Current()||lease.Original.Operation!=root.Operation||lease.Original.Binding!=root.Binding)return false;
            intent=original;custody=claim;allocation=Allocation;
            var detached=new Dictionary<string,RebirthRemoteResourceSourceCustody.Lease>(StringComparer.Ordinal);
            foreach(var pair in leases)detached.Add(pair.Key,pair.Value);
            sources=new System.Collections.ObjectModel.ReadOnlyDictionary<string,RebirthRemoteResourceSourceCustody.Lease>(detached);return true;
        }
        internal JObject CaptureAuditState(string owner,int deaths)=>closed||claim==null||owner!=claim.Owner?null:new JObject
        {
            ["source"]=claim.Source.ToString("N"),["job"]=claim.Job.ToString("N"),["binding"]=claim.Binding,
            ["originalDeaths"]=claim.Deaths,["sameLife"]=deaths==claim.Deaths,["sourceRevision"]=revision,
            ["artifactSaved"]=artifactSaved,["artifactDigest"]=savedArtifactDigest??"",["reservedBytes"]=Allocation.ReservedBytes,
            ["planningBytes"]=Allocation.PlanningBytes,["sourceGraphBytes"]=Allocation.SourceGraphBytes,["targetCurrent"]=claim.TargetCurrent()
        };
        private void Saved(string digest)
        {artifactSaved=true;savedArtifactDigest=digest;watch?.Dispose();watch=null;plan=null;}
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private bool TryExisting(RebirthRemoteResourceDurableJournal.Record root,out string digest)
        {
            digest=null;
            if(!RebirthRemoteResourcePlacedStationArtifactStore.TryRead(root,out var bytes,out var retained,Allocation)||
                retained.Original.Digest!=original.Digest||retained.Birth!=claim.Birth)return false;
            digest=RebirthRemoteResourcePlacedStationArtifactStore.Digest(bytes);return true;
        }
        internal bool Matches(EntityPlayer owner,RebirthRemoteResourceDurableJournal.Record root,
            RebirthRemoteResourceClientOriginal.Intent intent,RebirthRemoteResourcePlacedStationCustody.Claim custody,
            IReadOnlyDictionary<string,RebirthRemoteResourceSourceCustody.Lease> sources)
        {
            if(closed||!ReferenceEquals(actor,owner)||!ReferenceEquals(claim,custody)||root.Revision!=revision||
                root.Operation!=claim.Source||root.Binding!=claim.Binding||intent.Digest!=original.Digest||sources.Count!=leases.Count)return false;
            foreach(var pair in sources)if(!leases.TryGetValue(pair.Key,out var lease)||!ReferenceEquals(pair.Value,lease))return false;
            return Current(out _);
        }
        internal Attempt(EntityPlayer owner,RebirthRemoteResourcePlacedStationCustody.Claim custody,
            RebirthRemoteResourceClientOriginal.Intent intent,RebirthRemoteResourcePlacedStationPlan.Plan candidate,
            IReadOnlyDictionary<string,RebirthRemoteResourceSourceCustody.Lease> sourceLeases,RebirthRemoteResourceNativeBinding binding,long sourceRevision,
            RebirthRemoteResourcePlacedStationAllocation.Admission allocation)
        {actor=owner;claim=custody;original=intent;plan=candidate;leases=sourceLeases;target=binding;revision=sourceRevision;Allocation=allocation;}
        private bool Current(out RebirthRemoteResourceDurableJournal.Record root)
        {
            root=null;
            return !closed&&actor!=null&&claim.ExactBefore()&&actor.world==claim.World&&actor.IsSpawned()&&!actor.IsDead()&&
                actor.Died==original.Deaths&&ReferenceEquals(claim.World.GetEntity(actor.entityId),actor)&&
                RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var owner)&&owner.StorageKey==claim.Owner&&owner.CanonicalId==original.Owner&&
                RebirthOriginalActionScope.Matches(actor,owner.StorageKey,original.Creation)&&
                RebirthRemoteResourcePlacedStationOriginal.ValidateInput(original,out var position,out _,out string window,out _,out _,out _,out _,out _,out var payment,out bool turnsOn,out _)&&
                RebirthRemoteResourcePlacedStationScope.TryResolveLayout(actor,position,original.Creation,out var station,out string actualWindow,out var actualPayment,out bool actualTurnsOn)&&
                ReferenceEquals(station,claim.Station)&&actualWindow==window&&actualPayment==payment&&actualTurnsOn==turnsOn&&
                RebirthRemoteResourceDurableJournal.TryRead(claim.Owner,claim.Source,out root,out bool unknown)&&!unknown&&
                root.Revision==revision&&root.Phase==1&&!root.HasOwnerAction&&root.SourcesUnattempted&&claim.Matches(root,original)&&
                RebirthRemoteResourceDurableJournal.NeverPreparedOwnerAction(root);
        }
        internal Progress Pump(out string artifactDigest)
        {
            artifactDigest=null;
            try
            {
                if(!Current(out var root))return Progress.Unknown;
                if(!acceptedUploadObserved)
                {
                    if(!RebirthRemoteResourcePlacedStationOriginal.TryObserveAcceptedAdmission(actor,root,original,claim,Allocation))return Progress.Unknown;
                    acceptedUploadObserved=true;
                }
                if(artifactSaved)
                {
                    if(!RebirthRemoteResourcePlacedStationPrepared.TryRead(root,out _,out artifactDigest,Allocation)||artifactDigest!=savedArtifactDigest)return Progress.Unknown;
                    return Progress.ArtifactSaved;
                }
                // Reuse an already positively published immutable original;
                // TryRead reparses full native artifacts and source history.
                if(TryExisting(root,out artifactDigest))
                {
                    if(!RebirthRemoteResourcePlacedStationPrepared.TryPublish(root,artifactDigest,Allocation))return Progress.Unknown;
                    Saved(artifactDigest);return Progress.ArtifactSaved;
                }
                if(watch==null)
                {
                    watch=RebirthRemoteResourceNativePublication.BeginPlacedTarget(target,root,leases,()=>Current(out _));
                    return Progress.Observing; // snapshot slot pressure never means no payment or refusal
                }
                if(!watch.Current())return Progress.Unknown;
                if(!ObserveBefore(out var before))return Progress.Observing;
                if(!Current(out root)||!RebirthRemoteResourcePlacedStationArtifactStore.TryPublish(root,original,claim.Birth,plan,before,out artifactDigest)||
                    !RebirthRemoteResourcePlacedStationPrepared.TryPublish(root,artifactDigest,Allocation))return Progress.Unknown;
                Saved(artifactDigest);return Progress.ArtifactSaved;
            }
            catch{artifactDigest=null;return Progress.Unknown;}
        }
        private bool ObserveBefore(out RebirthRemoteResourcePlacedStationBefore.Evidence before)
        {
            before=null;
            // Explicit retained buffer charge survives the parser phase: the
            // native evidence object is then held during artifact publication.
            long retained=3L*(RebirthStationSnapshotEvidence.MaximumSnapshotBytes+RebirthStationRegionPayload.MaximumPayloadBytes);
            if(!Allocation.ReserveReadWorkspace(retained)||!RebirthRemoteResourcePlacedStationAllocation.TryBeginScratch(Allocation,
                RebirthRemoteResourcePlacedStationAllocation.ReadPhase.NativeBefore,out var scratch))return false;
            using(scratch)return RebirthRemoteResourcePlacedStationBefore.TryObserve(watch,plan,out before);
        }
        public void Dispose()
        {
            if(closed)return;watch?.Dispose();watch=null;
            // Once its full original is published, keep the allocation slot
            // until the purpose-specific positive terminal/cold handoff exists.
            // Stopping observation is not evidence that no source was paid.
            if(artifactSaved)return;
            ReleaseTransient();
        }
        internal bool RetireTerminal(RebirthRemoteResourcePlacedStationOriginal.TerminalArchive receipt)
        {
            if(closed||!artifactSaved||receipt.Root.Operation!=claim.Source||leases==null||!receipt.Current||
                !RebirthRemoteResourceSourceCustody.RetirePlacedTerminal(receipt,leases.Values)||
                !RebirthRemoteResourcePlacedStationOriginal.RetireTerminalObservers(receipt))return false;
            // Graph references are gone before the aggregate charge is freed.
            ReleaseTransient();return true;
        }
        internal bool RetireNeverEntered(RebirthRemoteResourcePlacedStationOriginal.NeverEnteredTerminal terminal)
        {
            if(closed||terminal?.Current!=true||terminal.Root.Operation!=claim.Source||!claim.Matches(terminal.Root,terminal.Original)||
                !XNode.DeepEquals(original.Copy(),terminal.Original.Copy()))return false;
            // Watch.Dispose closes it under publicationGate and drops its
            // original/plan clones. Native captured request/payload buffers
            // retain their independent callback lifetime/budget ownership.
            ReleaseTransient();return true;
        }
        internal void DropEndedWorldTransient()
        {
            if(closed)return;
            if(ReferenceEquals(GameManager.Instance?.World,claim.World)&&attemptDirectory==GameIO.GetSaveGameDir())return;
            ReleaseTransient();
        }
        private void ReleaseTransient()
        {
            if(closed)return;closed=true;watch?.Dispose();watch=null;
            if(attempts.TryGetValue(claim.Source,out var retained)&&ReferenceEquals(retained,this))attempts.Remove(claim.Source);
            retainedPlanningBytes-=Allocation.ReservedBytes;
            plan=null;original=null;leases=null;actor=null;claim=null;target=null;Allocation=null;
            // No source/target lease, journal, paid fragment or original is
            // released. Cold continuation still needs full durable proof.
        }
    }
    internal static bool TryBegin(EntityPlayer actor,RebirthRemoteResourceDurableJournal.Record root,
        RebirthRemoteResourceClientOriginal.Intent original,RebirthRemoteResourcePlacedStationCustody.Claim claim,
        IReadOnlyDictionary<string,RebirthRemoteResourceSourceCustody.Lease> sourceLeases,out Attempt attempt)
    {
        attempt=null;
        bool reserved=false;long charge=0;
        try
        {
            if(!BindWorld()||actor==null||root==null||original==null||claim==null||sourceLeases==null||sourceLeases.Count>RebirthRemoteResourceDurableJournal.MaximumSources||preparing.ContainsKey(root.Operation))return false;
            if(attempts.TryGetValue(root.Operation,out var existing))
            {if(!existing.Matches(actor,root,original,claim,sourceLeases))return false;attempt=existing;return true;}
            if(attempts.Count+coldReads.Count+uploadReads.Count+preparing.Count+retiredNativeReads.Count>=MaximumAttempts||coldReads.ContainsKey(root.Operation)||uploadReads.ContainsKey(root.Operation)||
                root.Phase!=1||!root.SourcesUnattempted||root.HasOwnerAction||!claim.Matches(root,original)||!claim.ExactBefore()||
                !RebirthRemoteResourcePlacedStationCustody.TryReadGuardClaim(claim.Station,out var live)||!ReferenceEquals(live,claim)||
                !RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var owner)||
                !RebirthRemoteResourcePlacedStationAllocation.TryAdmitPlanning(root,original,claim.Station,out var allocation,
                    RebirthRemoteResourcePlacedStationArtifact.MaximumOperationWorkspace-retainedPlanningBytes)||
                retainedPlanningBytes>RebirthRemoteResourcePlacedStationArtifact.MaximumOperationWorkspace-allocation.PlanningBytes)return false;
            charge=allocation.PlanningBytes;retainedPlanningBytes+=charge;reserved=true;
            preparing.Add(root.Operation,allocation);
            if(!RebirthRemoteResourcePlacedStationAllocation.TryBeginScratch(allocation,RebirthRemoteResourcePlacedStationAllocation.ReadPhase.Planning,out var scratch))return false;
            using(scratch)
            {
            if(!RebirthCamperEnergySavedOwner.TryRead(owner,out var before,out _))return false;
            var position=claim.Station.ToWorldPos();string id="W:"+position.x.ToString(CultureInfo.InvariantCulture)+","+position.y.ToString(CultureInfo.InvariantCulture)+","+position.z.ToString(CultureInfo.InvariantCulture);
            if(!RebirthRemoteResourceNativeBinding.TryResolve(claim.World,id,RemoteResourceSourceKind.WorkstationOutput,claim.Birth,out var target)||
                !RebirthRemoteResourcePlacedStationPlan.TryCreate(claim.Station,original,root,before,actor.entityId,true,sourceLeases,out var plan,allocation))return false;
            var retainedLeases=new Dictionary<string,RebirthRemoteResourceSourceCustody.Lease>(StringComparer.Ordinal);
            foreach(var pair in sourceLeases)retainedLeases.Add(pair.Key,pair.Value);
            if(!scratch.Current)return false;
            attempt=new Attempt(actor,claim,original,plan,retainedLeases,target,root.Revision,allocation);attempts.Add(root.Operation,attempt);reserved=false;return true;
            }
        }
        catch{attempt=null;return false;}
        finally{if(root!=null)preparing.Remove(root.Operation);if(reserved)retainedPlanningBytes-=charge;}
    }
}
