using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;
using UnityEngine;

#nullable disable

public sealed class RebirthNpcGuardPostRequest
{
    public string SessionKey { get; set; }
    public Vector3 Position { get; set; }
    public int DutyTicks { get; set; }
    public float HoldRadius { get; set; }
    public int Priority { get; set; }
    public bool StartImmediately { get; set; }
}


public sealed class RebirthNpcGuardDutyPackageSnapshot
{
    public Guid PackageId;
    public ulong AssignmentId;
    public long UpdatedUtcTicks;
    public string Detail;
}

public sealed class RebirthNpcGuardDutyPackageResult
{
    public Guid PackageId { get; internal set; }
    public ulong AssignmentId { get; internal set; }
    public bool Created { get; internal set; }
    public bool Started { get; internal set; }
    public string Detail { get; internal set; }
}

/// <summary>
/// Creates explicit, time-bounded guard-post assignments. The worker's normal order is restored
/// when duty completes, is cancelled, or fails. The existing guard navigation and stationary
/// controllers remain authoritative for movement and post holding.
/// </summary>
public static class RebirthNpcGuardDutyWorkPackageService
{
    public const string GuardPostDefinitionId = "rebirth.guard.post";
    private sealed class ActivePackage { public Guid Id; public ulong AssignmentId; public long UpdatedUtcTicks; public string Detail; }
    private static readonly object Sync = new object();
    private static readonly Dictionary<Guid, ActivePackage> Active = new Dictionary<Guid, ActivePackage>();
    private static readonly RebirthNpcFairRoundRobin<Guid> Schedule = new RebirthNpcFairRoundRobin<Guid>();
    private static long requests, created, started, rejected, reconciliations, completed, failed;

    public static void RegisterDefinitions()
    {
        RebirthNpcWorkDefinitionRegistry.Register(new RebirthNpcWorkDefinition(
            GuardPostDefinitionId, RebirthNpcWorkKind.GuardDuty, RebirthNpcWorkCapability.GuardDuty,
            275, 1, TimeSpan.FromSeconds(90), true), true);
    }

    public static RebirthNpcGuardDutyPackageResult CreateAndStart(RebirthNpcGuardPostRequest request)
    {
        Interlocked.Increment(ref requests);
        RebirthNpcGuardDutyPackageResult result = new RebirthNpcGuardDutyPackageResult { PackageId = Guid.NewGuid() };
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (request == null || string.IsNullOrWhiteSpace(request.SessionKey) || world == null || world.IsRemote())
            return Reject(result, "An authoritative world and NPC session are required.");
        int ticks = Math.Max(1, Math.Min(72000, request.DutyTicks <= 0 ? 600 : request.DutyTicks));
        float radius = Math.Max(0.5f, Math.Min(12f, request.HoldRadius <= 0f ? 2.5f : request.HoldRadius));
        string target = string.Format(CultureInfo.InvariantCulture, "guard:post:{0:R}:{1:R}:{2:R}:{3}:{4:R}",
            request.Position.x, request.Position.y, request.Position.z, ticks, radius);
        RebirthNpcWorkAssignment assignment; string detail;
        RebirthNpcWorkAssignmentResult create = RebirthNpcWorkAssignmentService.Create(
            new RebirthNpcWorkAssignmentRequest {
                SessionKey = request.SessionKey, DefinitionId = GuardPostDefinitionId,
                TargetKey = target, TargetPosition = request.Position, HasTargetPosition = true,
                Priority = request.Priority == 0 ? 275 : request.Priority
            }, out assignment, out detail);
        if (create != RebirthNpcWorkAssignmentResult.Succeeded || assignment == null)
            return Reject(result, detail.Length == 0 ? "Guard assignment creation failed." : detail);
        ActivePackage package = new ActivePackage { Id = result.PackageId, AssignmentId = assignment.AssignmentId,
            UpdatedUtcTicks = DateTime.UtcNow.Ticks, Detail = "Guard-post assignment created." };
        lock (Sync) { Active[package.Id] = package; Schedule.Add(package.Id); }
        result.AssignmentId = assignment.AssignmentId; result.Created = true; Interlocked.Increment(ref created);
        if (request.StartImmediately) { result.Started = RebirthNpcWorkAssignmentCoordinator.Start(assignment.AssignmentId, out detail); if (result.Started) Interlocked.Increment(ref started); }
        result.Detail = detail.Length == 0 ? package.Detail : detail;
        return result;
    }

