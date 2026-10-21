using System;
using System.Collections.Generic;

public static partial class RebirthNpcSpatialIndex
{
    internal static RebirthNpcRuntimeContinuationAdoption CaptureNativeAdoption(RebirthNpcSaveQuarantineScope scope)
    {
        lock(Sync)
        {
            try{RebirthNpcAncillaryOriginalScope.RequireScope(scope,spatialScope);}catch{return null;}
            var original=Entries;var cells=Cells;var binding=spatialScope;string image;
            try{image=SpatialImage().OuterXml;}catch{return null;}
            var required=new HashSet<RebirthNpcStableId>();foreach(var row in original.Values)required.Add(row.StableId);
            bool Current(){lock(Sync)return ReferenceEquals(Entries,original)&&ReferenceEquals(Cells,cells)&&ReferenceEquals(spatialScope,binding)&&SpatialImage().OuterXml==image;}
            World adopted=null;
            bool Adopt(World incoming,Dictionary<RebirthNpcStableId,RebirthNpcRuntimeNativeAdoption.NativeReentry> native)
            {
                lock(Sync)
                {
                    if(adopted!=null)return ReferenceEquals(adopted,incoming)&&spatialScope?.Current()==true;
                    if(ReferenceEquals(Entries,original)||ReferenceEquals(Cells,cells)||
                        spatialScope!=null&&!ReferenceEquals(spatialScope.World,incoming))return false;
                    foreach(var id in required)
                    {
                        if(!native.TryGetValue(id,out var source)||!source.Current()||!ReferenceEquals(source.World,incoming)||
                            source.Actor.RebirthRuntimeState?.StableId!=id)return false;
                        var position=source.Actor.position;
                        if(float.IsNaN(position.x)||float.IsInfinity(position.x)||float.IsNaN(position.y)||
                            float.IsInfinity(position.y)||float.IsNaN(position.z)||float.IsInfinity(position.z))return false;
                    }
                    foreach(var id in required)
                    {
                        var actor=native[id].Actor;var position=actor.position;
                        Upsert(actor.entityId,position.x,position.y,position.z);
                    }
                    if(!UseSpatialScope()||spatialScope?.Current()!=true)return false;
                    adopted=incoming;return true;
                }
            }
            return new RebirthNpcRuntimeContinuationAdoption(scope,required,Current,Adopt);
        }
    }
}

public static partial class RebirthNpcPathBudgetCoordinator
{
    internal static RebirthNpcRuntimeContinuationAdoption CaptureNativeAdoption(RebirthNpcSaveQuarantineScope scope)
    {
        lock(Sync)
        {
            try{RebirthNpcAncillaryOriginalScope.RequireScope(scope,pathScope);}catch{return null;}
            var binding=pathScope;var values=Remaining;string image=PathImage().OuterXml;
            bool Current(){lock(Sync)return ReferenceEquals(pathScope,binding)&&ReferenceEquals(Remaining,values)&&PathImage().OuterXml==image;}
            World adopted=null;
            bool Adopt(World incoming,Dictionary<RebirthNpcStableId,RebirthNpcRuntimeNativeAdoption.NativeReentry> native)
            {
                lock(Sync)
                {
                    if(adopted!=null)return ReferenceEquals(adopted,incoming)&&pathScope?.Current()==true;
                    if(ReferenceEquals(Remaining,values)||pathScope!=null||epoch>=0||accepted!=0||deferred!=0)return false;
                    // A budget is an incoming native tick resource. Retained
                    // old counters confer no extra path allowance or work claim.
                    EnsureInitialized();
                    if(pathScope?.Current()!=true)return false;
                    adopted=incoming;return true;
                }
            }
            return new RebirthNpcRuntimeContinuationAdoption(scope,new HashSet<RebirthNpcStableId>(),Current,Adopt);
        }
    }
}
