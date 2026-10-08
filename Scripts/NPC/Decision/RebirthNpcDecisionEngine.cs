
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

#nullable disable

public enum RebirthNpcTaskState : byte { Pending, Running, Suspended, Completed, Failed, Cancelled }
public enum RebirthNpcInterruptClass : byte { None, Routine, Threat, Combat, Emergency }

public sealed class RebirthNpcDecisionContext
{
    public RebirthNpcStableId NpcId;
    public long UtcTicks;
    public readonly Dictionary<string, float> Signals = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
}

public interface IRebirthNpcGoalProvider
{
    string GoalId { get; }
    float Score(RebirthNpcDecisionContext context);
    IRebirthNpcTask CreateTask(RebirthNpcDecisionContext context);
}

public interface IRebirthNpcTask
{
    string TaskId { get; }
    RebirthNpcInterruptClass InterruptClass { get; }
    RebirthNpcTaskState State { get; }
    bool Tick(RebirthNpcDecisionContext context, out string detail);
    void Cancel(string reason);
}

public sealed class RebirthNpcDelegateTask : IRebirthNpcTask
{
    private readonly Func<RebirthNpcDecisionContext, bool> step;
    private RebirthNpcTaskState state;
    public string TaskId { get; private set; }
    public RebirthNpcInterruptClass InterruptClass { get; private set; }
    public RebirthNpcTaskState State { get { return state; } }

    public RebirthNpcDelegateTask(string id, RebirthNpcInterruptClass interruptClass,
        Func<RebirthNpcDecisionContext, bool> step)
    {
        TaskId = string.IsNullOrEmpty(id) ? "unnamed" : id;
        InterruptClass = interruptClass;
        this.step = step;
        state = RebirthNpcTaskState.Pending;
    }

    public bool Tick(RebirthNpcDecisionContext context, out string detail)
    {
        detail = string.Empty;
        if (state == RebirthNpcTaskState.Completed || state == RebirthNpcTaskState.Failed ||
            state == RebirthNpcTaskState.Cancelled) return true;
        state = RebirthNpcTaskState.Running;
        try
        {
            bool complete = step == null || step(context);
            if (complete) state = RebirthNpcTaskState.Completed;
            return complete;
        }
        catch (Exception ex)
        {
            state = RebirthNpcTaskState.Failed;
            detail = ex.GetType().Name + ": " + ex.Message;
            return true;
        }
    }

    public void Cancel(string reason) { state = RebirthNpcTaskState.Cancelled; }
}

public static class RebirthNpcDecisionEngine
{
    private sealed class Runtime
    {
        public IRebirthNpcTask Active;
        public string GoalId = string.Empty;
        public float LastScore;
        public long NextEvaluation;
        public long CooldownUntil;
        public readonly Queue<string> RecentGoals = new Queue<string>();
    }

