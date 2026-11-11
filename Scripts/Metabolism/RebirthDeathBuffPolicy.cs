using GameEvent.SequenceActions;
using HarmonyLib;

[HarmonyPatch(typeof(EntityBuffs), nameof(EntityBuffs.RemoveDeathBuffs))]
internal static class RebirthDeathBuffPolicy
{
    private static readonly FastTags<TagGroup.Global> KeptInjuries =
        FastTags<TagGroup.Global>.Parse("deathpenalty_injured,rebirth_limb_injury");

    private static void Prefix(EntityBuffs __instance, ref FastTags<TagGroup.Global> excludeTags)
    {
        if (__instance.parent is not EntityPlayer player) return;

        var options = RebirthSandboxOptionManager.Current;
        bool applyOverride = options.PlayerProgression == RebirthPlayerProgressionMode.Rebirth &&
            GameStats.GetInt(EnumGameStats.DeathPenalty) != (int)EnumDeathPenalty.Permadeath;
        if (applyOverride)
            excludeTags = options.KeepInjuriesOnDeath ? KeptInjuries : FastTags<TagGroup.Global>.none;

#if REBIRTH_DEBUG
        RebirthDeathInjuryDiagnostics.Trace(player, "before-death-buff-cleanup", excludeTags, applyOverride);
#endif
    }

#if REBIRTH_DEBUG
    private static void Postfix(EntityBuffs __instance)
    {
        if (__instance.parent is EntityPlayer player)
            RebirthDeathInjuryDiagnostics.Trace(player, "after-death-buff-cleanup");
    }
#endif
}

[HarmonyPatch(typeof(ActionAddBuff), nameof(ActionAddBuff.PerformTargetAction))]
internal static class RebirthRespawnInfectionPolicy
{
    private static bool Prefix(ActionAddBuff __instance, ref BaseAction.ActionCompleteStates __result)
    {
        if (__instance.buffName != "buffInfectionCatch") return true;
        string sequence = __instance.Owner?.Name;
        if (sequence is not ("game_on_respawn_none" or "game_on_respawn_default" or "game_on_respawn_injured")) return true;

        var options = RebirthSandboxOptionManager.Current;
        bool keep = options.PlayerProgression == RebirthPlayerProgressionMode.Rebirth
            ? options.KeepInjuriesOnDeath
            : sequence == "game_on_respawn_injured";
        if (keep) return true;
        __result = BaseAction.ActionCompleteStates.Complete;
        return false;
    }
}
