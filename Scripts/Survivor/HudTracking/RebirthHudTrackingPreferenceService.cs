using Platform;
using System;
using System.Collections.Generic;
using UnityEngine;

#nullable disable

/// <summary>
/// Client-local preference context. Stable access compares lifecycle identities without SHA/path
/// derivation. World/player/platform changes and explicit session reset invalidate immediately.
/// Failed early resolution retries at most once per second until the cheap identity changes.
/// </summary>
public static class RebirthHudTrackingPreferenceService
{
    private static readonly object Sync = new object();
    private static RebirthHudTrackingPreferences current;
    private static RebirthHudTrackingPreferenceContext context;
    private static object contextWorld, contextPlayer, contextPlatform;
    private static string debugKey = string.Empty;
    private static bool contextProbed, loaded, writable, dirty;
    private static long contextEpoch, mutationRevision;
    private static float nextResolveRetry, nextPendingWriteRetry;
    private static DateTime dirtyAtUtc;
    private static string lastError = string.Empty;
    private static readonly TimeSpan SaveDebounce = TimeSpan.FromMilliseconds(500);
    private sealed class PendingWrite { public RebirthHudTrackingPreferenceContext Context; public RebirthHudTrackingPreferences Preferences; public long Revision; }
    private static readonly List<PendingWrite> pendingPreviousWrites = new List<PendingWrite>();
    public static event Action PreferencesChanged;

    public static long ContextEpoch { get { EnsureCurrentContext(); lock (Sync) return contextEpoch; } }
    public static long Revision { get { EnsureCurrentContext(); lock (Sync) return mutationRevision; } }
    public static RebirthHudTrackingPreferences GetCurrent()
    {
        EnsureCurrentContext();
        lock (Sync) return current != null ? current.Clone() : new RebirthHudTrackingPreferences();
    }
    public static bool TryGetCurrent(out RebirthHudTrackingPreferences preferences, out string error)
    {
        EnsureCurrentContext();
        lock (Sync)
        {
            preferences = current != null ? current.Clone() : new RebirthHudTrackingPreferences();
            error = lastError ?? string.Empty;
            return loaded && writable;
        }
    }
    public static string GetCurrentDebugKey() { EnsureCurrentContext(); lock (Sync) return debugKey; }

    public static bool Mutate(Action<RebirthHudTrackingPreferences> mutation, out string error)
    {
        error = string.Empty;
        if (mutation == null) { error = "mutation is null"; return false; }
        EnsureCurrentContext();
        Action changed;
        lock (Sync)
        {
            if (!loaded || !writable)
            {
                error = string.IsNullOrEmpty(lastError) ? "HUD tracking preferences are not writable in the current context" : lastError;
                return false;
            }
            if (current == null) current = new RebirthHudTrackingPreferences();
            mutation(current); current.NormalizeOrder();
            dirty = true; dirtyAtUtc = DateTime.UtcNow;
            unchecked { mutationRevision++; }
            changed = PreferencesChanged;
        }
        if (changed != null) changed();
        return true;
    }
    public static bool FlushPendingIfDue(out string error)
    {
        error = string.Empty;
        // No pending write: no identity derivation, clone, path calculation or IO. Readers
        // still probe real context transitions; lifecycle handlers flush the old exact path.
        lock (Sync) RetryPendingWritesLocked();
        lock (Sync) if (!dirty || current == null || DateTime.UtcNow - dirtyAtUtc < SaveDebounce) return true;
        EnsureCurrentContext();
        return SaveCurrent(out error);
    }
    public static bool SaveNow(out string error)
    {
        error = string.Empty;
        lock (Sync) if (!dirty) return true;
        EnsureCurrentContext();
        return SaveCurrent(out error);
    }
    private static bool SaveCurrent(out string error)
    {
        RebirthHudTrackingPreferenceContext target;
        RebirthHudTrackingPreferences snapshot;
        long savedRevision;
        lock (Sync)
        {
            error = string.Empty;
            if (!dirty) return true;
            if (!loaded || !writable || context == null) { error = lastError; return false; }
            snapshot = current.Clone(); target = context; savedRevision = mutationRevision;
        }
        bool ok = RebirthHudTrackingPreferenceStore.TrySave(target, snapshot, out error);
        lock (Sync)
        {
            // A mutation made while the detached snapshot was being written remains dirty.
            if (ReferenceEquals(context, target))
            {
                if (ok)
                {
                    // A successful current snapshot supersedes retained writes for this exact scope.
                    for (int i = pendingPreviousWrites.Count - 1; i >= 0; i--)
                        if (string.Equals(pendingPreviousWrites[i].Context.DebugKey, target.DebugKey, StringComparison.Ordinal))
                            pendingPreviousWrites.RemoveAt(i);
                    if (savedRevision == mutationRevision) dirty = false;
                }
                lastError = ok ? string.Empty : (error ?? string.Empty);
            }
        }
        return ok;
    }
    private static void FlushOldContextLocked()
    {
        if (!dirty || !loaded || !writable || context == null || current == null) return;
        RebirthHudTrackingPreferences snapshot=current.Clone();
        long revision=mutationRevision;
        string error;
        if (RebirthHudTrackingPreferenceStore.TrySave(context, snapshot, out error)) return;
        bool replaced=false;
        for(int i=0;i<pendingPreviousWrites.Count;i++)
        {
            if(string.Equals(pendingPreviousWrites[i].Context.DebugKey,context.DebugKey,StringComparison.Ordinal))
            { pendingPreviousWrites[i]=new PendingWrite{Context=context,Preferences=snapshot,Revision=revision};replaced=true;break; }
        }
        if(!replaced)pendingPreviousWrites.Add(new PendingWrite{Context=context,Preferences=snapshot,Revision=revision});
        Log.Warning("[REBIRTH HUD tracking] previous context preferences could not be saved; detached retry retained: " + error);
    }

