using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;

#nullable disable

/// <summary>
/// Calculates one authoritative sustained-DPS profile from the live ItemValue and player state.
/// The profile is cached outside the damage hot path and is reused by both combat training and UI.
/// Ranged DPS includes fire cadence, magazine/cylinder capacity, ammunition use, pellet count and
/// reload downtime. Melee DPS uses effective damage and effective attacks-per-minute for the
/// strongest sustainable attack mode.
/// </summary>
public static class RebirthWeaponSustainedDpsService
{
    public sealed class Profile
    {
        public bool Valid;
        public bool Ranged;
        public bool Melee;
        public bool HasPowerAttack;
        public bool Explosive;
        public string SkillId = string.Empty;
        public string ItemName = string.Empty;
        public int Signature;
        public float DamagePerAttack;
        public float ProjectilesPerAttack = 1f;
        public float AttacksPerMinute;
        public float NormalDamagePerAttack;
        public float PowerDamagePerAttack;
        public float NormalAttacksPerMinute;
        public float PowerAttacksPerMinute;
        public float NormalAttackDps;
        public float PowerAttackDps;
        public float NormalStaminaCost;
        public float PowerStaminaCost;
        public float NormalBlockDamagePerAttack;
        public float PowerBlockDamagePerAttack;
        public float NormalDamagePerStamina;
        public float PowerDamagePerStamina;
        public float RoundsPerMinute;
        public int MagazineSize = 1;
        public int AmmoPerAttack = 1;
        public float ReloadSeconds;
        public float BurstDps;
        public float SustainedDps;
        public int AttacksInWindow;
        public int ReloadsInWindow;
        public float ReloadsPerMinute;
        public string Detail = string.Empty;
    }

    private sealed class ReloadAnimatorMetadata
    {
        internal RuntimeAnimatorController Controller;
        internal float RefreshAt;
        internal string AnimatorName,ControllerName;
        internal AnimationClip[] Clips;
        internal int RoleScore;
    }
    private sealed class ReloadHierarchyMetadata
    {
        internal float RefreshAt;
        internal Animator[] Animators;
    }
    // Weak keys release destroyed player/model hierarchies rather than accumulating per-session entries.
    private static readonly ConditionalWeakTable<Animator,ReloadAnimatorMetadata> ReloadAnimators=new ConditionalWeakTable<Animator,ReloadAnimatorMetadata>();
    private static readonly ConditionalWeakTable<Transform,ReloadHierarchyMetadata> ReloadHierarchies=new ConditionalWeakTable<Transform,ReloadHierarchyMetadata>();
    private static ReloadAnimatorMetadata GetReloadMetadata(Animator animator)
    {
        var data=ReloadAnimators.GetValue(animator,key=>new ReloadAnimatorMetadata());
        var controller=animator.runtimeAnimatorController;
        float now=Time.realtimeSinceStartup;
        if(data.Controller!=controller||now>=data.RefreshAt)
        {
            data.Controller=controller;data.RefreshAt=now+1f;
            data.AnimatorName=(animator.name??string.Empty).Replace("_",string.Empty);
            data.ControllerName=(controller?.name??string.Empty).Replace("_",string.Empty);
            data.Clips=controller==null?null:controller.animationClips;
            data.RoleScore=ReadReloadAnimatorRoleScore(animator);
        }
        return data;
    }
    private sealed class CacheEntry
    {
        public int Signature;
        public bool LiveHeld;
        public float BuiltAt;
        public Profile Profile;
    }

    private sealed class RuntimeReloadStart
    {
        public string Key = string.Empty;
        public string ItemName = string.Empty;
        public float StartedAt;
        public float EffectiveReloadMultiplier = 1f;
    }

    // Temporary targeted diagnostics for the ranged-reload timing investigation. This is deliberately
    // bounded to reload start/complete events and can be disabled without removing the measurement path.
    public static bool ReloadTimingDebug = false;
    private static readonly HashSet<string> ReloadProfileDebugKeys = new HashSet<string>(StringComparer.Ordinal);
    private static readonly Dictionary<int, RuntimeReloadStart> ActiveReloads = new Dictionary<int, RuntimeReloadStart>();
    private static readonly Dictionary<string, float> MeasuredBaseReloadSeconds = new Dictionary<string, float>(StringComparer.Ordinal);

    private static readonly object Gate = new object();
    private static readonly Dictionary<string, CacheEntry> Cache = new Dictionary<string, CacheEntry>(StringComparer.Ordinal);
    // Keep the last live-held profile briefly after a switch so delayed arrows/bullets use the
    // profile that was actually active when the weapon was fired rather than a newly previewed one.
    private static readonly Dictionary<string, CacheEntry> RecentHeld = new Dictionary<string, CacheEntry>(StringComparer.Ordinal);
    private static readonly Dictionary<int, Dictionary<string, float>> PendingCombat = new Dictionary<int, Dictionary<string, float>>();
    private static readonly FastTags<TagGroup.Global> PrimaryTags = FastTags<TagGroup.Global>.Parse("primary");
    private static readonly FastTags<TagGroup.Global> SecondaryTags = FastTags<TagGroup.Global>.Parse("secondary");
    private static readonly FastTags<TagGroup.Global> PhysicalDamageTags = FastTags<TagGroup.Global>.Parse("physicalDamage");

    public static void RefreshHeldProfile(EntityPlayer player)
    {
        if (player == null || player.inventory == null) return;
        ItemValue held = player.inventory.holdingItemItemValue;
        if (held == null || held.IsEmpty()) return;
        Profile ignored;
        TryGetProfile(player, held, true, out ignored);
    }

    public static void InvalidateHeldProfile(EntityPlayer player)
    {
        if (player == null) return;
        // Do not clear RecentHeld here. Delayed projectile impacts must still be able to use the
        // profile that was active when the projectile was fired. The live held profile is rebuilt
        // on the next UI/skill query (or the periodic held-profile refresh).
        lock (Gate)
            Cache.Clear();
    }

    public static void ResetRuntime()
    {
        lock (Gate)
        {
            Cache.Clear();
            RecentHeld.Clear();
            PendingCombat.Clear();
            nextCombatSettlement = 0f;
            ActiveReloads.Clear();
            MeasuredBaseReloadSeconds.Clear();
            ReloadProfileDebugKeys.Clear();
        }
    }

    public static void QueueCombatProgress(EntityPlayer player, string skillId, float amount)
    {
        if (player == null || string.IsNullOrEmpty(skillId) || !Finite(amount) || amount <= 0f) return;
        lock (Gate)
        {
            Dictionary<string, float> bySkill;
            if (!PendingCombat.TryGetValue(player.entityId, out bySkill))
            {
                bySkill = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
                PendingCombat[player.entityId] = bySkill;
            }
            float current; bySkill.TryGetValue(skillId, out current);
            bySkill[skillId] = current + amount;
        }
    }

    public static void FlushQueuedCombatProgress(EntityPlayer player)
    {
        if (player == null) return;
        Dictionary<string, float> work = null;
        lock (Gate)
        {
            Dictionary<string, float> found;
            if (!PendingCombat.TryGetValue(player.entityId, out found) || found == null || found.Count == 0) return;
            work = new Dictionary<string, float>(found, StringComparer.OrdinalIgnoreCase);
            PendingCombat.Remove(player.entityId);
        }
        foreach (KeyValuePair<string, float> pair in work)
        {
            if (pair.Value <= 0f) continue;
            float skill, attribute;
            RebirthSkillAwardService.TryAwardCombat(player, pair.Key, pair.Value, "combat-batch:" + pair.Key, 0f, out skill, out attribute);
            if (RebirthSkillEvalDiagnostics.On)
                Log.Out("[REBIRTH SkillEval] combat award skill=" + pair.Key + " raw=" + pair.Value + " applied=" + skill + " attribute=" + attribute);
        }
    }

    private static float nextCombatSettlement;
    internal static void SettleQueuedCombatAtFrameEnd()
    {
        World current = GameManager.Instance?.World;
        if (current == null || current.IsRemote() || !RebirthWorldCharacterRepository.IsServerAuthority) return;
        // Native GameUpdate can be skipped by UpdateTick's early return. Delayed damage
        // must still settle without another hit, and without a held-profile dependency.
        float now = Time.realtimeSinceStartup;
        lock (Gate) { if (PendingCombat.Count == 0 || now < nextCombatSettlement) return; }
        nextCombatSettlement = now + Mathf.Max(0.10f, RebirthProgressionRuntimeConfig.WeaponFamilyPassiveSyncSeconds);
        FlushAllQueuedCombatProgress(current);
        RebirthSkillAwardService.FlushOwnerPublications();
    }

    public static void FlushAllQueuedCombatProgress(World world)
    {
        if (world == null || world.IsRemote() || world.Players == null || world.Players.list == null) return;
        List<EntityPlayer> players = world.Players.list;
        for (int i = 0; i < players.Count; i++) FlushQueuedCombatProgress(players[i]);
    }

    // NPC hand attacks are not player weapon families. Use the same damage query and AI cooldown
    // as ItemActionMelee / EAIApproachAndAttackTarget, with the controlled attacker as effect owner.
    public static bool TryGetControlledMeleeDps(EntityAlive attacker, ItemValue item, out float dps)
    {
        dps = 0f;
        if (attacker == null || attacker is EntityPlayer || item == null || item.ItemClass == null ||
            item.ItemClass.Actions == null || item.ItemClass.Actions.Length == 0) return false;
        var action = item.ItemClass.Actions[0] as ItemActionMelee;
        if (action == null) return false;
        float damage = action.GetDamageEntity(item, attacker, 0);
        // Native AI ticks at 20 Hz; both the action delay and AI timeout must have elapsed.
        float interval = Mathf.Max(action.Delay, attacker.GetAttackTimeoutTicks() * .05f);
        if (!Finite(damage) || !Finite(interval) || damage <= 0f || interval <= 0f) return false;
        dps = damage / interval;
        return Finite(dps) && dps > 0f;
    }

    public static bool TryGetCombatProfile(EntityPlayer player, ItemValue attackingItem, out Profile profile)
    {
        bool liveHeld = IsCurrentHeldItem(player, attackingItem);
        if (liveHeld) return TryGetProfile(player, attackingItem, true, out profile);

        // Projectile impacts can arrive after the player has already switched weapons. Preserve the
        // exact live profile that was cached while this item was held for a short, configurable grace
        // period so damage and training stay tied to the firing weapon state.
        profile = null;
        if (player != null && attackingItem != null)
        {
            string recentKey = RecentHeldKey(player, attackingItem);
            float now = Time.realtimeSinceStartup;
            float grace = Mathf.Max(0f, RebirthProgressionRuntimeConfig.WeaponDpsRecentHeldSeconds);
            lock (Gate)
            {
                CacheEntry recent;
                if (RecentHeld.TryGetValue(recentKey, out recent) && recent != null && recent.Profile != null &&
                    now - recent.BuiltAt <= grace)
                {
                    profile = recent.Profile;
                    return profile.Valid;
                }
            }
        }
        return TryGetProfile(player, attackingItem, false, out profile);
    }

