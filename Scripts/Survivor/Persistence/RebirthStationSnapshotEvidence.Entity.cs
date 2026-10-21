using System;
using System.IO;
using System.Runtime.CompilerServices;
using HarmonyLib;

// Exact native entity serializer spans inside an already scoped chunk snapshot.
// This supplies observation, never spawn, item credit or settlement permission.
public static partial class RebirthStationSnapshotEvidence
{
    private const int MaximumEntityEvidenceBytes=1024*1024;
    private sealed class EntitySpec:IDisposable
    {
        internal int Id,Class;internal byte[] Expected;internal IDisposable Allocation;internal Action<EntityImage> Captured;
        public void Dispose(){Expected=null;Captured=null;Allocation?.Dispose();Allocation=null;}
    }
    private sealed class EntityFrame
    {internal Stream Stream;internal long Start;internal int Length,Count;internal bool Invalid;}
    private sealed class EntitySnapshot
    {internal Request Request;internal long Generation,Start;internal int Length;}
    private sealed class EntityWrite
    {internal Frame Frame;internal EntityFrame Span;internal EntitySpec Spec;internal Stream Stream;internal long Start;}
    private static readonly ConditionalWeakTable<Request,EntitySpec> entitySpecs=new ConditionalWeakTable<Request,EntitySpec>();
    private static readonly ConditionalWeakTable<Frame,EntityFrame> entityFrames=new ConditionalWeakTable<Frame,EntityFrame>();
    private static readonly ConditionalWeakTable<object,EntitySnapshot> entitySnapshots=new ConditionalWeakTable<object,EntitySnapshot>();
    private static bool entityHooks;
    private static bool EntityCurrent(Request request)
    {
        try{return request!=null&&!request.Closed&&request.ThreadId==System.Threading.Thread.CurrentThread.ManagedThreadId&&request.Observe!=null&&request.Observe()&&!request.Closed;}
        catch{return false;}
    }
    internal static Request WatchEntity(object world,object chunk,int x,int z,int id,int entityClass,byte[] expectedNativeFrame,Func<bool> current,IRebirthStationSnapshotByteBudget budget=null,Action<EntityImage> captured=null)
    {
        if(!ThreadManager.IsMainThread()||id<0||entityClass<0||current==null||expectedNativeFrame==null||expectedNativeFrame.Length<1||expectedNativeFrame.Length>MaximumEntityEvidenceBytes)return null;
        var request=Watch(world,chunk,x,z,current,budget:budget);if(request==null)return null;EntitySpec spec=null;
        try
        {
            spec=new EntitySpec{Id=id,Class=entityClass,Captured=captured};
            if(budget!=null&&!budget.TryReserveAllocation(expectedNativeFrame.Length+8192L,out spec.Allocation)){request.Dispose();return null;}
            spec.Expected=(byte[])expectedNativeFrame.Clone();entitySpecs.Add(request,spec);spec=null;
            if(!entityHooks)
            {
                var harmony=new Harmony("rebirth.station.native-entity-span");
                foreach(var type in new[]{typeof(NativeEntitySerialization),typeof(RebirthStationSnapshotUpdatePatch),typeof(RebirthStationChunkSerializedPatch),typeof(RebirthStationSnapshotResetPatch),typeof(RebirthStationSnapshotWritePatch),typeof(RebirthStationRegionPublishedPatch)})RebirthHarmonyBootstrap.PatchClassOnce(harmony,type);
                entityHooks=true;
            }
            return request;
        }
        catch{request.Dispose();return null;}
        finally{spec?.Dispose();}
    }
    private static EntityWrite BeginEntityWrite(EntityCreationData native,Stream stream,StreamModeWrite mode)
    {
        var frame=active;
        if(mode!=StreamModeWrite.Persistency||frame?.Request==null||frame.Request.Closed||!entitySpecs.TryGetValue(frame.Request,out var spec)||native?.id!=spec.Id)return null;
        var span=entityFrames.GetOrCreateValue(frame);span.Count++;
        try
        {
            if(span.Count!=1||native.entityClass!=spec.Class||stream==null||!stream.CanSeek||!stream.CanRead||spec.Expected==null||!EntityCurrent(frame.Request)){span.Invalid=true;return null;}
            return new EntityWrite{Frame=frame,Span=span,Spec=spec,Stream=stream,Start=stream.Position};
        }
        catch{span.Invalid=true;return null;}
    }
    private static void EndEntityWrite(EntityWrite write,Stream stream,Exception error)
    {
        if(write==null)return;var span=write.Span;
        if(error!=null||!ReferenceEquals(active,write.Frame)||!ReferenceEquals(stream,write.Stream)||!EntityCurrent(write.Frame.Request)||write.Spec.Expected==null){span.Invalid=true;return;}
        long end;
        try{end=stream.Position;}catch{span.Invalid=true;return;}
        try
        {
            if(write.Start<0||end-write.Start!=write.Spec.Expected.Length){span.Invalid=true;return;}
            byte[] scratch=new byte[Math.Min(8192,write.Spec.Expected.Length)];stream.Position=write.Start;int offset=0;
            while(offset<write.Spec.Expected.Length)
            {
                int count=stream.Read(scratch,0,Math.Min(scratch.Length,write.Spec.Expected.Length-offset));if(count<=0){span.Invalid=true;return;}
                for(int i=0;i<count;i++)if(scratch[i]!=write.Spec.Expected[offset+i]){span.Invalid=true;return;}
                offset+=count;
            }
            span.Stream=stream;span.Start=write.Start;span.Length=write.Spec.Expected.Length;
        }
        catch{span.Invalid=true;}
        finally{try{stream.Position=end;}catch{span.Invalid=true;}}
    }
    private static bool ValidateEntityEvidence(Frame frame,Stream stream,byte[] snapshot)
    {
        if(!entitySpecs.TryGetValue(frame.Request,out var spec))return true;
        return EntityCurrent(frame.Request)&&spec.Expected!=null&&entityFrames.TryGetValue(frame,out var span)&&!span.Invalid&&span.Count==1&&ReferenceEquals(span.Stream,stream)&&
            span.Length==spec.Expected.Length&&SpanMatches(snapshot,span.Start,spec.Expected);
    }
    private static void CommitEntityEvidence(Frame frame)
    {
        if(!entitySpecs.TryGetValue(frame.Request,out var spec)||!entityFrames.TryGetValue(frame,out var span))return;
        entitySnapshots.Remove(frame.Snapshot);entitySnapshots.Add(frame.Snapshot,new EntitySnapshot{Request=frame.Request,Generation=frame.Generation,Start=span.Start,Length=span.Length});
        // Runs before the native snapshot returns to its queueing caller. An
        // observer failure revokes evidence, never the native save operation.
        try{if(spec.Captured!=null&&TryGetSerializedEntity(frame.Request,out var image))spec.Captured(image);}
        catch{entitySnapshots.Remove(frame.Snapshot);}
    }
    private static void InvalidateEntityEvidence(object snapshot)=>entitySnapshots.Remove(snapshot);
    private static void DisposeEntityEvidence(Request request)
    {if(entitySpecs.TryGetValue(request,out var spec)){entitySpecs.Remove(request);spec.Dispose();}}
    // An immutable description of serializer evidence, not a publication proof.
    // The cold observer must independently read the native region bytes.
    internal sealed class EntityImage
    {
        internal readonly int X,Z,Bytes,Length;internal readonly long Offset,Generation;internal readonly string Digest;
        internal EntityImage(int x,int z,int bytes,long offset,int length,long generation,string digest)
        {X=x;Z=z;Bytes=bytes;Offset=offset;Length=length;Generation=generation;Digest=digest;}
    }
    internal static bool TryGetSerializedEntity(Request request,out EntityImage image)
    {
        image=null;lock(gate)
        {
            if(!EntityCurrent(request)||request.Snapshot==null||!entitySpecs.TryGetValue(request,out var spec)||spec.Expected==null||
                !entitySnapshots.TryGetValue(request.Snapshot,out var span)||span.Request!=request||span.Generation!=request.Generation||
                !states.TryGetValue(request.Snapshot,out var state)||state.Request!=request||state.Generation!=request.Generation||
                state.Bytes==null||span.Length!=spec.Expected.Length||!SpanMatches(state.Bytes,span.Start,spec.Expected))return false;
            string digest;using(var sha=System.Security.Cryptography.SHA256.Create())digest=BitConverter.ToString(sha.ComputeHash(state.Bytes)).Replace("-","");
            image=new EntityImage(request.X,request.Z,state.Bytes.Length,span.Start,span.Length,span.Generation,digest);return true;
        }
    }
    internal static bool TryGetPublishedEntity(Request request,out PublishedSnapshot proof)
    {
        proof=null;lock(gate)
        {
            if(!EntityCurrent(request)||request.Snapshot==null||!entitySpecs.TryGetValue(request,out var spec)||spec.Expected==null||
                !entitySnapshots.TryGetValue(request.Snapshot,out var span)||span.Request!=request||span.Generation!=request.Generation||span.Length!=spec.Expected.Length)return false;
            return TryValidatePublishedSnapshotSpans(request,new[]{span.Start},new[]{spec.Expected},out proof);
        }
    }
    [HarmonyPatch(typeof(EntityCreationData),nameof(EntityCreationData.write),typeof(PooledBinaryWriter),typeof(StreamModeWrite))]
    private static class NativeEntitySerialization
    {
        private static void Prefix(EntityCreationData __instance,PooledBinaryWriter __0,StreamModeWrite __1,out EntityWrite __state)
        {
            __state=null;
            try{__state=BeginEntityWrite(__instance,__0?.BaseStream,__1);}
            catch{if(active!=null&&entityFrames.TryGetValue(active,out var span))span.Invalid=true;}
        }
        private static Exception Finalizer(PooledBinaryWriter __0,EntityWrite __state,Exception __exception)
        {
            try{EndEntityWrite(__state,__0?.BaseStream,__exception);}
            catch{if(__state!=null)__state.Span.Invalid=true;}
            return __exception;
        }
    }
}
