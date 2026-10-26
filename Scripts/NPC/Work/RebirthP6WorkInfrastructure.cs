using System;

// P6 shared NPC work infrastructure additions (new file; no existing signature changed).
internal static class RebirthP6WorkFollowState
{
    // Same source of truth as EAIRebirthWork.CanExecute and RebirthNpcEmbodiedWorkOwner.TryDispatchFromEai.
    internal static bool IsFollowing(EntityRebirthNPC actor)=>
        actor?.RebirthRuntimeState?.Order==RebirthNpcOrderState.Follow;
}

public static partial class RebirthNpcWorkAssignmentCoordinator
{
    private static int restartRecoveryCursor;
    // Assignments whose interrupted literature is already settled (book returned under receipt): never rescan.
    private static readonly System.Collections.Generic.HashSet<ulong> RecoveredIds=new System.Collections.Generic.HashSet<ulong>();
    internal static void MarkLiteratureRecovered(ulong assignmentId){if(RecoveredIds.Count<4096)RecoveredIds.Add(assignmentId);}
    // Per-world recovery state: assignment ids restart at 1 in a new world, so none of it may survive a world/save change.
    private static void ClearP6WorldState()
    {
        RecoveredIds.Clear();NotReleasedUntil.Clear();PendingSettles.Clear();restartRecoveryCursor=0;RebirthP6B13Bridge.ClearWorldState();
        RebirthNpcEmbodiedWorkOwner.ClearEarnedCache();
    }
    // True when an interrupted literature job's exact book is proven back in the NPC bag under the saved
    // operation-bound receipt (so the saved phase-4 transfer no longer pins the assignment).
    internal static bool LiteratureReturnedUnderReceipt(RebirthNpcWorkAssignment a)
    {
        try
        {
            if(a==null||!RebirthNpcLiteratureWorkGate.Allows(a)||
                !RebirthNpcWorkNavigationService.TryResolveNpc(a.NpcId,out var actor)||
                !RebirthNpcEmbodiedWorkIntent.TryCreate(actor,"concrete",a.AssignmentId,1,a.Revision,
                    "rebirth.p6.cancel-probe",1,()=>false,()=>{},()=>{},out var key))return false;
            return RebirthHumanRecipeReading.HasReturnedCancelReceipt(key);
        }
        catch{return false;}
    }
    internal static bool HasSession(ulong assignmentId){lock(Sync)return Sessions.ContainsKey(assignmentId);}

