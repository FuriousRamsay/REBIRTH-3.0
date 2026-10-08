using System;
using System.Collections.Generic;

#nullable disable

public interface IRebirthNpcFarmingWorkAdapter
{
    string AdapterId { get; } int Priority { get; }
    bool CanHandle(RebirthNpcConcreteWorkContext context);
    RebirthNpcWorkMutationResult BeginFarming(RebirthNpcConcreteWorkContext context);
    RebirthNpcWorkMutationResult TickFarming(RebirthNpcConcreteWorkContext context);
    void CancelFarming(RebirthNpcConcreteWorkContext context, string reason);
}
public interface IRebirthNpcMiningWorkAdapter
{
    string AdapterId { get; } int Priority { get; }
    bool CanHandle(RebirthNpcConcreteWorkContext context);
    RebirthNpcWorkMutationResult BeginMining(RebirthNpcConcreteWorkContext context);
    RebirthNpcWorkMutationResult TickMining(RebirthNpcConcreteWorkContext context);
    void CancelMining(RebirthNpcConcreteWorkContext context, string reason);
}
public interface IRebirthNpcScavengingWorkAdapter
{
    string AdapterId { get; } int Priority { get; }
    bool CanHandle(RebirthNpcConcreteWorkContext context);
    RebirthNpcWorkMutationResult BeginScavenging(RebirthNpcConcreteWorkContext context);
    RebirthNpcWorkMutationResult TickScavenging(RebirthNpcConcreteWorkContext context);
    void CancelScavenging(RebirthNpcConcreteWorkContext context, string reason);
}
public interface IRebirthNpcGuardDutyWorkAdapter
{
    string AdapterId { get; } int Priority { get; }
    bool CanHandle(RebirthNpcConcreteWorkContext context);
    RebirthNpcWorkMutationResult BeginGuardDuty(RebirthNpcConcreteWorkContext context);
    RebirthNpcWorkMutationResult TickGuardDuty(RebirthNpcConcreteWorkContext context);
    void CancelGuardDuty(RebirthNpcConcreteWorkContext context, string reason);
}
public interface IRebirthNpcMedicalWorkAdapter
{
    string AdapterId { get; } int Priority { get; }
    bool CanHandle(RebirthNpcConcreteWorkContext context);
    RebirthNpcWorkMutationResult BeginMedical(RebirthNpcConcreteWorkContext context);
    RebirthNpcWorkMutationResult TickMedical(RebirthNpcConcreteWorkContext context);
    void CancelMedical(RebirthNpcConcreteWorkContext context, string reason);
}
public interface IRebirthNpcRepairWorkAdapter
{
    string AdapterId { get; } int Priority { get; }
    bool CanHandle(RebirthNpcConcreteWorkContext context);
    RebirthNpcWorkMutationResult BeginRepair(RebirthNpcConcreteWorkContext context);
    RebirthNpcWorkMutationResult TickRepair(RebirthNpcConcreteWorkContext context);
    void CancelRepair(RebirthNpcConcreteWorkContext context, string reason);
}
public interface IRebirthNpcCraftingWorkAdapter
{
    string AdapterId { get; } int Priority { get; }
    bool CanHandle(RebirthNpcConcreteWorkContext context);
    RebirthNpcWorkMutationResult BeginOperation(RebirthNpcConcreteWorkContext context);
    RebirthNpcWorkMutationResult TickOperation(RebirthNpcConcreteWorkContext context);
    void CancelOperation(RebirthNpcConcreteWorkContext context, string reason);
}

