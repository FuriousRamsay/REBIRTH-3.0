using System;
using System.Collections.Generic;
using System.Linq;

public static partial class RebirthNpcNativeEquipmentBridge
{
    internal static RebirthNpcRuntimeContinuationAdoption CaptureNativeAdoption(RebirthNpcSaveQuarantineScope scope)
    {
        lock(Sync)
        {
            if(!ThreadManager.IsMainThread()||scope==null||!scope.IsUnchangedOriginal()||Applied.Count>8192||
                projectionWorld!=null&&!ReferenceEquals(projectionWorld,scope.World))return null;
            var original=Applied;var priorWorld=projectionWorld;string priorDirectory=projectionDirectory;ulong priorFloor=generationFloor;
            string image=QuarantineNativeProjectionImage().OuterXml;
            var desired=original.ToDictionary(pair=>pair.Key,pair=>new{pair.Value.Generation,Revision=pair.Value.RequestedRevision,
                Slots=new Dictionary<RebirthNpcEquipmentSlot,string>(pair.Value.RequestedSlots)});
            var owners=new Dictionary<RebirthNpcStableId,RebirthNpcRuntimeNativeAdoption.Outgoing>();
            foreach(var id in desired.Keys)
            {
                if(!RebirthNpcRuntimeRegistry.TryGetEntityId(id,out int entity)||scope.World.GetEntity(entity) is not EntityRebirthHumanoidNPC actor)return null;
                var source=RebirthNpcRuntimeNativeAdoption.CaptureOutgoing(actor,actor.RebirthRuntimeState);if(source==null)return null;owners.Add(id,source);
            }
            bool Current(){lock(Sync)return ReferenceEquals(Applied,original)&&ReferenceEquals(projectionWorld,priorWorld)&&projectionDirectory==priorDirectory&&
                generationFloor==priorFloor&&QuarantineNativeProjectionImage().OuterXml==image&&owners.Values.All(value=>value.Current());}
            World adopted=null;
            bool Adopt(World incoming,Dictionary<RebirthNpcStableId,RebirthNpcRuntimeNativeAdoption.NativeReentry> native)
            {
                lock(Sync)
                {
                    if(adopted!=null)return ReferenceEquals(adopted,incoming)&&ReferenceEquals(projectionWorld,incoming);
                    if(projectionWorld!=null&&!ReferenceEquals(projectionWorld,incoming)||!RebirthNpcRecoveryGeneration.TryReadObservedGeneration(incoming,out ulong generation))return false;
                    foreach(var owner in owners)
                    {
                        if(!native.TryGetValue(owner.Key,out var source)||!source.Matches(owner.Value)||source.Actor is not EntityRebirthHumanoidNPC actor||
                            actor.RebirthRuntimeState?.StableId!=owner.Key)return false;
                        var saved=RebirthNpcEquipmentService.GetSnapshot(owner.Key);var target=desired[owner.Key];
                        if(saved==null||target.Generation!=generation||saved.Revision!=target.Revision||!SameSlots(saved.Slots,target.Slots))return false;
                    }
                    SetProjectionScope();
                    foreach(var owner in owners)
                    {
                        var source=native[owner.Key];var target=desired[owner.Key];
                        if(ApplyRecoverySnapshot((EntityRebirthHumanoidNPC)source.Actor,owner.Key,generation,target.Revision,target.Slots)!=RebirthNpcNativeEquipmentApplyResult.Applied||
                            !source.Current()||!IsRecoveryConverged(owner.Key,generation,target.Revision))return false;
                    }
                    adopted=incoming;return true;
                }
            }
            return Current()?new RebirthNpcRuntimeContinuationAdoption(scope,new HashSet<RebirthNpcStableId>(owners.Keys),Current,Adopt):null;
        }
    }
}