    private static readonly object Sync = new object();
    private static readonly Dictionary<string, IRebirthNpcGoalProvider> Providers =
        new Dictionary<string, IRebirthNpcGoalProvider>(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<RebirthNpcStableId, Runtime> States =
        new Dictionary<RebirthNpcStableId, Runtime>();
    private static long evaluations, selections, taskTicks, completions, failures, interruptions, budgetDeferrals;
    private const int MaxPerTick = 24;
    private const int MaxDiscoveryPerTick = 96;
    private static readonly long EvaluationInterval = TimeSpan.FromSeconds(2).Ticks;
    private static IRebirthNpcGoalProvider[] providerSnapshot = new IRebirthNpcGoalProvider[0];
    private static RebirthNpcStableId[] npcSnapshot = new RebirthNpcStableId[0];
    private static int npcSnapshotRevision = -1;
    private static int decisionCursor;

    public static void Register(IRebirthNpcGoalProvider provider)
    {
        if (provider == null || string.IsNullOrEmpty(provider.GoalId)) return;
        lock (Sync)
        {
            Providers[provider.GoalId] = provider;
            IRebirthNpcGoalProvider[] snapshot = new IRebirthNpcGoalProvider[Providers.Count];
            Providers.Values.CopyTo(snapshot, 0);
            Array.Sort(snapshot, delegate(IRebirthNpcGoalProvider left, IRebirthNpcGoalProvider right)
            {
                return string.CompareOrdinal(left.GoalId, right.GoalId);
            });
            providerSnapshot = snapshot;
        }
    }

    public static bool SubmitSignal(RebirthNpcStableId npcId, string signal, float value)
    {
        if (npcId.IsEmpty || string.IsNullOrEmpty(signal)) return false;
        RebirthNpcDecisionSignalStore.Set(npcId, signal, value);
        return true;
    }

    public static bool Interrupt(RebirthNpcStableId npcId, RebirthNpcInterruptClass severity, string reason)
    {
        lock (Sync)
        {
            Runtime r;
            if (!States.TryGetValue(npcId, out r) || r.Active == null) return false;
            if (severity < r.Active.InterruptClass) return false;
            r.Active.Cancel(reason ?? "interrupted");
            r.Active = null;
            r.GoalId = string.Empty;
            r.NextEvaluation = 0;
            Interlocked.Increment(ref interruptions);
            return true;
        }
    }

    public static void Tick()
    {
        if (ConnectionManager.Instance != null && !ConnectionManager.Instance.IsServer) return;

        int revision;
        RebirthNpcStableId[] latest = RebirthNpcDecisionSignalStore.GetKnownNpcSnapshot(out revision);
        if (revision != npcSnapshotRevision)
        {
            npcSnapshot = latest;
            npcSnapshotRevision = revision;
            if (decisionCursor >= npcSnapshot.Length) decisionCursor = 0;
        }
        if (npcSnapshot.Length == 0) return;

        int worked = 0;
        int discovered = 0;
        long now = DateTime.UtcNow.Ticks;
        int available = npcSnapshot.Length;
        int discoveryLimit = Math.Min(MaxDiscoveryPerTick, available);
        while (discovered < discoveryLimit && worked < MaxPerTick)
        {
            if (decisionCursor >= available) decisionCursor = 0;
            RebirthNpcStableId id = npcSnapshot[decisionCursor++];
            discovered++;
            if (TickOne(id, now)) worked++;
        }

        if (worked >= MaxPerTick && discovered < available)
            Interlocked.Increment(ref budgetDeferrals);
    }

    private static bool TickOne(RebirthNpcStableId id, long now)
    {
        Runtime runtime;
        lock (Sync)
        {
            if (!States.TryGetValue(id, out runtime)) States[id] = runtime = new Runtime();
        }

        bool hasActive = runtime.Active != null;
        if (!hasActive && (now < runtime.NextEvaluation || now < runtime.CooldownUntil)) return false;

        RebirthNpcDecisionContext context = new RebirthNpcDecisionContext { NpcId = id, UtcTicks = now };
        RebirthNpcDecisionSignalStore.CopyTo(id, context.Signals);

        if (runtime.Active != null)
        {
            string detail;
            bool terminal;
            try
            {
                terminal = runtime.Active.Tick(context, out detail);
            }
            catch (Exception ex)
            {
                terminal = true;
                detail = ex.GetType().Name + ": " + ex.Message;
                Interlocked.Increment(ref failures);
            }
            Interlocked.Increment(ref taskTicks);
            if (terminal)
            {
                if (runtime.Active != null && runtime.Active.State == RebirthNpcTaskState.Completed)
                    Interlocked.Increment(ref completions);
                else if (runtime.Active != null && runtime.Active.State == RebirthNpcTaskState.Failed)
                    Interlocked.Increment(ref failures);
                runtime.Active = null;
                runtime.CooldownUntil = now + TimeSpan.FromMilliseconds(500).Ticks;
            }
            return true;
        }

        runtime.NextEvaluation = now + EvaluationInterval;
        IRebirthNpcGoalProvider[] providers = providerSnapshot;
        IRebirthNpcGoalProvider best = null;
        float bestScore = float.MinValue;
        for (int i = 0; i < providers.Length; i++)
        {
            IRebirthNpcGoalProvider provider = providers[i];
            float score;
            try { score = provider.Score(context); } catch { continue; }
            if (float.IsNaN(score) || float.IsInfinity(score)) continue;
            if (score > bestScore || (score == bestScore && best != null &&
                string.CompareOrdinal(provider.GoalId, best.GoalId) < 0))
            {
                best = provider;
                bestScore = score;
            }
        }
        Interlocked.Increment(ref evaluations);
        if (best == null || bestScore <= 0f) return true;

        IRebirthNpcTask task;
        try { task = best.CreateTask(context); } catch { Interlocked.Increment(ref failures); return true; }
        if (task == null) return true;
        runtime.Active = task;
        runtime.GoalId = best.GoalId;
        runtime.LastScore = bestScore;
        runtime.RecentGoals.Enqueue(best.GoalId);
        while (runtime.RecentGoals.Count > 16) runtime.RecentGoals.Dequeue();
        Interlocked.Increment(ref selections);
        return true;
    }

    public static void ReleaseNpc(RebirthNpcStableId npcId)
    {
        lock (Sync) States.Remove(npcId);
        RebirthNpcDecisionSignalStore.Remove(npcId);
    }

    public static void ResetForWorldChange()
    {
        lock (Sync) States.Clear();
        RebirthNpcDecisionSignalStore.ResetForWorldChange();
        npcSnapshot = new RebirthNpcStableId[0];
        npcSnapshotRevision = -1;
        decisionCursor = 0;
    }

    public static string GetReport()
    {
        int providers, states, active = 0;
        lock (Sync)
        {
            providers = Providers.Count; states = States.Count;
            foreach (Runtime r in States.Values) if (r.Active != null) active++;
        }
        return new StringBuilder("[REBIRTH NPC Decision]\n")
            .Append("providers=").Append(providers).Append(" npcStates=").Append(states)
            .Append(" activeTasks=").Append(active).Append(" evaluations=").Append(evaluations)
            .Append(" selections=").Append(selections).Append(" taskTicks=").Append(taskTicks)
            .Append(" completed=").Append(completions).Append(" failed=").Append(failures)
            .Append(" interrupted=").Append(interruptions).Append(" budgetDeferrals=").Append(budgetDeferrals)
            .ToString();
    }
}

public static class RebirthNpcDecisionSignalStore
{
    private static readonly object Sync = new object();
    private static readonly Dictionary<RebirthNpcStableId, Dictionary<string,float>> Signals =
        new Dictionary<RebirthNpcStableId, Dictionary<string,float>>();
    private static RebirthNpcStableId[] KnownSnapshot = new RebirthNpcStableId[0];
    private static int membershipRevision;
    private static int snapshotRevision = -1;
    public static void Set(RebirthNpcStableId id, string key, float value)
    {
        lock (Sync)
        {
            Dictionary<string,float> map;
            if (!Signals.TryGetValue(id, out map))
            {
                Signals[id] = map = new Dictionary<string,float>(StringComparer.OrdinalIgnoreCase);
                membershipRevision++;
            }
            map[key] = value;
        }
    }
    public static void CopyTo(RebirthNpcStableId id, Dictionary<string,float> destination)
    {
        lock (Sync)
        {
            Dictionary<string,float> map;
            if (!Signals.TryGetValue(id, out map)) return;
            foreach (KeyValuePair<string,float> pair in map) destination[pair.Key] = pair.Value;
        }
    }
    public static RebirthNpcStableId[] GetKnownNpcIds()
    {
        int ignored;
        return GetKnownNpcSnapshot(out ignored);
    }