    private static void RetryPendingWritesLocked()
    {
        if (pendingPreviousWrites.Count == 0) return;
        float now = Time.realtimeSinceStartup;
        if (now < nextPendingWriteRetry) return;
        nextPendingWriteRetry = now + 1f;
        for(int i=pendingPreviousWrites.Count-1;i>=0;i--)
        {
            PendingWrite pending=pendingPreviousWrites[i];
            if(pending==null||pending.Context==null||pending.Preferences==null){pendingPreviousWrites.RemoveAt(i);continue;}
            string error;
            RebirthHudTrackingPreferences disk;
            bool recoveredBackup;
            // Re-admit the file before detached writes: an external newer schema or unreadable
            // replacement must remain protected while an older snapshot awaits retry.
            if (!RebirthHudTrackingPreferenceStore.TryLoad(pending.Context, out disk, out recoveredBackup, out error)) continue;
            if(RebirthHudTrackingPreferenceStore.TrySave(pending.Context,pending.Preferences,out error))pendingPreviousWrites.RemoveAt(i);
        }
    }
    public static void ResetRuntime(bool flush)
    {
        lock (Sync)
        {
            if (flush) FlushOldContextLocked();
            current = null; context = null; contextWorld = contextPlayer = contextPlatform = null;
            contextProbed = loaded = writable = dirty = false;
            debugKey = lastError = string.Empty; nextResolveRetry = 0f; dirtyAtUtc = DateTime.MinValue;
            unchecked { contextEpoch++; mutationRevision++; }
        }
    }
    private static void EnsureCurrentContext()
    {
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        EntityPlayerLocal player = world != null ? world.GetPrimaryPlayer() : null;
        object platform = PlatformManager.InternalLocalUserIdentifier;
        lock (Sync)
        {
            RetryPendingWritesLocked();
            bool same = contextProbed && ReferenceEquals(contextWorld, world) && ReferenceEquals(contextPlayer, player)
                && object.Equals(contextPlatform, platform);
            if (same && (loaded || Time.realtimeSinceStartup < nextResolveRetry)) return;
            if (!same)
            {
                FlushOldContextLocked();
                current = null; context = null; loaded = writable = dirty = false;
                debugKey = string.Empty;
                contextWorld = world; contextPlayer = player; contextPlatform = platform; contextProbed = true;
                unchecked { contextEpoch++; mutationRevision++; }
            }
            RebirthHudTrackingPreferenceContext resolved;
            string error;
            // A vanished owner must not leave a previous owner's preferences writable.
            if (world == null || player == null || platform == null)
            {
                current = new RebirthHudTrackingPreferences(); writable = false;
                lastError = "world/local player identity is unavailable"; nextResolveRetry = Time.realtimeSinceStartup + 1f;
                return;
            }
            if (!RebirthHudTrackingPreferenceStore.TryResolveCurrentContext(out resolved, out error))
            {
                current = new RebirthHudTrackingPreferences(); writable = false;
                lastError = error ?? string.Empty; nextResolveRetry = Time.realtimeSinceStartup + 1f;
                return;
            }
            RebirthHudTrackingPreferences preferences;
            bool recoveredBackup;
            bool ok = RebirthHudTrackingPreferenceStore.TryLoad(resolved, out preferences, out recoveredBackup, out error);
            context = resolved; debugKey = resolved.DebugKey;
            current = ok && preferences != null ? preferences : new RebirthHudTrackingPreferences();
            loaded = true; writable = ok; dirty = false; dirtyAtUtc = DateTime.MinValue;
            // Failed departure preferences are newer than disk. Restore only their exact scope.
            // Keep protected/unreadable disk state non-writable; never bypass load refusal.
            if (ok)
            {
                for (int i = pendingPreviousWrites.Count - 1; i >= 0; i--)
                {
                    PendingWrite pending = pendingPreviousWrites[i];
                    if (!string.Equals(pending.Context.DebugKey, resolved.DebugKey, StringComparison.Ordinal)) continue;
                    current = pending.Preferences.Clone();
                    dirty = true; dirtyAtUtc = DateTime.UtcNow;
                    pendingPreviousWrites.RemoveAt(i);
                    break;
                }
            }
            lastError = ok ? string.Empty : (error ?? string.Empty);
            unchecked { contextEpoch++; mutationRevision++; }
        }
    }
}
