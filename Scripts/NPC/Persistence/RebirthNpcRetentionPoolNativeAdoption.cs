using System;
using System.Collections.Generic;

public static partial class RebirthNpcRetentionCoordinator
{
    private sealed class NativeDelegateCustody
    {
        internal World World,Incoming;internal Dictionary<string,Participant> Records;internal RebirthNpcNativeTransientAdoption Receipt;
        internal bool Stage(RebirthNpcRuntimeNativeAdoption.NativeReentry source)=>RebirthNpcAncillaryOriginalScope.TryFromNative(source,out var scope)&&StageScope(source.World,scope);
        internal bool StageOwnerScope(RebirthDogLifecycleService.NativeOwnerReentry source)=>RebirthNpcAncillaryOriginalScope.TryFromOwnerScope(source,out var scope)&&StageScope(source.World,scope);
        private bool StageScope(World incoming,RebirthNpcAncillaryOriginalScope scope)
        {
            lock(Sync)
            {
                if(Incoming!=null)return ReferenceEquals(Incoming,incoming)&&ReferenceEquals(retentionScope?.World,Incoming);
                if(Participants.Count!=0||retentionCallbacks!=0||retentionScope!=null||ReferenceEquals(Participants,Records)||
                    scope==null)return false;
                retentionScope=scope;Incoming=incoming;nativeDelegateCustodies.Add(this);return true;
            }
        }
    }
    private static readonly List<NativeDelegateCustody> nativeDelegateCustodies=new List<NativeDelegateCustody>();
    internal static RebirthNpcNativeTransientAdoption CaptureNativePendingAdoption(RebirthNpcSaveQuarantineScope scope)
    {
        lock(Sync)
        {
            try{RebirthNpcAncillaryOriginalScope.RequireScope(scope,retentionScope);}catch{return null;}
            if(retentionCallbacks!=0||Participants.Count>4096)return null;
            var custody=new NativeDelegateCustody{World=scope.World,Records=Participants};var records=Participants;var binding=retentionScope;
            var definitions=new Dictionary<string,Tuple<Participant,Func<long,int>>>(StringComparer.Ordinal);long chars=0;
            foreach(var pair in records)
            {if(pair.Value?.Sweep==null||pair.Value.Id!=pair.Key||(chars+=pair.Key.Length)>4L*1024*1024)return null;
                definitions.Add(pair.Key,Tuple.Create(pair.Value,pair.Value.Sweep));}
            long a=sweeps,b=itemsReleased,c=participantFailures;
            bool Current()
            {
                lock(Sync)
                {
                    if(!ReferenceEquals(Participants,records)||!ReferenceEquals(retentionScope,binding)||retentionCallbacks!=0||records.Count!=definitions.Count||
                        sweeps!=a||itemsReleased!=b||participantFailures!=c)return false;
                    foreach(var pair in definitions)if(!records.TryGetValue(pair.Key,out var row)||!ReferenceEquals(row,pair.Value.Item1)||
                        row.Id!=pair.Key||!ReferenceEquals(row.Sweep,pair.Value.Item2))return false;
                    return true;
                }
            }
            bool Available(){lock(Sync)return custody.Incoming==null?Participants.Count==0&&retentionCallbacks==0&&retentionScope==null&&!ReferenceEquals(Participants,records):ReferenceEquals(retentionScope?.World,custody.Incoming);}
            var receipt=new RebirthNpcNativeTransientAdoption("retention-delegates-empty",scope,Current,Available,()=>records.Count!=0,source=>{},custody.Stage,
                checked(chars+65536L*records.Count+1),()=>nativeDelegateCustodies.Remove(custody),ownerStage:source=>custody.Records.Count==0&&custody.StageOwnerScope(source));custody.Receipt=receipt;return receipt;
        }
    }
}

