#if DEBUG
using HarmonyLib;
using System.Collections.Generic;

#nullable disable

/// <summary>
/// Governed attack-harvest patch.
/// 
/// Target:
/// GameUtils.HarvestOnAttack(ItemActionData, Dictionary<string, ItemActionAttack.Bonuses>)
/// 
/// Not installed automatically.
/// </summary>
[HarmonyPatch(typeof(GameUtils), "HarvestOnAttack")]
public static class Harmony_RebirthAttackHarvestScaffoldPatch
{
    public static bool Prefix(
        ItemActionData _actionData,
        Dictionary<string, ItemActionAttack.Bonuses> ToolBonuses)
    {
        if (!RebirthAttackHarvestPolicy.ShouldPatchDoWork())
        {
            RebirthAttackHarvestCounters.RecordPolicyDisabled();
            return true;
        }

        if (RebirthAttackHarvestPolicy.ObserveOnly || !RebirthAttackHarvestPolicy.AllowReplacementBehavior)
        {
            RebirthAttackHarvestCounters.RecordObserveOnly();
            return true;
        }

        RebirthAttackHarvestReplacementResultKind result;
        if (RebirthAttackHarvestReplacementService.TryRunVanillaEquivalent(_actionData, ToolBonuses, out result))
            return false;

        return true;
    }
}
#endif
