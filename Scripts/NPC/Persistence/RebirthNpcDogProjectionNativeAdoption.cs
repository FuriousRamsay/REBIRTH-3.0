using System;
using System.Collections.Generic;
using System.Linq;

public static partial class RebirthDogStorageService
{
    internal static RebirthNpcRuntimeContinuationAdoption CaptureNativeAdoption(RebirthNpcSaveQuarantineScope scope)
    {
        lock(Sync)
        {
            if(!ThreadManager.IsMainThread()||scope==null||!scope.IsUnchangedOriginal()||!CanRetireEmptyStorage||
                storageWorld!=null&&!ReferenceEquals(storageWorld,scope.World))return null;
            var original=storageWorld;World adopted=null;
            bool Current(){lock(Sync)return ReferenceEquals(storageWorld,original)&&CanRetireEmptyStorage;}
            bool Adopt(World incoming,Dictionary<RebirthNpcStableId,RebirthNpcRuntimeNativeAdoption.NativeReentry> native)
            {lock(Sync){if(adopted!=null)return ReferenceEquals(adopted,incoming)&&ReferenceEquals(storageWorld,incoming);
                if(storageWorld!=null||OpenServerEntities.Count!=0)return false;storageWorld=incoming;adopted=incoming;return true;}}
            return new RebirthNpcRuntimeContinuationAdoption(scope,new HashSet<RebirthNpcStableId>(),Current,Adopt);
        }
    }
}

public static partial class RebirthDogCapacitySyncService
{
    internal static RebirthNpcRuntimeContinuationAdoption CaptureNativeAdoption(RebirthNpcSaveQuarantineScope scope)
    {
        lock(Sync)
        {
            if(!ThreadManager.IsMainThread()||scope==null||!scope.IsUnchangedOriginal()||clientWorld!=null||!CanRetireEmptyCapacity)return null;
            string image=CapacityImage().OuterXml;
            bool Current(){lock(Sync)return clientWorld==null&&CanRetireEmptyCapacity&&CapacityImage().OuterXml==image;}
            bool Adopt(World incoming,Dictionary<RebirthNpcStableId,RebirthNpcRuntimeNativeAdoption.NativeReentry> native)
            {lock(Sync)return ReferenceEquals(incoming,GameManager.Instance?.World)&&incoming.IsRemote()==false&&clientWorld==null&&CanRetireEmptyCapacity;}
            return new RebirthNpcRuntimeContinuationAdoption(scope,new HashSet<RebirthNpcStableId>(),Current,Adopt);
        }
    }
}

public static partial class RebirthDogAutoLootBridge
{
    internal static RebirthNpcOwnerNativeContinuationAdoption CaptureNativeOwnerAdoption(RebirthNpcSaveQuarantineScope scope)
    {
        lock(Sync)
        {
            if(!ThreadManager.IsMainThread()||scope==null||!scope.IsUnchangedOriginal()||replayingVanilla)return null;
            var original=Pending;var rows=new List<Tuple<PendingTransfer,string,string,int>>();
            foreach(var value in original.Values)
            {
                if(value==null||!ReferenceEquals(value.World,scope.World)||!ReferenceEquals(value.Actor?.world,scope.World)||
                    !RebirthStablePlayerIdentity.TryResolveServerEntity(value.Actor,out var owner))return null;
                string creation=RebirthCamperCharacterAccess.CurrentCreation(value.Actor);if(string.IsNullOrWhiteSpace(creation))return null;
                rows.Add(Tuple.Create(value,owner.CanonicalId,creation,value.Actor.Died));
            }
            bool Current(){lock(Sync)return !replayingVanilla&&ReferenceEquals(Pending,original)&&original.Count==rows.Count&&
                rows.All(row=>original.Values.Any(value=>ReferenceEquals(value,row.Item1)));}
            World adopted=null;
            bool Adopt(World incoming,Dictionary<string,RebirthDogLifecycleService.NativeOwnerReentry> native)
            {
                lock(Sync)
                {
                    if(adopted!=null)return ReferenceEquals(adopted,incoming);
                    if(replayingVanilla||ReferenceEquals(Pending,original))return false;
                    foreach(var row in rows)if(!native.TryGetValue(row.Item2,out var source)||!source.Current()||source.Creation!=row.Item3||source.Deaths!=row.Item4||
                        ReferenceEquals(row.Item1.World,incoming))return false;
                    // These are obsolete UI response/fallback bindings only.
                    // Never touch the original container/grid, invoke vanilla
                    // fallback, infer server disposition or replay a transfer.
                    original.Clear();adopted=incoming;return true;
                }
            }
            return new RebirthNpcOwnerNativeContinuationAdoption(scope,new HashSet<string>(rows.Select(row=>row.Item2),StringComparer.Ordinal),Current,Adopt);
        }
    }
}
