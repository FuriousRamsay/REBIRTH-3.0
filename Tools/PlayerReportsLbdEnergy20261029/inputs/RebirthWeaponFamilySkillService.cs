using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

#nullable disable

/// <summary>
/// Chunk 7 runtime owner for the 18 authoritative Survivor weapon-family Skills.
/// The service reuses native 7DTD handling passives and the existing REBIRTH combat LBD router.
/// It intentionally does not alter direct damage, magazine size, projectile count or firearm RPM.
/// </summary>
public static class RebirthWeaponFamilySkillService
{
    public const string PassiveBuff = "RebirthSurvivorWeaponFamilyPassives";
    public const string MeleeActiveCVar = "$rbSurvivorWeaponMeleeActive";
    public const string RangedActiveCVar = "$rbSurvivorWeaponRangedActive";
    public const string MeleeStaminaCVar = "$rbSurvivorWeaponMeleeStamina";
    public const string MeleeSpeedCVar = "$rbSurvivorWeaponMeleeSpeed";
    public const string RangedReloadCVar = "$rbSurvivorWeaponRangedReload";
    public const string RangedHandlingCVar = "$rbSurvivorWeaponRangedHandling";
    public const string RangedSpreadCVar = "$rbSurvivorWeaponRangedSpread";
    public const string RangedRecoilCVar = "$rbSurvivorWeaponRangedRecoil";