    public static bool TryGetDisplayProfile(EntityPlayer player, ItemValue item, out Profile profile)
    {
        // XUi item stacks can hold a copied ItemValue whose transient state (loaded ammo, use times,
        // metadata, etc.) is a frame behind the authoritative toolbelt ItemValue. Using the strict
        // combat signature for that copy makes the Selected Item panel look like a non-held preview
        // and therefore prevents it from using the held weapon's reload animation/controller.
        // Resolve a stable instance match back to the authoritative held ItemValue for display only.
        // Combat/projectile attribution continues to use the stricter IsCurrentHeldItem path.
        ItemValue displayItem;
        if (TryResolveHeldDisplayItem(player, item, out displayItem))
            return TryGetProfile(player, displayItem, true, out profile, true);
        return TryGetProfile(player, item, false, out profile, true);
    }

    private static bool TryResolveHeldDisplayItem(EntityPlayer player, ItemValue item, out ItemValue held)
    {
        held = null;
        if (player == null || player.inventory == null || item == null || item.IsEmpty()) return false;
        held = player.inventory.holdingItemItemValue;
        if (held == null || held.IsEmpty()) return false;
        if (ReferenceEquals(held, item)) return true;
        if (held.type != item.type || held.Quality != item.Quality || held.Seed != item.Seed) return false;
        if (!SameModificationIdentity(held.modifications, item.modifications)) return false;
        if (!SameModificationIdentity(held.cosmeticMods, item.cosmeticMods)) return false;

        if (ReloadTimingDebug && BuildItemSignature(held) != BuildItemSignature(item))
        {
            string key = "display-canonical|" + player.entityId.ToString(CultureInfo.InvariantCulture) + "|" +
                held.type.ToString(CultureInfo.InvariantCulture) + "|" + held.Seed.ToString(CultureInfo.InvariantCulture);
            bool shouldLog;
            lock (Gate) shouldLog = ReloadProfileDebugKeys.Add(key);
            if (shouldLog)
                Log.Out("[REBIRTH RELOAD TIMING] DISPLAY-CANONICAL item='" +
                    (held.ItemClass != null ? (held.ItemClass.GetItemName() ?? string.Empty) : string.Empty) +
                    "' entity=" + player.entityId.ToString(CultureInfo.InvariantCulture) +
                    " heldMeta=" + held.Meta.ToString(CultureInfo.InvariantCulture) +
                    " uiMeta=" + item.Meta.ToString(CultureInfo.InvariantCulture) +
                    " heldUse=" + held.UseTimes.ToString(CultureInfo.InvariantCulture) +
                    " uiUse=" + item.UseTimes.ToString(CultureInfo.InvariantCulture));
        }
        return true;
    }

    private static bool SameModificationIdentity(ItemValue[] a, ItemValue[] b)
    {
        int aLength = a != null ? a.Length : 0;
        int bLength = b != null ? b.Length : 0;
        if (aLength != bLength) return false;
        for (int i = 0; i < aLength; i++)
        {
            ItemValue av = a[i];
            ItemValue bv = b[i];
            int at = av != null ? av.type : 0;
            int bt = bv != null ? bv.type : 0;
            if (at != bt) return false;
            if (av != null && bv != null && (av.Quality != bv.Quality || av.Seed != bv.Seed)) return false;
        }
        return true;
    }

    public static int GetDisplayFingerprint(EntityPlayer player, ItemValue item)
    {
        Profile p;
        if (!TryGetDisplayProfile(player, item, out p)) return 0;
        return GetDisplayFingerprint(p);
    }

    // Callers already holding a display profile can reuse it without repeating signature/cache work.
    internal static int GetDisplayFingerprint(Profile p)
    {
        if (p == null || !p.Valid) return 0;
        unchecked
        {
            int h = p.Signature;
            h = h * 31 + Mathf.RoundToInt(p.SustainedDps * 100f);
            h = h * 31 + Mathf.RoundToInt(p.DamagePerAttack * 100f);
            h = h * 31 + Mathf.RoundToInt(p.ReloadSeconds * 100f);
            h = h * 31 + Mathf.RoundToInt(p.RoundsPerMinute * 10f);
            h = h * 31 + Mathf.RoundToInt(p.AttacksPerMinute * 10f);
            h = h * 31 + Mathf.RoundToInt(p.NormalDamagePerAttack * 100f);
            h = h * 31 + Mathf.RoundToInt(p.PowerDamagePerAttack * 100f);
            h = h * 31 + Mathf.RoundToInt(p.NormalAttacksPerMinute * 10f);
            h = h * 31 + Mathf.RoundToInt(p.PowerAttacksPerMinute * 10f);
            h = h * 31 + Mathf.RoundToInt(p.NormalAttackDps * 100f);
            h = h * 31 + Mathf.RoundToInt(p.PowerAttackDps * 100f);
            h = h * 31 + Mathf.RoundToInt(p.NormalStaminaCost * 100f);
            h = h * 31 + Mathf.RoundToInt(p.PowerStaminaCost * 100f);
            h = h * 31 + Mathf.RoundToInt(p.NormalBlockDamagePerAttack * 100f);
            h = h * 31 + Mathf.RoundToInt(p.PowerBlockDamagePerAttack * 100f);
            h = h * 31 + Mathf.RoundToInt(p.ProjectilesPerAttack * 100f);
            h = h * 31 + p.MagazineSize;
            h = h * 31 + p.AmmoPerAttack;
            h = h * 31 + p.AttacksInWindow;
            h = h * 31 + p.ReloadsInWindow;
            h = h * 31 + Mathf.RoundToInt(p.ReloadsPerMinute * 100f);
            return h;
        }
    }

    public static float CombatSkillMultiplier(float skillValue)
    {
        if (skillValue < 25f) return RebirthProgressionRuntimeConfig.CombatSkillMultiplier0To24;
        if (skillValue < 50f) return RebirthProgressionRuntimeConfig.CombatSkillMultiplier25To49;
        if (skillValue < 75f) return RebirthProgressionRuntimeConfig.CombatSkillMultiplier50To74;
        if (skillValue < 90f) return RebirthProgressionRuntimeConfig.CombatSkillMultiplier75To89;
        return RebirthProgressionRuntimeConfig.CombatSkillMultiplier90To100;
    }

    public static float CalculateCombatProgress(float effectiveDamage, float sustainedDps, float currentSkill)
    {
        if (!Finite(effectiveDamage) || !Finite(sustainedDps) || effectiveDamage <= 0f || sustainedDps <= 0f) return 0f;
        // Training is damage-proportional and normalized by live sustained work rate. The authored
        // equivalent-seconds ceiling bounds one event without reintroducing nominal/overkill damage.
        float seconds = effectiveDamage / sustainedDps;
        float maxEquivalent = Mathf.Max(0f, RebirthProgressionRuntimeConfig.CombatMaxEquivalentSecondsPerHit);
        if (maxEquivalent > 0f) seconds = Mathf.Min(seconds, maxEquivalent);
        float progress = seconds * Mathf.Max(0f, RebirthProgressionRuntimeConfig.CombatProgressPerSustainedSecond) * CombatSkillMultiplier(currentSkill);
        return Finite(progress) ? Mathf.Max(0f, progress) : 0f;
    }

    /// <summary>
    /// Resolves a live world-work rate for one authoritative block-work event. Credited work is
    /// the actual HP/state work committed by the game; cadence comes from the live held ItemValue
    /// with quality/mod/buff/progression effects. Deriving rate from the realized work keeps normal
    /// and power/tool-specific damage in the same unit without letting stronger tools multiply XP.
    /// </summary>
    public static bool TryGetWorldWorkRate(EntityPlayer player, ItemValue item, float creditedWork, out float workRate)
    {
        workRate = 0f;
        if (player == null || item == null || item.ItemClass == null || !Finite(creditedWork) || creditedWork <= 0f) return false;
        bool liveHeld = IsCurrentHeldItem(player, item);
        FastTags<TagGroup.Global> tags = PrimaryTags | PhysicalDamageTags | item.ItemClass.ItemTags;
        float apm = GetEffectWithFallback("AttacksPerMinute", item, player, tags, 0f, liveHeld,
            PrimaryTags, item.ItemClass.ItemTags, PhysicalDamageTags);
        float delay = GetActionDelay(item, 0);
        float interval = apm > 0f ? 60f / apm : Mathf.Max(0f, delay);
        if (!Finite(interval) || interval <= 0f) return false;
        workRate = creditedWork / interval;
        return Finite(workRate) && workRate > 0f;
    }

    public static float CalculateMeleeDps(float damage, float attacksPerMinute, float actionDelay)
    {
        if (!Finite(damage) || !Finite(attacksPerMinute) || !Finite(actionDelay) || damage <= 0f) return 0f;
        // Modern item definitions treat AttacksPerMinute as the effective cadence when present;
        // Action.Delay is the fallback for actions that do not publish an APM passive.
        float interval = attacksPerMinute > 0f ? 60f / attacksPerMinute : Mathf.Max(0f, actionDelay);
        return interval > 0f ? damage / interval : 0f;
    }

    public static float CalculateRangedSustainedDps(float shotDamage, float roundsPerMinute, float actionDelay,
        int magazineSize, float reloadSeconds, int ammoPerAttack, float windowSeconds, out int attacks, out int reloads)
    {
        attacks = 0; reloads = 0;
        if (!Finite(shotDamage) || !Finite(roundsPerMinute) || !Finite(actionDelay) || shotDamage <= 0f || windowSeconds <= 0f) return 0f;

        // RPM is the cadence while rounds remain in the magazine, not sustained shots/minute.
        // Sustained DPS uses the whole fire/reload cycle so one-shot and small-magazine weapons
        // are not incorrectly treated as if they could maintain their authored burst RPM forever.
        float interval = roundsPerMinute > 0f ? 60f / roundsPerMinute : Mathf.Max(0f, actionDelay);
        if (interval <= 0f) return 0f;
        int capacity = Math.Max(1, magazineSize);
        int use = Math.Max(1, ammoPerAttack);
        int attacksPerMagazine = Math.Max(1, capacity / use);
        float transition = reloadSeconds > 0f ? Mathf.Max(interval, reloadSeconds) : interval;
        float cycleSeconds = Mathf.Max(0.001f, (attacksPerMagazine - 1) * interval + transition);
        float sustained = (attacksPerMagazine * shotDamage) / cycleSeconds;

        int fullCycles = Math.Max(0, Mathf.FloorToInt(windowSeconds / cycleSeconds));
        attacks = fullCycles * attacksPerMagazine;
        reloads = reloadSeconds > 0f ? fullCycles : 0;
        float remainder = windowSeconds - fullCycles * cycleSeconds;
        if (remainder > 0f)
        {
            attacks++;
            float spent = 0f;
            for (int i = 1; i < attacksPerMagazine && spent + interval < remainder; i++)
            {
                spent += interval;
                attacks++;
            }
        }
        return Finite(sustained) ? Mathf.Max(0f, sustained) : 0f;
    }

