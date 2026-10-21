using System;
using System.Collections.Generic;
using System.Linq;

public static partial class RebirthNpcReplicationPerformanceService
{
    internal static RebirthNpcRuntimeContinuationAdoption CaptureNativeAdoption(RebirthNpcSaveQuarantineScope scope)
    {
        lock(Sync)
        {
            if(!ThreadManager.IsMainThread()||scope==null||!scope.IsUnchangedOriginal()||relationshipFlushActive||dispatchCallbacks!=0||
                RelationshipOrder.Count!=0||Adapter is not RebirthNpcNullReplicationDispatchAdapter)return null;
            RebirthNpcAncillaryOriginalScope.RequireScope(scope,projectionScope);
            var originalScope=projectionScope;var originalVisuals=Visuals;var originalSequences=RelationshipSequences;
            var originalReplay=RelationshipReplayIds;var originalReplayOrder=RelationshipReplayOrder;string image=ProjectionImage().OuterXml;
            var sequences=new Dictionary<string,long>(RelationshipSequences,StringComparer.Ordinal);
            var replay=new HashSet<Guid>(RelationshipReplayIds);var replayOrder=RelationshipReplayOrder.ToArray();var counts=ProjectionCounts();
            var owners=new Dictionary<RebirthNpcStableId,RebirthNpcRuntimeNativeAdoption.Outgoing>();
            var ids=new HashSet<RebirthNpcStableId>(Visuals.Keys);
            foreach(string key in sequences.Keys){if(!RebirthNpcStableId.TryParse(key,out var id))return null;ids.Add(id);}
            foreach(var id in ids)
            {
                if(!RebirthNpcRuntimeRegistry.TryGetEntityId(id,out int entity)||scope.World.GetEntity(entity) is not EntityRebirthNPC actor)return null;
                var source=RebirthNpcRuntimeNativeAdoption.CaptureOutgoing(actor,actor.RebirthRuntimeState);if(source==null)return null;owners.Add(id,source);
            }
            bool Current(){lock(Sync)return ReferenceEquals(projectionScope,originalScope)&&ReferenceEquals(Visuals,originalVisuals)&&
                ReferenceEquals(RelationshipSequences,originalSequences)&&ReferenceEquals(RelationshipReplayIds,originalReplay)&&
                ReferenceEquals(RelationshipReplayOrder,originalReplayOrder)&&!relationshipFlushActive&&dispatchCallbacks==0&&RelationshipOrder.Count==0&&
                Adapter is RebirthNpcNullReplicationDispatchAdapter&&ProjectionImage().OuterXml==image&&owners.Values.All(value=>value.Current());}
            World adopted=null;
            bool Adopt(World incoming,Dictionary<RebirthNpcStableId,RebirthNpcRuntimeNativeAdoption.NativeReentry> native)
            {
                lock(Sync)
                {
                    if(adopted!=null)return ReferenceEquals(adopted,incoming)&&projectionScope?.Current()==true;
                    if(!EmptyProjection||projectionScope!=null)return false;
                    foreach(var owner in owners)if(!native.TryGetValue(owner.Key,out var source)||!source.Matches(owner.Value)||source.Actor.RebirthRuntimeState?.StableId!=owner.Key)return false;
                    if(!UseProjectionScope())return false;
                    var visuals=new Dictionary<RebirthNpcStableId,VisualTrack>();
                    foreach(var id in originalVisuals.Keys)
                    {
                        var actor=native[id].Actor;
                        // New native baseline; old interpolation/velocity does
                        // not span the unloaded native World boundary.
                        visuals.Add(id,new VisualTrack{Current=new RebirthNpcVisualSample{NpcId=id,Revision=actor.RebirthRuntimeState.Revision,
                            ServerTicks=DateTime.UtcNow.Ticks,X=actor.position.x,Y=actor.position.y,Z=actor.position.z,Yaw=actor.rotation.y,IsCorrection=true}});
                    }
                    Visuals=visuals;RelationshipSequences=new Dictionary<string,long>(sequences,StringComparer.Ordinal);
                    RelationshipReplayIds=new HashSet<Guid>(replay);RelationshipReplayOrder=new Queue<Guid>(replayOrder);
                    SetProjectionCounts(counts);Epoch=-1;adopted=incoming;return true;
                }
            }
            return Current()?new RebirthNpcRuntimeContinuationAdoption(scope,ids,Current,Adopt):null;
        }
    }
}