public static class RebirthNpcConcreteWorkAdapterRegistry
{
    private static readonly object Sync=new object();
    private static readonly List<IRebirthNpcFarmingWorkAdapter> Farming=new List<IRebirthNpcFarmingWorkAdapter>();
    private static readonly List<IRebirthNpcMiningWorkAdapter> Mining=new List<IRebirthNpcMiningWorkAdapter>();
    private static readonly List<IRebirthNpcScavengingWorkAdapter> Scavenging=new List<IRebirthNpcScavengingWorkAdapter>();
    private static readonly List<IRebirthNpcGuardDutyWorkAdapter> GuardDuty=new List<IRebirthNpcGuardDutyWorkAdapter>();
    private static readonly List<IRebirthNpcMedicalWorkAdapter> Medical=new List<IRebirthNpcMedicalWorkAdapter>();
    private static readonly List<IRebirthNpcRepairWorkAdapter> Repair=new List<IRebirthNpcRepairWorkAdapter>();
    private static readonly List<IRebirthNpcCraftingWorkAdapter> Crafting=new List<IRebirthNpcCraftingWorkAdapter>();
    public static void Register(IRebirthNpcFarmingWorkAdapter a){if(a==null)throw new ArgumentNullException(nameof(a));lock(Sync){Replace(Farming,a.AdapterId);Farming.Add(a);Farming.Sort(delegate(IRebirthNpcFarmingWorkAdapter x,IRebirthNpcFarmingWorkAdapter y){int p=y.Priority.CompareTo(x.Priority);return p!=0?p:string.Compare(x.AdapterId,y.AdapterId,StringComparison.OrdinalIgnoreCase);});}}
    public static void Register(IRebirthNpcMiningWorkAdapter a){if(a==null)throw new ArgumentNullException(nameof(a));lock(Sync){Replace(Mining,a.AdapterId);Mining.Add(a);Mining.Sort(delegate(IRebirthNpcMiningWorkAdapter x,IRebirthNpcMiningWorkAdapter y){int p=y.Priority.CompareTo(x.Priority);return p!=0?p:string.Compare(x.AdapterId,y.AdapterId,StringComparison.OrdinalIgnoreCase);});}}
    public static void Register(IRebirthNpcScavengingWorkAdapter a){if(a==null)throw new ArgumentNullException(nameof(a));lock(Sync){Replace(Scavenging,a.AdapterId);Scavenging.Add(a);Scavenging.Sort(delegate(IRebirthNpcScavengingWorkAdapter x,IRebirthNpcScavengingWorkAdapter y){int p=y.Priority.CompareTo(x.Priority);return p!=0?p:string.Compare(x.AdapterId,y.AdapterId,StringComparison.OrdinalIgnoreCase);});}}
    public static void Register(IRebirthNpcGuardDutyWorkAdapter a){if(a==null)throw new ArgumentNullException(nameof(a));lock(Sync){Replace(GuardDuty,a.AdapterId);GuardDuty.Add(a);GuardDuty.Sort(delegate(IRebirthNpcGuardDutyWorkAdapter x,IRebirthNpcGuardDutyWorkAdapter y){int p=y.Priority.CompareTo(x.Priority);return p!=0?p:string.Compare(x.AdapterId,y.AdapterId,StringComparison.OrdinalIgnoreCase);});}}
    public static void Register(IRebirthNpcMedicalWorkAdapter a){if(a==null)throw new ArgumentNullException(nameof(a));lock(Sync){Replace(Medical,a.AdapterId);Medical.Add(a);Medical.Sort(delegate(IRebirthNpcMedicalWorkAdapter x,IRebirthNpcMedicalWorkAdapter y){int p=y.Priority.CompareTo(x.Priority);return p!=0?p:string.Compare(x.AdapterId,y.AdapterId,StringComparison.OrdinalIgnoreCase);});}}
    public static void Register(IRebirthNpcRepairWorkAdapter a){if(a==null)throw new ArgumentNullException(nameof(a));lock(Sync){Replace(Repair,a.AdapterId);Repair.Add(a);Repair.Sort(delegate(IRebirthNpcRepairWorkAdapter x,IRebirthNpcRepairWorkAdapter y){int p=y.Priority.CompareTo(x.Priority);return p!=0?p:string.Compare(x.AdapterId,y.AdapterId,StringComparison.OrdinalIgnoreCase);});}}
    public static void Register(IRebirthNpcCraftingWorkAdapter a){if(a==null)throw new ArgumentNullException(nameof(a));lock(Sync){Replace(Crafting,a.AdapterId);Crafting.Add(a);Crafting.Sort(delegate(IRebirthNpcCraftingWorkAdapter x,IRebirthNpcCraftingWorkAdapter y){int p=y.Priority.CompareTo(x.Priority);return p!=0?p:string.Compare(x.AdapterId,y.AdapterId,StringComparison.OrdinalIgnoreCase);});}}
    private static void Replace<T>(List<T> list,string id){for(int i=list.Count-1;i>=0;i--){object o=list[i];string existing=o is IRebirthNpcFarmingWorkAdapter?((IRebirthNpcFarmingWorkAdapter)o).AdapterId:o is IRebirthNpcMiningWorkAdapter?((IRebirthNpcMiningWorkAdapter)o).AdapterId:o is IRebirthNpcScavengingWorkAdapter?((IRebirthNpcScavengingWorkAdapter)o).AdapterId:o is IRebirthNpcGuardDutyWorkAdapter?((IRebirthNpcGuardDutyWorkAdapter)o).AdapterId:o is IRebirthNpcMedicalWorkAdapter?((IRebirthNpcMedicalWorkAdapter)o).AdapterId:o is IRebirthNpcRepairWorkAdapter?((IRebirthNpcRepairWorkAdapter)o).AdapterId:((IRebirthNpcCraftingWorkAdapter)o).AdapterId;if(string.Equals(existing,id,StringComparison.OrdinalIgnoreCase))list.RemoveAt(i);}}
    public static IRebirthNpcFarmingWorkAdapter SelectFarming(RebirthNpcConcreteWorkContext c){lock(Sync)for(int i=0;i<Farming.Count;i++)if(Farming[i].CanHandle(c))return Farming[i];return null;}
    public static IRebirthNpcMiningWorkAdapter SelectMining(RebirthNpcConcreteWorkContext c){lock(Sync)for(int i=0;i<Mining.Count;i++)if(Mining[i].CanHandle(c))return Mining[i];return null;}
    public static IRebirthNpcScavengingWorkAdapter SelectScavenging(RebirthNpcConcreteWorkContext c){lock(Sync)for(int i=0;i<Scavenging.Count;i++)if(Scavenging[i].CanHandle(c))return Scavenging[i];return null;}
    public static IRebirthNpcGuardDutyWorkAdapter SelectGuardDuty(RebirthNpcConcreteWorkContext c){lock(Sync)for(int i=0;i<GuardDuty.Count;i++)if(GuardDuty[i].CanHandle(c))return GuardDuty[i];return null;}
    public static IRebirthNpcMedicalWorkAdapter SelectMedical(RebirthNpcConcreteWorkContext c){lock(Sync)for(int i=0;i<Medical.Count;i++)if(Medical[i].CanHandle(c))return Medical[i];return null;}
    public static IRebirthNpcRepairWorkAdapter SelectRepair(RebirthNpcConcreteWorkContext c){lock(Sync)for(int i=0;i<Repair.Count;i++)if(Repair[i].CanHandle(c))return Repair[i];return null;}
    public static IRebirthNpcCraftingWorkAdapter SelectCrafting(RebirthNpcConcreteWorkContext c){lock(Sync)for(int i=0;i<Crafting.Count;i++)if(Crafting[i].CanHandle(c))return Crafting[i];return null;}
    public static string GetReport(){lock(Sync)return "[REBIRTH NPC Concrete Adapters] farming="+Farming.Count+" mining="+Mining.Count+" scavenging="+Scavenging.Count+" guardDuty="+GuardDuty.Count+" medical="+Medical.Count+" repair="+Repair.Count+" crafting="+Crafting.Count;}
}

