using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using UnityEngine;

#nullable disable

/// <summary>
/// Dormant scheduler foundation. No production subsystem is migrated onto this scaffold by RP73.
/// Registrations are delivered only for cadences/phases with an implemented pump. Mutations made
/// during a callback are committed at the next stable pump boundary.
/// </summary>
public static class RebirthFrameScheduler
{
    public struct TickHandle
    {
        internal int Id;
        public bool IsValid { get { return Id > 0; } }
    }

    private sealed class TickEntry
    {
        public int Id;
        public string ModuleId;
        public RebirthEnginePhase Phase;
        public RebirthTickCadenceKind Cadence;
        public float IntervalSeconds;
        public int BudgetMicros; // advisory metadata only; no enforcement is claimed here.
        public Action Callback;
        public bool Active;
        public float NextRunRealtime;
        public int ConsecutiveExceptions;
    }

    private const int MaxConsecutiveExceptions = 8;
    private static readonly List<TickEntry> s_entries = new List<TickEntry>(64);
    private static readonly List<TickEntry> s_pendingAdds = new List<TickEntry>(16);
    private static int s_nextId = 1;
    private static bool s_modEventRegistered;
    private static bool s_pumping;
    private static int s_ownerThreadId;

    private static bool IsPhasePumped(RebirthEnginePhase phase)
    {
        return phase == RebirthEnginePhase.GameUpdateEvent;
    }

    private static bool IsCadenceDelivered(RebirthTickCadenceKind cadence)
    {
        return cadence == RebirthTickCadenceKind.EveryFrame || cadence == RebirthTickCadenceKind.Interval;
    }

    public static TickHandle Register(string moduleId, RebirthEnginePhase phase, RebirthTickCadenceKind cadence, float intervalSeconds, int budgetMicros, Action callback)
    {
        if (callback == null) return default(TickHandle);
        if (!IsPhasePumped(phase))
        {
            Log.Error("[RebirthFrameScheduler] Register refused: phase " + phase + " has no delivery pump (module=" + moduleId + ").");
            return default(TickHandle);
        }
        if (!IsCadenceDelivered(cadence))
        {
            Log.Error("[RebirthFrameScheduler] Register refused: cadence " + cadence + " has no delivery route (module=" + moduleId + ").");
            return default(TickHandle);
        }
        if (cadence == RebirthTickCadenceKind.Interval && (float.IsNaN(intervalSeconds) || float.IsInfinity(intervalSeconds) || intervalSeconds <= 0f))
        {
            Log.Error("[RebirthFrameScheduler] Register refused: Interval requires a finite intervalSeconds > 0 (module=" + moduleId + ").");
            return default(TickHandle);
        }
        if (budgetMicros < 0)
        {
            Log.Error("[RebirthFrameScheduler] Register refused: budgetMicros cannot be negative (module=" + moduleId + "). Budget is advisory in this scaffold.");
            return default(TickHandle);
        }
        if (!EnsureModEventRegisteredForPhase(phase))
            return default(TickHandle);

        int currentThread = Thread.CurrentThread.ManagedThreadId;
        if (s_ownerThreadId == 0) s_ownerThreadId = currentThread;
        if (currentThread != s_ownerThreadId)
        {
            Log.Error("[RebirthFrameScheduler] Register refused from non-owner thread (module=" + moduleId + ").");
            return default(TickHandle);
        }

        TickEntry entry = new TickEntry
        {
            Id = s_nextId++,
            ModuleId = string.IsNullOrEmpty(moduleId) ? "<unknown>" : moduleId,
            Phase = phase,
            Cadence = cadence,
            IntervalSeconds = intervalSeconds,
            BudgetMicros = budgetMicros,
            Callback = callback,
            Active = true,
            NextRunRealtime = SafeRealtime() + (cadence == RebirthTickCadenceKind.Interval ? intervalSeconds : 0f)
        };
        if (s_pumping) s_pendingAdds.Add(entry); else s_entries.Add(entry);
        return new TickHandle { Id = entry.Id };
    }

    public static void Unregister(TickHandle handle)
    {
        if (!handle.IsValid) return;
        MarkInactive(s_entries, handle.Id);
        MarkInactive(s_pendingAdds, handle.Id);
        if (!s_pumping) ApplyBoundaryMutations();
    }

    private static void MarkInactive(List<TickEntry> list, int id)
    {
        for (int i=0;i<list.Count;i++)
        {
            TickEntry e=list[i];
            if (e!=null && e.Id==id) { e.Active=false; e.Callback=null; return; }
        }
    }