    public static void Tick(int budget)
    {
        RebirthNpcGuardPostWorkAdapter.TickPendingRestores(Math.Max(1,budget));
        if(budget<=0)return;
        for(int processed=0;processed<budget;processed++)
        {
            Guid packageId; ActivePackage p;
            lock(Sync)
            {
                if(!Schedule.TryTake(out packageId))break;
                if(!Active.TryGetValue(packageId,out p))continue;
            }
            RebirthNpcWorkAssignment a;
            if(!RebirthNpcWorkAssignmentService.TryGet(p.AssignmentId,out a))
            {
                lock(Sync)RemovePackageLocked(p.Id);
                Interlocked.Increment(ref failed);
                continue;
            }
            p.UpdatedUtcTicks=DateTime.UtcNow.Ticks; p.Detail="Guard assignment state="+a.Status+".";
            bool terminal=false;
            if(a.Status==RebirthNpcWorkAssignmentStatus.Completed)
            { terminal=true; Interlocked.Increment(ref completed); }
            else if(a.Status==RebirthNpcWorkAssignmentStatus.Failed||a.Status==RebirthNpcWorkAssignmentStatus.Cancelled)
            { terminal=true; Interlocked.Increment(ref failed); }
            lock(Sync)
            {
                if(terminal)RemovePackageLocked(p.Id);
                else if(Active.ContainsKey(p.Id))Schedule.Return(p.Id);
            }
            Interlocked.Increment(ref reconciliations);
        }
    }


    public static RebirthNpcGuardDutyPackageSnapshot[] GetSnapshots()
    {
        lock (Sync)
        {
            RebirthNpcGuardDutyPackageSnapshot[] snapshots = new RebirthNpcGuardDutyPackageSnapshot[Active.Count];
            int index = 0;
            foreach (ActivePackage package in Active.Values)
                snapshots[index++] = new RebirthNpcGuardDutyPackageSnapshot { PackageId = package.Id, AssignmentId = package.AssignmentId, UpdatedUtcTicks = package.UpdatedUtcTicks, Detail = package.Detail };
            Array.Sort(snapshots, delegate(RebirthNpcGuardDutyPackageSnapshot a, RebirthNpcGuardDutyPackageSnapshot b) { return a.PackageId.CompareTo(b.PackageId); });
            return snapshots;
        }
    }

    private static void RemovePackageLocked(Guid packageId)
    {
        Active.Remove(packageId);
        Schedule.Remove(packageId);
    }

    public static void ResetForWorldChange()
    {
        lock (Sync) { Active.Clear(); Schedule.Clear(); }
        RebirthNpcGuardPostWorkAdapter.ResetForWorldChange();
    }

    private static RebirthNpcGuardDutyPackageResult Reject(RebirthNpcGuardDutyPackageResult r,string detail){r.Detail=detail;Interlocked.Increment(ref rejected);return r;}
    public static string GetReport(){lock(Sync)return "[REBIRTH NPC Guard Packages] active="+Active.Count+" requests="+Interlocked.Read(ref requests)+" created="+Interlocked.Read(ref created)+" started="+Interlocked.Read(ref started)+" rejected="+Interlocked.Read(ref rejected)+" reconciliations="+Interlocked.Read(ref reconciliations)+" completed="+Interlocked.Read(ref completed)+" failed="+Interlocked.Read(ref failed);}
}

public sealed class RebirthNpcGuardPostWorkAdapter : IRebirthNpcGuardDutyWorkAdapter
{
    private sealed class DutyState
    {
        public int InitialTicks; public int RemainingTicks; public float Radius; public Vector3 Post;
        public RebirthNpcStableId NpcId; public RebirthNpcOrderState PreviousOrder; public Vector3 PreviousGuard; public bool PreviousHasGuard;
        public bool Restored; public bool PendingRestore;
    }
    private static readonly object Sync=new object();
    private static readonly Dictionary<ulong,DutyState> States=new Dictionary<ulong,DutyState>();
    private static long begun,ticks,completed,cancelled,restored,orderFailures,driftCorrections;
    public string AdapterId{get{return "rebirth.guard.post.native";}} public int Priority{get{return 500;}}
    public bool CanHandle(RebirthNpcConcreteWorkContext c){return c!=null&&c.Assignment!=null&&c.Assignment.TargetKey!=null&&c.Assignment.TargetKey.StartsWith("guard:post:",StringComparison.OrdinalIgnoreCase);}