public abstract class RebirthNpcConcreteWorkExecutorBase : IRebirthNpcWorkAssignmentExecutor
{
    protected sealed class State
    {
        public RebirthNpcConcreteWorkContext Context; public IRebirthNpcWorkTargetAdapter TargetAdapter;
        internal RebirthNpcEmbodiedWorkIntent Intent; internal RebirthNpcWorkAssignment OriginalAssignment; public bool SettlementUnresolved;
        public int RetryCount; public long NextRetryUtcTicks; public long StartedUtcTicks; public bool BeginCompleted;
        public Dictionary<string,int> Produced=new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string,int> Consumed=new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase);
    }
    private readonly object sync=new object(); private readonly Dictionary<ulong,State> states=new Dictionary<ulong,State>();
    public abstract string ExecutorId{get;} public abstract int Priority{get;} protected abstract RebirthNpcWorkKind Kind{get;}
    public virtual bool CanExecute(RebirthNpcWorkAssignment a,RebirthNpcWorkDefinition d){return d!=null&&d.Kind==Kind;}
    public bool Begin(RebirthNpcWorkAssignment a,RebirthNpcWorkReservation r,out string detail)
    {
        detail=string.Empty;
        RebirthNpcEmbodiedWorkIntent intent=RebirthNpcEmbodiedWorkOwner.CurrentDispatchIntent;
        if(a==null||r==null||intent==null||intent.Path!="concrete"||intent.PrimaryId!=a.AssignmentId||intent.SecondaryId!=r.ReservationId||intent.ExecutorId!=ExecutorId||!RebirthNpcEmbodiedWorkOwner.IsDispatching(intent)){detail="Owned EAI dispatch required.";return false;}
        lock(sync)if(states.ContainsKey(a.AssignmentId)){detail="Original executor state remains held.";return false;}
        RebirthNpcWorkDefinition d;if(!RebirthNpcWorkDefinitionRegistry.TryGet(a.DefinitionId,out d)){detail="Definition disappeared.";return false;}
        IRebirthNpcWorkTargetAdapter ta;RebirthNpcWorkTargetSnapshot target;
        if(!RebirthNpcWorkTargetAdapterRegistry.TryResolve(a,d,out ta,out target,out detail))return false;
        State s=new State{Intent=intent,OriginalAssignment=a.Clone(),StartedUtcTicks=DateTime.UtcNow.Ticks,TargetAdapter=ta,Context=new RebirthNpcConcreteWorkContext{Assignment=a,Definition=d,Reservation=r,Target=target,OperationId=DeriveOperationId(a.AssignmentId),Attempt=0,Progress=0f,AuthorityKey=a.ActorId??string.Empty}};
        Func<bool> originalCurrent=()=>
        {
            RebirthNpcWorkAssignment live;var observed=s.Context.Assignment;var original=s.OriginalAssignment;
            State held;lock(sync)if(states.TryGetValue(a.AssignmentId,out held)&&!ReferenceEquals(held,s))return false;
            return observed!=null&&observed.AssignmentId==original.AssignmentId&&observed.Revision==original.Revision&&
                observed.Status==original.Status&&observed.NpcId.Equals(original.NpcId)&&observed.ActorId==original.ActorId&&
                observed.DefinitionId==original.DefinitionId&&observed.TargetKey==original.TargetKey&&
                observed.TargetPosition.Equals(original.TargetPosition)&&observed.HasTargetPosition==original.HasTargetPosition&&
                observed.Priority==original.Priority&&ReferenceEquals(observed.Schedule,original.Schedule)&&
                RebirthNpcWorkAssignmentService.TryGet(a.AssignmentId,out live)&&live.Revision==original.Revision&&live.Status==original.Status;
        };
        RebirthNpcWorkFailureCategory failure;if(!ta.Validate(s.Context,out failure,out detail))return false;
        if(!RebirthNpcEmbodiedWorkOwner.IsDispatching(intent)||!originalCurrent()){detail="Original owner/assignment changed during validation.";return false;}
        lock(sync)states.Add(a.AssignmentId,s);
        RebirthNpcWorkMutationResult result=BeginCore(s,out detail);
        if(!RebirthNpcEmbodiedWorkOwner.IsDispatching(intent)||!originalCurrent()){s.SettlementUnresolved=true;detail="Original owner/assignment changed after Begin.";return false;}bool terminal;
        if(!AcceptResult(s,result,ref detail,out terminal)){CleanupRejectedBegin(s,detail);return false;}
        if(terminal){if(result.Disposition!=RebirthNpcWorkExecutionDisposition.Completed){CleanupRejectedBegin(s,detail);return false;}s.BeginCompleted=true;s.Context.Progress=1f;}
        lock(sync)states[a.AssignmentId]=s;Publish(s,s.BeginCompleted?RebirthNpcWorkExecutorPhase.Finalizing:RebirthNpcWorkExecutorPhase.AcquiringInputs,string.Empty);return true;
    }
    public bool Tick(RebirthNpcWorkAssignment a,RebirthNpcWorkReservation r,out bool completed,out string detail)
    {
        completed=false;detail=string.Empty;if(a==null||r==null){detail="Original assignment unavailable.";return false;}State s;lock(sync)if(!states.TryGetValue(a.AssignmentId,out s)){detail="Executor state is unavailable.";return false;}
        if(!RebirthNpcEmbodiedWorkOwner.IsDispatching(s.Intent)||s.SettlementUnresolved){detail="Original EAI ownership unavailable or settlement unresolved.";return false;}
        if(r==null||r.ReservationId!=s.Context.Reservation.ReservationId){detail="Original reservation changed.";return false;}
        RebirthNpcWorkAssignment original=s.OriginalAssignment,live;
        uint executingRevision=original.Revision==uint.MaxValue?1U:original.Revision+1U;
        if(a.Revision!=executingRevision||a.Status!=RebirthNpcWorkAssignmentStatus.Executing||
            !a.NpcId.Equals(original.NpcId)||a.DefinitionId!=original.DefinitionId||a.ActorId!=original.ActorId||
            a.TargetKey!=original.TargetKey||!a.TargetPosition.Equals(original.TargetPosition)||
            a.HasTargetPosition!=original.HasTargetPosition||a.Priority!=original.Priority||
            a.CreatedUtcTicks!=original.CreatedUtcTicks||!ReferenceEquals(a.Schedule,original.Schedule)||
            !RebirthNpcWorkAssignmentService.TryGet(a.AssignmentId,out live)||live.Revision!=a.Revision||
            live.Status!=a.Status||live.ActorId!=a.ActorId||live.TargetKey!=a.TargetKey||
            !live.TargetPosition.Equals(a.TargetPosition)||live.HasTargetPosition!=a.HasTargetPosition||
            !live.NpcId.Equals(a.NpcId)||live.DefinitionId!=a.DefinitionId)
        {detail="Canonical original assignment changed.";return false;}
        s.Context.Assignment=a;s.Context.Reservation=r;s.Context.Attempt=s.RetryCount;
        Func<bool> originalCurrent=()=>
        {
            State held;lock(sync)if(!states.TryGetValue(original.AssignmentId,out held)||!ReferenceEquals(held,s))return false;
            var observed=s.Context.Assignment;RebirthNpcWorkAssignment canonical;
            return ReferenceEquals(observed,a)&&observed.Revision==executingRevision&&
                observed.Status==RebirthNpcWorkAssignmentStatus.Executing&&observed.NpcId.Equals(original.NpcId)&&
                observed.DefinitionId==original.DefinitionId&&observed.ActorId==original.ActorId&&observed.TargetKey==original.TargetKey&&
                observed.TargetPosition.Equals(original.TargetPosition)&&observed.HasTargetPosition==original.HasTargetPosition&&
                observed.Priority==original.Priority&&observed.CreatedUtcTicks==original.CreatedUtcTicks&&
                ReferenceEquals(observed.Schedule,original.Schedule)&&
                RebirthNpcWorkAssignmentService.TryGet(original.AssignmentId,out canonical)&&canonical.Revision==executingRevision&&
                canonical.Status==RebirthNpcWorkAssignmentStatus.Executing&&canonical.NpcId.Equals(original.NpcId)&&
                canonical.DefinitionId==original.DefinitionId&&canonical.ActorId==original.ActorId&&canonical.TargetKey==original.TargetKey&&
                canonical.TargetPosition.Equals(original.TargetPosition)&&canonical.HasTargetPosition==original.HasTargetPosition;
        };
        if(s.BeginCompleted){completed=true;detail="Work completed during executor begin.";return true;}
        if(s.NextRetryUtcTicks>DateTime.UtcNow.Ticks){detail="Retry backoff active.";Publish(s,RebirthNpcWorkExecutorPhase.Suspended,detail);return true;}
        RebirthNpcWorkFailureCategory failure;if(!s.TargetAdapter.Validate(s.Context,out failure,out detail))return HandleFailure(s,failure,detail,out completed);
        if(!RebirthNpcEmbodiedWorkOwner.IsDispatching(s.Intent)||!originalCurrent()){detail="Original owner/context changed during validation.";return false;}
        if(!s.TargetAdapter.IsNpcInRange(s.Context,out detail))
        {
            RebirthNpcWorkNavigationDisposition navigation=RebirthNpcWorkNavigationService.Request(s.Context,out detail);
            if(navigation==RebirthNpcWorkNavigationDisposition.PermanentlyUnavailable)return HandleFailure(s,RebirthNpcWorkFailureCategory.PathingFailure,detail,out completed);
            Publish(s,navigation==RebirthNpcWorkNavigationDisposition.TemporarilyUnavailable?RebirthNpcWorkExecutorPhase.Suspended:RebirthNpcWorkExecutorPhase.MovingToTarget,detail);
            return true;
        }
        if(!RebirthNpcEmbodiedWorkOwner.IsDispatching(s.Intent)||!originalCurrent()){detail="Original owner/context changed during range check.";return false;}
        RebirthNpcWorkMutationResult result=TickCore(s,out detail);
        if(!RebirthNpcEmbodiedWorkOwner.IsDispatching(s.Intent)||!originalCurrent()){s.SettlementUnresolved=true;detail="Original owner/assignment changed after Tick.";return false;}bool terminal;if(!AcceptResult(s,result,ref detail,out terminal))return false;
        completed=terminal&&result.Disposition==RebirthNpcWorkExecutionDisposition.Completed;if(completed)Publish(s,RebirthNpcWorkExecutorPhase.Finalizing,detail);return true;
    }
    public void End(RebirthNpcWorkAssignment a,RebirthNpcWorkReservation r,string reason)
    {
        if(a==null||r==null)return;
        State s;lock(sync)if(!states.TryGetValue(a.AssignmentId,out s))return;
        if(!RebirthNpcEmbodiedWorkOwner.IsDispatching(s.Intent)||r.ReservationId!=s.Context.Reservation.ReservationId)return;
        // Existing void cancellation cannot prove settlement. Preserve original state even on throw.
        s.SettlementUnresolved=true;
        // No typed native cancellation proof; retain original state without EndCore.
        // Navigation cancellation is also unqualified.
    }
    protected abstract RebirthNpcWorkMutationResult BeginCore(State s,out string detail);
    protected abstract RebirthNpcWorkMutationResult TickCore(State s,out string detail);
    protected abstract void EndCore(State s,string reason);
    private bool AcceptResult(State s,RebirthNpcWorkMutationResult r,ref string detail,out bool terminal)
    {
        terminal=false;if(r==null){detail="Adapter returned no result.";return false;}if(!string.IsNullOrEmpty(r.Detail))detail=r.Detail;
        Merge(s.Produced,r.Produced);Merge(s.Consumed,r.Consumed);s.Context.Progress=Math.Min(1f,s.Context.Progress+Math.Max(0f,r.ProgressDelta));
        if(r.Disposition==RebirthNpcWorkExecutionDisposition.Retry)return HandleRetry(s,r.FailureCategory,detail);
        if(r.Disposition==RebirthNpcWorkExecutionDisposition.Suspend){s.NextRetryUtcTicks=DateTime.UtcNow.AddSeconds(5).Ticks;Publish(s,RebirthNpcWorkExecutorPhase.Suspended,detail);return true;}
        terminal=r.Disposition==RebirthNpcWorkExecutionDisposition.Completed||r.Disposition==RebirthNpcWorkExecutionDisposition.Failed;
        Publish(s,terminal?RebirthNpcWorkExecutorPhase.Finalizing:RebirthNpcWorkExecutorPhase.Mutating,detail);
        return r.Disposition!=RebirthNpcWorkExecutionDisposition.Failed;
    }
    private bool HandleFailure(State s,RebirthNpcWorkFailureCategory failure,string detail,out bool completed){completed=false;if(failure==RebirthNpcWorkFailureCategory.TargetUnavailable||failure==RebirthNpcWorkFailureCategory.OutOfRange||failure==RebirthNpcWorkFailureCategory.InventoryUnavailable||failure==RebirthNpcWorkFailureCategory.WorkstationUnavailable)return HandleRetry(s,failure,detail);return false;}
    private bool HandleRetry(State s,RebirthNpcWorkFailureCategory failure,string detail){if(s.RetryCount>=5)return false;s.RetryCount++;s.NextRetryUtcTicks=DateTime.UtcNow.AddSeconds(Math.Min(30,1<<Math.Min(5,s.RetryCount))).Ticks;Publish(s,RebirthNpcWorkExecutorPhase.Suspended,detail);return true;}
    private void CleanupRejectedBegin(State s,string detail)
    {
        // A declined Begin may have mutated native state; generic Cancel is no settlement receipt.
        s.SettlementUnresolved=true;
        Publish(s,RebirthNpcWorkExecutorPhase.Suspended,"Begin settlement unresolved: "+(detail??string.Empty));
    }
    private void Publish(State s,RebirthNpcWorkExecutorPhase p,string suspension){RebirthNpcWorkExecutionTelemetry.Upsert(new RebirthNpcWorkExecutionSnapshot{AssignmentId=s.Context.Assignment.AssignmentId,ExecutorId=ExecutorId,Phase=p,StableTargetId=s.Context.Target.StableTargetId,Progress=s.Context.Progress,RetryCount=s.RetryCount,SuspensionReason=p==RebirthNpcWorkExecutorPhase.Suspended?suspension:string.Empty});}
    private static void Merge(Dictionary<string,int> into,Dictionary<string,int> from){if(from==null)return;foreach(KeyValuePair<string,int> p in from){int n;into.TryGetValue(p.Key,out n);into[p.Key]=n+p.Value;}}
    private static Guid DeriveOperationId(ulong id){byte[] b=new byte[16];BitConverter.GetBytes(id).CopyTo(b,0);BitConverter.GetBytes(unchecked((long)0x334B524F5750434E)).CopyTo(b,8);return new Guid(b);}
}