    // Restart recovery from saved rows only: a Reserved/Executing literature assignment with no live
    // session or owner, a saved phase-4 workMove and a held bookMove. Uses a throwaway unstaged intent
    // (never actuates: producer predicate is false) purely as the identity key for the operation-bound
    // receipt return. Completion (saved read marker) is excluded and left to the existing outcome path.
    internal static void RecoverRestartedLiteratureBatch(int maximumNpcs)
    {
        var world=GameManager.Instance?.World;
        if(!ThreadManager.IsMainThread()||world==null||world.IsRemote()||
            RebirthNpcPersistenceCoordinator.IsRecoveryInstalling||RebirthNpcRuntimeRegistry.HasForeignDomain)return;
        RetryPendingSettles();
        var residents=RebirthNpcRuntimeRegistry.GetSnapshot();
        if(residents.Length==0)return;
        int count=Math.Min(Math.Max(1,maximumNpcs),residents.Length);
        for(int i=0;i<count;i++)
        {
            var id=residents[(restartRecoveryCursor++&int.MaxValue)%residents.Length].StableId;
            try{RecoverRestartedLiterature(id);}catch{}
        }
    }
    // After a load the checkpoint reconcile turns Reserved/Executing into Suspended. A water or craft job whose
    // saved row is already safe to release (IsReleasedTerminal) has no session or owner left, and a Suspended
    // water job would make Assignment.Create reject the maintainer's resuming assignment, so end it as the live
    // settle does: Cancelled, reservation released, row kept.
    private struct ReleaseMemo{internal uint Revision;internal long UntilTicks;}
    private static readonly System.Collections.Generic.Dictionary<ulong,ReleaseMemo> NotReleasedUntil=
        new System.Collections.Generic.Dictionary<ulong,ReleaseMemo>();
    private static bool SettleRestartedReleased(RebirthNpcWorkAssignment a)
    {
        bool water=RebirthP8NpcWaterRefillWork.Allows(a),craft=!water&&RebirthNpcRecipeQueueWork.Allows(a);
        if(!water&&!craft)return false;
        // Negative verdicts are cached per assignment revision for a few seconds: the saved-person parse is not free.
        long now=DateTime.UtcNow.Ticks;
        if(NotReleasedUntil.TryGetValue(a.AssignmentId,out var memo)&&memo.Revision==a.Revision&&now<memo.UntilTicks)return false;
        // Nothing executes here (Suspended, no session), so a filled bucket that was never marked held can be
        // marked now; otherwise no live executor would ever do it and the provider would stay wedged.
        if(water)RebirthP8NpcWaterRefillWork.MarkHeldIfFilled(a.NpcId,a.AssignmentId);
        if(water?!RebirthP8NpcWaterRefillWork.IsReleasedTerminal(a.NpcId,a.AssignmentId):
            !RebirthNpcStationQueueOriginal.IsReleasedTerminal(a.NpcId,a.AssignmentId))
        {
            if(NotReleasedUntil.Count>=1024)NotReleasedUntil.Clear();
            NotReleasedUntil[a.AssignmentId]=new ReleaseMemo{Revision=a.Revision,UntilTicks=now+10*TimeSpan.TicksPerSecond};
            return false;
        }
        // Explicit pending state: once the assignment is Cancelled it no longer appears in GetForNpc(npc,false), so
        // a failed reservation readback must be re-entered from this table, never from the assignment list.
        if(!PendingSettles.ContainsKey(a.AssignmentId))PendingSettles[a.AssignmentId]=new PendingSettle{Npc=a.NpcId,Kind=(byte)(water?0:1)};
        return RetryPendingSettle(a.AssignmentId);
    }
    private sealed class PendingSettle{internal RebirthNpcStableId Npc;internal byte Kind;internal int Attempts;internal long NextTicks;}
    private static readonly System.Collections.Generic.Dictionary<ulong,PendingSettle> PendingSettles=
        new System.Collections.Generic.Dictionary<ulong,PendingSettle>();
    private static bool RetryPendingSettle(ulong id)
    {
        if(!PendingSettles.TryGetValue(id,out var p))return true;
        long now=DateTime.UtcNow.Ticks;
        if(now<p.NextTicks)return false;
        bool literature=p.Kind==2;
        if(!SettleAssignmentAuthoritative(id,literature?RebirthNpcWorkAssignmentStatus.Suspended:RebirthNpcWorkAssignmentStatus.Cancelled,
            literature?"Restart recovery; exact book returned under saved receipt.":"Restart; saved row released and kept.",
            "Restart settle."))
        {
            // Escalating backoff (1 s .. 64 s); an unresolvable settlement logs once and never spins.
            p.Attempts++;
            if(p.Attempts==20)Log.Warning("[REBIRTH NPC Work] Restart settlement of assignment "+id+
                " cannot be proven (status or reservation readback keeps failing); the saved original is kept.");
            p.NextTicks=now+TimeSpan.TicksPerSecond*(1L<<Math.Min(p.Attempts,6));
            return false;
        }
        if(p.Kind==0)RebirthP8NpcWaterRefillWork.ReleaseFor(p.Npc,id);
        else if(p.Kind==1)RebirthNpcStationQueueOriginal.ReleaseWatch(p.Npc,id);
        PendingSettles.Remove(id);return true;
    }
    private static void RetryPendingSettles()
    {
        if(PendingSettles.Count==0)return;
        foreach(var id in new System.Collections.Generic.List<ulong>(PendingSettles.Keys))
            if(RetryPendingSettle(id))MarkLiteratureRecovered(id);
    }
    private static void RecoverRestartedLiterature(RebirthNpcStableId npc)
    {
        if(RebirthNpcEmbodiedWorkOwner.HasRetainedOwner(npc)||
            !RebirthNpcWorkNavigationService.TryResolveNpc(npc,out var actor)||actor.IsDead())return;
        foreach(var a in RebirthNpcWorkAssignmentService.GetForNpc(npc,false))
        {
            if(a.Status!=RebirthNpcWorkAssignmentStatus.Reserved&&a.Status!=RebirthNpcWorkAssignmentStatus.Executing&&
                a.Status!=RebirthNpcWorkAssignmentStatus.Suspended)continue;
            if(RecoveredIds.Contains(a.AssignmentId))continue;
            if(a.Status==RebirthNpcWorkAssignmentStatus.Suspended&&!HasSession(a.AssignmentId)&&
                SettleRestartedReleased(a)){MarkLiteratureRecovered(a.AssignmentId);continue;}
            if(!RebirthNpcLiteratureWorkGate.Allows(a)||HasSession(a.AssignmentId))continue;
            if(!RebirthNpcWorkNativeTransferOriginal.TryReadSaved(npc,
                RebirthNpcConcreteWorkExecutorBase.DeriveOperationId(a.AssignmentId),out var saved))
            {
                // No saved transfer: a Suspended job holds no book; do not rescan it every pass.
                if(a.Status==RebirthNpcWorkAssignmentStatus.Suspended)MarkLiteratureRecovered(a.AssignmentId);
                continue;
            }
            if(saved.Phase<4||saved.Owner!=a.ActorId)continue;
            if(!RebirthNpcEmbodiedWorkIntent.TryCreate(actor,"concrete",a.AssignmentId,1,a.Revision,
                "rebirth.p6.restart-recovery",1,()=>false,()=>{},()=>{},out var key))continue;
            if(!RebirthHumanRecipeReading.TryCancelWorkWithReceipt(key)||
                !RebirthHumanRecipeReading.HasReturnedCancelReceipt(key))continue;
            // Receipt proven: Suspended readback plus exact reservation release/absence before the id is skipped.
            if(!PendingSettles.ContainsKey(a.AssignmentId))PendingSettles[a.AssignmentId]=new PendingSettle{Npc=a.NpcId,Kind=2};
            if(RetryPendingSettle(a.AssignmentId))MarkLiteratureRecovered(a.AssignmentId);
            return;
        }
    }
}

