using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

// Reconstruct paid processing scope after reload from server native persisted
// identities. No player lease, debit or owner effect is manufactured here.
internal static class RebirthCamperStationProcessingRecoveryWatch
{
    private sealed class Attempt { internal float Next; }
    private sealed class Entry
    {
        internal RebirthCamperStationState Station;
        internal RebirthCamperStationAddress Address;
        internal RebirthWorldCharacterRecord Owner;
        internal RebirthStablePlayerIdentity Identity;
        internal RebirthStationGridAdmission Admission;
        internal RebirthCamperStationPublicationRecord Publication;
        internal RebirthCamperStationQueuedAuthority.Binding Scope;
        internal RebirthCamperStationSaveEvidence.Request Request;
        internal RebirthCamperStationSaveEvidence.Publication Proof;
        internal float Deadline;
        internal bool Releasing;
        internal bool Current()=>Scope.IsProcessingCurrent()&&
            RebirthWorldCharacterRepository.HasSavedCamperStationPublication(Identity,Admission,Publication)&&
            RebirthCamperQueuedPublicationMatch.MatchesRestoredStation(Station,Address,Admission,Scope.QueuedOwner)&&
            !RebirthCamperStationCompletionCallsite.IsHeld(Station)&&
            !HasTerminal(Owner,Admission.JobId);
    }
    private static readonly ConditionalWeakTable<RebirthCamperStationState,Attempt> attempts=new ConditionalWeakTable<RebirthCamperStationState,Attempt>();
    private static readonly List<Entry> pending=new List<Entry>();
    private static bool installed;
    private static float next;
    private sealed class PersistedOwner { internal RebirthStablePlayerIdentity Identity;internal RebirthWorldCharacterRecord Owner;internal float NextLoad; }
    private static object ownerWorld,ownerPlayers;
    private static int ownerCount=-1;
    private static readonly List<PersistedOwner> owners=new List<PersistedOwner>();
    private static void RefreshOwners(EntityVehicle vehicle,PersistentPlayerList list)
    {
        if(ReferenceEquals(ownerWorld,vehicle.world)&&ReferenceEquals(ownerPlayers,list)&&ownerCount==list.Players.Count)return;
        ownerWorld=vehicle.world;ownerPlayers=list;ownerCount=list.Players.Count;owners.Clear();
        var identities=new HashSet<string>(StringComparer.Ordinal);
        foreach(var pair in list.Players)
            if(pair.Key!=null&&pair.Value?.PrimaryId!=null&&pair.Key.Equals(pair.Value.PrimaryId)&&
                RebirthStablePlayerIdentity.TryFromPersistentPlayer(pair.Value,out var identity)&&identities.Add(identity.StorageKey))
            {
                RebirthWorldCharacterRepository.TryGet(identity,out var owner);
                owners.Add(new PersistedOwner{Identity=identity,Owner=owner,NextLoad=UnityEngine.Time.realtimeSinceStartup+60f});
            }
    }
    internal static void Install()
    {
        if(installed)return;
        ModEvents.GameUpdate.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SGameUpdateData>(Tick));installed=true;
    }
    internal static void TryObserve(RebirthCamperStationState station)
    {
        if(!installed||!ThreadManager.IsMainThread()||station?.BoundVehicle?.world==null||
            station.BoundVehicle.world.IsRemote()||!RebirthWorldCharacterRepository.IsServerAuthority||
            pending.Count>=64||RebirthCamperStationSaveEvidence.IsPending(station)||
            RebirthCamperStationCompletionCallsite.IsHeld(station))return;
        foreach(var entry in pending)if(ReferenceEquals(entry.Station,station))return;
        float now=UnityEngine.Time.realtimeSinceStartup;
        var attempt=attempts.GetValue(station,_=>new Attempt());if(now<attempt.Next)return;attempt.Next=now+30f;
        try
        {
            var runtime=RebirthCamperModuleRuntime.For(station.BoundVehicle);
            var installedStation=runtime.Stations.Find(station.InstallationId);
            if(runtime.TransferPending||installedStation==null||!ReferenceEquals(installedStation.State,station))return;
            var address=new RebirthCamperStationAddress(runtime.Identity,station.InstallationId);
            var players=GameManager.Instance?.GetPersistentPlayerList();if(players?.Players==null)return;
            RefreshOwners(station.BoundVehicle,players);
            // Resolve by saved platform identity and unique owner records, never
            // by StartingEntityId, which is only original native recipe custody.
            foreach(var row in station.Queue)
            {
                if(row?.Recipe==null||!RebirthStationGridQueue.TryGetJobId(row.Recipe,out var job)||
                    RebirthCamperStationQueuedAuthority.TryGetCurrent(station,row,out _))continue;
                Entry chosen=null;int matches=0;
                foreach(var persisted in owners)
                {
                    var identity=persisted.Identity;var owner=persisted.Owner;
                    // Repository cache replacement invalidates the old reference;
                    // refresh only this native identity, not every player file.
                    if(owner==null&&now>=persisted.NextLoad||owner!=null&&!RebirthWorldCharacterRepository.IsCurrentCachedRecord(owner))
                    {
                        persisted.NextLoad=now+60f;
                        RebirthWorldCharacterRepository.TryGet(identity,out persisted.Owner);
                    }
                    owner=persisted.Owner;
                    if(owner?.Progression==null||!owner.Progression.StationPreparations.TryGetValue(job,out var admission)||admission==null||
                        !admission.TryGetMobileAddress(out var savedAddress)||!savedAddress.Equals(address)||
                        !owner.Progression.CamperStationPublications.TryGetValue(job,out var saved)||saved==null||
                        !RebirthCamperStationPublicationRecord.TryRead(saved.Write(),admission,out var publication)||
                        (int)publication.Write().Attribute("queuedOwner")!=row.StartingEntityId||HasTerminal(owner,job)||
                        !RebirthWorldCharacterRepository.HasSavedCamperStationPublication(identity,admission,publication))continue;
                    var scope=new RebirthCamperStationQueuedAuthority.Binding(null,owner,identity,station,address,admission,publication);
                    if(!scope.IsProcessingCurrent()||!RebirthCamperQueuedPublicationMatch.MatchesRestoredStation(station,address,admission,scope.QueuedOwner))continue;
                    matches++;chosen=new Entry{Station=station,Address=address,Owner=owner,Identity=identity,
                        Admission=admission.Clone(),Publication=publication.Clone(),Scope=scope,Deadline=now+30f};
                }
                if(matches!=1||chosen==null)return;
                pending.Add(chosen); // Own cleanup even if native begin throws.
                chosen.Request=RebirthCamperStationSaveEvidence.BeginProcessingRecovery(station,address,chosen.Current);
                if(chosen.Request==null)chosen.Releasing=true;
                return;
            }
        }
        catch(Exception error){Log.Warning("[Camper] Processing recovery remains held: "+error.GetType().Name);}
    }
    private static bool HasTerminal(RebirthWorldCharacterRecord owner,string job)
    {
        var p=owner.Progression;
        return p.StationPublications.ContainsKey(job)||p.StationTerminalIntents.ContainsKey(job)||
            p.StationCompletionPublications.ContainsKey(job)||p.StationCompletionExpectationProjections.ContainsKey(job)||p.StationCancellationAttempts.ContainsKey(job)||
            p.StationCancellationPublications.ContainsKey(job)||p.CamperStationCompletions.ContainsKey(job)||
            p.CamperStationTrainingPlans.ContainsKey(job)||p.CamperStationCancellations.ContainsKey(job)||
            p.CamperStationRefundDeliveries.ContainsKey(job)||p.StationCancellationRefunds.ContainsKey(job)||
            p.StationRefundDeliveries.ContainsKey(job)||p.StationRefundArchives.ContainsKey(job);
    }
    private static void Tick(ref ModEvents.SGameUpdateData data)
    {
        if(!ThreadManager.IsMainThread())return;
        float now=UnityEngine.Time.realtimeSinceStartup;if(now<next)return;next=now+.25f;
        for(int index=pending.Count-1;index>=0;index--)
        {
            var entry=pending[index];
            try
            {
                if(!entry.Releasing)
                {
                    if(now>=entry.Deadline||!entry.Current()||entry.Request==null)entry.Releasing=true;
                    else if(RebirthCamperStationSaveEvidence.TryObserve(entry.Request,out var proof))
                    {entry.Proof=proof;entry.Releasing=true;}
                }
                if(!entry.Releasing)continue;
                entry.Request?.Dispose(); // Release native save custody before processing may resume.
                if(entry.Proof!=null&&entry.Current())
                    RebirthCamperStationQueuedAuthority.TryRegisterProcessingRecovered(entry.Owner,entry.Identity,
                        entry.Station,entry.Address,entry.Admission,entry.Publication,entry.Proof);
                pending.RemoveAt(index);
                attempts.GetValue(entry.Station,_=>new Attempt()).Next=now+1f;
            }
            catch(Exception error){entry.Releasing=true;Log.Warning("[Camper] Processing recovery retained cleanup: "+error.GetType().Name);}
        }
    }
}