public sealed class RebirthNpcFarmingWorkExecutor : RebirthNpcConcreteWorkExecutorBase
{
    private readonly Dictionary<ulong,IRebirthNpcFarmingWorkAdapter> adapters=new Dictionary<ulong,IRebirthNpcFarmingWorkAdapter>();
    public override string ExecutorId{get{return "rebirth.work.farming";}} public override int Priority{get{return 300;}} protected override RebirthNpcWorkKind Kind{get{return RebirthNpcWorkKind.Farming;}}
    protected override RebirthNpcWorkMutationResult BeginCore(State s,out string d){var a=RebirthNpcConcreteWorkAdapterRegistry.SelectFarming(s.Context);if(a==null){d="No farming adapter supports the target.";return new RebirthNpcWorkMutationResult{Disposition=RebirthNpcWorkExecutionDisposition.Failed,FailureCategory=RebirthNpcWorkFailureCategory.InvalidTarget,Detail=d};}adapters[s.Context.Assignment.AssignmentId]=a;var r=a.BeginFarming(s.Context);d=r==null?"Farming adapter returned no result.":r.Detail;return r;}
    protected override RebirthNpcWorkMutationResult TickCore(State s,out string d){IRebirthNpcFarmingWorkAdapter a;if(!adapters.TryGetValue(s.Context.Assignment.AssignmentId,out a)){d="Farming adapter state disappeared.";return null;}var r=a.TickFarming(s.Context);d=r==null?"Work adapter returned no result.":r.Detail;return r;}
    protected override void EndCore(State s,string reason){IRebirthNpcFarmingWorkAdapter a;if(adapters.TryGetValue(s.Context.Assignment.AssignmentId,out a)){a.CancelFarming(s.Context,reason);adapters.Remove(s.Context.Assignment.AssignmentId);}}
}
public sealed class RebirthNpcMiningWorkExecutor : RebirthNpcConcreteWorkExecutorBase
{
    private readonly Dictionary<ulong,IRebirthNpcMiningWorkAdapter> adapters=new Dictionary<ulong,IRebirthNpcMiningWorkAdapter>();
    public override string ExecutorId{get{return "rebirth.work.mining";}} public override int Priority{get{return 290;}} protected override RebirthNpcWorkKind Kind{get{return RebirthNpcWorkKind.Mining;}}
    protected override RebirthNpcWorkMutationResult BeginCore(State s,out string d){var a=RebirthNpcConcreteWorkAdapterRegistry.SelectMining(s.Context);if(a==null){d="No mining adapter supports the target.";return new RebirthNpcWorkMutationResult{Disposition=RebirthNpcWorkExecutionDisposition.Failed,FailureCategory=RebirthNpcWorkFailureCategory.InvalidTarget,Detail=d};}adapters[s.Context.Assignment.AssignmentId]=a;var r=a.BeginMining(s.Context);d=r==null?"Mining adapter returned no result.":r.Detail;return r;}
    protected override RebirthNpcWorkMutationResult TickCore(State s,out string d){IRebirthNpcMiningWorkAdapter a;if(!adapters.TryGetValue(s.Context.Assignment.AssignmentId,out a)){d="Mining adapter state disappeared.";return null;}var r=a.TickMining(s.Context);d=r==null?"Work adapter returned no result.":r.Detail;return r;}
    protected override void EndCore(State s,string reason){IRebirthNpcMiningWorkAdapter a;if(adapters.TryGetValue(s.Context.Assignment.AssignmentId,out a)){a.CancelMining(s.Context,reason);adapters.Remove(s.Context.Assignment.AssignmentId);}}
}
public sealed class RebirthNpcScavengingWorkExecutor : RebirthNpcConcreteWorkExecutorBase
{
    private readonly Dictionary<ulong,IRebirthNpcScavengingWorkAdapter> adapters=new Dictionary<ulong,IRebirthNpcScavengingWorkAdapter>();
    public override string ExecutorId{get{return "rebirth.work.scavenging";}} public override int Priority{get{return 285;}} protected override RebirthNpcWorkKind Kind{get{return RebirthNpcWorkKind.Scavenging;}}
    protected override RebirthNpcWorkMutationResult BeginCore(State s,out string d){var a=RebirthNpcConcreteWorkAdapterRegistry.SelectScavenging(s.Context);if(a==null){d="No scavenging adapter supports the target.";return new RebirthNpcWorkMutationResult{Disposition=RebirthNpcWorkExecutionDisposition.Failed,FailureCategory=RebirthNpcWorkFailureCategory.InvalidTarget,Detail=d};}adapters[s.Context.Assignment.AssignmentId]=a;var r=a.BeginScavenging(s.Context);d=r==null?"Scavenging adapter returned no result.":r.Detail;return r;}
    protected override RebirthNpcWorkMutationResult TickCore(State s,out string d){IRebirthNpcScavengingWorkAdapter a;if(!adapters.TryGetValue(s.Context.Assignment.AssignmentId,out a)){d="Scavenging adapter state disappeared.";return null;}var r=a.TickScavenging(s.Context);d=r==null?"Work adapter returned no result.":r.Detail;return r;}
    protected override void EndCore(State s,string reason){IRebirthNpcScavengingWorkAdapter a;if(adapters.TryGetValue(s.Context.Assignment.AssignmentId,out a)){a.CancelScavenging(s.Context,reason);adapters.Remove(s.Context.Assignment.AssignmentId);}}
}
public sealed class RebirthNpcRepairWorkExecutor : RebirthNpcConcreteWorkExecutorBase
{
    private readonly Dictionary<ulong,IRebirthNpcRepairWorkAdapter> adapters=new Dictionary<ulong,IRebirthNpcRepairWorkAdapter>();
    public override string ExecutorId{get{return "rebirth.work.repair";}} public override int Priority{get{return 280;}} protected override RebirthNpcWorkKind Kind{get{return RebirthNpcWorkKind.Repair;}}
    public override bool CanExecute(RebirthNpcWorkAssignment a,RebirthNpcWorkDefinition d){return d!=null&&(d.Kind==RebirthNpcWorkKind.Repair||d.Kind==RebirthNpcWorkKind.Construction);}
    protected override RebirthNpcWorkMutationResult BeginCore(State s,out string d){var a=RebirthNpcConcreteWorkAdapterRegistry.SelectRepair(s.Context);if(a==null){d="No repair adapter supports the target.";return new RebirthNpcWorkMutationResult{Disposition=RebirthNpcWorkExecutionDisposition.Failed,FailureCategory=RebirthNpcWorkFailureCategory.InvalidTarget,Detail=d};}adapters[s.Context.Assignment.AssignmentId]=a;var r=a.BeginRepair(s.Context);d=r==null?"Repair adapter returned no result.":r.Detail;return r;}
    protected override RebirthNpcWorkMutationResult TickCore(State s,out string d){IRebirthNpcRepairWorkAdapter a;if(!adapters.TryGetValue(s.Context.Assignment.AssignmentId,out a)){d="Repair adapter state disappeared.";return null;}var r=a.TickRepair(s.Context);d=r==null?"Work adapter returned no result.":r.Detail;return r;}
    protected override void EndCore(State s,string reason){IRebirthNpcRepairWorkAdapter a;if(adapters.TryGetValue(s.Context.Assignment.AssignmentId,out a)){a.CancelRepair(s.Context,reason);adapters.Remove(s.Context.Assignment.AssignmentId);}}
}
public sealed class RebirthNpcCraftingWorkExecutor : RebirthNpcConcreteWorkExecutorBase
{
    private readonly Dictionary<ulong,IRebirthNpcCraftingWorkAdapter> adapters=new Dictionary<ulong,IRebirthNpcCraftingWorkAdapter>();
    public override string ExecutorId{get{return "rebirth.work.crafting-workstation";}} public override int Priority{get{return 260;}} protected override RebirthNpcWorkKind Kind{get{return RebirthNpcWorkKind.Crafting;}}
    public override bool CanExecute(RebirthNpcWorkAssignment a,RebirthNpcWorkDefinition d){return d!=null&&(d.Kind==RebirthNpcWorkKind.Crafting||d.Kind==RebirthNpcWorkKind.Workstation);}
    protected override RebirthNpcWorkMutationResult BeginCore(State s,out string d){var a=RebirthNpcConcreteWorkAdapterRegistry.SelectCrafting(s.Context);if(a==null){d="No crafting/workstation adapter supports the operation.";return new RebirthNpcWorkMutationResult{Disposition=RebirthNpcWorkExecutionDisposition.Failed,FailureCategory=RebirthNpcWorkFailureCategory.WorkstationUnavailable,Detail=d};}adapters[s.Context.Assignment.AssignmentId]=a;var r=a.BeginOperation(s.Context);d=r==null?"Crafting adapter returned no result.":r.Detail;return r;}
    protected override RebirthNpcWorkMutationResult TickCore(State s,out string d){IRebirthNpcCraftingWorkAdapter a;if(!adapters.TryGetValue(s.Context.Assignment.AssignmentId,out a)){d="Crafting adapter state disappeared.";return null;}var r=a.TickOperation(s.Context);d=r==null?"Work adapter returned no result.":r.Detail;return r;}
    protected override void EndCore(State s,string reason){IRebirthNpcCraftingWorkAdapter a;if(adapters.TryGetValue(s.Context.Assignment.AssignmentId,out a)){a.CancelOperation(s.Context,reason);adapters.Remove(s.Context.Assignment.AssignmentId);}}
}