    public RebirthNpcWorkMutationResult BeginGuardDuty(RebirthNpcConcreteWorkContext context)
    {
        Vector3 post; int dutyTicks; float radius; string error;
        if(!TryParse(context.Assignment.TargetKey,out post,out dutyTicks,out radius,out error))return Fail(RebirthNpcWorkFailureCategory.InvalidTarget,error);
        EntityRebirthNPC npc; if(!RebirthNpcWorkNavigationService.TryResolveNpc(context.Assignment.NpcId,out npc))return Retry("Guard NPC is not loaded.");
        DutyState state=new DutyState{InitialTicks=dutyTicks,RemainingTicks=dutyTicks,Radius=radius,Post=post,NpcId=context.Assignment.NpcId,PreviousOrder=npc.RebirthRuntimeState.Order,PreviousGuard=npc.RebirthRuntimeState.GuardPosition,PreviousHasGuard=npc.RebirthRuntimeState.HasGuardPosition};
        RebirthNpcTransactionResult set=npc.SetRebirthOrder(RebirthNpcOrderState.Guard,post,true);
        if(!set.Succeeded){Interlocked.Increment(ref orderFailures);return Fail(RebirthNpcWorkFailureCategory.AuthorizationDenied,set.Error);}
        lock(Sync)States[context.Assignment.AssignmentId]=state;Interlocked.Increment(ref begun);
        return RebirthNpcWorkMutationResult.Continue(0f,"Guard order applied; moving to assigned post.");
    }

    public RebirthNpcWorkMutationResult TickGuardDuty(RebirthNpcConcreteWorkContext context)
    {
        DutyState state;lock(Sync)if(!States.TryGetValue(context.Assignment.AssignmentId,out state))return Fail(RebirthNpcWorkFailureCategory.ExecutorFault,"Guard-duty state is unavailable.");
        EntityRebirthNPC npc;if(!RebirthNpcWorkNavigationService.TryResolveNpc(context.Assignment.NpcId,out npc))return Retry("Guard NPC is temporarily unloaded.");
        if(npc.RebirthRuntimeState.Order!=RebirthNpcOrderState.Guard||!npc.RebirthRuntimeState.HasGuardPosition||(npc.RebirthRuntimeState.GuardPosition-state.Post).sqrMagnitude>0.01f)
        {
            RebirthNpcTransactionResult reset=npc.SetRebirthOrder(RebirthNpcOrderState.Guard,state.Post,true);
            if(!reset.Succeeded){Interlocked.Increment(ref orderFailures);return Retry(reset.Error);}
            Interlocked.Increment(ref driftCorrections);
        }
        if(state.RemainingTicks>0)state.RemainingTicks--;Interlocked.Increment(ref ticks);
        float step=1f/Math.Max(1,state.InitialTicks);
        if(state.RemainingTicks>0)return RebirthNpcWorkMutationResult.Continue(step,"Holding guard post; remaining ticks="+state.RemainingTicks+".");
        string restoreDetail;
        if(!TryRestore(context.Assignment.AssignmentId,npc,state,out restoreDetail))
            return new RebirthNpcWorkMutationResult{Disposition=RebirthNpcWorkExecutionDisposition.Retry,FailureCategory=RebirthNpcWorkFailureCategory.TargetUnavailable,MutationCommitted=false,ProgressDelta=0f,Detail=restoreDetail};
        Interlocked.Increment(ref completed);
        return new RebirthNpcWorkMutationResult{Disposition=RebirthNpcWorkExecutionDisposition.Completed,MutationCommitted=true,ProgressDelta=step,Detail="Guard duty completed and previous NPC order restored."};
    }

    public void CancelGuardDuty(RebirthNpcConcreteWorkContext context,string reason)
    {
        DutyState state;lock(Sync)if(!States.TryGetValue(context.Assignment.AssignmentId,out state))return;
        EntityRebirthNPC npc;RebirthNpcWorkNavigationService.TryResolveNpc(context.Assignment.NpcId,out npc);string ignored;if(!TryRestore(context.Assignment.AssignmentId,npc,state,out ignored)){lock(Sync){DutyState current;if(States.TryGetValue(context.Assignment.AssignmentId,out current)){current.PendingRestore=true;States[context.Assignment.AssignmentId]=current;}}}Interlocked.Increment(ref cancelled);
    }


