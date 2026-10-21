using System;
using System.Collections.Generic;
using System.Linq;

public static partial class RebirthCompanionBehaviorService
{
    internal static RebirthNpcRuntimeContinuationAdoption CaptureNativeAdoption(RebirthNpcSaveQuarantineScope scope)
    {
        lock(Sync)
        {
            if(!ThreadManager.IsMainThread()||scope==null||!scope.IsUnchangedOriginal())return null;
            if(behaviorWorld!=null&&!ReferenceEquals(behaviorWorld,scope.World)||NpcStates.Count>8192)return null;
            var original=NpcStates;var priorWorld=behaviorWorld;ulong priorGeneration=behaviorGeneration;
            var values=original.ToDictionary(pair=>pair.Key,pair=>new State{Mode=pair.Value.Mode,StopAttacking=pair.Value.StopAttacking});
            var owners=new Dictionary<RebirthNpcStableId,RebirthNpcRuntimeNativeAdoption.Outgoing>();
            foreach(var id in values.Keys)
            {
                if(!RebirthNpcRuntimeRegistry.TryGetEntityId(id,out int entity)||scope.World.GetEntity(entity) is not EntityRebirthNPC actor)return null;
                var source=RebirthNpcRuntimeNativeAdoption.CaptureOutgoing(actor,actor.RebirthRuntimeState);if(source==null)return null;owners.Add(id,source);
            }
            bool Current(){lock(Sync)return ReferenceEquals(NpcStates,original)&&ReferenceEquals(behaviorWorld,priorWorld)&&behaviorGeneration==priorGeneration&&
                original.Count==values.Count&&values.All(pair=>original.TryGetValue(pair.Key,out var state)&&state.Mode==pair.Value.Mode&&state.StopAttacking==pair.Value.StopAttacking)&&owners.Values.All(value=>value.Current());}
            World adopted=null;
            bool Adopt(World incoming,Dictionary<RebirthNpcStableId,RebirthNpcRuntimeNativeAdoption.NativeReentry> native)
            {
                lock(Sync)
                {
                    if(adopted!=null)return ReferenceEquals(adopted,incoming)&&ReferenceEquals(behaviorWorld,incoming);
                    if(NpcStates.Count!=0||behaviorWorld!=null||!RebirthNpcRecoveryGeneration.TryReadObservedGeneration(incoming,out ulong generation))return false;
                    foreach(var owner in owners)if(!native.TryGetValue(owner.Key,out var source)||!source.Matches(owner.Value))return false;
                    var next=values.ToDictionary(pair=>pair.Key,pair=>new State{Mode=pair.Value.Mode,StopAttacking=pair.Value.StopAttacking});
                    NpcStates=next;behaviorWorld=incoming;behaviorGeneration=generation;adopted=incoming;return true;
                }
            }
            return Current()?new RebirthNpcRuntimeContinuationAdoption(scope,new HashSet<RebirthNpcStableId>(values.Keys),Current,Adopt):null;
        }
    }
}

public static partial class RebirthCompanionInventoryLockService
{
    internal static RebirthNpcRuntimeContinuationAdoption CaptureNativeAdoption(RebirthNpcSaveQuarantineScope scope)
    {
        lock(Sync)
        {
            if(!ThreadManager.IsMainThread()||scope==null||!scope.IsUnchangedOriginal())return null;
            if(itemLockWorld!=null&&!ReferenceEquals(itemLockWorld,scope.World)||NpcLocks.Count>8192)return null;
            var original=NpcLocks;var priorWorld=itemLockWorld;ulong priorGeneration=itemLockGeneration;
            var values=new HashSet<string>(original,StringComparer.OrdinalIgnoreCase);
            var owners=new Dictionary<RebirthNpcStableId,RebirthNpcRuntimeNativeAdoption.Outgoing>();
            foreach(string key in values)
            {
                int split=key.IndexOf('|');if(split<=0||!RebirthNpcStableId.TryParse(key.Substring(0,split),out var id))return null;
                if(owners.ContainsKey(id))continue;
                if(!RebirthNpcRuntimeRegistry.TryGetEntityId(id,out int entity)||scope.World.GetEntity(entity) is not EntityRebirthNPC actor)return null;
                var source=RebirthNpcRuntimeNativeAdoption.CaptureOutgoing(actor,actor.RebirthRuntimeState);if(source==null)return null;owners.Add(id,source);
            }
            bool Current(){lock(Sync)return ReferenceEquals(NpcLocks,original)&&ReferenceEquals(itemLockWorld,priorWorld)&&itemLockGeneration==priorGeneration&&values.SetEquals(original)&&owners.Values.All(value=>value.Current());}
            World adopted=null;
            bool Adopt(World incoming,Dictionary<RebirthNpcStableId,RebirthNpcRuntimeNativeAdoption.NativeReentry> native)
            {
                lock(Sync)
                {
                    if(adopted!=null)return ReferenceEquals(adopted,incoming)&&ReferenceEquals(itemLockWorld,incoming);
                    if(NpcLocks.Count!=0||itemLockWorld!=null||!RebirthNpcRecoveryGeneration.TryReadObservedGeneration(incoming,out ulong generation))return false;
                    foreach(var owner in owners)if(!native.TryGetValue(owner.Key,out var source)||!source.Matches(owner.Value))return false;
                    NpcLocks=new HashSet<string>(values,StringComparer.OrdinalIgnoreCase);itemLockWorld=incoming;itemLockGeneration=generation;adopted=incoming;return true;
                }
            }
            return Current()?new RebirthNpcRuntimeContinuationAdoption(scope,new HashSet<RebirthNpcStableId>(owners.Keys),Current,Adopt):null;
        }
    }
}
