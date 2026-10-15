using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;

// The vehicle coordinator owns time. The native window owns player grid edits;
// commit them before ticking and project the resulting station after ticking.
internal static class RebirthCamperStationOpenQueueProcessing
{
    private sealed class Window { internal XUiC_WorkstationWindowGroup Value; internal bool Failed; }
    private static readonly ConditionalWeakTable<RebirthCamperStationState,Window> windows
        =new ConditionalWeakTable<RebirthCamperStationState,Window>();
    private static readonly MethodInfo commit=AccessTools.Method(typeof(XUiC_WorkstationWindowGroup),"syncTEfromUI");
    internal static void Register(XUiC_WorkstationWindowGroup window)
    {
        if(!(window?.WorkstationData?.TileEntity is RebirthCamperStationState station))return;
        if(!RebirthCamperStationWindowSession.Admitted(window,station))return;
        var holder=windows.GetValue(station,_=>new Window());
        if(holder.Failed)throw new InvalidOperationException("Mobile presentation requires reconciliation.");
        holder.Value=window;
        try { RebirthCamperStationQueueProjection.Refresh(window,station); }
        catch { holder.Failed=true;station.lastTickTime=0;throw; }
    }
    internal static bool OwnsQueue(RebirthCamperStationState station)
        =>station!=null&&windows.TryGetValue(station,out var holder)&&
            ReferenceEquals(holder.Value?.WorkstationData?.TileEntity,station)&&
            RebirthCamperStationWindowSession.IsInUse(station);
    internal static bool CanCommit(RebirthCamperStationState station)
        =>station==null||!windows.TryGetValue(station,out var holder)||!holder.Failed;

    internal static IDisposable Begin(RebirthCamperStationState station)
    {
        if(!ThreadManager.IsMainThread())throw new InvalidOperationException("Mobile queue requires game thread.");
        if(!CanCommit(station))
        { station.lastTickTime=0;throw new InvalidOperationException("Mobile presentation requires reconciliation."); }
        if(RebirthCamperRemoteStationTransport.HasServerLease(station))return null;
        if(!RebirthCamperStationWindowSession.IsInUse(station))return null;
        if(!windows.TryGetValue(station,out var holder)||holder.Failed||commit==null||
            !RebirthCamperStationWindowSession.Admitted(holder.Value,station))
            throw new InvalidOperationException("Open mobile queue has no current presentation.");
        // Native sync also resets the clock. Preserve its single vehicle clock,
        // including if a native inventory callback interrupts the commit.
        ulong tick=station.lastTickTime;
        try { commit.Invoke(holder.Value,null); }
        finally { station.lastTickTime=tick; }
        // Grid callbacks may alter custody/access during the native commit.
        if(!RebirthCamperStationWindowSession.Admitted(holder.Value,station)||
            RebirthCamperStationSaveEvidence.IsPending(station)||
            RebirthCamperStationCompletionCallsite.IsHeld(station)||
            !RebirthCamperStationQueuedAuthority.CanProcessNativeQueue(station))
        { station.lastTickTime=0;throw new InvalidOperationException("Mobile queue authority changed during grid commit."); }
        return new Scope(station,holder.Value);
    }
    private sealed class Scope:IDisposable
    {
        private RebirthCamperStationState station;
        private readonly XUiC_WorkstationWindowGroup window;
        private readonly bool accessing;
        internal Scope(RebirthCamperStationState station,XUiC_WorkstationWindowGroup window)
        {
            this.station=station;this.window=window;accessing=station.bUserAccessing;
            // Do not call SetUserAccessing: the actual native/session lock remains
            // held. This flag only selects the native processing implementation.
            station.bUserAccessing=false;
        }
        public void Dispose()
        {
            var live=station;if(live==null)return;station=null;
            live.bUserAccessing=accessing;
            // Always publish partially completed native work, even on exception,
            // so stale UI outputs cannot later erase or duplicate that work.
            try
            {
                if(!RebirthCamperStationWindowSession.Admitted(window,live))
                    throw new InvalidOperationException("Mobile presentation lost its lease during processing.");
                RebirthCamperStationQueueProjection.Refresh(window,live);
            }
            catch
            {
                if(windows.TryGetValue(live,out var holder))holder.Failed=true;
                live.lastTickTime=0;throw;
            }
        }
    }
}
[HarmonyPatch(typeof(XUiC_WorkstationWindowGroup),nameof(XUiC_WorkstationWindowGroup.OnOpen))]
internal static class RebirthCamperStationOpenQueuePresentation
{
    private static void Postfix(XUiC_WorkstationWindowGroup __instance)
        =>RebirthCamperStationOpenQueueProcessing.Register(__instance);
}
[HarmonyPatch(typeof(XUiC_WorkstationWindowGroup),"syncTEfromUI")]
internal static class RebirthCamperStationOpenQueueSync
{
    private static bool Prefix(XUiC_WorkstationWindowGroup __instance,out ulong? __state)
    {
        var station=__instance?.WorkstationData?.TileEntity as RebirthCamperStationState;
        __state=station?.lastTickTime;
        // Remote grids are acknowledged one position at a time. Native window
        // synchronization must never overwrite the detached server presentation.
        return station?.BoundVehicle?.world?.IsRemote()==true?false:
            !RebirthCamperStationGridCustody.IsHeld(station)&&RebirthCamperStationOpenQueueProcessing.CanCommit(station);
    }
    private static Exception Finalizer(XUiC_WorkstationWindowGroup __instance,ulong? __state,Exception __exception)
    {
        if(__state.HasValue&&__instance?.WorkstationData?.TileEntity is RebirthCamperStationState station)
            station.lastTickTime=__state.Value;
        return __exception;
    }
}