    public static string BuildUiValue(Profile p)
    {
        if (p == null || !p.Valid) return string.Empty;
        if (p.Ranged)
        {
            string reload = p.ReloadSeconds > 0f
                ? "\n[8E8E8E][sub]" + p.ReloadSeconds.ToString("0.##", CultureInfo.InvariantCulture) + " s reload[/sub][-]"
                : string.Empty;
            return p.SustainedDps.ToString("0.#", CultureInfo.InvariantCulture) + reload;
        }
        return p.SustainedDps.ToString("0.#", CultureInfo.InvariantCulture) +
            "\n[BBBBBB]" + p.DamagePerAttack.ToString("0.#", CultureInfo.InvariantCulture) + " dmg  •  " +
            p.AttacksPerMinute.ToString("0", CultureInfo.InvariantCulture) + " APM[-]";
    }

    private static bool TryGetProfile(EntityPlayer player, ItemValue item, bool liveHeld, out Profile profile, bool displayTools = false)
    {
        profile = null;
        if (item == null || item.IsEmpty() || item.ItemClass == null) return false;
        string skillId = RebirthProgressionRuntimeConfig.ClassifyCombat(item);
        bool supported = RebirthWeaponFamilySkillService.IsChunk7Skill(skillId) ||
            string.Equals(skillId, "skill.deployable_turrets", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(skillId, "skill.drone_operations", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(skillId, "skill.explosives", StringComparison.OrdinalIgnoreCase);
        var actions = item.ItemClass.Actions;
        bool toolMelee = displayTools && actions != null && actions.Length > 0 &&
            (actions[0] is ItemActionMelee || actions[0] is ItemActionDynamicMelee || string.Equals(item.ItemClass.DisplayType, "motorTool", StringComparison.OrdinalIgnoreCase));
        bool toolRanged = displayTools && !toolMelee && actions != null && actions.Length > 0 && actions[0] is ItemActionRanged;
        if (!supported && !toolMelee && !toolRanged) return false;
        int signature = BuildSignature(player, item, skillId, liveHeld);
        string key = (player != null ? player.entityId.ToString(CultureInfo.InvariantCulture) : "none") + "|" + signature.ToString(CultureInfo.InvariantCulture) + "|" + (liveHeld ? "1" : "0");
        float now = Time.realtimeSinceStartup;
        float maxAge = Mathf.Max(0.10f, RebirthProgressionRuntimeConfig.WeaponDpsProfileRefreshSeconds);
        lock (Gate)
        {
            CacheEntry found;
            if (Cache.TryGetValue(key, out found) && found != null && found.Profile != null && now - found.BuiltAt <= maxAge)
            {
                profile = found.Profile;
                if (liveHeld) RememberRecentHeld(player, item, profile, now);
                return profile.Valid;
            }
        }

        Profile built = BuildProfile(player, item, skillId, liveHeld, signature, toolMelee, toolRanged);
        lock (Gate)
        {
            if (Cache.Count > 256) Cache.Clear();
            Cache[key] = new CacheEntry { Signature = signature, LiveHeld = liveHeld, BuiltAt = now, Profile = built };
            if (liveHeld) RememberRecentHeld(player, item, built, now);
        }
        profile = built;
        return profile != null && profile.Valid;
    }

    private static Profile BuildProfile(EntityPlayer player, ItemValue item, string skillId, bool liveHeld, int signature, bool toolMelee = false, bool toolRanged = false)
    {
        bool melee = RebirthWeaponFamilySkillService.IsMeleeSkill(skillId) || toolMelee;
        bool ranged = RebirthWeaponFamilySkillService.IsRangedSkill(skillId) || toolRanged;
        bool explosive = string.Equals(skillId, "skill.explosives", StringComparison.OrdinalIgnoreCase);
        bool deviceRanged = string.Equals(skillId, "skill.deployable_turrets", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(skillId, "skill.drone_operations", StringComparison.OrdinalIgnoreCase);
        Profile p = new Profile { SkillId = skillId, ItemName = item.ItemClass.GetItemName() ?? string.Empty, Signature = signature, Melee = melee, Ranged = ranged || deviceRanged, Explosive = explosive };
        if (!melee && !ranged && !deviceRanged && !explosive) return p;

        if (explosive)
        {
            // Explosives already use the dedicated Chunk-10 native blast skill bridge. The UI only
            // needs a valid profile so the skill and its damage-based training rate can be shown;
            // do not pretend a thrown explosive has firearm-style reload/capacity statistics.
            float authoredExplosionDamage = ReadDynamicNumericByTokens(item.ItemClass.Properties,
                new string[] { "explosion", "entity", "damage" },
                new string[] { "block" });
            float liveExplosionDamage = GetEffectWithFallback("ExplosionEntityDamage", item, player,
                item.ItemClass.ItemTags, authoredExplosionDamage, liveHeld,
                item.ItemClass.ItemTags, authoredExplosionDamage);
            p.DamagePerAttack = liveExplosionDamage > 0f ? liveExplosionDamage : authoredExplosionDamage;
            float explosiveApm = GetEffect("AttacksPerMinute", item, player, item.ItemClass.ItemTags, 0f, liveHeld);
            float explosiveDelay = GetActionDelay(item, 0);
            float explosiveInterval = explosiveApm > 0f ? 60f / explosiveApm : Mathf.Max(0.05f, explosiveDelay);
            p.AttacksPerMinute = explosiveApm;
            p.BurstDps = p.SustainedDps = p.DamagePerAttack > 0f && explosiveInterval > 0f ? p.DamagePerAttack / explosiveInterval : 0f;
            p.Valid = p.SustainedDps > 0f;
            p.Detail = p.Valid ? "explosive-live-work-rate" : "explosive-work-rate-unavailable";
            return p;
        }

        if (melee)
        {
            bool motorTool = string.Equals(item.ItemClass.DisplayType, "motorTool", StringComparison.OrdinalIgnoreCase);
            p.HasPowerAttack = !motorTool && item.ItemClass.Actions != null && item.ItemClass.Actions.Length > 1 &&
                (item.ItemClass.Actions[1] is ItemActionMelee || item.ItemClass.Actions[1] is ItemActionDynamicMelee);
            float bestDps = 0f, bestDamage = 0f, bestApm = 0f;
            for (int i = 0; i < (p.HasPowerAttack ? 2 : 1); i++)
            {
                FastTags<TagGroup.Global> tags = (i == 0 ? PrimaryTags : SecondaryTags) | PhysicalDamageTags;
                float fallbackDamageBase = i == 0
                    ? GetEffect("EntityDamage", item, player, PrimaryTags, 0f, liveHeld)
                    : GetEffect("EntityDamage", item, player, SecondaryTags, 0f, liveHeld);
                float damage = GetEffectWithFallback("EntityDamage", item, player, tags, 0f, liveHeld,
                    i == 0 ? PrimaryTags : SecondaryTags, item.ItemClass.ItemTags, PhysicalDamageTags,
                    fallbackDamageBase > 0f ? fallbackDamageBase : 0f);
                float displayedDamage;
                string displayedTitle = i == 0 ? "Melee Damage" : "Power Attack Damage";
                if (TryGetDisplayedStatValueByTitle(displayedTitle, item, player, liveHeld, out displayedDamage) && displayedDamage > 0f)
                    damage = displayedDamage;
                float apm = GetEffectWithFallback("AttacksPerMinute", item, player, tags, 0f, liveHeld,
                    i == 0 ? PrimaryTags : SecondaryTags, item.ItemClass.ItemTags);
                if (motorTool)
                    apm = GetEffectWithFallback("RoundsPerMinute", item, player, item.ItemClass.ItemTags, 0f, liveHeld,
                        PrimaryTags, apm);
                float stamina = GetEffectWithFallback("StaminaLoss", item, player, tags, 0f, liveHeld,
                    i == 0 ? PrimaryTags : SecondaryTags, item.ItemClass.ItemTags);
                float blockDamage = GetEffectWithFallback("BlockDamage", item, player, tags, 0f, liveHeld,
                    i == 0 ? PrimaryTags : SecondaryTags, item.ItemClass.ItemTags, PhysicalDamageTags);
                if (i == 1 && blockDamage > 0f && p.NormalBlockDamagePerAttack > 0f &&
                    damage > 0f && p.NormalDamagePerAttack > 0f && blockDamage <= p.NormalBlockDamagePerAttack * 1.01f)
                    blockDamage = p.NormalBlockDamagePerAttack * (damage / p.NormalDamagePerAttack);
                float delay = GetActionDelay(item, i);
                if (!liveHeld && RebirthWeaponFamilySkillService.IsMeleeSkill(skillId))
                {
                    apm = ApplyPreviewWeaponFamilySpeed(player, skillId, apm);
                    stamina = ApplyPreviewWeaponFamilyStamina(player, skillId, stamina);
                }
                float dps = CalculateMeleeDps(damage, apm, delay);
                float efficiency = stamina > 0f ? damage / stamina : 0f;

                if (i == 0)
                {
                    p.NormalDamagePerAttack = damage;
                    p.NormalAttacksPerMinute = apm;
                    p.NormalAttackDps = dps;
                    p.NormalStaminaCost = stamina;
                    p.NormalBlockDamagePerAttack = blockDamage;
                    p.NormalDamagePerStamina = efficiency;
                }
                else
                {
                    p.PowerDamagePerAttack = damage;
                    p.PowerAttacksPerMinute = apm;
                    p.PowerAttackDps = dps;
                    p.PowerStaminaCost = stamina;
                    p.PowerBlockDamagePerAttack = blockDamage;
                    p.PowerDamagePerStamina = efficiency;
                }

                if (dps > bestDps) { bestDps = dps; bestDamage = damage; bestApm = apm; }
            }
            p.DamagePerAttack = bestDamage;
            p.AttacksPerMinute = bestApm;
            p.BurstDps = p.SustainedDps = bestDps;
            p.Valid = bestDps > 0f;
            p.Detail = p.Valid ? "melee-normal-power-live-dps" : "melee-profile-unavailable";
            return p;
        }

        FastTags<TagGroup.Global> rangedTags = PrimaryTags | PhysicalDamageTags;

        // The native Selected Item display evaluates firearm output with the item's own tag set.
        // The b10 diagnostic proved that primary/physical tags returned only the 9mm ammunition
        // contribution (32.8) while ItemTags returned the displayed total (37.925). Use ItemTags
        // first so Sustained DPS is built from the same effective damage the player sees.
        FastTags<TagGroup.Global> itemTags = item.ItemClass.ItemTags;
        float perProjectile = GetEffect("EntityDamage", item, player, itemTags, 0f, liveHeld);
        if (perProjectile <= 0f && !TryGetDisplayedStatValue(PassiveEffects.EntityDamage, item, player, out perProjectile))
            perProjectile = GetEffectWithFallback("EntityDamage", item, player, rangedTags, 0f, liveHeld,
                itemTags, PrimaryTags, PhysicalDamageTags);

        float pellets = GetEffect("RoundRayCount", item, player, itemTags, 1f, liveHeld);
        if (pellets <= 0f && !TryGetDisplayedStatValue(PassiveEffects.RoundRayCount, item, player, out pellets))
            pellets = GetEffectWithFallback("RoundRayCount", item, player, rangedTags, 1f, liveHeld,
                itemTags, PrimaryTags);
        pellets = Mathf.Max(1f, pellets);
        float shotDamage = perProjectile * pellets;

        float rpm = GetEffect("RoundsPerMinute", item, player, itemTags, 0f, liveHeld);
        if (rpm <= 0f && !TryGetDisplayedStatValueByName("RoundsPerMinute", item, player, out rpm))
            rpm = GetEffectWithFallback("RoundsPerMinute", item, player, rangedTags, 0f, liveHeld,
                itemTags, PrimaryTags);
        float actionDelay = GetActionDelay(item, 0);
        if (rpm <= 0f && actionDelay > 0f) rpm = 60f / actionDelay;
        // Some guns carry a tag-conditional RoundsPerMinute base_set of 1 (for example the double barrel with perkBoomstick in its own tag list). The game does not apply it to the action (the gun fires a shot every fraction of a second), so reading it as 1 round per minute made the sustained DPS about 2 and inflated Shotguns training 30-50x. Prefer the live action delay, and treat an implausible rate as unresolved.
        if (liveHeld) { try { var liveRanged = player != null && player.inventory != null && player.inventory.holdingItemData != null ? player.inventory.holdingItemData.actionData[0] as ItemActionRanged.ItemActionDataRanged : null; if (liveRanged != null && liveRanged.Delay > 0.01f) rpm = 60f / liveRanged.Delay; } catch { } }
        if (rpm > 0f && rpm < 5f && actionDelay > 0f && 60f / actionDelay > rpm) rpm = 60f / actionDelay;

        float magazineRaw = GetEffect("MagazineSize", item, player, itemTags, 0f, liveHeld);
        if (magazineRaw <= 0f && !TryGetDisplayedStatValueByName("MagazineSize", item, player, out magazineRaw))
            magazineRaw = ReadAction0Numeric(item.ItemClass, 0f, "Magazine_size", "MagazineSize", "magazine_size", "magazineSize");
        if (magazineRaw <= 0f)
            magazineRaw = GetEffectWithFallback("MagazineSize", item, player, rangedTags, 1f, liveHeld,
                itemTags, PrimaryTags, 1f);
        int magazine = Math.Max(1, Mathf.RoundToInt(magazineRaw));

        float reloadSpeedMultiplier = ResolveEffectiveReloadMultiplier(player, item, skillId, liveHeld);
        object action = item.ItemClass.Actions != null && item.ItemClass.Actions.Length > 0 ? item.ItemClass.Actions[0] : null;
        float baseReload = ReadReloadSeconds(item.ItemClass, action);
        float measuredBaseReload;
        bool hasMeasuredReload = TryGetMeasuredBaseReloadSeconds(player, item, skillId, out measuredBaseReload);
        float animationBaseReload = 0f;
        bool hasAnimationReload = !hasMeasuredReload && liveHeld && TryGetHeldReloadAnimationSeconds(player, item, skillId, out animationBaseReload);
        // ReloadSpeedMultiplier is the Animator reload-speed parameter: a larger value makes the
        // animation finish sooner. Keep a neutral/base cycle here and divide by the current live
        // speed multiplier. That lets Bandolier, equipment, buffs and weapon-family skill changes
        // alter the displayed/normalization reload time before another real reload is performed.
        float neutralReload = hasMeasuredReload ? measuredBaseReload : (hasAnimationReload ? animationBaseReload : baseReload);
        float reload = neutralReload > 0f ? neutralReload / Mathf.Max(0.05f, reloadSpeedMultiplier) : 0f;
        if (ReloadTimingDebug && liveHeld)
        {
            string reloadSource = hasMeasuredReload ? "measured" : (hasAnimationReload ? "animation" : (baseReload > 0f ? "authored" : "unresolved"));
            LogReloadProfileProbe(player, item, skillId, signature, reloadSource, baseReload,
                hasAnimationReload ? animationBaseReload : 0f, hasMeasuredReload ? measuredBaseReload : 0f,
                reloadSpeedMultiplier, reload);
        }
        float ammoUse = ReadAction0Numeric(item.ItemClass, 0f,
            "BulletUsePerShot", "Bullet_use_per_shot", "bulletsUsedPerShot", "BulletsUsedPerShot", "AmmoUsePerShot", "AmmoPerShot");
        if (ammoUse <= 0f)
            ammoUse = ReadNumeric(action, 1f,
                "BulletUsePerShot", "Bullet_use_per_shot", "bulletsUsedPerShot", "BulletsUsedPerShot", "AmmoUsePerShot", "AmmoPerShot");
        int ammoPerAttack = Math.Max(1, Mathf.RoundToInt(ammoUse));
        float window = Mathf.Max(10f, RebirthProgressionRuntimeConfig.WeaponDpsSustainWindowSeconds);
        int attacks, reloads;
        float sustained = CalculateRangedSustainedDps(shotDamage, rpm, actionDelay, magazine, reload, ammoPerAttack, window, out attacks, out reloads);
        float interval = rpm > 0f ? 60f / rpm : Mathf.Max(0f, actionDelay);
        p.DamagePerAttack = perProjectile;
        p.ProjectilesPerAttack = pellets;
        p.RoundsPerMinute = rpm;
        p.MagazineSize = magazine;
        p.AmmoPerAttack = ammoPerAttack;
        p.ReloadSeconds = reload;
        p.BurstDps = interval > 0f ? shotDamage / interval : 0f;
        p.SustainedDps = sustained;
        p.AttacksInWindow = attacks;
        p.ReloadsInWindow = reloads;
        p.ReloadsPerMinute = window > 0f ? reloads * 60f / window : 0f;
        p.Valid = sustained > 0f;
        p.Detail = hasMeasuredReload ? "ranged-sustained-measured-reload"
            : (hasAnimationReload ? "ranged-sustained-animation-reload"
            : (baseReload > 0f ? "ranged-sustained-with-authored-reload" : "ranged-sustained-reload-unresolved"));
        return p;
    }

    private static float ApplyPreviewWeaponFamilySpeed(EntityPlayer player, string skillId, float apm)
    {
        if (player == null || apm <= 0f) return apm;
        float value;
        if (!RebirthSkillOutcomeValueService.TryGetEffective(player, skillId, out value)) return apm;
        float delta = RebirthWeaponFamilySkillService.SignedEndpoint(value, RebirthProgressionRuntimeConfig.MeleeSpeedNegative, RebirthProgressionRuntimeConfig.MeleeSpeedPositive);
        return Mathf.Max(0f, apm * Mathf.Max(0.05f, 1f + delta));
    }

    private static float ResolveEffectiveReloadMultiplier(EntityPlayer player, ItemValue item, string skillId, bool liveHeld)
    {
        if (item == null || item.ItemClass == null) return 1f;
        FastTags<TagGroup.Global> tags = item.ItemClass.ItemTags;
        float multiplier = GetEffect("ReloadSpeedMultiplier", item, player, tags, 1f, liveHeld);
        if (multiplier <= 0f)
            multiplier = GetEffectWithFallback("ReloadSpeedMultiplier", item, player, PrimaryTags | PhysicalDamageTags, 1f, liveHeld,
                tags, PrimaryTags, 1f);
        multiplier = Mathf.Max(0.05f, multiplier);
        if (!liveHeld && RebirthWeaponFamilySkillService.IsRangedSkill(skillId)) multiplier = ApplyPreviewWeaponFamilyReload(player, skillId, multiplier);
        return Mathf.Max(0.05f, multiplier);
    }

    private static float ApplyPreviewWeaponFamilyReload(EntityPlayer player, string skillId, float multiplier)
    {
        if (player == null || multiplier <= 0f) return multiplier;
        float value;
        if (!RebirthSkillOutcomeValueService.TryGetEffective(player, skillId, out value)) return multiplier;
        float delta = RebirthWeaponFamilySkillService.SignedEndpoint(value, RebirthProgressionRuntimeConfig.RangedReloadNegative, RebirthProgressionRuntimeConfig.RangedReloadPositive);
        return Mathf.Max(0.05f, multiplier * Mathf.Max(0.05f, 1f + delta));
    }

    private static float ApplyPreviewWeaponFamilyStamina(EntityPlayer player, string skillId, float stamina)
    {
        if (player == null || stamina <= 0f) return stamina;
        float value;
        if (!RebirthSkillOutcomeValueService.TryGetEffective(player, skillId, out value)) return stamina;
        float delta = RebirthWeaponFamilySkillService.SignedEndpoint(value, RebirthProgressionRuntimeConfig.MeleeStaminaNegative, RebirthProgressionRuntimeConfig.MeleeStaminaPositive);
        return Mathf.Max(0f, stamina * Mathf.Max(0.05f, 1f + delta));
    }

    private static bool TryGetDisplayedStatValueByName(string effectName, ItemValue item, EntityPlayer player, out float value)
    {
        value = 0f;
        PassiveEffects effect;
        if (!Enum.TryParse(effectName, true, out effect)) return false;
        return TryGetDisplayedStatValue(effect, item, player, out value);
    }

    private static bool TryGetDisplayedStatValue(PassiveEffects effect, ItemValue item, EntityPlayer player, out float value)
    {
        value = 0f;
        if (item == null || item.ItemClass == null || UIDisplayInfoManager.Current == null) return false;
        try
        {
            var display = UIDisplayInfoManager.Current.GetDisplayStatsForTag(item.ItemClass.DisplayType);
            if (display == null || display.DisplayStats == null) return false;
            DisplayInfoEntry stat = null;
            for (int i = 0; i < display.DisplayStats.Count; i++)
            {
                DisplayInfoEntry candidate = display.DisplayStats[i];
                if (candidate != null && candidate.StatType == effect) { stat = candidate; break; }
            }
            FastTags<TagGroup.Global> tags = stat != null && stat.TagsSet
                ? stat.Tags
                : (effect == PassiveEffects.EntityDamage || effect == PassiveEffects.BlockDamage
                    ? PrimaryTags | PhysicalDamageTags
                    : item.ItemClass.ItemTags);
            float resolved = EffectManager.GetValue(effect, item, _entity: player, tags: tags,
                calcEquipment: false, calcHoldingItem: false, calcProgression: false, calcBuffs: false, useMods: true);
            try { XUiM_ItemStack.degradationMaxMod(effect, item, player, tags, true, ref resolved); } catch { }
            if (!Finite(resolved) || resolved <= 0f) return false;
            value = resolved;
            return true;
        }
        catch { return false; }
    }

    private static bool TryGetDisplayedStatValueByTitle(string titleNeedle, ItemValue item, EntityPlayer player, bool includeLiveBuffs, out float value)
    {
        value = 0f;
        if (item == null || item.ItemClass == null || UIDisplayInfoManager.Current == null || string.IsNullOrEmpty(titleNeedle)) return false;
        try
        {
            var display = UIDisplayInfoManager.Current.GetDisplayStatsForTag(item.ItemClass.DisplayType);
            if (display == null || display.DisplayStats == null) return false;
            DisplayInfoEntry stat = null;
            for (int i = 0; i < display.DisplayStats.Count; i++)
            {
                DisplayInfoEntry candidate = display.DisplayStats[i];
                if (candidate == null) continue;
                string title = !string.IsNullOrEmpty(candidate.TitleOverride) ? candidate.TitleOverride : (UIDisplayInfoManager.Current.GetLocalizedName(candidate.StatType) ?? string.Empty);
                if (title.IndexOf(titleNeedle, StringComparison.OrdinalIgnoreCase) >= 0) { stat = candidate; break; }
            }
            if (stat == null) return false;
            FastTags<TagGroup.Global> tags = stat.TagsSet ? stat.Tags : item.ItemClass.ItemTags;
            float resolved = EffectManager.GetValue(stat.StatType, item, _entity: player, tags: tags,
                calcEquipment: player != null, calcHoldingItem: false, calcProgression: player != null,
                calcBuffs: player != null && includeLiveBuffs, useMods: true);
            try { XUiM_ItemStack.degradationMaxMod(stat.StatType, item, player, tags, true, ref resolved); } catch { }
            if (!Finite(resolved) || resolved <= 0f) return false;
            value = resolved;
            return true;
        }
        catch { return false; }
    }

    // Enum.TryParse on a string allocates and is slow; the set of effect names is tiny and fixed, so parse each once.
    private static readonly Dictionary<string, PassiveEffects> ParsedEffects = new Dictionary<string, PassiveEffects>(StringComparer.OrdinalIgnoreCase);

    private static bool TryGetPassiveEffect(string name, out PassiveEffects effect)
    {
        if (name == null) { effect = default(PassiveEffects); return false; }
        lock (ParsedEffects)
        {
            if (ParsedEffects.TryGetValue(name, out effect)) return true;
        }
        if (!Enum.TryParse(name, true, out effect)) return false;
        lock (ParsedEffects) ParsedEffects[name] = effect;
        return true;
    }

    private static float GetEffect(string name, ItemValue item, EntityPlayer player, FastTags<TagGroup.Global> tags, float baseValue, bool includeLiveBuffs)
    {
        PassiveEffects effect;
        if (!TryGetPassiveEffect(name, out effect)) return baseValue;
        try
        {
            float value = EffectManager.GetValue(effect, item, baseValue, _entity: player, tags: tags,
                calcEquipment: player != null, calcHoldingItem: false, calcProgression: player != null,
                calcBuffs: player != null && includeLiveBuffs, useMods: true);
            // Match the game's item-stat presentation path for durability/wear effects as well.
            // This matters for damage-derived DPS because a worn weapon must not train or display
            // against a pristine theoretical value. Fail open if the runtime does not expose it.
            try { XUiM_ItemStack.degradationMaxMod(effect, item, player, tags, true, ref value); } catch { }
            return value;
        }
        catch { return baseValue; }
    }


    private static float GetEffectWithFallback(string name, ItemValue item, EntityPlayer player,
        FastTags<TagGroup.Global> primaryTags, float baseValue, bool includeLiveBuffs,
        params object[] fallbacks)
    {
        float value = GetEffect(name, item, player, primaryTags, baseValue, includeLiveBuffs);
        if (IsResolvedEffect(value, baseValue)) return value;
        for (int i = 0; fallbacks != null && i < fallbacks.Length; i++)
        {
            object fallback = fallbacks[i];
            if (fallback == null) continue;
            if (fallback is FastTags<TagGroup.Global>)
            {
                value = GetEffect(name, item, player, (FastTags<TagGroup.Global>)fallback, baseValue, includeLiveBuffs);
                if (IsResolvedEffect(value, baseValue)) return value;
            }
            else
            {
                try
                {
                    float numeric = Convert.ToSingle(fallback, CultureInfo.InvariantCulture);
                    if (Finite(numeric) && (numeric > 0f || (baseValue > 0f && Mathf.Abs(numeric - baseValue) > 0.0001f)))
                        return numeric;
                }
                catch { }
            }
        }
        return value;
    }

    private static bool IsResolvedEffect(float value, float baseValue)
    {
        if (!Finite(value)) return false;
        if (baseValue > 0f) return Mathf.Abs(value - baseValue) > 0.0001f || value > 0f;
        return value > 0f;
    }

    private static float GetActionDelay(ItemValue item, int index)
    {
        if (item == null || item.ItemClass == null || item.ItemClass.Actions == null || index < 0 || index >= item.ItemClass.Actions.Length || item.ItemClass.Actions[index] == null) return 0f;
        try { return Mathf.Max(0f, item.ItemClass.Actions[index].Delay); }
        catch { return Mathf.Max(0f, ReadNumeric(item.ItemClass.Actions[index], 0f, "Delay", "delay", "ActionDelay", "actionDelay")); }
    }

    private static float ReadReloadSeconds(ItemClass itemClass, object action)
    {
        // Reload_time is authored inside the Action0 DynamicProperties class in 7DTD items.xml.
        // Read that exact class first. The previous GetString("Action0", "Reload_time") call did
        // not traverse the class hierarchy and therefore returned zero for vanilla firearms.
        float exact = ReadAction0Numeric(itemClass, 0f,
            "Reload_time", "reload_time", "ReloadTime", "reloadTime", "ReloadDuration", "reloadDuration",
            "ReloadTimeFull", "reloadTimeFull", "BaseReloadTime", "baseReloadTime");
        if (exact > 0f) return exact;

        exact = ReadNumeric(action, 0f,
            "ReloadTime", "reloadTime", "Reload_time", "reload_time", "ReloadDuration", "reloadDuration",
            "ReloadTimeFull", "reloadTimeFull", "BaseReloadTime", "baseReloadTime");
        if (exact > 0f) return exact;

        exact = Mathf.Max(0f, ReadNumericByTokens(action, "reload", "time", "duration"));
        if (exact > 0f) return exact;

        if (itemClass != null)
        {
            exact = ReadDynamicNumericExact(itemClass.Properties,
                "Reload_time", "reload_time", "ReloadTime", "reloadTime", "ReloadDuration", "reloadDuration",
                "ReloadTimeFull", "reloadTimeFull", "BaseReloadTime", "baseReloadTime");
            if (exact > 0f) return exact;

            exact = ReadDynamicNumericByTokens(itemClass.Properties,
                new string[] { "reload" }, new string[] { "speed", "multiplier" },
                new string[] { "time", "duration" });
            if (exact > 0f) return exact;
        }

        // In vanilla 3.2 some firearms intentionally omit Action0.Reload_time and carry their
        // native base reload duration only as the documented value beside ReloadSpeedMultiplier.
        // Runtime reflection therefore reports zero even though the weapon has a real reload.
        // Keep this as a last-resort exact-item fallback; the live ReloadSpeedMultiplier is still
        // applied after this value, so quality/mod/skill changes remain dynamic.
        if (itemClass != null)
        {
            string name = itemClass.GetItemName() ?? string.Empty;
            if (string.Equals(name, "gunHandgunT0PipePistol", StringComparison.OrdinalIgnoreCase)) return 2f;
            if (string.Equals(name, "gunHandgunT1Pistol", StringComparison.OrdinalIgnoreCase)) return 2f;
            if (string.Equals(name, "gunHandgunT3SMG5", StringComparison.OrdinalIgnoreCase)) return 4.1f;
            if (string.Equals(name, "gunHandgunT2Magnum44", StringComparison.OrdinalIgnoreCase)) return 4f;
            if (string.Equals(name, "gunHandgunT3DesertVulture", StringComparison.OrdinalIgnoreCase)) return 2f;
        }
        return 0f;
    }

    private static float ReadAction0Numeric(ItemClass itemClass, float fallback, params string[] names)
    {
        if (itemClass == null || itemClass.Properties == null || itemClass.Properties.Classes == null)
            return fallback;
        DynamicProperties action0;
        if (!itemClass.Properties.Classes.TryGetValue("Action0", out action0) || action0 == null || action0.Values == null)
            return fallback;
        for (int i = 0; names != null && i < names.Length; i++)
        {
            string raw;
            if (!action0.Values.TryGetValue(names[i], out raw) || string.IsNullOrEmpty(raw)) continue;
            float value;
            if (float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && Finite(value) && value > 0f)
                return value;
        }
        return fallback;
    }

    private static float ReadDynamicNumericExact(DynamicProperties properties, params string[] names)
    {
        if (properties == null) return 0f;
        if (properties.Values != null)
        {
            foreach (KeyValuePair<string, string> pair in properties.Values)
            {
                for (int i = 0; names != null && i < names.Length; i++)
                {
                    if (!string.Equals(pair.Key, names[i], StringComparison.OrdinalIgnoreCase)) continue;
                    float value;
                    if (float.TryParse(pair.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && Finite(value) && value > 0f)
                        return value;
                }
            }
        }
        if (properties.Classes != null)
        {
            foreach (KeyValuePair<string, DynamicProperties> pair in properties.Classes)
            {
                float value = ReadDynamicNumericExact(pair.Value, names);
                if (value > 0f) return value;
            }
        }
        return 0f;
    }

    private static float ReadDynamicNumericByTokens(DynamicProperties properties, string[] required, string[] excluded, params string[] alternatives)
    {
        if (properties == null) return 0f;
        if (properties.Values != null)
        {
            foreach (KeyValuePair<string, string> pair in properties.Values)
            {
                string key = pair.Key ?? string.Empty;
                bool ok = true;
                for (int i = 0; required != null && i < required.Length; i++)
                    if (key.IndexOf(required[i], StringComparison.OrdinalIgnoreCase) < 0) { ok = false; break; }
                if (!ok) continue;
                for (int i = 0; excluded != null && i < excluded.Length; i++)
                    if (key.IndexOf(excluded[i], StringComparison.OrdinalIgnoreCase) >= 0) { ok = false; break; }
                if (!ok) continue;
                if (alternatives != null && alternatives.Length > 0)
                {
                    bool any = false;
                    for (int i = 0; i < alternatives.Length; i++)
                        if (key.IndexOf(alternatives[i], StringComparison.OrdinalIgnoreCase) >= 0) { any = true; break; }
                    if (!any) continue;
                }
                float value;
                if (float.TryParse(pair.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && Finite(value) && value > 0f)
                    return value;
            }
        }
        if (properties.Classes != null)
        {
            foreach (KeyValuePair<string, DynamicProperties> pair in properties.Classes)
            {
                float value = ReadDynamicNumericByTokens(pair.Value, required, excluded, alternatives);
                if (value > 0f) return value;
            }
        }
        return 0f;
    }

    private static float ReadNumericByTokens(object instance, string required, params string[] alternatives)
    {
        if (instance == null) return 0f;
        Type type = instance.GetType();
        while (type != null)
        {
            FieldInfo[] fields; PropertyInfo[] props;
            try { fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic); } catch { fields = new FieldInfo[0]; }
            for (int i = 0; i < fields.Length; i++)
            {
                string name = fields[i].Name ?? string.Empty;
                if (!TokenMatch(name, required, alternatives)) continue;
                try { float value; if (TryNumber(fields[i].GetValue(instance), out value) && value > 0f) return value; } catch { }
            }
            try { props = type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic); } catch { props = new PropertyInfo[0]; }
            for (int i = 0; i < props.Length; i++)
            {
                string name = props[i].Name ?? string.Empty;
                if (props[i].GetIndexParameters().Length != 0 || !TokenMatch(name, required, alternatives)) continue;
                try { float value; if (TryNumber(props[i].GetValue(instance, null), out value) && value > 0f) return value; } catch { }
            }
            type = type.BaseType;
        }
        return 0f;
    }

    private static bool TokenMatch(string name, string required, string[] alternatives)
    {
        if (string.IsNullOrEmpty(name) || name.IndexOf(required, StringComparison.OrdinalIgnoreCase) < 0) return false;
        if (name.IndexOf("speed", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("multiplier", StringComparison.OrdinalIgnoreCase) >= 0) return false;
        for (int i = 0; alternatives != null && i < alternatives.Length; i++)
            if (name.IndexOf(alternatives[i], StringComparison.OrdinalIgnoreCase) >= 0) return true;
        return false;
    }

    private static float ReadNumeric(object instance, float fallback, params string[] names)
    {
        if (instance == null) return fallback;
        Type type = instance.GetType();
        while (type != null)
        {
            for (int i = 0; i < names.Length; i++)
            {
                try
                {
                    FieldInfo field = type.GetField(names[i], BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.IgnoreCase);
                    if (field != null && TryNumber(field.GetValue(instance), out float fv)) return fv;
                    PropertyInfo prop = type.GetProperty(names[i], BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.IgnoreCase);
                    if (prop != null && prop.GetIndexParameters().Length == 0 && TryNumber(prop.GetValue(instance, null), out float pv)) return pv;
                }
                catch { }
            }
            type = type.BaseType;
        }
        return fallback;
    }

    private static bool TryNumber(object value, out float number)
    {
        number = 0f;
        if (value == null) return false;
        try
        {
            number = Convert.ToSingle(value, CultureInfo.InvariantCulture);
            return Finite(number);
        }
        catch { return false; }
    }

    private static bool TryGetHeldReloadAnimationSeconds(EntityPlayer player, ItemValue item, string skillId, out float seconds)
    {
        seconds = 0f;
        if (player == null || player.inventory == null || item == null || item.IsEmpty()) return false;
        try
        {
            string itemName = item.ItemClass != null ? (item.ItemClass.GetItemName() ?? string.Empty) : string.Empty;
            string family = string.Empty;
            if (!string.IsNullOrEmpty(skillId))
            {
                int dot = skillId.LastIndexOf('.');
                family = (dot >= 0 ? skillId.Substring(dot + 1) : skillId).Replace("_", string.Empty);
            }

            // The first-person held-item/arms animator is not guaranteed to be under
            // EntityPlayer.transform (and it can legitimately be disabled/inactive while the
            // inventory UI is open). Read clip metadata from the actual held model first, then
            // fall back to the player hierarchy. Clip metadata remains valid when an Animator is
            // disabled, so do not discard those animators.
            HashSet<Animator> seen = new HashSet<Animator>();
            int bestScore = int.MinValue;
            float bestLength = 0f;

            ItemInventoryData heldData = null;
            try { heldData = player.inventory.holdingItemData; } catch { }
            if (heldData != null)
                ScanReloadAnimationRoot(heldData.model, 100, itemName, family, seen, ref bestScore, ref bestLength);

            Transform heldTransform = null;
            try { heldTransform = player.inventory.GetHoldingItemTransform(); } catch { }
            ScanReloadAnimationRoot(heldTransform, 90, itemName, family, seen, ref bestScore, ref bestLength);
            ScanReloadAnimationRoot(player.transform, 0, itemName, family, seen, ref bestScore, ref bestLength);

            if (bestLength <= 0f) return false;
            seconds = bestLength;
            return true;
        }
        catch { return false; }
    }

    private static void ScanReloadAnimationRoot(Transform root, int rootScore, string itemName, string family,
        HashSet<Animator> seen, ref int bestScore, ref float bestLength)
    {
        if (root == null || seen == null) return;
        Animator[] animators;
        try
        {
            var hierarchy=ReloadHierarchies.GetValue(root,key=>new ReloadHierarchyMetadata());
            if(hierarchy.Animators==null||Time.realtimeSinceStartup>=hierarchy.RefreshAt)
            {hierarchy.Animators=root.GetComponentsInChildren<Animator>(true);hierarchy.RefreshAt=Time.realtimeSinceStartup+1f;}
            animators=hierarchy.Animators;
        }
        catch { return; }

        for (int i = 0; animators != null && i < animators.Length; i++)
        {
            Animator animator = animators[i];
            if (animator == null || !seen.Add(animator) || animator.runtimeAnimatorController == null) continue;
            ReloadAnimatorMetadata metadata;
            try { metadata=GetReloadMetadata(animator); }
            catch { continue; }
            string animatorName=metadata.AnimatorName,controllerName=metadata.ControllerName;
            AnimationClip[] clips=metadata.Clips;
            int roleScore=metadata.RoleScore;

            for (int c = 0; clips != null && c < clips.Length; c++)
            {
                AnimationClip clip = clips[c];
                if (clip == null || clip.length <= 0.05f || clip.length > 20f) continue;
                string clipName = (clip.name ?? string.Empty).Replace("_", string.Empty);
                if (clipName.IndexOf("reload", StringComparison.OrdinalIgnoreCase) < 0) continue;

                int score = rootScore + roleScore + 10;
                if (!string.IsNullOrEmpty(family) && (clipName.IndexOf(family, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    animatorName.IndexOf(family, StringComparison.OrdinalIgnoreCase) >= 0 || controllerName.IndexOf(family, StringComparison.OrdinalIgnoreCase) >= 0)) score += 8;
                if (itemName.IndexOf("Shotgun", StringComparison.OrdinalIgnoreCase) >= 0 && (clipName.IndexOf("shotgun", StringComparison.OrdinalIgnoreCase) >= 0 || controllerName.IndexOf("shotgun", StringComparison.OrdinalIgnoreCase) >= 0)) score += 6;
                if (itemName.IndexOf("Pistol", StringComparison.OrdinalIgnoreCase) >= 0 && (clipName.IndexOf("pistol", StringComparison.OrdinalIgnoreCase) >= 0 || controllerName.IndexOf("pistol", StringComparison.OrdinalIgnoreCase) >= 0)) score += 6;
                if (itemName.IndexOf("Rifle", StringComparison.OrdinalIgnoreCase) >= 0 && (clipName.IndexOf("rifle", StringComparison.OrdinalIgnoreCase) >= 0 || controllerName.IndexOf("rifle", StringComparison.OrdinalIgnoreCase) >= 0)) score += 6;
                if (itemName.IndexOf("Pipe", StringComparison.OrdinalIgnoreCase) >= 0 && (clipName.IndexOf("pipe", StringComparison.OrdinalIgnoreCase) >= 0 || controllerName.IndexOf("pipe", StringComparison.OrdinalIgnoreCase) >= 0)) score += 4;

                if (score > bestScore || (score == bestScore && clip.length > bestLength))
                {
                    bestScore = score;
                    bestLength = clip.length;
                }
            }
        }
    }


    private static int GetReloadAnimatorRoleScore(Animator animator)
    {
        if(animator==null)return 0;
        try{return GetReloadMetadata(animator).RoleScore;}catch{return 0;}
    }
    private static int ReadReloadAnimatorRoleScore(Animator animator)
    {
        if (animator == null) return 0;
        try
        {
            StateMachineBehaviour[] behaviours = animator.GetBehaviours<StateMachineBehaviour>();
            int score = 0;
            for (int i = 0; behaviours != null && i < behaviours.Length; i++)
            {
                StateMachineBehaviour behaviour = behaviours[i];
                if (behaviour == null) continue;
                string typeName = behaviour.GetType().Name ?? string.Empty;
                if (string.Equals(typeName, "AnimatorRangedReloadState", StringComparison.Ordinal)) return 1000;
                if (string.Equals(typeName, "AnimatorWeaponRangedReloadState", StringComparison.Ordinal)) score = Math.Max(score, 400);
            }
            return score;
        }
        catch { return 0; }
    }

    public static void OnRuntimeReloadStarted(EntityPlayer player)
    {
        if (player == null || player.inventory == null) return;
        ItemValue held = player.inventory.holdingItemItemValue;
        if (held == null || held.IsEmpty() || held.ItemClass == null) return;
        string skillId = RebirthProgressionRuntimeConfig.ClassifyCombat(held);
        if (!RebirthWeaponFamilySkillService.IsRangedSkill(skillId)) return;
        string key = BuildReloadMeasurementKey(player, held, skillId);
        float effectiveMultiplier = ResolveEffectiveReloadMultiplier(player, held, skillId, true);
        lock (Gate)
        {
            ActiveReloads[player.entityId] = new RuntimeReloadStart
            {
                Key = key,
                ItemName = held.ItemClass.GetItemName() ?? string.Empty,
                StartedAt = Time.realtimeSinceStartup,
                EffectiveReloadMultiplier = effectiveMultiplier
            };
        }
        if (ReloadTimingDebug)
        {
            Log.Out("[REBIRTH RELOAD TIMING] START item='" + (held.ItemClass.GetItemName() ?? string.Empty) +
                "' entity=" + player.entityId.ToString(CultureInfo.InvariantCulture) +
                " authored=" + ReadReloadSeconds(held.ItemClass, held.ItemClass.Actions != null && held.ItemClass.Actions.Length > 0 ? held.ItemClass.Actions[0] : null).ToString("0.###", CultureInfo.InvariantCulture) +
                " effectiveMultiplier=" + effectiveMultiplier.ToString("0.###", CultureInfo.InvariantCulture));
            LogReloadAnimationClips(player);
        }
    }

    public static void OnRuntimeReloadCompleted(EntityPlayer player)
    {
        if (player == null) return;
        RuntimeReloadStart start = null;
        lock (Gate)
        {
            ActiveReloads.TryGetValue(player.entityId, out start);
            ActiveReloads.Remove(player.entityId);
        }
        if (start == null) return;
        float elapsed = Time.realtimeSinceStartup - start.StartedAt;
        if (!Finite(elapsed) || elapsed < 0.05f || elapsed > 30f) return;
        float multiplier = Mathf.Max(0.05f, start.EffectiveReloadMultiplier);
        // Runtime elapsed is neutralDuration / ReloadSpeedMultiplier, so recover the neutral
        // cycle by multiplying. The neutral value can then be reused with future live modifiers.
        float normalizedBase = elapsed * multiplier;
        lock (Gate)
        {
            MeasuredBaseReloadSeconds[start.Key] = normalizedBase;
            Cache.Clear();
            RecentHeld.Clear();
        }
        if (ReloadTimingDebug)
            Log.Out("[REBIRTH RELOAD TIMING] COMPLETE item='" + start.ItemName +
                "' entity=" + player.entityId.ToString(CultureInfo.InvariantCulture) +
                " measuredEffective=" + elapsed.ToString("0.###", CultureInfo.InvariantCulture) + "s" +
                " effectiveMultiplier=" + multiplier.ToString("0.###", CultureInfo.InvariantCulture) +
                " normalizedBase=" + normalizedBase.ToString("0.###", CultureInfo.InvariantCulture) + "s");
    }

    private static bool TryGetMeasuredBaseReloadSeconds(EntityPlayer player, ItemValue item, string skillId, out float seconds)
    {
        seconds = 0f;
        if (player == null || item == null || item.IsEmpty()) return false;
        string key = BuildReloadMeasurementKey(player, item, skillId);
        lock (Gate)
            return MeasuredBaseReloadSeconds.TryGetValue(key, out seconds) && Finite(seconds) && seconds > 0f;
    }

    private static string BuildReloadMeasurementKey(EntityPlayer player, ItemValue item, string skillId)
    {
        unchecked
        {
            int h = 17;
            h = h * 31 + (item != null ? item.type : 0);
            h = h * 31 + (item != null ? item.Quality : 0);
            h = h * 31 + (item != null ? item.Seed : 0);
            h = h * 31 + (item != null ? item.SelectedAmmoTypeIndex : 0);
            ItemValue[] mods = item != null ? item.modifications : null;
            h = h * 31 + (mods != null ? mods.Length : 0);
            for (int i = 0; mods != null && i < mods.Length; i++)
            {
                ItemValue mod = mods[i];
                h = h * 31 + (mod != null ? mod.type : 0);
                h = h * 31 + (mod != null ? mod.Quality : 0);
                h = h * 31 + (mod != null ? mod.Seed : 0);
            }
            // Do not key the normalized base cycle by player reload buffs or skill. Those are
            // applied live after measurement so Bandolier and other reload modifiers update immediately.
            return (player != null ? player.entityId.ToString(CultureInfo.InvariantCulture) : "none") + "|" + h.ToString(CultureInfo.InvariantCulture);
        }
    }

    private static void LogReloadProfileProbe(EntityPlayer player, ItemValue item, string skillId, int signature,
        string source, float authoredBase, float animationBase, float measuredBase, float multiplier, float effectiveReload)
    {
        if (!ReloadTimingDebug || player == null || item == null || item.ItemClass == null) return;
        string itemName = item.ItemClass.GetItemName() ?? string.Empty;
        string key = player.entityId.ToString(CultureInfo.InvariantCulture) + "|" +
            signature.ToString(CultureInfo.InvariantCulture) + "|" + (source ?? string.Empty);
        lock (Gate)
        {
            if (!ReloadProfileDebugKeys.Add(key)) return;
            if (ReloadProfileDebugKeys.Count > 256) ReloadProfileDebugKeys.Clear();
        }

        ItemInventoryData heldData = null;
        Transform heldModel = null;
        Transform heldTransform = null;
        try { heldData = player.inventory != null ? player.inventory.holdingItemData : null; } catch { }
        try { heldModel = heldData != null ? heldData.model : null; } catch { }
        try { heldTransform = player.inventory != null ? player.inventory.GetHoldingItemTransform() : null; } catch { }

        Log.Out("[REBIRTH RELOAD TIMING] PROFILE item='" + itemName +
            "' entity=" + player.entityId.ToString(CultureInfo.InvariantCulture) +
            " skill='" + (skillId ?? string.Empty) + "'" +
            " source=" + (source ?? string.Empty) +
            " authored=" + authoredBase.ToString("0.###", CultureInfo.InvariantCulture) +
            " animation=" + animationBase.ToString("0.###", CultureInfo.InvariantCulture) +
            " measuredBase=" + measuredBase.ToString("0.###", CultureInfo.InvariantCulture) +
            " effectiveMultiplier=" + multiplier.ToString("0.###", CultureInfo.InvariantCulture) +
            " finalReload=" + effectiveReload.ToString("0.###", CultureInfo.InvariantCulture) +
            " heldData=" + (heldData != null ? "yes" : "no") +
            " heldModel=" + DescribeTransform(heldModel) +
            " heldTransform=" + DescribeTransform(heldTransform));

        LogReloadAnimationClips(player, true);
    }

    private static string DescribeTransform(Transform transform)
    {
        if (transform == null) return "<null>";
        try
        {
            return "'" + (transform.name ?? string.Empty) + "'#" + transform.GetInstanceID().ToString(CultureInfo.InvariantCulture) +
                " active=" + (transform.gameObject != null && transform.gameObject.activeInHierarchy ? "1" : "0");
        }
        catch { return "<unavailable>"; }
    }

    private static void LogReloadAnimationClips(EntityPlayer player, bool includeAllClips = false)
    {
        if (!ReloadTimingDebug || player == null || player.inventory == null) return;
        try
        {
            HashSet<Animator> seen = new HashSet<Animator>();
            List<Animator> animators = new List<Animator>();
            ItemInventoryData heldData = null;
            try { heldData = player.inventory.holdingItemData; } catch { }
            if (heldData != null) AddReloadDebugAnimators(heldData.model, seen, animators);
            Transform heldTransform = null;
            try { heldTransform = player.inventory.GetHoldingItemTransform(); } catch { }
            AddReloadDebugAnimators(heldTransform, seen, animators);
            AddReloadDebugAnimators(player.transform, seen, animators);

            Log.Out("[REBIRTH RELOAD TIMING] ROOTS heldData=" + (heldData != null ? "yes" : "no") +
                " heldModel=" + DescribeTransform(heldData != null ? heldData.model : null) +
                " heldTransform=" + DescribeTransform(heldTransform) +
                " playerRoot=" + DescribeTransform(player.transform) +
                " animatorCount=" + animators.Count.ToString(CultureInfo.InvariantCulture));

            int reloadNamedCount = 0;
            int detailedAnimatorCount = 0;
            for (int i = 0; i < animators.Count && detailedAnimatorCount < 16; i++)
            {
                Animator animator = animators[i];
                if (animator == null) continue;
                RuntimeAnimatorController controller = animator.runtimeAnimatorController;
                AnimationClip[] clips = controller != null ? controller.animationClips : null;
                int roleScore = GetReloadAnimatorRoleScore(animator);
                Log.Out("[REBIRTH RELOAD TIMING] ANIMATOR name='" + (animator.name ?? string.Empty) +
                    "' controller='" + (controller != null ? (controller.name ?? string.Empty) : "<null>") +
                    "' id=" + animator.GetInstanceID().ToString(CultureInfo.InvariantCulture) +
                    " active=" + (animator.gameObject != null && animator.gameObject.activeInHierarchy ? "1" : "0") +
                    " enabled=" + (animator.enabled ? "1" : "0") +
                    " initialized=" + (animator.isInitialized ? "1" : "0") +
                    " layers=" + animator.layerCount.ToString(CultureInfo.InvariantCulture) +
                    " roleScore=" + roleScore.ToString(CultureInfo.InvariantCulture) +
                    " clipCount=" + (clips != null ? clips.Length : 0).ToString(CultureInfo.InvariantCulture));
                detailedAnimatorCount++;

                int allClipLogged = 0;
                for (int c = 0; clips != null && c < clips.Length; c++)
                {
                    AnimationClip clip = clips[c];
                    if (clip == null) continue;
                    bool reloadNamed = (clip.name ?? string.Empty).IndexOf("reload", StringComparison.OrdinalIgnoreCase) >= 0;
                    if (!reloadNamed && (!includeAllClips || allClipLogged >= 40)) continue;
                    if (reloadNamed) reloadNamedCount++;
                    else allClipLogged++;
                    Log.Out("[REBIRTH RELOAD TIMING] CLIP animator='" + (animator.name ?? string.Empty) +
                        "' controller='" + (controller != null ? (controller.name ?? string.Empty) : "<null>") +
                        "' clip='" + (clip.name ?? string.Empty) +
                        "' length=" + clip.length.ToString("0.###", CultureInfo.InvariantCulture) +
                        " reloadNamed=" + (reloadNamed ? "1" : "0") +
                        " animatorSpeed=" + animator.speed.ToString("0.###", CultureInfo.InvariantCulture));
                }

                try
                {
                    for (int layer = 0; layer < animator.layerCount; layer++)
                    {
                        AnimatorClipInfo[] current = animator.GetCurrentAnimatorClipInfo(layer);
                        for (int ci = 0; current != null && ci < current.Length; ci++)
                        {
                            AnimationClip clip = current[ci].clip;
                            if (clip == null) continue;
                            Log.Out("[REBIRTH RELOAD TIMING] CURRENT layer=" + layer.ToString(CultureInfo.InvariantCulture) +
                                " animator='" + (animator.name ?? string.Empty) +
                                "' clip='" + (clip.name ?? string.Empty) +
                                "' length=" + clip.length.ToString("0.###", CultureInfo.InvariantCulture) +
                                " weight=" + current[ci].weight.ToString("0.###", CultureInfo.InvariantCulture));
                        }
                    }
                }
                catch { }
            }
            if (reloadNamedCount == 0)
                Log.Out("[REBIRTH RELOAD TIMING] CLIP no reload-named clips found on the held model/player hierarchy; authored Reload_time fallback will be used until a runtime reload is measured.");
        }
        catch (Exception ex)
        {
            Log.Warning("[REBIRTH RELOAD TIMING] CLIP inspection failed: " + ex.GetType().Name + ": " + ex.Message);
        }
    }

    private static void AddReloadDebugAnimators(Transform root, HashSet<Animator> seen, List<Animator> output)
    {
        if (root == null || seen == null || output == null) return;
        Animator[] found;
        try { found = root.GetComponentsInChildren<Animator>(true); }
        catch { return; }
        for (int i = 0; found != null && i < found.Length; i++)
        {
            Animator animator = found[i];
            if (animator != null && seen.Add(animator)) output.Add(animator);
        }
    }

    private static bool IsCurrentHeldItem(EntityPlayer player, ItemValue item)
    {
        if (player == null || player.inventory == null || item == null) return false;
        ItemValue held = player.inventory.holdingItemItemValue;
        return held != null && !held.IsEmpty() && BuildItemSignature(held) == BuildItemSignature(item);
    }

    private static string RecentHeldKey(EntityPlayer player, ItemValue item)
    {
        return (player != null ? player.entityId.ToString(CultureInfo.InvariantCulture) : "none") + "|" +
            BuildItemSignature(item).ToString(CultureInfo.InvariantCulture);
    }

    private static void RememberRecentHeld(EntityPlayer player, ItemValue item, Profile profile, float now)
    {
        if (player == null || item == null || profile == null || !profile.Valid) return;
        string key = RecentHeldKey(player, item);
        if (RecentHeld.Count > 128) RecentHeld.Clear();
        RecentHeld[key] = new CacheEntry { Signature = profile.Signature, LiveHeld = true, BuiltAt = now, Profile = profile };
    }

    private static int BuildSignature(EntityPlayer player, ItemValue item, string skillId, bool liveHeld)
    {
        unchecked
        {
            int h = BuildItemSignature(item);
            h = h * 31 + (skillId == null ? 0 : StringComparer.OrdinalIgnoreCase.GetHashCode(skillId));
            h = h * 31 + (liveHeld ? 1 : 0);
            if (player != null)
            {
                float value;
                if (RebirthSkillOutcomeValueService.TryGetEffective(player, skillId, out value)) h = h * 31 + Mathf.RoundToInt(value * 100f);
                if (player.Buffs != null)
                {
                    h = h * 31 + Mathf.RoundToInt(player.Buffs.GetCustomVar(RebirthWeaponFamilySkillService.MeleeSpeedCVar) * 10000f);
                    h = h * 31 + Mathf.RoundToInt(player.Buffs.GetCustomVar(RebirthWeaponFamilySkillService.RangedReloadCVar) * 10000f);
                }
                if (item != null && RebirthWeaponFamilySkillService.IsRangedSkill(skillId))
                    h = h * 31 + Mathf.RoundToInt(ResolveEffectiveReloadMultiplier(player, item, skillId, liveHeld) * 10000f);
            }
            return h;
        }
    }

    private static int BuildItemSignature(ItemValue value)
    {
        if (value == null || value.IsEmpty()) return 0;
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + value.type;
            hash = hash * 31 + value.Meta;
            hash = hash * 31 + value.Quality;
            hash = hash * 31 + value.Seed;
            hash = hash * 31 + value.UseTimes.GetHashCode();
            hash = hash * 31 + value.SelectedAmmoTypeIndex;
            if (value.Metadata != null && value.Metadata.Count > 0)
            {
                List<string> keys = new List<string>(value.Metadata.Keys);
                keys.Sort(StringComparer.Ordinal);
                for (int i = 0; i < keys.Count; i++)
                {
                    string key = keys[i] ?? string.Empty;
                    object metadata = value.Metadata[key];
                    hash = hash * 31 + StringComparer.Ordinal.GetHashCode(key);
                    hash = hash * 31 + (metadata != null ? metadata.GetHashCode() : 0);
                }
            }
            ItemValue[] mods = value.modifications;
            hash = hash * 31 + (mods != null ? mods.Length : 0);
            for (int i = 0; mods != null && i < mods.Length; i++)
            {
                ItemValue mod = mods[i];
                hash = hash * 31 + (mod != null ? mod.type : 0);
                hash = hash * 31 + (mod != null ? mod.Quality : 0);
                hash = hash * 31 + (mod != null ? mod.Seed : 0);
            }
            ItemValue[] cosmetics = value.cosmeticMods;
            hash = hash * 31 + (cosmetics != null ? cosmetics.Length : 0);
            for (int i = 0; cosmetics != null && i < cosmetics.Length; i++)
            {
                ItemValue mod = cosmetics[i];
                hash = hash * 31 + (mod != null ? mod.type : 0);
                hash = hash * 31 + (mod != null ? mod.Quality : 0);
                hash = hash * 31 + (mod != null ? mod.Seed : 0);
            }
            return hash;
        }
    }

    private static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
}

[HarmonyPatch]
public static class RebirthRangedProfileWeaponStatePatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        MethodInfo[] methods = typeof(ItemActionRanged).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        for (int i = 0; i < methods.Length; i++)
        {
            MethodInfo method = methods[i];
            if (method == null) continue;
            if (string.Equals(method.Name, "StartHolding", StringComparison.Ordinal) ||
                string.Equals(method.Name, "OnModificationsChanged", StringComparison.Ordinal))
                yield return method;
        }
    }

    private static void Postfix(object[] __args)
    {
        if (__args == null || __args.Length == 0) return;
        ItemActionData data = __args[0] as ItemActionData;
        EntityPlayer player = data != null && data.invData != null ? data.invData.holdingEntity as EntityPlayer : null;
        if (player == null) return;
        RebirthWeaponSustainedDpsService.InvalidateHeldProfile(player);
        RebirthWeaponSustainedDpsService.RefreshHeldProfile(player);
    }
}

[HarmonyPatch]
public static class RebirthRangedProfileEquipmentStatePatch
{
    private static readonly FieldInfo EntityField = AccessTools.Field(typeof(Equipment), "m_entity");

    private static IEnumerable<MethodBase> TargetMethods()
    {
        List<MethodInfo> methods = AccessTools.GetDeclaredMethods(typeof(Equipment));
        for (int i = 0; methods != null && i < methods.Count; i++)
        {
            MethodInfo method = methods[i];
            if (method != null && string.Equals(method.Name, "SetSlotItem", StringComparison.Ordinal))
                yield return method;
        }
    }

    private static void Postfix(Equipment __instance)
    {
        if (__instance == null || EntityField == null) return;
        EntityPlayer player = null;
        try { player = EntityField.GetValue(__instance) as EntityPlayer; }
        catch { }
        if (player == null) return;
        RebirthWeaponSustainedDpsService.InvalidateHeldProfile(player);
    }
}

[HarmonyPatch]
public static class RebirthRangedReloadStartTimingPatch
{
    private static MethodBase ResolveTarget()
    {
        MethodInfo[] methods = typeof(ItemActionRanged).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        for (int i = 0; i < methods.Length; i++)
            if (methods[i] != null && string.Equals(methods[i].Name, "ReloadGun", StringComparison.Ordinal)) return methods[i];
        return null;
    }

    private static bool Prepare() { return ResolveTarget() != null; }
    private static MethodBase TargetMethod() { return ResolveTarget(); }

    private static void Prefix(ItemActionRanged __instance)
    {
        if (__instance == null || GameManager.Instance == null || GameManager.Instance.World == null) return;
        EntityPlayerLocal player = GameManager.Instance.World.GetPrimaryPlayer();
        if (player == null || player.inventory == null) return;
        ItemValue held = player.inventory.holdingItemItemValue;
        if (held == null || held.IsEmpty() || held.ItemClass == null || held.ItemClass.Actions == null || held.ItemClass.Actions.Length == 0) return;
        if (!(__instance is ItemActionRanged)) return;
        RebirthWeaponSustainedDpsService.OnRuntimeReloadStarted(player);
    }
}

[HarmonyPatch]
public static class RebirthRangedReloadCompleteTimingPatch
{
    private static MethodBase ResolveTarget()
    {
        MethodInfo[] methods = typeof(ItemActionRanged).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        for (int i = 0; i < methods.Length; i++)
            if (methods[i] != null && string.Equals(methods[i].Name, "CompleteReload", StringComparison.Ordinal)) return methods[i];
        return null;
    }

    private static bool Prepare() { return ResolveTarget() != null; }
    private static MethodBase TargetMethod() { return ResolveTarget(); }

    private static void Postfix(ItemActionRanged __instance)
    {
        if (__instance == null || GameManager.Instance == null || GameManager.Instance.World == null) return;
        EntityPlayerLocal player = GameManager.Instance.World.GetPrimaryPlayer();
        if (player == null || player.inventory == null) return;
        ItemValue held = player.inventory.holdingItemItemValue;
        if (held == null || held.IsEmpty() || held.ItemClass == null || held.ItemClass.Actions == null || held.ItemClass.Actions.Length == 0) return;
        if (!(__instance is ItemActionRanged)) return;
        RebirthWeaponSustainedDpsService.OnRuntimeReloadCompleted(player);
    }
}


[HarmonyPatch(typeof(GameManager), nameof(GameManager.Update))]
public static class RebirthCombatProgressionFrameSettlementPatch
{
    private static void Postfix()
    {
        RebirthWeaponSustainedDpsService.SettleQueuedCombatAtFrameEnd();
    }
}