    private static readonly HashSet<string> MeleeSkills = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "skill.spears","skill.clubs","skill.swords","skill.axes","skill.batons","skill.hammers","skill.knives","skill.scythes","skill.knuckles","skill.unarmed"
    };

    private static readonly HashSet<string> RangedSkills = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "skill.archery","skill.pistols","skill.revolvers","skill.heavy_handguns","skill.shotguns","skill.assault_rifles","skill.tactical_rifles","skill.long_range_rifles"
    };

    private static bool installed;
    private static float nextPassiveSync;

    public static string Install()
    {
        if (installed) return "[REBIRTH Survivor Weapon Families] already installed";
        ModEvents.GameUpdate.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SGameUpdateData>(OnGameUpdate));
        ModEvents.GameStarting.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SGameStartingData>(OnGameStarting));
        ModEvents.WorldShuttingDown.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SWorldShuttingDownData>(OnWorldShuttingDown));
        ModEvents.GameShutdown.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SGameShutdownData>(OnGameShutdown));
        installed = true;
        return "[REBIRTH Survivor Weapon Families] installed";
    }

    public static float SignedEndpoint(float skillValue, float negativeAtMinus50, float positiveAt100)
    {
        float v = Mathf.Clamp(skillValue, -50f, 100f);
        if (v < 0f) return negativeAtMinus50 * (-v / 50f);
        if (v > 0f) return positiveAt100 * (v / 100f);
        return 0f;
    }

    public static bool IsChunk7Skill(string skillId)
    { return IsMeleeSkill(skillId) || IsRangedSkill(skillId); }

    public static bool IsMeleeSkill(string skillId)
    { return !string.IsNullOrEmpty(skillId) && MeleeSkills.Contains(skillId); }

    public static bool IsRangedSkill(string skillId)
    { return !string.IsNullOrEmpty(skillId) && RangedSkills.Contains(skillId); }

    public static string[] GetMeleeSkillIds()
    {
        string[] r = new string[MeleeSkills.Count]; MeleeSkills.CopyTo(r); Array.Sort(r, StringComparer.OrdinalIgnoreCase); return r;
    }

    public static string[] GetRangedSkillIds()
    {
        string[] r = new string[RangedSkills.Count]; RangedSkills.CopyTo(r); Array.Sort(r, StringComparer.OrdinalIgnoreCase); return r;
    }

    public static void SyncNativePassiveEffects(EntityPlayer player)
    {
        if (player == null || player.Buffs == null) return;
        string skillId = string.Empty;
        ItemValue held = player.inventory != null ? player.inventory.holdingItemItemValue : null;
        if (held != null) skillId = RebirthProgressionRuntimeConfig.ClassifyCombat(held);

        float value = 0f;
        bool hasSkill = IsChunk7Skill(skillId) && TryGetSkillValue(player, skillId, out value);
        if (!hasSkill)
        {
            SetAllZero(player);
            if (player.Buffs.HasBuff(PassiveBuff)) player.Buffs.RemoveBuff(PassiveBuff);
            return;
        }

        if (IsMeleeSkill(skillId))
        {
            SetCVar(player, MeleeActiveCVar, 1f); SetCVar(player, RangedActiveCVar, 0f);
            SetCVar(player, MeleeStaminaCVar, SignedEndpoint(value, RebirthProgressionRuntimeConfig.MeleeStaminaNegative, RebirthProgressionRuntimeConfig.MeleeStaminaPositive) + RebirthRageService.GetStaminaDelta(player));
            SetCVar(player, MeleeSpeedCVar, SignedEndpoint(value, RebirthProgressionRuntimeConfig.MeleeSpeedNegative, RebirthProgressionRuntimeConfig.MeleeSpeedPositive) + RebirthRageService.GetSpeedDelta(player));
            SetCVar(player, RangedReloadCVar, 0f); SetCVar(player, RangedHandlingCVar, 0f); SetCVar(player, RangedSpreadCVar, 0f); SetCVar(player, RangedRecoilCVar, 0f);
        }
        else
        {
            SetCVar(player, MeleeActiveCVar, 0f); SetCVar(player, RangedActiveCVar, 1f);
            SetCVar(player, MeleeStaminaCVar, 0f); SetCVar(player, MeleeSpeedCVar, 0f);
            SetCVar(player, RangedReloadCVar, SignedEndpoint(value, RebirthProgressionRuntimeConfig.RangedReloadNegative, RebirthProgressionRuntimeConfig.RangedReloadPositive));
            SetCVar(player, RangedHandlingCVar, SignedEndpoint(value, RebirthProgressionRuntimeConfig.RangedHandlingNegative, RebirthProgressionRuntimeConfig.RangedHandlingPositive));
            SetCVar(player, RangedSpreadCVar, SignedEndpoint(value, RebirthProgressionRuntimeConfig.RangedSpreadNegative, RebirthProgressionRuntimeConfig.RangedSpreadPositive));
            SetCVar(player, RangedRecoilCVar, SignedEndpoint(value, RebirthProgressionRuntimeConfig.RangedRecoilNegative, RebirthProgressionRuntimeConfig.RangedRecoilPositive));
        }
        if (!player.Buffs.HasBuff(PassiveBuff)) player.Buffs.AddBuff(PassiveBuff);
    }

    public static string BuildDebugReport(EntityPlayer player)
    {
        StringBuilder b = new StringBuilder();
        b.AppendLine("[REBIRTH Survivor Weapon Families]");
        b.AppendLine("status=IMPLEMENTED_PREBOOT compileClaim=False authority=passives(client+server snapshot); LBD=server-authoritative existing combat router");
        b.AppendLine("scope=18 families (10 melee + 8 ranged); directDamage=False firearmRPM=False magazine=False projectileCount=False jams=False scopeSway=False");
        if (player == null) { b.AppendLine("player=<null>"); return b.ToString(); }
        ItemValue held = player.inventory != null ? player.inventory.holdingItemItemValue : null;
        string itemName = held != null && held.ItemClass != null ? (held.ItemClass.GetItemName() ?? string.Empty) : string.Empty;
        string skillId = held != null ? RebirthProgressionRuntimeConfig.ClassifyCombat(held) : string.Empty;
        float value = 0f;
        bool available = IsChunk7Skill(skillId) && TryGetSkillValue(player, skillId, out value);
        b.AppendLine("player=" + player.entityId + " item=" + (itemName.Length > 0 ? itemName : "<none>") + " classified=" + (skillId.Length > 0 ? skillId : "<none>") + " chunk7=" + IsChunk7Skill(skillId));
        if (!available) { b.AppendLine("activeSkill=<none>"); return b.ToString(); }
        b.AppendLine("activeSkill=" + skillId + " effectiveOutcomeValue=" + value.ToString("0.###") + " family=" + (IsMeleeSkill(skillId) ? "melee" : "ranged"));
        RebirthWeaponSustainedDpsService.Profile profile;
        if (held != null && RebirthWeaponSustainedDpsService.TryGetDisplayProfile(player, held, out profile) && profile != null && profile.Valid)
        {
            b.AppendLine("sustainedDps=" + profile.SustainedDps.ToString("0.###") + " burstDps=" + profile.BurstDps.ToString("0.###") +
                         " damagePerAttack=" + profile.DamagePerAttack.ToString("0.###") + " source=" + profile.Detail);
            if (profile.Ranged)
                b.AppendLine("rpm=" + profile.RoundsPerMinute.ToString("0.###") + " magazine=" + profile.MagazineSize +
                             " reloadSeconds=" + profile.ReloadSeconds.ToString("0.###") + " reloadsPerMinute=" + profile.ReloadsPerMinute.ToString("0.###") +
                             " projectiles=" + profile.ProjectilesPerAttack.ToString("0.###") + " ammoPerAttack=" + profile.AmmoPerAttack);
            else
                b.AppendLine("attacksPerMinute=" + profile.AttacksPerMinute.ToString("0.###"));
        }
        if (IsMeleeSkill(skillId))
        {
            b.AppendLine("nativeEffects=StaminaLoss(primary/secondary),AttacksPerMinute");
            b.AppendLine("staminaDelta=" + SignedEndpoint(value, RebirthProgressionRuntimeConfig.MeleeStaminaNegative, RebirthProgressionRuntimeConfig.MeleeStaminaPositive).ToString("0.###") +
                         " actionSpeedDelta=" + SignedEndpoint(value, RebirthProgressionRuntimeConfig.MeleeSpeedNegative, RebirthProgressionRuntimeConfig.MeleeSpeedPositive).ToString("0.###"));
        }
        else
        {
            b.AppendLine("nativeEffects=ReloadSpeedMultiplier,WeaponHandling,SpreadMultiplierHip/Aiming,KickDegreesVerticalMin/Max,HorizontalMin/Max");
            b.AppendLine("reloadDelta=" + SignedEndpoint(value, RebirthProgressionRuntimeConfig.RangedReloadNegative, RebirthProgressionRuntimeConfig.RangedReloadPositive).ToString("0.###") +
                         " handlingDelta=" + SignedEndpoint(value, RebirthProgressionRuntimeConfig.RangedHandlingNegative, RebirthProgressionRuntimeConfig.RangedHandlingPositive).ToString("0.###") +
                         " spreadDelta=" + SignedEndpoint(value, RebirthProgressionRuntimeConfig.RangedSpreadNegative, RebirthProgressionRuntimeConfig.RangedSpreadPositive).ToString("0.###") +
                         " recoilDelta=" + SignedEndpoint(value, RebirthProgressionRuntimeConfig.RangedRecoilNegative, RebirthProgressionRuntimeConfig.RangedRecoilPositive).ToString("0.###"));
        }
        return b.ToString();
    }

    private static bool TryGetSkillValue(EntityPlayer player, string id, out float value)
    {
        return RebirthSkillOutcomeValueService.TryGetEffective(player,id,out value);
    }

    private static void OnGameUpdate(ref ModEvents.SGameUpdateData data)
    {
        if (GameManager.Instance == null || GameManager.Instance.World == null) return;
        World world = GameManager.Instance.World;
        float now = Time.realtimeSinceStartup;
        if (now < nextPassiveSync) return;
        nextPassiveSync = now + Mathf.Max(0.10f, RebirthProgressionRuntimeConfig.WeaponFamilyPassiveSyncSeconds);
        if (world.IsRemote())
        {
            EntityPlayerLocal local = world.GetPrimaryPlayer();
            if (local != null)
            {
                SyncNativePassiveEffects(local);
                RebirthWeaponSustainedDpsService.RefreshHeldProfile(local);
            }
            return;
        }
        ConnectionManager connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (connection == null || !connection.IsServer || !RebirthWorldCharacterRepository.IsServerAuthority || !RebirthSurvivorMode.IsEnabledForCurrentWorld()) return;
        if (world.Players == null || world.Players.list == null) return;
        List<EntityPlayer> players = world.Players.list;
        for (int i = 0; i < players.Count; i++)
        {
            SyncNativePassiveEffects(players[i]);
            RebirthWeaponSustainedDpsService.RefreshHeldProfile(players[i]);
        }
    }

    private static void OnGameStarting(ref ModEvents.SGameStartingData data) { ResetRuntime(); }
    private static void OnWorldShuttingDown(ref ModEvents.SWorldShuttingDownData data)
    {
        if (GameManager.Instance != null) RebirthWeaponSustainedDpsService.FlushAllQueuedCombatProgress(GameManager.Instance.World);
        ResetRuntime();
    }
    private static void OnGameShutdown(ref ModEvents.SGameShutdownData data)
    {
        if (GameManager.Instance != null) RebirthWeaponSustainedDpsService.FlushAllQueuedCombatProgress(GameManager.Instance.World);
        ResetRuntime();
    }
    private static void ResetRuntime()
    {
        nextPassiveSync = 0f;
        RebirthWeaponSustainedDpsService.ResetRuntime();
    }

    private static void SetAllZero(EntityPlayer player)
    {
        SetCVar(player, MeleeActiveCVar, 0f); SetCVar(player, RangedActiveCVar, 0f);
        SetCVar(player, MeleeStaminaCVar, 0f); SetCVar(player, MeleeSpeedCVar, 0f);
        SetCVar(player, RangedReloadCVar, 0f); SetCVar(player, RangedHandlingCVar, 0f); SetCVar(player, RangedSpreadCVar, 0f); SetCVar(player, RangedRecoilCVar, 0f);
    }

    private static void SetCVar(EntityPlayer player, string name, float value)
    {
        if (player == null || player.Buffs == null) return;
        if (Math.Abs(player.Buffs.GetCustomVar(name) - value) > 0.0005f) player.Buffs.SetCustomVar(name, value);
    }
}
