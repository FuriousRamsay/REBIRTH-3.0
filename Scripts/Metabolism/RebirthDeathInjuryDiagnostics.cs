#if REBIRTH_DEBUG
using System.Diagnostics;
using System.Text;
using HarmonyLib;

internal static class RebirthDeathInjuryDiagnostics
{
    internal static bool Tracks(string name) => name is
        "buffInjuryConcussion" or "buffDrugPainkillers" or "buffInfectionCatch" or
        "buffArmSprained" or "buffArmBroken" or "buffArmSplinted" or "buffArmCast" or
        "buffLegSprained" or "buffLegBroken" or "buffLegSplinted" or "buffLegCast" or "buffBrokenLimbStatus";

    internal static void Trace(EntityPlayer player, string phase,
        FastTags<TagGroup.Global>? excludedTags = null, bool? appliedOverride = null)
    {
        if (!RebirthDebug.IsOn("metabolism") || player.Buffs is not EntityBuffs buffs) return;

        var options = RebirthSandboxOptionManager.Current;
        var line = new StringBuilder("[REBIRTH Injury] phase=").Append(phase)
            .Append(" entity=").Append(player.entityId)
            .Append(" role=").Append(GameManager.IsDedicatedServer ? "dedicated" :
                player.world?.IsRemote() == true ? "client" : "local-host")
            .Append(" progression=").Append(options.PlayerProgression)
            .Append(" keepInjuries=").Append(options.KeepInjuriesOnDeath)
            .Append(" nativePenalty=").Append((EnumDeathPenalty)GameStats.GetInt(EnumGameStats.DeathPenalty))
            .Append(" concussionCounter=").Append(buffs.GetCustomVar("$concussionCounter"));
        if (appliedOverride.HasValue)
            line.Append(" override=").Append(appliedOverride.Value);

        line.Append(" buffs=[");
        bool any = false;
        foreach (BuffValue buff in buffs.ActiveBuffs)
        {
            if (buff == null || !Tracks(buff.BuffName)) continue;
            if (any) line.Append(", ");
            any = true;
            line.Append(buff.BuffName).Append(buff.Invalid ? "(invalid" : buff.Remove ? "(marked-remove" : "(active");
            if (!buff.Invalid && excludedTags.HasValue && buff.BuffClass is BuffClass definition)
                line.Append(" removeOnDeath=").Append(definition.RemoveOnDeath)
                    .Append(" matchesKeepTags=").Append(definition.Tags.Test_AnySet(excludedTags.Value));
            line.Append(')');
        }
        RebirthDebug.Log("metabolism", line.Append(']').ToString());
    }
}

[HarmonyPatch(typeof(EntityBuffs), nameof(EntityBuffs.RemoveBuff))]
internal static class RebirthInjuryRemovalTrace
{
    private static void Prefix(EntityBuffs __instance, string _name, bool _netSync)
    {
        if (!RebirthDebug.IsOn("metabolism") || !RebirthDeathInjuryDiagnostics.Tracks(_name) ||
            __instance.parent is not EntityPlayer player) return;

        var buff = __instance.GetBuff(_name);
        if (buff == null || buff.Invalid || buff.Remove) return;

        RebirthDeathInjuryDiagnostics.Trace(player, $"explicit-remove buff={_name} netSync={_netSync}");
        RebirthDebug.Log("metabolism", "[REBIRTH Injury] removal caller " + new StackTrace(1, false));
    }
}

[HarmonyPatch(typeof(EntityPlayerLocal), nameof(EntityPlayerLocal.AfterPlayerRespawn))]
internal static class RebirthInjuryLocalRespawnTrace
{
    private static void Postfix(EntityPlayerLocal __instance, RespawnType _type)
    {
        if (_type == RespawnType.Died)
            RebirthDeathInjuryDiagnostics.Trace(__instance, "client-respawn");
    }
}
#endif