public static partial class RebirthNpcWorkAssignmentService
{
    // Read-only snapshot of every non-terminal assignment of every NPC (Ready, Reserved, Executing,
    // Suspended, ...), cloned under the original lock. For cross-NPC claim checks (storage cells).
    internal static RebirthNpcWorkAssignment[] GetAllActiveSnapshot()
    {
        if(!ReadAssignmentScope())return Array.Empty<RebirthNpcWorkAssignment>();
        lock(Sync)
        {
            var result=new System.Collections.Generic.List<RebirthNpcWorkAssignment>();
            foreach(var pair in ById)
                if(!IsTerminal(pair.Value.Status))result.Add(pair.Value.Clone());
            return result.ToArray();
        }
    }
}

public static partial class RebirthNpcWorkReservationService
{
    internal enum P6Presence { Absent, Present, ScopeUnavailable }
    // Distinguishes "no reservation" from "reservation scope unavailable" (TryGetForAssignment reports both as false).
    internal static P6Presence GetPresence(ulong assignmentId)
    {
        if(!(RebirthNpcWorkReleaseGate.Enabled?UseReservationScope():UseLiteratureReservationScope(assignmentId)))
            return P6Presence.ScopeUnavailable;
        lock(Sync)
        {
            PruneExpiredLocked(DateTime.UtcNow.Ticks);
            return ByAssignment.TryGetValue(assignmentId,out var id)&&ById.ContainsKey(id)?P6Presence.Present:P6Presence.Absent;
        }
    }
    // True only when the assignment provably has no reservation afterwards (released and read back, or already absent).
    internal static bool ReleaseAndConfirm(ulong assignmentId,string reason)
    {
        var state=GetPresence(assignmentId);
        if(state==P6Presence.Absent)return true;
        if(state==P6Presence.ScopeUnavailable)return false;
        if(!ReleaseAssignment(assignmentId,reason))return false;
        return GetPresence(assignmentId)==P6Presence.Absent;
    }
}
