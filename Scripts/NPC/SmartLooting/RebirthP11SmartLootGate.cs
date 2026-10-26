using System;
using System.Collections.Generic;

// Qualification gates for B10/B11 beyond the named-container path. Every flag is
// a compile-time false; see _Documentation/NPC/SmartLooting/P11_B10_B11_20261022.md
// for the evidence Codex's batch must produce before any is enabled.
internal static class RebirthP11SmartLootGate
{
#if DEBUG
    // DEBUG builds only: read-only views over RebirthP11GateOverride (default OFF, never persisted, armed solely by the
    // guarded `rbp11 gates` console command on the CodexTest save). Release keeps the compile-time constants below.
    internal static bool GeneratedStorageQualified => RebirthP11GateOverride.GeneratedOn;
    internal static bool UnifiedOrderingQualified => RebirthP11GateOverride.UnifiedOn;
    internal static bool CorpseSourceQualified => RebirthP11GateOverride.CorpseOn;
    internal static bool NpcAttributedOpenQualified => RebirthP11GateOverride.NpcOpenOn;
    internal static bool FollowActivityQualified => RebirthP11GateOverride.FollowOn;
    internal static bool NpcLockpickQualified => RebirthP11GateOverride.LockpickOn;
#else
    // Chunk-metadata incarnation for generated/POI storage (RebirthP11StorageIncarnation).
    internal const bool GeneratedStorageQualified = false;
    // Player Take All + NPC loot + autoloot-zed share one cross-kind ordering.
    internal const bool UnifiedOrderingQualified = false;
    // Entity (corpse) loot containers as a Smart Loot source.
    internal const bool CorpseSourceQualified = false;
    // NPC-attributed first-touch loot generation. Blocked natively: LootManager.LootContainerOpened
    // performs castclass EntityPlayer on the opener id.
    internal const bool NpcAttributedOpenQualified = false;
    // P6 Follow-activity dispatcher calls RebirthP11FollowLootActivity.TryStep.
    internal const bool FollowActivityQualified = false;
    // Locked-container NPC lockpick action (consume real Lockpicks, LockPickTime/BreakChance).
    internal const bool NpcLockpickQualified = false;
#endif
}

internal enum RebirthP11LootSource : byte { NpcLoot, PlayerTakeAll, AutolootZed }
internal enum RebirthP11LootTarget : byte { Human = 0, Dog = 1, Player = 2 }

// Pure, allocation-light ordering for same-owner candidates. Matching partial stacks
// outrank empty cells; within a class humans precede dogs; ties break on the stable
// id ordinal, so every host computes the same sequence. The player is never a
// candidate: it is the vanilla remainder when this list is exhausted. Strangers and
// unloaded inventories are filtered by the caller before a candidate is constructed.
internal readonly struct RebirthP11LootCandidate
{
    internal readonly RebirthP11LootTarget Target;
    internal readonly string StableId;
    internal readonly bool MatchesStack;
    internal readonly int Slot;
    internal RebirthP11LootCandidate(RebirthP11LootTarget target, string stableId, bool matchesStack, int slot)
    { Target = target; StableId = stableId ?? string.Empty; MatchesStack = matchesStack; Slot = slot; }
}

internal sealed class RebirthP11LootCandidateComparer : IComparer<RebirthP11LootCandidate>
{
    internal static readonly RebirthP11LootCandidateComparer Instance = new RebirthP11LootCandidateComparer();
    public int Compare(RebirthP11LootCandidate a, RebirthP11LootCandidate b)
    {
        int c = b.MatchesStack.CompareTo(a.MatchesStack);
        if (c != 0) return c;
        c = ((byte)a.Target).CompareTo((byte)b.Target);
        if (c != 0) return c;
        c = string.CompareOrdinal(a.StableId, b.StableId);
        return c != 0 ? c : a.Slot.CompareTo(b.Slot);
    }
}
