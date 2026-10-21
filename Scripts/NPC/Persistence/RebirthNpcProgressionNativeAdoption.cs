using System;
using System.Collections.Generic;

public static partial class RebirthNpcProgressionProjectionRevisionService
{
    internal static RebirthNpcRuntimeContinuationAdoption CaptureNativeAdoption(RebirthNpcSaveQuarantineScope scope)
    {
        lock(Sync)
        {
            if(scope==null||revisionWorld!=null&&!ReferenceEquals(revisionWorld,scope.World))return null;
            var original=Revisions;var world=revisionWorld;string image=RevisionImage().OuterXml;
            uint epoch=RebirthNpcNetworkEpoch.CaptureOutgoingNativeEpoch(scope);
            bool Current(){lock(Sync)return ReferenceEquals(Revisions,original)&&ReferenceEquals(revisionWorld,world)&&RevisionImage().OuterXml==image;}
            bool Adopt(World incoming,Dictionary<RebirthNpcStableId,RebirthNpcRuntimeNativeAdoption.NativeReentry> native)
            {
                if(!RebirthNpcNetworkEpoch.IncomingNativeEpochCurrent(incoming,epoch))return false;
                lock(Sync)
                {
                    if(ReferenceEquals(Revisions,original)||revisionWorld!=null&&!ReferenceEquals(revisionWorld,incoming))return false;
                    if(revisionWorld==null&&Revisions.Count!=0)return false;
                    // New epoch publication owns its own revisions. Historical
                    // revision counters remain with the original delivery graph.
                    revisionWorld=incoming;return true;
                }
            }
            return new RebirthNpcRuntimeContinuationAdoption(scope,new HashSet<RebirthNpcStableId>(),Current,Adopt);
        }
    }
}

public static partial class RebirthNpcProgressionClientCache
{
    internal static RebirthNpcRuntimeContinuationAdoption CaptureNativeAdoption(RebirthNpcSaveQuarantineScope scope)
    {
        lock(Sync)
        {
            if(scope==null||projectionWorld!=null&&!ReferenceEquals(projectionWorld,scope.World))return null;
            var original=Values;var generations=Generations;var world=projectionWorld;
            uint priorEpoch=projectionEpoch,priorConnection=projectionConnection;
            uint outgoingEpoch=RebirthNpcNetworkEpoch.CaptureOutgoingNativeEpoch(scope);
            string image=ProgressionCacheImage().OuterXml;
            bool Current(){lock(Sync)return ReferenceEquals(Values,original)&&ReferenceEquals(Generations,generations)&&
                ReferenceEquals(projectionWorld,world)&&projectionEpoch==priorEpoch&&projectionConnection==priorConnection&&ProgressionCacheImage().OuterXml==image;}
            bool Adopt(World incoming,Dictionary<RebirthNpcStableId,RebirthNpcRuntimeNativeAdoption.NativeReentry> native)
            {
                if(!RebirthNpcNetworkEpoch.IncomingNativeEpochCurrent(incoming,outgoingEpoch))return false;
                uint incomingEpoch=RebirthNpcNetworkEpoch.GetServerEpoch();
                lock(Sync)
                {
                    if(ReferenceEquals(Values,original)||ReferenceEquals(Generations,generations))return false;
                    if(projectionWorld!=null)return ReferenceEquals(projectionWorld,incoming)&&projectionEpoch==incomingEpoch&&projectionConnection==0;
                    if(Values.Count!=0||Generations.Count!=0||projectionEpoch!=0||projectionConnection!=0)return false;
                    // Populate only through real incoming baseline/progression
                    // handlers; no retained cache row is made current by IDs.
                    projectionWorld=incoming;projectionEpoch=incomingEpoch;projectionConnection=0;return true;
                }
            }
            return new RebirthNpcRuntimeContinuationAdoption(scope,new HashSet<RebirthNpcStableId>(),Current,Adopt);
        }
    }
}
