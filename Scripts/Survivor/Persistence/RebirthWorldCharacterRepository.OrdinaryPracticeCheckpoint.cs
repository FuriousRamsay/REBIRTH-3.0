using System;
using System.Collections.Generic;
using UnityEngine;

public static partial class RebirthWorldCharacterRepository
{
    private const int MaximumOrdinaryPracticeCheckpoints = 256;
    private const double OrdinaryPracticeCheckpointSeconds = RebirthOrdinarySkillPractice.CheckpointSeconds;
    private const double OrdinaryPracticeCheckpointRetrySeconds = 5.0;

    private sealed class OrdinaryPracticeCheckpoint
    {
        internal readonly RebirthWorldCharacterRecord Record;
        internal double NextAttemptAt;

        internal OrdinaryPracticeCheckpoint(RebirthWorldCharacterRecord record, double dueAt)
        {
            Record = record;
            NextAttemptAt = dueAt;
        }
    }

    // This list is cleared only at the repository's positive epoch reset.
    // Record identity also binds the immutable origin and creation.
    private static readonly List<OrdinaryPracticeCheckpoint> OrdinaryPracticeCheckpoints = new List<OrdinaryPracticeCheckpoint>();
    private static double nextOrdinaryPracticeAttemptAt = double.PositiveInfinity;

    internal static bool TryScheduleOrdinaryPracticeCheckpoint(RebirthWorldCharacterRecord record, bool retry = false)
    {
        if (!ThreadManager.IsMainThread()) return false;
        lock (Sync)
        {
            if (!serverAuthority || !ScopeCurrentLocked() ||
                !Cache.TryGetValue(record.StablePlayerKey, out var current) || !ReferenceEquals(current, record))
                return false;
            foreach (var ticket in OrdinaryPracticeCheckpoints)
                if (ReferenceEquals(ticket.Record, record)) return true;
            if (OrdinaryPracticeCheckpoints.Count >= MaximumOrdinaryPracticeCheckpoints) return false;

            double dueAt = Time.realtimeSinceStartupAsDouble + (retry ? OrdinaryPracticeCheckpointRetrySeconds : OrdinaryPracticeCheckpointSeconds);
            OrdinaryPracticeCheckpoints.Add(new OrdinaryPracticeCheckpoint(record, dueAt));
            nextOrdinaryPracticeAttemptAt = Math.Min(nextOrdinaryPracticeAttemptAt, dueAt);
            return true;
        }
    }

    internal static void TickOrdinaryPracticeCheckpoints()
    {
        if (double.IsPositiveInfinity(nextOrdinaryPracticeAttemptAt)) return;
        double now = Time.realtimeSinceStartupAsDouble;
        if (now < nextOrdinaryPracticeAttemptAt || !ThreadManager.IsMainThread()) return;

        OrdinaryPracticeCheckpoint due = null;
        RepositoryWriteSelection selected = null;
        try
        {
            lock (Sync)
            {
                if (!serverAuthority || !ScopeCurrentLocked())
                {
                    // A temporarily unavailable original world is not retirement.
                    nextOrdinaryPracticeAttemptAt = now + OrdinaryPracticeCheckpointRetrySeconds;
                    return;
                }
                nextOrdinaryPracticeAttemptAt = double.PositiveInfinity;
                for (int i = 0; i < OrdinaryPracticeCheckpoints.Count;)
                {
                    var ticket = OrdinaryPracticeCheckpoints[i];
                    var record = ticket.Record;
                    if (!record.Dirty || !Cache.TryGetValue(record.StablePlayerKey, out var current) || !ReferenceEquals(current, record))
                    {
                        OrdinaryPracticeCheckpoints.RemoveAt(i);
                        continue;
                    }
                    if (ticket.NextAttemptAt < nextOrdinaryPracticeAttemptAt)
                    {
                        nextOrdinaryPracticeAttemptAt = ticket.NextAttemptAt;
                        due = ticket;
                    }
                    i++;
                }
                if (due == null || now < due.NextAttemptAt) return;
                // The oldest due exact record owns this frame's single write. A
                // refusal moves only that attempt behind other already-due records.
                due.NextAttemptAt = now + OrdinaryPracticeCheckpointRetrySeconds;
                selected = CaptureWriteSelectionLocked(due.Record.StablePlayerKey, due.Record);
            }
            if (SaveSnapshot(selected, "ordinary-practice-checkpoint"))
                lock (Sync) OrdinaryPracticeCheckpoints.Remove(due);
        }
        catch (Exception ex)
        {
            if (due != null) AddIssue(due.Record.StablePlayerKey, selected?.Path, "Ordinary practice checkpoint retained: " + ex.Message, false);
        }
    }

    private static void ClearOrdinaryPracticeCheckpointsLocked()
    {
        OrdinaryPracticeCheckpoints.Clear();
        nextOrdinaryPracticeAttemptAt = double.PositiveInfinity;
    }
}
