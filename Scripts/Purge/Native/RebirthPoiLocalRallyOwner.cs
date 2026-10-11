using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Scripting;

// Shared local/listen-server and authenticated remote reset execution owner.
// Region workers require their own admitted execution context.
internal static class RebirthPoiLocalRallyOwner
{
    private sealed class Lease
    {
        internal ObjectiveRallyPoint Objective;
        internal RebirthPoiOriginalRallyRequest Request;
        internal RebirthPoiAuthenticatedRallyScope Authenticated;
        internal RebirthPoiAuthenticatedPartyScope Party;
        internal Action<RebirthPoiRallyCompletionReceipt> Completed;
        internal Action<bool> Failed;
        internal int PlayerId => Request!=null?Request.PlayerId:Authenticated.PlayerId;
        internal Vector3 Poi => Request!=null?Request.PoiPosition:Authenticated.Poi;
        internal Guid Transaction => Request!=null?Request.RequestId:Authenticated.Request;
        internal Guid SavedWorld => Request!=null?Request.SavedWorldId:Authenticated.SavedWorldId;
        internal Quest Quest;
        internal World World;
        internal List<PrefabInstance> Targets;
        internal int[] Shared;
        internal int[] EligibleShared;
        internal readonly List<Tuple<PrefabInstance,QuestLockInstance,QuestLockInstance>> Locks=new List<Tuple<PrefabInstance,QuestLockInstance,QuestLockInstance>>();
        internal RebirthPoiNativeResetExecution Execution;
        internal bool NativeStarted;
        internal bool Current => leases.TryGetValue(Objective,out var original)&&ReferenceEquals(original,this)&&(Request!=null?Request.IsOriginalCurrent:Authenticated.IsOriginalCurrent)&&ReferenceEquals(GameManager.Instance?.World,World)&&(Party!=null?Party.IsCurrent&&SameShared(EligibleShared,Party.Eligible):SameShared(Shared,Quest.GetSharedWithIDList())&&SameShared(EligibleShared,EligibleParty(Quest)));
    }
    private static readonly Dictionary<ObjectiveRallyPoint,Lease> leases=new Dictionary<ObjectiveRallyPoint,Lease>();
    internal static int[] EligibleParty(Quest quest)
    {
        if(quest.sharedWithList==null)return null;
        Rect area=quest.GetLocationRect();var owner=quest.OwnerJournal.OwnerPlayer;
        return quest.sharedWithList.Where(player=>{
            if(player==null)return false;
            if(area==Rect.zero)return Vector3.Distance(owner.position,player.position)<15f;
            Vector3 point=player.position;point.y=point.z;return area.Contains(point);
        }).Select(player=>player.entityId).ToArray();
    }
    private static bool SameShared(int[] original,int[] current) { return original==null?current==null:current!=null&&original.SequenceEqual(current); }
    private static double Now => (double)Stopwatch.GetTimestamp()/Stopwatch.Frequency;
    internal static void Reset() { leases.Clear(); }
    internal static bool TryStart(ObjectiveRallyPoint objective,Vector3 position)
    {
        if(leases.ContainsKey(objective))return true;
        RebirthPoiOriginalRallyRequest request;
        if(!RebirthPoiOriginalRallyRequest.TryCapture(objective,position,out request))return false;
        var world=GameManager.Instance.World;
        if(world.IsRemote())return false;
        var targets=GameManager.Instance.GetDynamicPrefabDecorator()?.GetPrefabsFromWorldPosInside(position,objective.OwnerQuest.QuestTags);
        if(targets==null || targets.Count<1 || targets.Count>64)return false;
        var lease=new Lease{Objective=objective,Request=request,Quest=objective.OwnerQuest,World=world,Targets=targets,Shared=objective.OwnerQuest.GetSharedWithIDList()?.ToArray(),EligibleShared=EligibleParty(objective.OwnerQuest)};
        leases.Add(objective,lease);
        try { ThreadManager.StartCoroutine(Run(lease));return true; }
        catch { leases.Remove(objective);return false; }
    }
    internal static bool TryStartAuthenticated(RebirthPoiAuthenticatedRallyScope scope,Action<RebirthPoiRallyCompletionReceipt> completed,Action<bool> failed)
    {
        if(scope==null || !scope.IsOriginalCurrent || completed==null || failed==null)return false;
        var world=GameManager.Instance.World;var quest=scope.OriginalQuest;
        var objective=quest.Objectives.OfType<ObjectiveRallyPoint>().SingleOrDefault(o=>o.Phase==0 || o.Phase==quest.CurrentPhase);
        if(objective==null || world.IsRemote())return false;
        Lease existing;if(leases.TryGetValue(objective,out existing))return existing.Authenticated!=null && existing.Authenticated.Request==scope.Request;
        var targets=GameManager.Instance.GetDynamicPrefabDecorator()?.GetPrefabsFromWorldPosInside(scope.Poi,quest.QuestTags);
        if(targets==null || targets.Count<1 || targets.Count>64)return false;
        RebirthPoiAuthenticatedPartyScope party;if(!RebirthPoiAuthenticatedPartyScope.TryCapture(world,quest,scope.PlayerId,out party))return false;
        var lease=new Lease{Objective=objective,Authenticated=scope,Party=party,Completed=completed,Failed=failed,Quest=quest,World=world,Targets=targets,Shared=quest.GetSharedWithIDList()?.ToArray(),EligibleShared=party.Eligible};
        leases.Add(objective,lease);
        try { ThreadManager.StartCoroutine(Run(lease));return true; }
        catch { leases.Remove(objective);return false; }
    }
    private static bool Requirements(Lease lease)
    {
        if(lease.Request!=null)return lease.Quest.CheckRequirements();
        bool satisfied;return RebirthPoiServerQuestRequirements.TryCheck(lease.Quest,lease.World.GetEntity(lease.PlayerId) as EntityPlayer,out satisfied)&&satisfied;
    }
    private static bool Locks(Lease lease)
    {
        try
        {
            if(!lease.Current || !Requirements(lease) || !lease.Quest.QuestClass.CanActivate())return false;
            ulong reason;
            if(QuestEventManager.Current.CheckForPOILockouts(lease.PlayerId,new Vector2(lease.Poi.x,lease.Poi.z),out reason)!=QuestEventManager.POILockoutReasonTypes.None)return false;
            var shared=lease.EligibleShared;
            foreach(var target in lease.Targets)
                if(target.prefab.GetQuestTag(lease.Quest.QuestTags) && (target.lockInstance==null || target.lockInstance.CheckQuestLock()))
                {
                    var old=target.lockInstance;var installed=new QuestLockInstance(lease.PlayerId);
                    lease.Locks.Add(Tuple.Create(target,old,installed));target.lockInstance=installed;
                    if(shared!=null)installed.AddQuesters(shared);
                }
            return lease.Current;
        }
        catch { return false; }
    }
    private static void RestoreLocks(Lease lease)
    {
        if(lease.NativeStarted || !ReferenceEquals(GameManager.Instance?.World,lease.World))return;
        foreach(var receipt in lease.Locks)
            if(ReferenceEquals(receipt.Item1.lockInstance,receipt.Item3))receipt.Item1.lockInstance=receipt.Item2;
    }
    private static void Report(Lease lease)
    { if(lease.Authenticated!=null){lease.Failed(lease.NativeStarted);return;}if(ReferenceEquals(GameManager.Instance?.World,lease.World))GameManager.ShowTooltip(lease.World.GetPrimaryPlayer(),Localization.Get(lease.NativeStarted?"xuiRebirthPoiResetUncertain":"xuiRebirthPoiResetUnavailable")); }
    private static IEnumerator Run(Lease lease)
    {
        double deadline=Now+90;
        bool success=false;
        try
        {
            RebirthPoiWorldStore store=null;
            while(lease.Current && Now<deadline && !RebirthPoiWorldLifecycle.Instance.TryGetStore(out store))yield return null;
            if(store==null || !lease.Current)yield break;
            // Discovery records are metadata only, never an activation or clear assertion.
            // All original targets must qualify before any quest lock or native reset.
            foreach(var target in lease.Targets)
            {
                RebirthPoiNativeAuthoredManifest authored;
                if(!RebirthPoiNativeAuthoredManifest.TryCapture(store.Published.Binding,lease.World,target,out authored))yield break;
                while(lease.Current && Now<deadline)
                {
                    var snapshot=store.Published;RebirthPoiClearanceRecord record;
                    if(snapshot.TryGet(authored.Identity,out record))break;
                    if(!store.HasPending)store.TryDiscover(snapshot,authored.Identity,!authored.Slots.Any(s=>s.Expected.Combat));
                    yield return null;
                }
                if(!lease.Current || Now>=deadline)yield break;
            }
            var shared=lease.EligibleShared;
            if(!RebirthPoiNativeResetExecution.TryCreateWorldReset(store,lease.World,lease.Targets,lease.Quest.QuestTags,lease.PlayerId,shared,lease.Quest.QuestClass,RebirthPoiResetCaller.Quest,lease.Transaction,()=>lease.Current,()=>Now,out lease.Execution))yield break;
            while(lease.Current && Now<deadline && !lease.Execution.TryAdmitBeforeMutation())
            {
                var state=lease.Execution.Batch.State;
                if(state==RebirthPoiResetProtocolState.Unknown || state==RebirthPoiResetProtocolState.Refused)yield break;
                yield return null;
            }
            if(!lease.Current || Now>=deadline || !Locks(lease))yield break;
            // Native child yields are witnessed by the same exact execution context.
            lease.NativeStarted=false;
            while(lease.Current && Now<deadline)
            {
                var state=lease.Execution.Batch.State;
                if(state==RebirthPoiResetProtocolState.Unknown || state==RebirthPoiResetProtocolState.Refused)yield break;
                bool more=lease.Execution.MoveNext();lease.NativeStarted=lease.Execution.Batch.HasNativeStarted;
                if(!more)break;
                yield return lease.Execution.Current;
            }
            RebirthPoiResetBatchCompletion completed;
            if(!lease.Current || !lease.Execution.Batch.TryGetCompleted(out completed) || !ReferenceEquals(completed.Scope,lease.World) || completed.World!=lease.SavedWorld || completed.Transaction!=lease.Transaction || !completed.IsQuestBatch)yield break;
            if(lease.Authenticated!=null)
            {
                RebirthPoiRallyCompletionReceipt receipt;
                if(lease.Authenticated.TryCompleteOriginal(lease.Execution.Batch,out receipt))
                { lease.Completed(receipt);success=true; }
            }
            else success=lease.Request.TryApplyConfirmed(new RebirthPoiRallyCompletionReceipt(lease.Transaction,completed.World,lease.Request.QuestUniqueId,lease.Request.QuestCode,lease.PlayerId,completed.GlobalRevision));
        }
        finally
        {
            if(success)Release(lease);
            else
            {
                RestoreLocks(lease);
                // Cancellation can require durable retries. Keep the original owner
                // alive while those receipts settle; never resume native mutation.
                try { ThreadManager.StartCoroutine(Cleanup(lease)); }
                catch(Exception error) { Release(lease);Log.Warning("[RebirthPurge] Local cleanup scheduler failed: "+error.Message); }
            }
        }
    }
    private static void Release(Lease lease)
    { if(leases.TryGetValue(lease.Objective,out var original)&&ReferenceEquals(original,lease))leases.Remove(lease.Objective); }
    private static IEnumerator Cleanup(Lease lease)
    {
        try
        {
            try { lease.Execution?.Dispose(); }
            catch(Exception error) { Log.Warning("[RebirthPurge] Local reset cleanup failed: "+error.Message); }
            double deadline=Now+30;
            while(!lease.NativeStarted && lease.Execution!=null && ReferenceEquals(GameManager.Instance?.World,lease.World) && Now<deadline && !lease.Execution.Batch.PollCancellation())yield return null;
        }
        finally
        {
            Release(lease);
            try { Report(lease); }
            catch(Exception error) { Log.Warning("[RebirthPurge] Local reset notification failed: "+error.Message); }
        }
    }
}
[HarmonyPatch(typeof(ObjectiveRallyPoint),nameof(ObjectiveRallyPoint.RallyPointActivate))]
internal static class RebirthPoiLocalRallyOwnerHook
{
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(ObjectiveRallyPoint __instance,Vector3 prefabPos,bool activate)
    {
        var world=GameManager.Instance?.World;
        if(!RebirthPurgeReleasePolicy.Enabled || !RebirthSandboxOptionManager.Current.PoiClearTrackingEnabled || !activate || world==null || !ReferenceEquals(__instance?.OwnerQuest?.OwnerJournal?.OwnerPlayer,world.GetPrimaryPlayer()) || !(__instance.OwnerQuest.OwnerJournal.OwnerPlayer is EntityPlayerLocal) || !__instance.OwnerQuest.PositionData.ContainsKey(Quest.PositionDataTypes.POIPosition))return true;
        if(!(world.IsRemote()?RebirthPoiRemoteRallyOwner.TryStart(__instance,prefabPos):RebirthPoiLocalRallyOwner.TryStart(__instance,prefabPos)))GameManager.ShowTooltip(world.GetPrimaryPlayer(),Localization.Get("xuiRebirthPoiResetUnavailable"));
        return false;
    }
}
// Capture before native RemoveSharedNotInRange changes the owning quest.
[HarmonyPatch(typeof(ObjectiveRallyPoint),nameof(ObjectiveRallyPoint.Current_BlockActivate))]
internal static class RebirthPoiLocalRallyEntryHook
{
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(ObjectiveRallyPoint __instance,Vector3i blockPos)
    {
        var world=GameManager.Instance?.World;var quest=__instance?.OwnerQuest;
        if(!RebirthPurgeReleasePolicy.Enabled || !RebirthSandboxOptionManager.Current.PoiClearTrackingEnabled || world==null || quest?.OwnerJournal?.OwnerPlayer==null || !ReferenceEquals(quest.OwnerJournal.OwnerPlayer,world.GetPrimaryPlayer()) || !(quest.OwnerJournal.OwnerPlayer is EntityPlayerLocal) || !quest.PositionData.ContainsKey(Quest.PositionDataTypes.POIPosition))return true;
        if(quest.SharedOwnerID!=-1 || __instance.Complete || quest.OwnerJournal.ActiveQuest!=null || __instance.RallyPos!=blockPos)return false;
        if(Twitch.TwitchManager.HasInstance && Twitch.TwitchManager.Current.IsVoting)
        { GameManager.ShowTooltip(world.GetPrimaryPlayer(),Localization.Get("ttWaitForVoteQuest"));return false; }
        int hour=GameUtils.WorldTimeToHours(world.worldTime),start=__instance.startTime,end=__instance.endTime;
        if(start!=-1 && end!=-1 && (start<end?(hour<start || hour>=end):(hour<start && hour>=end)))
        { GameManager.ShowTooltip(world.GetPrimaryPlayer(),string.Format(Localization.Get("ObjectiveRallyPointInvalidStartTime"),start,end));return false; }
        Vector3 poi=quest.PositionData[Quest.PositionDataTypes.POIPosition];
        if(world.IsRemote()) { if(!RebirthPoiRemoteRallyOwner.TryStart(__instance,poi))GameManager.ShowTooltip(world.GetPrimaryPlayer(),Localization.Get("xuiRebirthPoiResetUnavailable"));return false; }
        ulong extra;
        var reason=QuestEventManager.Current.CheckForPOILockouts(quest.OwnerJournal.OwnerPlayer.entityId,new Vector2(poi.x,poi.z),out extra);
        __instance.RallyPointActivate(poi,reason==QuestEventManager.POILockoutReasonTypes.None,reason,extra);
        return false;
    }
}
[Preserve]
public sealed class RebirthPoiLocalRallyOwnerModApi:IModApi
{
    private static bool installed;
    public void InitMod(Mod mod)
    {
        if(!RebirthPurgeReleasePolicy.Enabled || installed)return;
        var harmony=new Harmony("rebirth.purge.local-rally-owner");
        try { harmony.CreateClassProcessor(typeof(RebirthPoiLocalRallyOwnerHook)).Patch();harmony.CreateClassProcessor(typeof(RebirthPoiLocalRallyEntryHook)).Patch();harmony.CreateClassProcessor(typeof(RebirthPoiRemoteRallyLegacyLockHook)).Patch();harmony.CreateClassProcessor(typeof(RebirthPoiScriptResetHook)).Patch();harmony.CreateClassProcessor(typeof(RebirthPoiScriptResetReuseHook)).Patch();harmony.CreateClassProcessor(typeof(RebirthPoiAdministrativeResetHook)).Patch();harmony.CreateClassProcessor(typeof(RebirthPoiSleeperEventResetHook)).Patch();harmony.CreateClassProcessor(typeof(RebirthPoiSleeperEventVolumeReceiptHook)).Patch(); }
        catch { harmony.UnpatchSelf();throw; }
        ModEvents.GameStarting.RegisterHandler(Starting);ModEvents.WorldShuttingDown.RegisterHandler(Shutdown);ModEvents.GameUpdate.RegisterHandler(Update);installed=true;
    }
    private static void Update(ref ModEvents.SGameUpdateData data) { RebirthPoiRemoteRallyOwner.Pulse(); }
    private static void Starting(ref ModEvents.SGameStartingData data) { RebirthPoiLocalRallyOwner.Reset();RebirthPoiRemoteRallyOwner.Reset();RebirthPoiScriptResetOwner.Reset();RebirthPoiSleeperEventResetOwner.Reset(); }
    private static void Shutdown(ref ModEvents.SWorldShuttingDownData data) { RebirthPoiLocalRallyOwner.Reset();RebirthPoiRemoteRallyOwner.Reset();RebirthPoiScriptResetOwner.Reset();RebirthPoiSleeperEventResetOwner.Reset(); }
}