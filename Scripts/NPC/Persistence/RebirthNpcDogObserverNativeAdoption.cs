using System;
using System.Collections.Generic;
using System.Linq;

public static partial class RebirthDogChunkObserverService
{
    internal static RebirthNpcOwnerNativeContinuationAdoption CaptureNativeOwnerAdoption(RebirthNpcSaveQuarantineScope scope)
    {
        lock(Sync)
        {
            if(!ThreadManager.IsMainThread()||scope==null||!scope.IsUnchangedOriginal()||Observers.Count>1024||
                observerWorld!=null&&!ReferenceEquals(observerWorld,scope.World))return null;
            var original=Observers;var priorWorld=observerWorld;string image=DogObserverImage().OuterXml;
            var accounts=new Dictionary<RebirthNpcStableId,string>();
            var heads=new Dictionary<RebirthNpcStableId,Tuple<uint,string>>();
            foreach(var entry in original.Values)
            {
                if(entry==null||!OwnsNativeObserver(entry)||!RebirthNpcAggregatePersistenceStore.TryGetView(entry.StableId,out var saved)||
                    saved?.Ownership==null||saved.Dog==null||saved.Ownership.OwnerPlatformIdOrPersistentPlayerId!=entry.OwnerId)return null;
                string account=RebirthDogLifecycleService.ResolveNativeAdoptionAccount(entry.OwnerId);if(string.IsNullOrEmpty(account))return null;
                accounts.Add(entry.StableId,account);heads.Add(entry.StableId,Tuple.Create(saved.AggregateRevision,saved.AggregateChecksum));
            }
            var rows=original.Values.ToArray();var removed=new HashSet<RebirthNpcStableId>();
            bool Current(){lock(Sync)return ReferenceEquals(Observers,original)&&ReferenceEquals(observerWorld,priorWorld)&&DogObserverImage().OuterXml==image;}
            bool OldMembership(ObserverEntry entry,bool present)
            {
                if(entry.Manager==null||entry.Observer==null||!ReferenceEquals(entry.World,scope.World)||
                    !ReferenceEquals(entry.Manager.m_World,scope.World)||!ReferenceEquals(scope.World.m_ChunkManager,entry.Manager)||
                    entry.Observer.id!=entry.NativeId||entry.Manager.m_ObservedEntities==null||entry.Manager.m_ObservedEntities.Count>16384)return false;
                int references=0,ids=0;foreach(var item in entry.Manager.m_ObservedEntities)
                {if(ReferenceEquals(item,entry.Observer))references++;if(item!=null&&item.id==entry.NativeId)ids++;}
                return present?references==1&&ids==1:references==0&&ids==0;
            }
            World adopted=null;
            bool Adopt(World incoming,Dictionary<string,RebirthDogLifecycleService.NativeOwnerReentry> native)
            {
                lock(Sync)
                {
                    if(adopted!=null)return ReferenceEquals(adopted,incoming)&&ReferenceEquals(observerWorld,incoming);
                    if(ReferenceEquals(Observers,original)||observerWorld!=null&&!ReferenceEquals(observerWorld,incoming)||
                        incoming.m_ChunkManager==null||ReferenceEquals(incoming.m_ChunkManager,scope.World.m_ChunkManager))return false;
                    foreach(var row in rows)
                    {
                        if(!native.TryGetValue(accounts[row.StableId],out var owner)||!owner.Current()||
                            !RebirthDogLifecycleService.TryResolveOwnerId(owner.Actor,out var ownerId)||ownerId!=row.OwnerId||
                            !RebirthNpcAggregatePersistenceStore.TryGetView(row.StableId,out var saved)||saved?.Ownership==null||
                            saved.AggregateRevision!=heads[row.StableId].Item1||saved.AggregateChecksum!=heads[row.StableId].Item2||
                            saved.Ownership.OwnerPlatformIdOrPersistentPlayerId!=row.OwnerId||
                            !OldMembership(row,!removed.Contains(row.StableId)))return false;
                    }
                    foreach(var row in rows)
                    {
                        if(!removed.Contains(row.StableId))
                        {
                            try{row.Manager.RemoveChunkObserver(row.Observer);}finally{if(OldMembership(row,false))removed.Add(row.StableId);}
                            if(!OldMembership(row,false))return false;removed.Add(row.StableId);
                        }
                        // Fresh native resource in the actual incoming manager.
                        // Original pending recall flags/hold position are intent;
                        // no old native observer or manager is installed here.
                        Ensure(row.StableId,row.OwnerId,row.Position,row.FollowRecoveryRequested,row.ManualRecallAllOrders);
                        if(!Observers.TryGetValue(row.StableId,out var next)||!OwnsNativeObserver(next)||!ReferenceEquals(next.World,incoming)||
                            next.OwnerId!=row.OwnerId||next.FollowRecoveryRequested!=row.FollowRecoveryRequested||
                            next.ManualRecallAllOrders!=row.ManualRecallAllOrders)return false;
                    }
                    if(rows.Length==0&&observerWorld==null)observerWorld=incoming;
                    adopted=incoming;return true;
                }
            }
            return Current()?new RebirthNpcOwnerNativeContinuationAdoption(scope,new HashSet<string>(accounts.Values,StringComparer.Ordinal),Current,Adopt):null;
        }
    }
}