public sealed class RebirthNpcGuardDutyWorkExecutor : RebirthNpcConcreteWorkExecutorBase
{
    private readonly Dictionary<ulong,IRebirthNpcGuardDutyWorkAdapter> adapters=new Dictionary<ulong,IRebirthNpcGuardDutyWorkAdapter>();
    public override string ExecutorId{get{return "rebirth.work.guard-duty";}} public override int Priority{get{return 275;}} protected override RebirthNpcWorkKind Kind{get{return RebirthNpcWorkKind.GuardDuty;}}
    protected override RebirthNpcWorkMutationResult BeginCore(State s,out string d){var a=RebirthNpcConcreteWorkAdapterRegistry.SelectGuardDuty(s.Context);if(a==null){d="No guard-duty adapter supports the post.";return new RebirthNpcWorkMutationResult{Disposition=RebirthNpcWorkExecutionDisposition.Failed,FailureCategory=RebirthNpcWorkFailureCategory.InvalidTarget,Detail=d};}adapters[s.Context.Assignment.AssignmentId]=a;var r=a.BeginGuardDuty(s.Context);d=r==null?"Guard-duty adapter returned no result.":r.Detail;return r;}
    protected override RebirthNpcWorkMutationResult TickCore(State s,out string d){IRebirthNpcGuardDutyWorkAdapter a;if(!adapters.TryGetValue(s.Context.Assignment.AssignmentId,out a)){d="Guard-duty adapter state disappeared.";return null;}var r=a.TickGuardDuty(s.Context);d=r==null?"Work adapter returned no result.":r.Detail;return r;}
    protected override void EndCore(State s,string reason){IRebirthNpcGuardDutyWorkAdapter a;if(adapters.TryGetValue(s.Context.Assignment.AssignmentId,out a)){a.CancelGuardDuty(s.Context,reason);adapters.Remove(s.Context.Assignment.AssignmentId);}}
}