    public static void TickPendingRestores(int budget)
    {
        if(budget<=0)return; KeyValuePair<ulong,DutyState>[] snapshot;
        lock(Sync){snapshot=new KeyValuePair<ulong,DutyState>[States.Count];int i=0;foreach(KeyValuePair<ulong,DutyState> pair in States)if(pair.Value.PendingRestore)snapshot[i++]=pair;if(i<snapshot.Length)Array.Resize(ref snapshot,i);}
        int limit=Math.Min(budget,snapshot.Length);
        for(int i=0;i<limit;i++){DutyState state=snapshot[i].Value;EntityRebirthNPC npc;if(!RebirthNpcWorkNavigationService.TryResolveNpc(state.NpcId,out npc))continue;string detail;TryRestore(snapshot[i].Key,npc,state,out detail);}
    }

    private static bool TryRestore(ulong id,EntityRebirthNPC npc,DutyState state,out string detail)
    {
        detail=string.Empty;
        if(state.Restored)return true;
        if(npc==null){detail="Previous NPC order cannot be restored while the NPC is unloaded.";return false;}
        RebirthNpcTransactionResult r=npc.SetRebirthOrder(state.PreviousOrder,state.PreviousGuard,state.PreviousHasGuard);
        if(!r.Succeeded){Interlocked.Increment(ref orderFailures);detail=string.IsNullOrEmpty(r.Error)?"Previous NPC order restoration failed.":r.Error;return false;}
        state.Restored=true;Interlocked.Increment(ref restored);
        lock(Sync)States.Remove(id);
        return true;
    }
    private static bool TryParse(string key,out Vector3 post,out int ticks,out float radius,out string error)
    {
        post=Vector3.zero;ticks=0;radius=0f;error=string.Empty;string[] p=(key??string.Empty).Split(':');
        float x,y,z;if(p.Length!=7||!float.TryParse(p[2],NumberStyles.Float,CultureInfo.InvariantCulture,out x)||!float.TryParse(p[3],NumberStyles.Float,CultureInfo.InvariantCulture,out y)||!float.TryParse(p[4],NumberStyles.Float,CultureInfo.InvariantCulture,out z)||!int.TryParse(p[5],NumberStyles.Integer,CultureInfo.InvariantCulture,out ticks)||!float.TryParse(p[6],NumberStyles.Float,CultureInfo.InvariantCulture,out radius)){error="Guard target contract is invalid.";return false;}
        post=new Vector3(x,y,z);ticks=Math.Max(1,Math.Min(72000,ticks));radius=Math.Max(0.5f,Math.Min(12f,radius));return true;
    }
    private static RebirthNpcWorkMutationResult Retry(string d){return new RebirthNpcWorkMutationResult{Disposition=RebirthNpcWorkExecutionDisposition.Retry,FailureCategory=RebirthNpcWorkFailureCategory.TargetUnavailable,Detail=d};}
    private static RebirthNpcWorkMutationResult Fail(RebirthNpcWorkFailureCategory f,string d){return new RebirthNpcWorkMutationResult{Disposition=RebirthNpcWorkExecutionDisposition.Failed,FailureCategory=f,Detail=d};}
    public static ulong[] GetActiveAssignmentIds()
    {
        lock (Sync) { ulong[] ids = new ulong[States.Count]; States.Keys.CopyTo(ids, 0); Array.Sort(ids); return ids; }
    }
    public static void ResetForWorldChange() { lock (Sync) States.Clear(); }
    public static string GetReport(){lock(Sync)return "[REBIRTH NPC Guard Adapter] active="+States.Count+" begun="+Interlocked.Read(ref begun)+" ticks="+Interlocked.Read(ref ticks)+" completed="+Interlocked.Read(ref completed)+" cancelled="+Interlocked.Read(ref cancelled)+" restored="+Interlocked.Read(ref restored)+" orderFailures="+Interlocked.Read(ref orderFailures)+" driftCorrections="+Interlocked.Read(ref driftCorrections);}
}