    public static RebirthNpcStableId[] GetKnownNpcSnapshot(out int revision)
    {
        lock (Sync)
        {
            if (snapshotRevision != membershipRevision)
            {
                RebirthNpcStableId[] ids = new RebirthNpcStableId[Signals.Count];
                Signals.Keys.CopyTo(ids, 0);
                Array.Sort(ids, delegate(RebirthNpcStableId left, RebirthNpcStableId right)
                {
                    int high = left.High.CompareTo(right.High);
                    return high != 0 ? high : left.Low.CompareTo(right.Low);
                });
                KnownSnapshot = ids;
                snapshotRevision = membershipRevision;
            }
            revision = membershipRevision;
            return KnownSnapshot;
        }
    }

    public static void Remove(RebirthNpcStableId id)
    {
        lock (Sync)
        {
            if (Signals.Remove(id)) membershipRevision++;
        }
    }

    public static void ResetForWorldChange()
    {
        lock (Sync)
        {
            Signals.Clear();
            KnownSnapshot = new RebirthNpcStableId[0];
            membershipRevision++;
            snapshotRevision = membershipRevision;
        }
    }

    public static float EvaluateProgressionCadence(RebirthNpcStableId npcId,float baseSeconds)
    { return RebirthNpcProgressionAiIntegration.EvaluateDecisionCadenceSeconds(npcId,baseSeconds); }
}
