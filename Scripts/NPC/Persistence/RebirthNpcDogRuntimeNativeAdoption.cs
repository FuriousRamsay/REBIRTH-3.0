using System;
using System.Collections.Generic;
using System.Linq;

public static partial class RebirthDogRuntimeService
{
    internal static RebirthNpcRuntimeContinuationAdoption CaptureNativeAdoption(RebirthNpcSaveQuarantineScope scope)
    {
        lock(Sync)lock(MovementTraceSync)
        {
            if(!ThreadManager.IsMainThread()||scope==null||!scope.IsUnchangedOriginal()||Cache.Count>1024||
                dogRuntimeWorld!=null&&!ReferenceEquals(dogRuntimeWorld,scope.World))return null;
            var original=Cache;var originalTrace=ActiveMovementTrace;var priorWorld=dogRuntimeWorld;string image=DogRuntimeImage().OuterXml;
            var snapshots=original.Values.Select(value=>value.CopyForNative(value.Actor)).ToArray();
            var trace=originalTrace?.CopyForNative(originalTrace.Actor);
            var owners=new Dictionary<RebirthNpcStableId,RebirthNpcRuntimeNativeAdoption.Outgoing>();
            foreach(var actor in snapshots.Select(value=>value.Actor).Concat(trace==null?new EntityRebirthDogCompanion[0]:new[]{trace.Actor}).Distinct())
            {
                if(actor==null||!ReferenceEquals(actor.world,scope.World))return null;
                var source=RebirthNpcRuntimeNativeAdoption.CaptureOutgoing(actor,actor.RebirthRuntimeState);if(source==null)return null;
                owners[source.StableId]=source;
            }
            bool Current(){lock(Sync)lock(MovementTraceSync)return ReferenceEquals(Cache,original)&&ReferenceEquals(ActiveMovementTrace,originalTrace)&&
                ReferenceEquals(dogRuntimeWorld,priorWorld)&&DogRuntimeImage().OuterXml==image&&owners.Values.All(value=>value.Current());}
            World adopted=null;
            bool Adopt(World incoming,Dictionary<RebirthNpcStableId,RebirthNpcRuntimeNativeAdoption.NativeReentry> native)
            {
                lock(Sync)lock(MovementTraceSync)
                {
                    if(adopted!=null)return ReferenceEquals(adopted,incoming)&&ReferenceEquals(dogRuntimeWorld,incoming);
                    if(Cache.Count!=0||ActiveMovementTrace!=null||dogRuntimeWorld!=null)return false;
                    foreach(var owner in owners)if(!native.TryGetValue(owner.Key,out var source)||!source.HasDogOwner||!source.Matches(owner.Value)||
                        source.Actor is not EntityRebirthDogCompanion||source.Actor.RebirthRuntimeState?.StableId!=owner.Key)return false;
                    var next=new Dictionary<int,RuntimeCache>();
                    foreach(var snapshot in snapshots)
                    {
                        var dog=(EntityRebirthDogCompanion)native[snapshot.StableId].Actor;
                        if(next.ContainsKey(dog.entityId))return false;next.Add(dog.entityId,snapshot.CopyForNative(dog));
                    }
                    var nextTrace=trace==null?null:trace.CopyForNative((EntityRebirthDogCompanion)native[trace.StableId].Actor);
                    Cache=next;ActiveMovementTrace=nextTrace;dogRuntimeWorld=incoming;adopted=incoming;return true;
                }
            }
            return Current()?new RebirthNpcRuntimeContinuationAdoption(scope,new HashSet<RebirthNpcStableId>(owners.Keys),Current,Adopt):null;
        }
    }
}