    private static void ApplyBoundaryMutations()
    {
        s_entries.RemoveAll(e => e == null || !e.Active || e.Callback == null);
        if (s_pendingAdds.Count > 0)
        {
            for (int i=0;i<s_pendingAdds.Count;i++)
            {
                TickEntry e=s_pendingAdds[i];
                if (e!=null && e.Active && e.Callback!=null) s_entries.Add(e);
            }
            s_pendingAdds.Clear();
        }
    }

    public static string GetStatus()
    {
        int active=0, inactive=0;
        for (int i=0;i<s_entries.Count;i++) { TickEntry e=s_entries[i]; if(e==null||!e.Active) inactive++; else active++; }
        return "[RebirthFrameScheduler] activeTicks="+active+" inactiveTicks="+inactive+" pendingAdds="+s_pendingAdds.Count+" modEventRegistered="+s_modEventRegistered+" delivery=EveryFrame|Interval budget=enforcementNotImplemented";
    }

    public static void TickPhase(RebirthEnginePhase phase)
    {
        if (!IsPhasePumped(phase)) return;
        int currentThread=Thread.CurrentThread.ManagedThreadId;
        if (s_ownerThreadId==0) s_ownerThreadId=currentThread;
        if (currentThread!=s_ownerThreadId)
        {
            Log.Error("[RebirthFrameScheduler] TickPhase refused on non-owner thread phase="+phase+".");
            return;
        }

        ApplyBoundaryMutations();
        float now=SafeRealtime();
        s_pumping=true;
        int stableCount=s_entries.Count; // additions during callbacks begin next pump.
        try
        {
            for (int i=0;i<stableCount;i++)
            {
                TickEntry entry=s_entries[i];
                if(entry==null||!entry.Active||entry.Callback==null||entry.Phase!=phase) continue;
                if(entry.Cadence==RebirthTickCadenceKind.Interval && now<entry.NextRunRealtime) continue;
                long sampleStart=0L; bool sampled=RebirthModuleCostStats.ShouldSampleThisFrame();
                if(sampled) sampleStart=RebirthModuleCostStats.BeginSample();
                try { entry.Callback(); entry.ConsecutiveExceptions=0; }
                catch(Exception ex)
                {
                    entry.ConsecutiveExceptions++;
                    if(entry.ConsecutiveExceptions>=MaxConsecutiveExceptions)
                    {
                        entry.Active=false; entry.Callback=null;
                        Log.Error("[RebirthFrameScheduler] module="+entry.ModuleId+" phase="+entry.Phase+" DISABLED after "+entry.ConsecutiveExceptions+" consecutive exceptions. Last: "+ex.GetType().Name+": "+ex.Message);
                    }
                    else Log.Warning("[RebirthFrameScheduler] module="+entry.ModuleId+" phase="+entry.Phase+" exception="+ex.GetType().Name+": "+ex.Message);
                }
                if(sampled) RebirthModuleCostStats.EndSample(entry.ModuleId,sampleStart,0);
                if(entry.Active && entry.Cadence==RebirthTickCadenceKind.Interval) entry.NextRunRealtime=now+entry.IntervalSeconds;
            }
        }
        finally
        {
            s_pumping=false;
            ApplyBoundaryMutations();
        }
    }

    public static void OnGameUpdate(ref ModEvents.SGameUpdateData _data) { TickPhase(RebirthEnginePhase.GameUpdateEvent); }

    public static void ResetForLifecycle()
    {
        s_entries.Clear(); s_pendingAdds.Clear(); s_pumping=false; s_nextId=1;
        // Handler registration belongs to ModEvents for the process lifetime; entries are world/session scoped.
    }

    private static bool EnsureModEventRegisteredForPhase(RebirthEnginePhase phase)
    {
        if (phase != RebirthEnginePhase.GameUpdateEvent) return false;
        if (s_modEventRegistered) return true;
        try { ModEvents.GameUpdate.RegisterHandler(OnGameUpdate); s_modEventRegistered=true; return true; }
        catch(Exception ex)
        {
            Log.Error("[RebirthFrameScheduler] failed to register ModEvents.GameUpdate; registration rejected: "+ex.GetType().Name+": "+ex.Message);
            return false;
        }
    }

    private static float SafeRealtime()
    {
        try { float value=Time.realtimeSinceStartup; return float.IsNaN(value)||float.IsInfinity(value)?0f:value; }
        catch { return 0f; }
    }
}
