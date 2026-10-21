using System;
using System.Collections.Generic;

// Allocation/lifetime account only. No receipt, actor, native write or completion
// permission is created here. Native background callers never invoke world code.
internal sealed class RebirthProductiveEarnedObservationBudget:IRebirthStationSnapshotByteBudget,IDisposable
{
    private const int Roots=4;
    private const long NativeBody=RebirthProductiveEarnedNativeBody.MaximumBodyBytes;
    private const long ItemBytes=196608;
    // Directly derived from shared bounded measurement components, not an
    // assumed native heap size or a cap copied from another transaction domain.
    private const long MeasureScratch=256L*RebirthNativeItemConformanceReader.MaximumNodes+528L*RebirthNativeItemConformanceReader.MaximumChildSlots+
        128L*(ItemBytes/5)+2L*RebirthNativeItemConformanceReader.MaximumAggregateStringBytes+ItemBytes*4+65536;
    private const long FrameGraph=512L*RebirthNativeItemConformanceReader.MaximumNodes+528L*RebirthNativeItemConformanceReader.MaximumChildSlots+
        16L*(RebirthProductiveEarnedRowProof.MaximumFrameBytes/4)+384L*(RebirthProductiveEarnedRowProof.MaximumFrameBytes/5)+
        2L*RebirthNativeItemConformanceReader.MaximumAggregateStringBytes;
    private const long NativeGraph=RebirthRemoteResourcePlacedStationArtifact.MaximumOperationWorkspace/3;
    internal const long DecoderMaximum=2*NativeGraph+4*FrameGraph+2*MeasureScratch+4*NativeBody+2L*RebirthProductiveEarnedRowProof.MaximumArtifactBytes;
    private const long Snapshot=RebirthStationSnapshotEvidence.MaximumSnapshotBytes,Payload=RebirthStationRegionPayload.MaximumPayloadBytes;
    private const long ProofWorkspace=2*NativeGraph+MeasureScratch+4*Snapshot+2*Snapshot+
        (5*Snapshot+Payload+128*1024)+Payload+512*1024;
    internal const long MaximumAllBytes=Roots*4L*RebirthProductiveEarnedRowProof.MaximumArtifactBytes+
        (DecoderMaximum>ProofWorkspace?DecoderMaximum:ProofWorkspace);
    private sealed class Account {internal string Binding;internal int Owners;internal long BaseBytes,Dynamic;internal bool TerminalRetained;}
    private static readonly object gate=new object();
    private static readonly Dictionary<string,Account> accounts=new Dictionary<string,Account>(StringComparer.Ordinal);
    private static long total;private static object phase;
    private static bool CanAddRoot()
    {
        if(accounts.Count>=2*Roots)return false;int active=0;
        foreach(var value in accounts.Values)if(!value.TerminalRetained)active++;
        return active<Roots;
    }
    // Allocation lifecycle only, called AFTER the owning real terminal. Keep
    // the same account/callbacks and byte ceiling while a successor starts.
    // This label is never a terminal receipt or an effect permission.
    internal void RetainCompletedAllocation()
    {
        lock(gate)if(!closed&&(key.StartsWith("UIO1:",StringComparison.Ordinal)||key.StartsWith("MP015N:",StringComparison.Ordinal))&&accounts.TryGetValue(key,out var current)&&ReferenceEquals(current,account))
            account.TerminalRetained=true;
    }
    // The ordinary producer calls this only after a positive saved native
    // output terminal. It preserves this account's byte charge while allowing
    // the one paid successor to open its separate original account.
    internal void RetainNativeTerminalAllocation()
    {lock(gate)if(!closed&&accounts.TryGetValue(key,out var current)&&ReferenceEquals(current,account))account.TerminalRetained=true;}
    private readonly string key;private readonly Account account;private bool closed;
    private RebirthProductiveEarnedObservationBudget(string key,Account account){this.key=key;this.account=account;}
    public int MaximumSnapshotBytes=>RebirthStationSnapshotEvidence.MaximumSnapshotBytes;
    // Allocation identity only for the separate owning-card domain. The UIO1
    // caller independently proves actor/lease/artifact/current native images.
    internal static RebirthProductiveEarnedObservationBudget TryOpenUiFrame(World world,Guid birth,Guid operation,byte[] image)
        =>TryOpenPurposeFrame("UIO1:",world,birth,operation,image);
    // Allocation identity for the separate MP015 native original. This does
    // not create a UIO registration, progression or native setter permission.
    internal static RebirthProductiveEarnedObservationBudget TryOpenNativeCompletion(World world,Guid birth,Guid operation,byte[] image)
        =>TryOpenPurposeFrame("MP015N:",world,birth,operation,image);
    private static RebirthProductiveEarnedObservationBudget TryOpenPurposeFrame(string purpose,World world,Guid birth,Guid operation,byte[] image)
    {
        if(!ThreadManager.IsMainThread()||!ReferenceEquals(world,GameManager.Instance?.World)||birth==Guid.Empty||operation==Guid.Empty||
            image==null||image.Length<1||image.Length>RebirthProductiveEarnedRowProof.MaximumArtifactBytes)return null;
        string worldKey=RebirthProductiveEarnedArtifact.WorldIdentity(world);if(worldKey==null)return null;
        string binding;using(var sha=System.Security.Cryptography.SHA256.Create())binding=Convert.ToBase64String(sha.ComputeHash(image));
        string key=purpose+worldKey+":"+birth.ToString("N")+":"+operation.ToString("N");long bytes=checked(4L*image.Length);
        lock(gate)
        {
            if(accounts.TryGetValue(key,out var existing))
            {if(existing.Binding!=binding||existing.BaseBytes!=bytes)return null;existing.Owners++;return new RebirthProductiveEarnedObservationBudget(key,existing);}
            if(!CanAddRoot()||total>MaximumAllBytes-bytes)return null;
            var account=new Account{Binding=binding,Owners=1,BaseBytes=bytes};accounts.Add(key,account);total+=bytes;
            return new RebirthProductiveEarnedObservationBudget(key,account);
        }
    }
    internal static RebirthProductiveEarnedObservationBudget TryOpen(RebirthProductiveEarnedArtifact.Prepared artifact,RebirthProductiveEarnedRowProof.Frame frame)
    {
        try
        {
            if(!ThreadManager.IsMainThread()||artifact?.Current()!=true||frame==null||frame.Operation!=artifact.Operation||frame.Birth!=artifact.Birth)return null;
            string binding=Convert.ToBase64String(artifact.Marker(frame)),key=frame.World+":"+frame.Birth.ToString("N")+":"+frame.Operation.ToString("N");
            long bytes=checked(4L*artifact.ImageLength);
            lock(gate)
            {
                if(accounts.TryGetValue(key,out var existing))
                {if(existing.Binding!=binding||existing.BaseBytes!=bytes)return null;existing.Owners++;return new RebirthProductiveEarnedObservationBudget(key,existing);}
                if(!CanAddRoot()||bytes<1||bytes>4L*RebirthProductiveEarnedRowProof.MaximumArtifactBytes||total>MaximumAllBytes-bytes)return null;
                var value=new Account{Binding=binding,Owners=1,BaseBytes=bytes};accounts.Add(key,value);total+=bytes;return new RebirthProductiveEarnedObservationBudget(key,value);
            }
        }
        catch{return null;}
    }
    // Client-native immutable retention is an allocation identity only. The
    // domain still validates file/scope/nonce/lease before every effect.
    internal static RebirthProductiveEarnedObservationBudget TryOpenOwner(EntityPlayerLocal actor,
        RebirthProductiveEarnedRowProof.Frame frame,byte[] image)
    {
        try
        {
            var world=GameManager.Instance?.World;
            if(!ThreadManager.IsMainThread()||actor==null||actor.IsDead()||world?.IsRemote()!=true||
                !ReferenceEquals(actor.world,world)||!ReferenceEquals(world.GetPrimaryPlayer(),actor)||frame==null||
                frame.Operation==Guid.Empty||frame.Birth==Guid.Empty||frame.World!=RebirthProductiveEarnedArtifact.WorldIdentity(world)||
                image==null||image.Length<1||image.Length>RebirthProductiveEarnedRowProof.MaximumArtifactBytes)return null;
            string binding=Convert.ToBase64String(RebirthProductiveEarnedRowProof.Marker(frame,image));
            string key=frame.World+":"+frame.Birth.ToString("N")+":"+frame.Operation.ToString("N");
            long bytes=checked(4L*image.Length);
            lock(gate)
            {
                if(accounts.TryGetValue(key,out var existing))
                {if(existing.Binding!=binding||existing.BaseBytes!=bytes)return null;existing.Owners++;return new RebirthProductiveEarnedObservationBudget(key,existing);}
                if(!CanAddRoot()||total>MaximumAllBytes-bytes)return null;
                var value=new Account{Binding=binding,Owners=1,BaseBytes=bytes};accounts.Add(key,value);total+=bytes;
                return new RebirthProductiveEarnedObservationBudget(key,value);
            }
        }
        catch{return null;}
    }
    internal static bool TryMeasureTools(byte[] frame,out long graph,out long scratch)
    {
        graph=scratch=0;
        try
        {
            if(frame==null||frame.Length<2||frame.Length>RebirthProductiveEarnedRowProof.MaximumFrameBytes||frame[0]!=1||frame[1]==0)return false;
            int offset=2;graph=checked(128L*frame[1]+32);
            for(int slot=0;slot<frame[1];slot++)
            {
                if(offset>frame.Length-2)return false;
                int count=frame[offset]|frame[offset+1]<<8;offset+=2;if(count==0)continue;
                if(!RebirthNativeItemConformanceReader.TryMeasureCanonicalV9PrefixStructure(frame,offset,out var measured,out int consumed))return false;
                offset=checked(offset+consumed);graph=checked(graph+measured.CloneAllocationBound);
                scratch=Math.Max(scratch,measured.ParserScratchBound);
            }
            return offset==frame.Length;
        }
        catch{graph=scratch=0;return false;}
    }
    internal static bool TryRetainDetached(long bytes,out IDisposable allocation)
    {
        allocation=null;if(!ThreadManager.IsMainThread()||bytes<1)return false;
        lock(gate){if(total>MaximumAllBytes-bytes)return false;total+=bytes;allocation=new Detached(bytes);return true;}
    }
    // Resource accounting only. The original token remains live throughout a
    // measured grow/narrow, including asynchronous holders of that same token.
    internal static bool TryResizeDetached(IDisposable allocation,long measured)
    {
        if(!ThreadManager.IsMainThread()||measured<1||allocation is not Detached retained)return false;
        lock(gate)
        {
            if(retained.bytes<1)return false;long difference=measured-retained.bytes;
            if(difference>0&&total>MaximumAllBytes-difference)return false;
            total+=difference;retained.bytes=measured;return true;
        }
    }
    private sealed class Detached:IDisposable
    {
        internal long bytes;internal Detached(long value){bytes=value;}
        public void Dispose(){lock(gate){total-=bytes;bytes=0;}}
    }
    internal sealed class Decoder:IDisposable
    {
        private long bytes;private bool closed;internal readonly int Thread=System.Threading.Thread.CurrentThread.ManagedThreadId;
        internal Decoder(long value){bytes=value;}
        internal bool Narrow(long measured)
        {
            lock(gate){if(closed||!ReferenceEquals(phase,this)||measured<1||measured>bytes)return false;total-=bytes-measured;bytes=measured;return true;}
        }
        internal bool Admit(RebirthProductiveEarnedRowProof.Frame frame,int imageBytes)
        {
            if(frame?.BeforeNative==null||frame.AfterNative==null||imageBytes<1||imageBytes>RebirthProductiveEarnedRowProof.MaximumArtifactBytes)return false;
            if(!RebirthProductiveEarnedNativeBody.TryMeasure(frame.BeforeNative,frame.BeforeElapsedOffset,out long beforeGraph,out long beforeScratch)||
                !RebirthProductiveEarnedNativeBody.TryMeasure(frame.AfterNative,frame.AfterElapsedOffset,out long afterGraph,out long afterScratch))return false;
            try{return Narrow(checked(beforeGraph+afterGraph+4*FrameGraph+2*Math.Max(MeasureScratch,Math.Max(beforeScratch,afterScratch))+
                2L*(frame.BeforeNative.Length+frame.AfterNative.Length)+2L*imageBytes));}catch{return false;}
        }
        internal bool IsCurrent {get{lock(gate)return !closed&&ReferenceEquals(phase,this);}}
        public void Dispose(){lock(gate){if(closed)return;closed=true;total-=bytes;if(ReferenceEquals(phase,this))phase=null;}}
    }
    internal static Decoder TryEnterDecoder()
    {
        lock(gate)
        {
            if(phase!=null||total>MaximumAllBytes-DecoderMaximum)return null;
            var scope=new Decoder(DecoderMaximum);phase=scope;total+=DecoderMaximum;return scope;
        }
    }
    // Saved-candidate discovery may compose its existing child/partial/location
    // decoders under one conservative decoder reservation on this same thread.
    // An observation phase or another thread never lends that reservation.
    internal static IDisposable TryEnterCandidateDecoder()
    {
        lock(gate)
        {
            if(phase is Decoder decoder&&decoder.IsCurrent&&decoder.Thread==System.Threading.Thread.CurrentThread.ManagedThreadId)return new Borrow();
            return TryEnterDecoder();
        }
    }
    // One graph-bearing earned observation at a time. A queued original keeps
    // its immutable artifacts; pressure is not rejection/payment/unused proof.
    internal sealed class Observation:IDisposable
    {
        internal RebirthProductiveEarnedObservationBudget Owner;internal IDisposable Graph;internal bool Closed;internal long Outstanding;
        internal void Retire(){if(Closed&&Outstanding==0&&ReferenceEquals(phase,this))phase=null;}
        public void Dispose()
        {
            lock(gate){if(Closed)return;Closed=true;Graph?.Dispose();Graph=null;Retire();}
        }
    }
    // Allocation scope only. Same-thread decoder nesting is already covered
    // by its conservative graph/parser workspace. An own native observation
    // owns its measured graph/comparison workspace until callback tokens retire.
    internal IDisposable TryEnterCallbacks()
    {
        lock(gate)
        {
            if(closed)return null;
            if(phase is Decoder decoder&&decoder.IsCurrent&&decoder.Thread==System.Threading.Thread.CurrentThread.ManagedThreadId)return new Borrow();
            if(phase is Observation active&&!active.Closed&&ReferenceEquals(active.Owner.account,account))return new Borrow();
        }
        return TryEnterDecoder();
    }
    private sealed class Borrow:IDisposable {public void Dispose(){}}
    internal Observation TryObserveNative(byte[] native,int elapsedOffset)
    {
        long graph,scratch;
        using(var decoder=TryEnterDecoder())
        {
            if(decoder==null||!RebirthProductiveEarnedNativeBody.TryMeasure(native,elapsedOffset,out graph,out scratch))return null;
        }
        return TryObserve(graph,scratch,native.Length);
    }
    internal Observation TryObserveMobile(RebirthCamperStationState station)
    {
        long graph,scratch;int length;
        using(var decoder=TryEnterDecoder())
        {
            if(decoder==null)return null;
            var bytes=RebirthCamperStationSnapshot.CaptureNativeMeasurement(station,MaximumSnapshotBytes);length=bytes.Length;
            if(!RebirthRemoteResourcePlacedStationNativeReader.TryMeasure(bytes,out graph,out scratch))return null;
        }
        return TryObserve(graph,scratch,length);
    }
    internal Observation TryObserve(long measuredGraph,long measuredScratch,int actualNativeBytes)
    {
        if(measuredGraph<0||measuredScratch<0||actualNativeBytes<1||actualNativeBytes>Snapshot)return null;
        long bytes;try{bytes=checked(2*measuredGraph+measuredScratch+4L*actualNativeBytes+512*1024);}catch{return null;}
        lock(gate)
        {
            if(closed||phase!=null||bytes>ProofWorkspace||!Reserve(bytes,out var allocation))return null;
            var scope=new Observation{Owner=this,Graph=allocation};phase=scope;return scope;
        }
    }
    public bool TryReserveAllocation(long bytes,out IDisposable reservation)
    {
        lock(gate)
        {
            reservation=null;
            if(closed||phase is not Observation active||active.Closed||!ReferenceEquals(active.Owner.account,account))return false;
            if(!Reserve(bytes,out reservation))return false;
            active.Outstanding+=bytes;((Allocation)reservation).Observation=active;return true;
        }
    }
    private bool Reserve(long bytes,out IDisposable reservation)
    {
        reservation=null;
        if(closed||bytes<1||total>MaximumAllBytes-bytes||!accounts.TryGetValue(key,out var actual)||!ReferenceEquals(actual,account))return false;
        total+=bytes;account.Dynamic+=bytes;reservation=new Allocation(key,account,bytes);return true;
    }
    private sealed class Allocation:IDisposable
    {
        private readonly string key;private readonly Account account;private readonly long bytes;private bool closed;internal Observation Observation;
        internal Allocation(string key,Account account,long bytes){this.key=key;this.account=account;this.bytes=bytes;}
        public void Dispose(){lock(gate){if(closed)return;closed=true;total-=bytes;account.Dynamic-=bytes;if(Observation!=null){Observation.Outstanding-=bytes;Observation.Retire();}Remove(key,account);}}
    }
    private static void Remove(string key,Account value)
    {if(value.Owners==0&&value.Dynamic==0&&accounts.TryGetValue(key,out var current)&&ReferenceEquals(current,value)){total-=value.BaseBytes;accounts.Remove(key);}}
    public void Dispose(){lock(gate){if(closed)return;closed=true;account.Owners--;Remove(key,account);}}
}