public sealed class RebirthNpcMedicalWorkExecutor : RebirthNpcConcreteWorkExecutorBase
{
    private readonly Dictionary<ulong,IRebirthNpcMedicalWorkAdapter> adapters=new Dictionary<ulong,IRebirthNpcMedicalWorkAdapter>();
    public override string ExecutorId{get{return "rebirth.work.medical";}} public override int Priority{get{return 285;}} protected override RebirthNpcWorkKind Kind{get{return RebirthNpcWorkKind.Medical;}}
    protected override RebirthNpcWorkMutationResult BeginCore(State s,out string d){var a=RebirthNpcConcreteWorkAdapterRegistry.SelectMedical(s.Context);if(a==null){d="No medical adapter supports the patient.";return new RebirthNpcWorkMutationResult{Disposition=RebirthNpcWorkExecutionDisposition.Failed,FailureCategory=RebirthNpcWorkFailureCategory.InvalidTarget,Detail=d};}adapters[s.Context.Assignment.AssignmentId]=a;var r=a.BeginMedical(s.Context);d=r==null?"Medical adapter returned no result.":r.Detail;return r;}
    protected override RebirthNpcWorkMutationResult TickCore(State s,out string d){IRebirthNpcMedicalWorkAdapter a;if(!adapters.TryGetValue(s.Context.Assignment.AssignmentId,out a)){d="Medical adapter state disappeared.";return null;}var r=a.TickMedical(s.Context);d=r==null?"Work adapter returned no result.":r.Detail;return r;}
    protected override void EndCore(State s,string reason){IRebirthNpcMedicalWorkAdapter a;if(adapters.TryGetValue(s.Context.Assignment.AssignmentId,out a)){a.CancelMedical(s.Context,reason);adapters.Remove(s.Context.Assignment.AssignmentId);}}
}
