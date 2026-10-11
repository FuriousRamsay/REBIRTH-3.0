using System;using System.Collections.Generic;using UnityEngine;
public sealed class RebirthBossEventSpawnPlan
{
    public int BossClass;
    public readonly List<int> Supports = new List<int>(2);
    public readonly List<int> Regulars = new List<int>();
    public readonly List<Vector3> Positions = new List<Vector3>();
    public Vector3 SpawnOrigin;
}

public static class RebirthBossEventCompositionResolver
{
    private const int MaximumSelectionAttemptsPerSlot = 32;

    public static int RollRegularCount(RebirthBossEventSize size, Random r) { if (size == RebirthBossEventSize.Small) return r.Next(4, 7); if (size == RebirthBossEventSize.Large) return r.Next(11, 16); return r.Next(7, 11); }
    public static bool TryBuild(World world, EntityPlayer owner, RebirthBossEventProgressionSnapshot p, int regularCount, Random rng, out RebirthBossEventSpawnPlan plan, out string failure)
    {
        plan = new RebirthBossEventSpawnPlan(); failure = null;
        bool purge=RebirthSandboxOptionManager.Current.IsPurge;
        if(purge && !RebirthBossEventPlacement.TryBuildAtomicPlacement(world,owner,regularCount+3,rng,plan.Positions,out plan.SpawnOrigin,out failure))return false;
        List<int> selected = new List<int>();
        for (int i = 0; i < regularCount + 3; i++)
        {
            bool accepted = false;

            for (int attempt = 0;
                 attempt < MaximumSelectionAttemptsPerSlot;
                 attempt++)
            {
                RebirthSpawnTrace trace;

                RebirthSpawnContext context =
                    new RebirthSpawnContext
                    {
                        Surface = RebirthSpawnSurface.Biome,
                        ProgressionMode =
                            purge?RebirthSpawnProgressionMode.Biome:RebirthSpawnProgressionMode.Gamestage,
                        GameStage =
                            purge?0:p.EffectiveEventGameStage,
                        Biome =
                            RebirthBossEventIdentity.GetBiome(
                                world,
                                purge?plan.Positions[i]:owner.position),
                        HistoryKey =
                            "bossevent:" + p.OwnerStableId
                    };

                if (!RebirthSpawnCompositionService.TrySelect(
                        context,
                        rng.NextDouble,
                        out trace) ||
                    trace == null)
                {
                    failure =
                        "composition-empty: " +
                        RebirthSpawnCompositionService.GetStatus();
                    return false;
                }

                // 3.1 EntityClass IDs are signed string hashes. Negative values are valid.
                // Validate against the live registry instead of testing id > 0.
                EntityClass selectedClass =
                    EntityClass.GetEntityClass(
                        trace.EntityClassId);

                if (selectedClass == null)
                {
                    failure =
                        "composition-invalid-entity:" +
                        (trace.EntityName ?? "<unknown>") +
                        " id=" + trace.EntityClassId;
                    return false;
                }

                // Boss Events never use screamers, crawlers or demolition zombies.
                if (RebirthSpawnCompositionService.IsForbiddenRestrictedSpawnEntity(selectedClass))
                {
                    RebirthBossEventDiagnostics.Write(
                        "event composition excluded entity="
                        + selectedClass.entityClassName
                        + " reason=forbidden-special"
                        + " slot=" + i
                        + " attempt=" + (attempt + 1));
                    continue;
                }

                int selectedId = trace.EntityClassId;

                // Role identity rules:
                //  - supports must differ from the boss and from each other;
                //  - regulars must differ from the boss and both supports;
                //  - regulars may repeat other regulars.
                bool duplicateRole =
                    (i == 1 && selected.Count >= 1 && selectedId == selected[0]) ||
                    (i == 2 && selected.Count >= 2 &&
                        (selectedId == selected[0] || selectedId == selected[1])) ||
                    (i >= 3 && selected.Count >= 3 &&
                        (selectedId == selected[0] ||
                         selectedId == selected[1] ||
                         selectedId == selected[2]));

                if (duplicateRole)
                {
                    RebirthBossEventDiagnostics.Write(
                        "event composition excluded entity="
                        + selectedClass.entityClassName
                        + " reason=role-duplicate"
                        + " slot=" + i
                        + " attempt=" + (attempt + 1));
                    continue;
                }

                selected.Add(selectedId);
                accepted = true;
                break;
            }

            if (!accepted)
            {
                failure =
                    "composition-event-exclusion-exhausted:slot=" + i +
                    " excluded=forbidden-special-or-role-duplicate";
                return false;
            }
        }
        plan.BossClass = selected[0]; plan.Supports.Add(selected[1]); plan.Supports.Add(selected[2]); for (int i = 3; i < selected.Count; i++) plan.Regulars.Add(selected[i]);
        int requiredPositions = 1 + plan.Supports.Count + plan.Regulars.Count;
        if (!purge && !RebirthBossEventPlacement.TryBuildAtomicPlacement(world, owner, requiredPositions, rng, plan.Positions, out plan.SpawnOrigin, out failure)) return false;
        return true;
    }
}