public static partial class RebirthNpcSerializationBufferPool
{
    private sealed class NativePoolCustody
    {
        internal World World,Incoming;internal HashSet<byte[]> Leases;internal Stack<byte[]> Buffers;
        internal RebirthNpcAncillaryOriginalScope Original;internal RebirthNpcNativeTransientAdoption Receipt;
        internal bool Stage(RebirthNpcRuntimeNativeAdoption.NativeReentry source)=>RebirthNpcAncillaryOriginalScope.TryFromNative(source,out var scope)&&StageScope(source.World,scope);
        internal bool StageOwnerScope(RebirthDogLifecycleService.NativeOwnerReentry source)=>RebirthNpcAncillaryOriginalScope.TryFromOwnerScope(source,out var scope)&&StageScope(source.World,scope);
        private bool StageScope(World incoming,RebirthNpcAncillaryOriginalScope scope)
        {
            lock(Sync)
            {
                if(Incoming!=null)return ReferenceEquals(Incoming,incoming)&&ReferenceEquals(poolScope?.World,Incoming);
                if(Leased.Count!=0||RebirthNpcSerializationBufferPool.Buffers.Count!=0||poolScope!=null||ReferenceEquals(Leased,Leases)||
                    scope==null)return false;
                poolScope=scope;Incoming=incoming;return true;
            }
        }
    }
    private static readonly List<NativePoolCustody> nativePoolCustodies=new List<NativePoolCustody>();
    private static bool TryReturnNativeRetained(byte[] buffer,bool clear)
    {
        lock(Sync)
        {
            foreach(var custody in nativePoolCustodies)
            {
                if(ReferenceEquals(Leased,custody.Leases)||!custody.Leases.Contains(buffer))continue;
                // The exact leased array is the original return token. Never
                // put it in the incoming pool or interpret it as saved data.
                custody.Leases.Remove(buffer);if(clear)Array.Clear(buffer,0,buffer.Length);return true;
            }
            return false;
        }
    }
    internal static RebirthNpcNativeTransientAdoption CaptureNativePendingAdoption(RebirthNpcSaveQuarantineScope scope)
    {
        lock(Sync)
        {
            try{RebirthNpcAncillaryOriginalScope.RequireScope(scope,poolScope);}catch{return null;}
            if(Leased.Count>8192||Buffers.Count>MaximumRetained)return null;
            long bytes=0;foreach(var buffer in Leased){if(buffer==null||(bytes=checked(bytes+buffer.LongLength))>64L*1024*1024)return null;}
            var custody=new NativePoolCustody{World=scope.World,Leases=Leased,Buffers=Buffers,Original=poolScope};
            var leased=Leased;var buffers=Buffers;var binding=poolScope;var identities=new HashSet<byte[]>(leased);var rows=buffers.ToArray();
            long a=rentals,b=returns,c=allocations,d=rejectedReturns;
            bool Current(){lock(Sync){if(!ReferenceEquals(Leased,leased)||!ReferenceEquals(Buffers,buffers)||!ReferenceEquals(poolScope,binding)||
                !leased.SetEquals(identities)||rentals!=a||returns!=b||allocations!=c||rejectedReturns!=d||buffers.Count!=rows.Length)return false;
                var current=buffers.ToArray();for(int i=0;i<rows.Length;i++)if(!ReferenceEquals(rows[i],current[i]))return false;return true;}}
            bool Available(){lock(Sync)return custody.Incoming==null?Leased.Count==0&&Buffers.Count==0&&poolScope==null&&!ReferenceEquals(Leased,leased):ReferenceEquals(poolScope?.World,custody.Incoming);}
            var receipt=new RebirthNpcNativeTransientAdoption("serialization-pool-idle",scope,Current,Available,()=>leased.Count!=0,source=>{},custody.Stage,
                checked(bytes+65536),()=>nativePoolCustodies.Remove(custody),ownerStage:source=>custody.Leases.Count==0&&custody.StageOwnerScope(source));custody.Receipt=receipt;
            // Register before detach: a worker may return its exact array
            // between pointer detachment and the first incoming NPC callback.
            nativePoolCustodies.Add(custody);return receipt;
        }
    }
}
