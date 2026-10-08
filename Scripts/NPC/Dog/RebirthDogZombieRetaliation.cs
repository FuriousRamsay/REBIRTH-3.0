using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

#nullable disable

/// <summary>
/// Dog combat/faction bridge for the 3.1 port.
///
/// 2.6 treated the recruited dog as a whiteriver companion and used the same faction
/// contract at target-selection, retaliation, pursuit and damage time. 3.1 has separate
/// class whitelists in the hurt-target selector, nearest-target selector and attack task.
/// A dog can therefore become a zombie revenge/attack target while the attack task still
/// rejects EntityRebirthDogCompanion, leaving the zombie with a target that no runnable
/// attack task can service. This module makes the dog a valid authored zombie target in
/// all three stages, then applies one dynamic relationship policy before target/damage use.
/// </summary>
public static class RebirthDogZombieRetaliationInstaller
{
    private static readonly Harmony Harmony = new Harmony("rebirth.dog.combat-faction.3.1");
    private static bool installed;

    public static void Install()
    {
        if (installed) return;
        installed = true;
        RebirthHarmonyBootstrap.PatchClassOnce(Harmony, typeof(RebirthDogZombieEaiInitializationPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(Harmony, typeof(RebirthDogZombieRevengePromotionPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(Harmony, typeof(RebirthDogStationaryAttackTargetGuardPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(Harmony, typeof(RebirthDogStationaryRevengeTargetGuardPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(Harmony, typeof(RebirthDogZombieHurtTargetValidationPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(Harmony, typeof(RebirthDogZombieNearestTargetValidationPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(Harmony, typeof(RebirthDogZombieApproachTargetValidationPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(Harmony, typeof(RebirthDogBaseFriendlyFirePatch));
        RebirthHarmonyBootstrap.PatchClassOnce(Harmony, typeof(RebirthDogPlayerFriendlyFirePatch));
        RebirthHarmonyBootstrap.PatchClassOnce(Harmony, typeof(RebirthDogTargetBarNamePatch));
    }
}


/// <summary>
/// Base 3.1 XUiC_TargetBar localizes EntityClass.entityClassName for every non-player.
/// Rebirth dogs have a persistent custom EntityName, so replace only the name binding for
/// this entity type and leave every other target-bar binding/entity untouched.
/// </summary>
[HarmonyPatch(typeof(XUiC_TargetBar), "GetBindingValueInternal")]
internal static class RebirthDogTargetBarNamePatch
{
    private static void Postfix(XUiC_TargetBar __instance, ref string value, string bindingName, ref bool __result)
    {
        if (__instance == null || !string.Equals(bindingName, "name", StringComparison.OrdinalIgnoreCase)) return;
        EntityRebirthDogCompanion dog = __instance.Target as EntityRebirthDogCompanion;
        if (dog == null) return;
        RebirthDogLifecycleService.SynchronizeDisplayName(dog);
        string displayName = dog.EntityName;
        if (string.IsNullOrWhiteSpace(displayName)) return;
        value = displayName.Trim();
        __result = true;
    }
}

/// <summary>
/// Single dog-specific relationship authority used by targeting, retaliation and damage.
/// It intentionally does not replace the whole game's faction manager; it closes the
/// companion-specific gaps that 2.6 handled in VerifyFactionStanding.
/// </summary>
public static class RebirthDogCombatRelationService
{
    public static bool AreFriendly(EntityAlive left, EntityAlive right, out string reason)
    {
        reason = string.Empty;
        if (left == null || right == null) return false;
        if (left == right) { reason = "same-entity"; return true; }

        EntityRebirthDogCompanion leftDog = left as EntityRebirthDogCompanion;
        EntityRebirthDogCompanion rightDog = right as EntityRebirthDogCompanion;

        if (leftDog != null && IsDogOwner(leftDog, right))
        {
            reason = "dog-owner";
            return true;
        }
        if (rightDog != null && IsDogOwner(rightDog, left))
        {
            reason = "dog-owner";
            return true;
        }
        if (leftDog != null && right is EntityDrone && IsDogOwnerDrone(leftDog, (EntityDrone)right))
        {
            reason = "same-owner-drone";
            return true;
        }
        if (rightDog != null && left is EntityDrone && IsDogOwnerDrone(rightDog, (EntityDrone)left))
        {
            reason = "same-owner-drone";
            return true;
        }

        RebirthNpcRuntimeState leftState = (left as EntityRebirthNPC)?.RebirthRuntimeState;
        RebirthNpcRuntimeState rightState = (right as EntityRebirthNPC)?.RebirthRuntimeState;
        if (leftState != null && rightState != null &&
            leftState.OwnershipKind != RebirthNpcOwnershipKind.None &&
            leftState.OwnershipKind == rightState.OwnershipKind &&
            !string.IsNullOrWhiteSpace(leftState.OwnerId) &&
            string.Equals(leftState.OwnerId, rightState.OwnerId, StringComparison.OrdinalIgnoreCase))
        {
            reason = "same-companion-owner";
            return true;
        }

        // A dog inherits the owner's player-vs-player protection. This prevents party/allied
        // players from bypassing the player's normal FriendlyFireCheck by shooting the dog.
        if (leftDog != null && right is EntityPlayer && IsProtectedByOwnerPvp(leftDog, (EntityPlayer)right))
        {
            reason = "owner-pvp-protected";
            return true;
        }
        if (rightDog != null && left is EntityPlayer && IsProtectedByOwnerPvp(rightDog, (EntityPlayer)left))
        {
            reason = "owner-pvp-protected";
            return true;
        }

        try
        {
            FactionManager.Relationship tier = FactionManager.Instance.GetRelationshipTier(left, right);
            if (tier == FactionManager.Relationship.Like ||
                tier == FactionManager.Relationship.Love ||
                tier == FactionManager.Relationship.Leader)
            {
                reason = "faction-" + tier;
                return true;
            }
        }
        catch { }

        return false;
    }

    public static bool CanDamage(EntityAlive attacker, EntityAlive victim, out string reason)
    {
        reason = "allowed";
        if (victim == null) return true;
        if (attacker == null) return true; // environment / non-entity damage

        string friendlyReason;
        if (AreFriendly(attacker, victim, out friendlyReason))
        {
            reason = "blocked:" + friendlyReason;
            return false;
        }

        if ((attacker is EntityZombie && victim is EntityRebirthDogCompanion) ||
            (attacker is EntityRebirthDogCompanion && victim is EntityZombie))
        {
            reason = "hostile:dog-zombie";
            return true;
        }

        reason = "allowed:not-allied";
        return true;
    }

    public static bool CanDogTarget(EntityRebirthDogCompanion dog, EntityAlive target,
        EntityPlayer owner, bool retaliation, out string reason)
    {
        reason = string.Empty;
        if (dog == null || target == null || target == dog || target.IsDead())
        {
            reason = "invalid-target";
            return false;
        }
        if (!IsDogCombatActive(dog, out reason)) return false;
        if (IsDogStationaryNoAttack(dog, out reason)) return false;
        if (IsTemporarilyUnavailable(target))
        {
            reason = "target-lifecycle-unavailable";
            return false;
        }

        string friendlyReason;
        if (AreFriendly(dog, target, out friendlyReason))
        {
            reason = "friendly:" + friendlyReason;
            return false;
        }

        if (retaliation && dog.GetRevengeTarget() != target)
        {
            reason = "not-current-revenge";
            return false;
        }

        if (target is EntityZombie)
        {
            reason = "hostile:dog-vs-zombie";
            return true;
        }

        bool ownerThreat = owner != null &&
                           (target.GetAttackTarget() == owner || target.GetRevengeTarget() == owner);
        if (ownerThreat)
        {
            reason = "hostile:owner-threat";
            return true;
        }

        try
        {
            FactionManager.Relationship tier = FactionManager.Instance.GetRelationshipTier(dog, target);
            if (tier == FactionManager.Relationship.Hate || tier == FactionManager.Relationship.Dislike)
            {
                reason = "hostile:faction-" + tier;
                return true;
            }
            reason = "not-hostile:faction-" + tier;
            return false;
        }
        catch
        {
            if (target is EntityEnemy || target is EntityEnemyAnimal)
            {
                reason = "hostile:enemy-fallback";
                return true;
            }
            reason = "not-hostile:no-faction-decision";
            return false;
        }
    }

    public static bool CanZombieTargetDog(EntityAlive zombie, EntityRebirthDogCompanion dog, out string reason)
    {
        reason = string.Empty;
        if (!(zombie is EntityZombie) || dog == null || zombie == dog || zombie.IsDead() || dog.IsDead())
        {
            reason = "invalid-zombie-or-dog";
            return false;
        }
        if (RebirthProtectCrateService.IsProtectedDefender(zombie))
        {
            reason = "protect-crate-defender";
            return false;
        }
        if (!IsDogCombatActive(dog, out reason)) return false;

        string friendlyReason;
        if (AreFriendly(zombie, dog, out friendlyReason))
        {
            reason = "friendly:" + friendlyReason;
            return false;
        }

        try
        {
            FactionManager.Relationship tier = FactionManager.Instance.GetRelationshipTier(zombie, dog);
            if (tier == FactionManager.Relationship.Hate || tier == FactionManager.Relationship.Dislike)
            {
                reason = "hostile:faction-" + tier;
                return true;
            }
        }
        catch { }

        // 2.6's faction exception table defaulted undead<->whiteriver to attackable:
        // neither direction was listed among the no-attack exceptions. Preserve that gameplay
        // even if a 3.1 faction relationship file reports Neutral for this custom dog class.
        reason = "hostile:2.6-undead-whiteriver-default";
        return true;
    }

    public static string DescribeFaction(EntityAlive entity)
    {
        if (entity == null) return "<null>";
        string name = string.Empty;
        try
        {
            if (entity.EntityClass != null && entity.EntityClass.Properties.Values.ContainsKey("Faction"))
                name = entity.EntityClass.Properties.Values["Faction"] ?? string.Empty;
        }
        catch { }
        if (string.IsNullOrWhiteSpace(name))
        {
            try
            {
                Faction f = FactionManager.Instance.GetFaction(entity.factionId);
                if (f != null) name = f.Name;
            }
            catch { }
        }
        return string.IsNullOrWhiteSpace(name) ? ("id:" + entity.factionId) : name;
    }

    public static string DescribeRelationship(EntityAlive left, EntityAlive right)
    {
        if (left == null || right == null) return "n/a";
        try { return FactionManager.Instance.GetRelationshipTier(left, right).ToString(); }
        catch { return "unknown"; }
    }

    private static bool IsDogCombatActive(EntityRebirthDogCompanion dog, out string reason)
    {
        reason = string.Empty;
        if (dog == null || dog.IsDead()) { reason = "dog-dead"; return false; }
        RebirthNpcRuntimeState state = dog.RebirthRuntimeState;
        if (state == null) { reason = "dog-no-runtime-state"; return false; }
        if (state.Presence != RebirthNpcPresenceState.Active)
        {
            reason = "dog-presence-" + state.Presence;
            return false;
        }
        RebirthNpcPersistentRecordView record;
        RebirthDogPersistentRecordView dogRecord;
        if (RebirthDogStateService.TryGetView(state.StableId, out record, out dogRecord) && dogRecord != null &&
            dogRecord.Lifecycle != RebirthDogLifecycleKind.Active)
        {
            reason = "dog-lifecycle-" + dogRecord.Lifecycle;
            return false;
        }
        if (IsTemporarilyUnavailable(dog))
        {
            reason = "dog-temporarily-unavailable";
            return false;
        }
        return true;
    }

    public static bool IsDogStationaryNoAttack(EntityRebirthDogCompanion dog, out string reason)
    {
        reason = string.Empty;
        RebirthNpcRuntimeState state = dog != null ? dog.RebirthRuntimeState : null;
        if (state == null) return false;
        if (state.Order == RebirthNpcOrderState.Stay)
        {
            reason = "dog-order-stay";
            return true;
        }
        if (state.Order == RebirthNpcOrderState.Guard &&
            RebirthDogStateService.IsGuardStationaryStay(state.StableId))
        {
            reason = "dog-order-stationary-guard";
            return true;
        }
        return false;
    }

    private static bool IsTemporarilyUnavailable(EntityAlive entity)
    {
        if (entity == null || entity.Buffs == null) return false;
        return entity.Buffs.GetCustomVar("onMission") == 1f ||
               entity.Buffs.GetCustomVar("$FR_NPC_Respawn") == 1f ||
               entity.Buffs.GetCustomVar("$FR_NPC_Hidden") == 1f;
    }

    private static bool IsDogOwner(EntityRebirthDogCompanion dog, EntityAlive other)
    {
        EntityPlayer player = other as EntityPlayer;
        if (dog == null || player == null || dog.RebirthRuntimeState == null) return false;
        string playerOwnerId;
        if (!RebirthDogLifecycleService.TryResolveOwnerId(player, out playerOwnerId)) return false;
        return !string.IsNullOrWhiteSpace(dog.RebirthRuntimeState.OwnerId) &&
               string.Equals(dog.RebirthRuntimeState.OwnerId, playerOwnerId, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsDogOwnerDrone(EntityRebirthDogCompanion dog, EntityDrone drone)
    {
        if (dog == null || drone == null || dog.RebirthRuntimeState == null || dog.world == null) return false;
        EntityPlayer owner = ResolveOwner(dog.world, dog.RebirthRuntimeState.OwnerId);
        if (owner == null) return false;
        try
        {
            if (drone.Owner == owner) return true;
            return drone.belongsPlayerId == owner.entityId;
        }
        catch { return false; }
    }

    private static bool IsProtectedByOwnerPvp(EntityRebirthDogCompanion dog, EntityPlayer otherPlayer)
    {
        if (dog == null || otherPlayer == null || dog.RebirthRuntimeState == null || dog.world == null) return false;
        EntityPlayer owner = ResolveOwner(dog.world, dog.RebirthRuntimeState.OwnerId);
        if (owner == null) return false;
        if (owner == otherPlayer) return true;

        // Friendly factions must get the same dog pass-through/friendly-fire protection as
        // the owner. FriendlyFireCheck alone does not cover every faction relationship.
        try
        {
            FactionManager.Relationship tier = FactionManager.Instance.GetRelationshipTier(owner, otherPlayer);
            if (tier == FactionManager.Relationship.Like ||
                tier == FactionManager.Relationship.Love ||
                tier == FactionManager.Relationship.Leader)
                return true;
        }
        catch { }

        try { return !owner.FriendlyFireCheck(otherPlayer); }
        catch { return false; }
    }

    private static EntityPlayer ResolveOwner(World world, string ownerId)
    {
        if (world == null || string.IsNullOrWhiteSpace(ownerId)) return null;
        List<EntityPlayer> players = world.Players != null ? world.Players.list : null;
        if (players == null) return null;
        for (int i = 0; i < players.Count; i++)
        {
            EntityPlayer p = players[i];
            string id;
            if (p != null && RebirthDogLifecycleService.TryResolveOwnerId(p, out id) &&
                string.Equals(id, ownerId, StringComparison.OrdinalIgnoreCase)) return p;
        }
        return null;
    }
}

internal static class RebirthDogZombieEaiBridge
{
    private static readonly Type DogType = typeof(EntityRebirthDogCompanion);

    public static EntityAlive GetTaskEntity(EAIBase task)
    {
        return task != null ? task.theEntity : null;
    }

    public static void AugmentZombieManager(EAIManager manager)
    {
        if (manager == null || !(manager.entity is EntityZombie) ||
            RebirthProtectCrateService.IsProtectedDefender(manager.entity)) return;

        foreach (EAIBase task in EnumerateAllTasks(manager))
        {
            EAISetAsTargetIfHurt hurt = task as EAISetAsTargetIfHurt;
            if (hurt != null) { EnsureDogAccepted(hurt); continue; }

            EAISetNearestEntityAsTarget nearest = task as EAISetNearestEntityAsTarget;
            if (nearest != null) { EnsureDogAccepted(nearest); continue; }

            EAIApproachAndAttackTarget approach = task as EAIApproachAndAttackTarget;
            if (approach != null) EnsureDogAccepted(approach);
        }
    }

    public static bool EnsureDogAccepted(EAISetAsTargetIfHurt task)
    {
        if (task == null || task.targetClasses == null || task.targetClasses.Count == 0) return false;
        for (int i = 0; i < task.targetClasses.Count; i++)
        {
            Type accepted = task.targetClasses[i].type;
            if (accepted != null && accepted.IsAssignableFrom(DogType)) return true;
        }
        task.targetClasses.Add(new EAISetAsTargetIfHurt.TargetClass { type = DogType });
        return true;
    }

    public static bool EnsureDogAccepted(EAISetNearestEntityAsTarget task)
    {
        if (task == null || task.targetClasses == null || task.targetClasses.Count == 0) return false;
        EAISetNearestEntityAsTarget.TargetClass template = task.targetClasses[0];
        for (int i = 0; i < task.targetClasses.Count; i++)
        {
            EAISetNearestEntityAsTarget.TargetClass current = task.targetClasses[i];
            Type accepted = current.type;
            if (accepted != null && accepted.IsAssignableFrom(DogType)) return true;
            if (accepted != null && accepted.IsAssignableFrom(typeof(EntityPlayer))) template = current;
        }
        template.type = DogType;
        task.targetClasses.Add(template);
        return true;
    }

    public static bool EnsureDogAccepted(EAIApproachAndAttackTarget task)
    {
        if (task == null || task.targetClasses == null || task.targetClasses.Count == 0) return false;
        EAIApproachAndAttackTarget.TargetClass template = task.targetClasses[0];
        for (int i = 0; i < task.targetClasses.Count; i++)
        {
            EAIApproachAndAttackTarget.TargetClass current = task.targetClasses[i];
            Type accepted = current.type;
            if (accepted != null && accepted.IsAssignableFrom(DogType)) return true;
            if (accepted != null && accepted.IsAssignableFrom(typeof(EntityPlayer))) template = current;
        }
        template.type = DogType;
        task.targetClasses.Add(template);
        return true;
    }

    public static bool TaskAcceptsDog(EAIBase task)
    {
        EAISetAsTargetIfHurt hurt = task as EAISetAsTargetIfHurt;
        if (hurt != null) return Accepts(hurt.targetClasses);
        EAISetNearestEntityAsTarget nearest = task as EAISetNearestEntityAsTarget;
        if (nearest != null) return Accepts(nearest.targetClasses);
        EAIApproachAndAttackTarget approach = task as EAIApproachAndAttackTarget;
        return approach != null && Accepts(approach.targetClasses);
    }

    public static IEnumerable<EAIBase> EnumerateAllTasks(EAIManager manager)
    {
        if (manager == null) yield break;
        if (manager.tasks != null)
        {
            List<EAITaskEntry> entries = manager.tasks.Tasks;
            for (int i = 0; entries != null && i < entries.Count; i++)
                if (entries[i] != null && entries[i].action != null) yield return entries[i].action;
        }
        if (manager.targetTasks != null)
        {
            List<EAITaskEntry> entries = manager.targetTasks.Tasks;
            for (int i = 0; entries != null && i < entries.Count; i++)
                if (entries[i] != null && entries[i].action != null) yield return entries[i].action;
        }
    }

    public static string GetExecutingTaskNames(EntityAlive entity)
    {
        EAIManager manager = entity != null ? entity.aiManager : null;
        if (manager == null) return "none";
        List<string> names = new List<string>();
        AppendExecuting(names, manager.tasks, "A:");
        AppendExecuting(names, manager.targetTasks, "T:");
        return names.Count == 0 ? "none" : string.Join("|", names.ToArray());
    }

    public static string GetDogFilterSummary(EntityAlive entity)
    {
        EAIManager manager = entity != null ? entity.aiManager : null;
        if (manager == null) return "hurt=0 nearest=0 approach=0";
        bool hurt = false, nearest = false, approach = false;
        foreach (EAIBase task in EnumerateAllTasks(manager))
        {
            bool accepts = TaskAcceptsDog(task);
            if (task is EAISetAsTargetIfHurt) hurt |= accepts;
            else if (task is EAISetNearestEntityAsTarget) nearest |= accepts;
            else if (task is EAIApproachAndAttackTarget) approach |= accepts;
        }
        return "hurt=" + (hurt ? "1" : "0") + " nearest=" + (nearest ? "1" : "0") +
               " approach=" + (approach ? "1" : "0");
    }

    private static bool Accepts(List<EAISetAsTargetIfHurt.TargetClass> classes)
    {
        if (classes == null || classes.Count == 0) return false;
        for (int i = 0; i < classes.Count; i++)
            if (classes[i].type != null && classes[i].type.IsAssignableFrom(DogType)) return true;
        return false;
    }

    private static bool Accepts(List<EAISetNearestEntityAsTarget.TargetClass> classes)
    {
        if (classes == null || classes.Count == 0) return false;
        for (int i = 0; i < classes.Count; i++)
            if (classes[i].type != null && classes[i].type.IsAssignableFrom(DogType)) return true;
        return false;
    }

    private static bool Accepts(List<EAIApproachAndAttackTarget.TargetClass> classes)
    {
        if (classes == null || classes.Count == 0) return false;
        for (int i = 0; i < classes.Count; i++)
            if (classes[i].type != null && classes[i].type.IsAssignableFrom(DogType)) return true;
        return false;
    }

    private static void AppendExecuting(List<string> names, EAITaskList taskList, string prefix)
    {
        if (taskList == null) return;
        List<EAITaskEntry> entries = taskList.GetExecutingTasks();
        for (int i = 0; entries != null && i < entries.Count; i++)
        {
            EAIBase action = entries[i] != null ? entries[i].action : null;
            if (action != null) names.Add(prefix + action.GetType().Name);
        }
    }
}

[HarmonyPatch(typeof(EAIManager), nameof(EAIManager.CopyPropertiesFromEntityClass))]
internal static class RebirthDogZombieEaiInitializationPatch
{
    private static void Postfix(EAIManager __instance)
    {
        RebirthDogZombieEaiBridge.AugmentZombieManager(__instance);
    }
}

/// <summary>
/// Stay is a hard non-combat order. Prevent base EAI from assigning a fresh attack/revenge
/// target during EntityAlive.OnUpdate before RebirthDogRuntimeService gets its tick.
/// Null assignments remain allowed so all normal target-clearing paths still work.
/// </summary>
[HarmonyPatch(typeof(EntityAlive), nameof(EntityAlive.SetAttackTarget))]
internal static class RebirthDogStationaryAttackTargetGuardPatch
{
    private static bool Prefix(EntityAlive __instance, EntityAlive _attackTarget)
    {
        EntityRebirthDogCompanion dog = __instance as EntityRebirthDogCompanion;
        if (dog == null || _attackTarget == null) return true;
        string reason;
        return !RebirthDogCombatRelationService.IsDogStationaryNoAttack(dog, out reason);
    }
}

[HarmonyPatch(typeof(EntityAlive), nameof(EntityAlive.SetRevengeTarget))]
internal static class RebirthDogStationaryRevengeTargetGuardPatch
{
    private static bool Prefix(EntityAlive __instance, EntityAlive _other)
    {
        EntityRebirthDogCompanion dog = __instance as EntityRebirthDogCompanion;
        if (dog == null || _other == null) return true;
        string reason;
        return !RebirthDogCombatRelationService.IsDogStationaryNoAttack(dog, out reason);
    }
}

/// <summary>
/// DamageEntity already creates the revenge signal. Promote a hostile dog revenge target to the
/// current attack target immediately. This intentionally replaces the base/legacy 66% incumbent
/// target preference for this one case: an entity that just injured the zombie is the retaliation
/// target requested by the companion gameplay contract.
/// </summary>
[HarmonyPatch(typeof(EntityAlive), nameof(EntityAlive.SetRevengeTarget))]
internal static class RebirthDogZombieRevengePromotionPatch
{
    private static void Postfix(EntityAlive __instance, EntityAlive _other)
    {
        EntityZombie zombie = __instance as EntityZombie;
        EntityRebirthDogCompanion dog = _other as EntityRebirthDogCompanion;
        if (zombie == null || dog == null) return;

        string reason;
        if (!RebirthDogCombatRelationService.CanZombieTargetDog(zombie, dog, out reason)) return;
        try
        {
            zombie.ClearInvestigatePosition();
            zombie.SetAttackTarget(dog, 400);
            zombie.ConditionalTriggerSleeperWakeUp();
        }
        catch (Exception ex)
        {
            Log.Warning("[REBIRTH Dog Combat] revenge promotion failed zombie=" + zombie.entityId +
                        " dog=" + dog.entityId + " " + ex.GetType().Name + ": " + ex.Message);
        }
    }
}

[HarmonyPatch(typeof(EAISetAsTargetIfHurt), nameof(EAISetAsTargetIfHurt.CanExecute))]
internal static class RebirthDogZombieHurtTargetValidationPatch
{
    private static void Prefix(EAISetAsTargetIfHurt __instance)
    {
        EntityAlive zombie = __instance != null ? __instance.theEntity : null;
        if (!(zombie is EntityZombie)) return;
        if (zombie.GetRevengeTarget() is EntityRebirthDogCompanion)
            RebirthDogZombieEaiBridge.EnsureDogAccepted(__instance);
    }
}

[HarmonyPatch(typeof(EAISetNearestEntityAsTarget), nameof(EAISetNearestEntityAsTarget.CanExecute))]
internal static class RebirthDogZombieNearestTargetValidationPatch
{
    private static bool Prefix(EAISetNearestEntityAsTarget __instance, ref bool __result)
    {
        EntityAlive zombie = __instance != null ? __instance.theEntity : null;
        if (!(zombie is EntityZombie)) return true;

        // A valid retaliation target already owns the combat decision. Do not let the lower
        // nearest-target scan immediately overwrite it with the player on the following tick.
        EntityRebirthDogCompanion currentDog = zombie.GetAttackTarget() as EntityRebirthDogCompanion;
        if (currentDog != null)
        {
            string currentReason;
            if (RebirthDogCombatRelationService.CanZombieTargetDog(zombie, currentDog, out currentReason))
            {
                __result = false;
                return false;
            }
            if (zombie.GetRevengeTarget() == currentDog) zombie.SetRevengeTarget(null);
            zombie.SetAttackTarget(null, 0);
            zombie.ClearInvestigatePosition();
        }
        return true;
    }

    private static void Postfix(EAISetNearestEntityAsTarget __instance, ref bool __result)
    {
        if (!__result || __instance == null) return;
        EntityAlive zombie = __instance.theEntity;
        if (!(zombie is EntityZombie)) return;
        EntityRebirthDogCompanion dog = __instance.targetEntity as EntityRebirthDogCompanion;
        if (dog == null) return;
        string reason;
        if (RebirthDogCombatRelationService.CanZombieTargetDog(zombie, dog, out reason)) return;
        __instance.targetEntity = null;
        __instance.closeTargetEntity = null;
        __result = false;
    }
}

[HarmonyPatch(typeof(EAIApproachAndAttackTarget), nameof(EAIApproachAndAttackTarget.CanExecute))]
internal static class RebirthDogZombieApproachTargetValidationPatch
{
    private static void Prefix(EAIApproachAndAttackTarget __instance)
    {
        EntityAlive zombie = __instance != null ? __instance.theEntity : null;
        if (!(zombie is EntityZombie) || !(zombie.GetAttackTarget() is EntityRebirthDogCompanion)) return;
        RebirthDogZombieEaiBridge.EnsureDogAccepted(__instance);
    }

    private static void Postfix(EAIApproachAndAttackTarget __instance, ref bool __result)
    {
        EntityAlive zombie = __instance != null ? __instance.theEntity : null;
        if (!(zombie is EntityZombie)) return;
        EntityRebirthDogCompanion dog = zombie.GetAttackTarget() as EntityRebirthDogCompanion;
        if (dog == null) return;

        string reason;
        if (RebirthDogCombatRelationService.CanZombieTargetDog(zombie, dog, out reason)) return;

        if (zombie.GetRevengeTarget() == dog) zombie.SetRevengeTarget(null);
        zombie.SetAttackTarget(null, 0);
        zombie.ClearInvestigatePosition();
        __result = false;
    }
}

/// <summary>Protects dogs/generic companions from friendly dog-side damage through the base victim gate.</summary>
[HarmonyPatch(typeof(EntityAlive), nameof(EntityAlive.FriendlyFireCheck))]
internal static class RebirthDogBaseFriendlyFirePatch
{
    private static bool Prefix(EntityAlive __instance, EntityAlive other, ref bool __result)
    {
        if (!(__instance is EntityRebirthDogCompanion) && !(other is EntityRebirthDogCompanion)) return true;
        string reason;
        if (RebirthDogCombatRelationService.CanDamage(other, __instance, out reason)) return true;
        __result = false;
        return false;
    }
}

/// <summary>EntityPlayer overrides FriendlyFireCheck, so protect an owner from dog damage here too.</summary>
[HarmonyPatch(typeof(EntityPlayer), nameof(EntityPlayer.FriendlyFireCheck))]
internal static class RebirthDogPlayerFriendlyFirePatch
{
    private static bool Prefix(EntityPlayer __instance, EntityAlive other, ref bool __result)
    {
        if (!(other is EntityRebirthDogCompanion)) return true;
        string reason;
        if (RebirthDogCombatRelationService.CanDamage(other, __instance, out reason)) return true;
        __result = false;
        return false;
    }
}

/// <summary>One-shot, auto-stopping combat trace. No permanent debug toggle is required.</summary>
public static class RebirthDogCombatDebugService
{
    private const float TraceDurationSeconds = 12f;
    private const float TraceSampleSeconds = 0.25f;
    private static bool traceRunning;

    public static bool StartTargetingDebug(EntityPlayer player, int requestedDogEntityId,
        int requestedZombieEntityId, out string reason)
    {
        reason = string.Empty;
        if (player == null || player.world == null || GameManager.Instance == null)
        {
            reason = "[REBIRTH Dog Targeting] player/world unavailable.";
            return false;
        }
        if (traceRunning)
        {
            reason = "[REBIRTH Dog Targeting] a dog combat/targeting trace is already running; let it complete first.";
            return false;
        }

        EntityRebirthDogCompanion dog = ResolveDog(player, requestedDogEntityId);
        if (dog == null)
        {
            reason = "[REBIRTH Dog Targeting] no loaded owned dog was found.";
            return false;
        }
        EntityZombie zombie = ResolveZombie(player.world, dog, requestedZombieEntityId);
        if (zombie == null)
        {
            reason = "[REBIRTH Dog Targeting] no living zombie was found near dog entity=" + dog.entityId + ".";
            return false;
        }

        traceRunning = true;
        GameManager.Instance.StartCoroutine(TargetingTrace(player, dog.entityId, zombie.entityId));
        reason = "[REBIRTH Dog Targeting] tracing dog=" + dog.entityId + " zombie=" + zombie.entityId +
                 " for " + TraceDurationSeconds.ToString("0", CultureInfo.InvariantCulture) +
                 "s at " + TraceSampleSeconds.ToString("0.00", CultureInfo.InvariantCulture) +
                 "s intervals. Start this while the zombie is sleeping, then shoot/wake it and keep it in your view.";
        return true;
    }

    private static IEnumerator TargetingTrace(EntityPlayer player, int dogId, int zombieId)
    {
        int samples = 0;
        float end = Time.realtimeSinceStartup + TraceDurationSeconds;
        try
        {
            while (Time.realtimeSinceStartup < end)
            {
                World world = GameManager.Instance != null ? GameManager.Instance.World : null;
                if (world == null) break;
                EntityRebirthDogCompanion dog = world.GetEntity(dogId) as EntityRebirthDogCompanion;
                EntityZombie zombie = world.GetEntity(zombieId) as EntityZombie;
                samples++;
                Log.Out(BuildTargetingSample(samples, player, dog, zombie, dogId, zombieId));
                yield return new WaitForSeconds(TraceSampleSeconds);
            }
        }
        finally
        {
            traceRunning = false;
            Log.Out("[REBIRTH Dog Targeting] COMPLETE dog=" + dogId + " zombie=" + zombieId + " samples=" + samples);
        }
    }

    private static string BuildTargetingSample(int sample, EntityPlayer player, EntityRebirthDogCompanion dog,
        EntityZombie zombie, int dogId, int zombieId)
    {
        StringBuilder b = new StringBuilder(1200);
        b.Append("[REBIRTH Dog Targeting] sample=").Append(sample);

        if (dog == null)
        {
            b.Append(" DOG{id=").Append(dogId).Append(" missing=1}");
        }
        else
        {
            RebirthNpcRuntimeState state = dog.RebirthRuntimeState;
            b.Append(" DOG{id=").Append(dog.entityId)
             .Append(" order=").Append(state != null ? state.Order.ToString() : "<none>")
             .Append(" atk=").Append(TargetId(dog.GetAttackTarget()))
             .Append(" rev=").Append(TargetId(dog.GetRevengeTarget()))
             .Append(" pos=").Append(FormatVector(dog.position)).Append('}');
        }

        EntityPlayer owner = dog != null ? ResolveOwnerForDebug(player, dog) : player;
        if (owner != null)
        {
            b.Append(" OWNER{id=").Append(owner.entityId)
             .Append(" pos=").Append(FormatVector(owner.position)).Append('}');
        }

        if (zombie == null)
        {
            b.Append(" TARGET{id=").Append(zombieId).Append(" missing=1}");
        }
        else
        {
            b.Append(" TARGET{id=").Append(zombie.entityId)
             .Append(" class=").Append(zombie.EntityClass != null ? zombie.EntityClass.entityClassName : "<none>")
             .Append(" hp=").Append(zombie.Health).Append('/').Append(zombie.GetMaxHealth())
             .Append(" sleeping=").Append(zombie.IsSleeping ? "1" : "0")
             .Append(" sleepingOrWaking=").Append(zombie.sleepingOrWakingUp ? "1" : "0")
             .Append(" alertTicks=").Append(zombie.GetAlertTicks())
             .Append(" investigate=").Append(zombie.HasInvestigatePosition ? "1" : "0")
             .Append(" atk=").Append(TargetId(zombie.GetAttackTarget()))
             .Append(" rev=").Append(TargetId(zombie.GetRevengeTarget()))
             .Append(" pos=").Append(FormatVector(zombie.position)).Append('}');
        }

        if (dog != null && zombie != null)
            b.Append(' ').Append(RebirthDogRuntimeService.GetCombatTargetDebugSnapshot(dog, owner, zombie));

        return b.ToString();
    }

    public static bool StartCombatDebug(EntityPlayer player, int requestedDogEntityId,
        int requestedZombieEntityId, out string reason)
    {
        reason = string.Empty;
        if (player == null || player.world == null || GameManager.Instance == null)
        {
            reason = "[REBIRTH Dog Combat] player/world unavailable.";
            return false;
        }
        if (traceRunning)
        {
            reason = "[REBIRTH Dog Combat] a combat trace is already running; wait for its automatic completion.";
            return false;
        }

        EntityRebirthDogCompanion dog = ResolveDog(player, requestedDogEntityId);
        if (dog == null)
        {
            reason = "[REBIRTH Dog Combat] no loaded owned dog was found.";
            return false;
        }
        EntityZombie zombie = ResolveZombie(player.world, dog, requestedZombieEntityId);
        if (zombie == null)
        {
            reason = "[REBIRTH Dog Combat] no living zombie was found near dog entity=" + dog.entityId + ".";
            return false;
        }

        traceRunning = true;
        GameManager.Instance.StartCoroutine(Trace(player, dog.entityId, zombie.entityId));
        reason = "[REBIRTH Dog Combat] tracing dog=" + dog.entityId + " zombie=" + zombie.entityId +
                 " for " + TraceDurationSeconds.ToString("0", CultureInfo.InvariantCulture) +
                 "s at " + TraceSampleSeconds.ToString("0.00", CultureInfo.InvariantCulture) +
                 "s intervals. Let the dog attack the zombie during this capture.";
        return true;
    }

    private static IEnumerator Trace(EntityPlayer player, int dogId, int zombieId)
    {
        int samples = 0;
        float end = Time.realtimeSinceStartup + TraceDurationSeconds;
        try
        {
            while (Time.realtimeSinceStartup < end)
            {
                World world = GameManager.Instance != null ? GameManager.Instance.World : null;
                if (world == null) break;
                EntityRebirthDogCompanion dog = world.GetEntity(dogId) as EntityRebirthDogCompanion;
                EntityZombie zombie = world.GetEntity(zombieId) as EntityZombie;
                samples++;
                Log.Out(BuildSample(samples, player, dog, zombie, dogId, zombieId));
                yield return new WaitForSeconds(TraceSampleSeconds);
            }
        }
        finally
        {
            traceRunning = false;
            Log.Out("[REBIRTH Dog Combat] COMPLETE dog=" + dogId + " zombie=" + zombieId + " samples=" + samples);
        }
    }

    private static string BuildSample(int sample, EntityPlayer player, EntityRebirthDogCompanion dog,
        EntityZombie zombie, int dogId, int zombieId)
    {
        StringBuilder b = new StringBuilder(900);
        b.Append("[REBIRTH Dog Combat] sample=").Append(sample);

        if (dog == null)
        {
            b.Append(" DOG{id=").Append(dogId).Append(" missing=1}");
        }
        else
        {
            RebirthNpcRuntimeState state = dog.RebirthRuntimeState;
            RebirthNpcPersistentRecordView record = null;
            RebirthDogPersistentRecordView dr = null;
            bool got = state != null && RebirthDogStateService.TryGetView(state.StableId, out record, out dr);
            b.Append(" DOG{id=").Append(dog.entityId)
             .Append(" name=\"").Append(dog.EntityName ?? string.Empty).Append("\"")
             .Append(" stable=").Append(state != null ? state.StableId.ToString() : "<none>")
             .Append(" owner=").Append(state != null ? state.OwnerId : "<none>")
             .Append(" order=").Append(state != null ? state.Order.ToString() : "<none>")
             .Append(" mode=").Append(got && dr != null ? dr.CombatMode.ToString() : "<none>")
             .Append(" stopped=").Append(got && dr != null && dr.AttackStopped ? "1" : "0")
             .Append(" hp=").Append(dog.Health).Append('/').Append(dog.GetMaxHealth())
             .Append(" faction=").Append(RebirthDogCombatRelationService.DescribeFaction(dog))
             .Append(" atk=").Append(TargetId(dog.GetAttackTarget()))
             .Append(" rev=").Append(TargetId(dog.GetRevengeTarget())).Append('}');
        }

        if (zombie == null)
        {
            b.Append(" ZOMBIE{id=").Append(zombieId).Append(" missing=1}");
        }
        else
        {
            b.Append(" ZOMBIE{id=").Append(zombie.entityId)
             .Append(" class=").Append(zombie.EntityClass != null ? zombie.EntityClass.entityClassName : "<none>")
             .Append(" hp=").Append(zombie.Health).Append('/').Append(zombie.GetMaxHealth())
             .Append(" faction=").Append(RebirthDogCombatRelationService.DescribeFaction(zombie))
             .Append(" atk=").Append(TargetId(zombie.GetAttackTarget()))
             .Append(" rev=").Append(TargetId(zombie.GetRevengeTarget()))
             .Append(" investigate=").Append(zombie.HasInvestigatePosition ? "1" : "0")
             .Append(" investigateTicks=").Append(zombie.GetInvestigatePositionTicks())
             .Append(" alertTicks=").Append(zombie.GetAlertTicks())
             .Append(" move=").Append(zombie.moveHelper != null && zombie.moveHelper.IsActive ? "1" : "0")
             .Append(" blocked=").Append(zombie.moveHelper != null ? zombie.moveHelper.BlockedTime.ToString("0.00", CultureInfo.InvariantCulture) : "n/a")
             .Append('}');
        }

        if (dog != null && zombie != null)
        {
            EntityPlayer owner = ResolveOwnerForDebug(player, dog);
            string dzReason, zdReason, damageDz, damageZd;
            bool dogTargets = RebirthDogCombatRelationService.CanDogTarget(dog, zombie, owner, false, out dzReason);
            bool zombieTargets = RebirthDogCombatRelationService.CanZombieTargetDog(zombie, dog, out zdReason);
            bool dogDamages = RebirthDogCombatRelationService.CanDamage(dog, zombie, out damageDz);
            bool zombieDamages = RebirthDogCombatRelationService.CanDamage(zombie, dog, out damageZd);
            b.Append(" REL{dogToZombie=").Append(RebirthDogCombatRelationService.DescribeRelationship(dog, zombie))
             .Append(" target=").Append(dogTargets ? "1" : "0").Append(':').Append(dzReason)
             .Append(" damage=").Append(dogDamages ? "1" : "0").Append(':').Append(damageDz)
             .Append(" zombieToDog=").Append(RebirthDogCombatRelationService.DescribeRelationship(zombie, dog))
             .Append(" target=").Append(zombieTargets ? "1" : "0").Append(':').Append(zdReason)
             .Append(" damage=").Append(zombieDamages ? "1" : "0").Append(':').Append(damageZd).Append('}');
        }

        if (zombie != null)
        {
            b.Append(" EAI{").Append(RebirthDogZombieEaiBridge.GetExecutingTaskNames(zombie)).Append('}')
             .Append(" FILTERS{").Append(RebirthDogZombieEaiBridge.GetDogFilterSummary(zombie)).Append('}');
        }
        return b.ToString();
    }

    private static EntityRebirthDogCompanion ResolveDog(EntityPlayer player, int requestedId)
    {
        if (requestedId > 0)
        {
            EntityRebirthDogCompanion requested = player.world.GetEntity(requestedId) as EntityRebirthDogCompanion;
            if (requested != null && Owns(player, requested)) return requested;
            return null;
        }
        EntityRebirthDogCompanion best = null;
        float bestSq = float.MaxValue;
        RebirthNpcRuntimeState[] states = RebirthNpcRuntimeRegistry.GetSnapshot();
        string ownerId;
        if (!RebirthDogLifecycleService.TryResolveOwnerId(player, out ownerId)) return null;
        for (int i = 0; i < states.Length; i++)
        {
            RebirthNpcRuntimeState state = states[i];
            if (state == null || !string.Equals(state.ProfileId, RebirthDogDefinitions.ProfileId, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(state.OwnerId ?? string.Empty, ownerId, StringComparison.OrdinalIgnoreCase)) continue;
            int entityId;
            if (!RebirthNpcRuntimeRegistry.TryGetEntityId(state.StableId, out entityId)) continue;
            EntityRebirthDogCompanion dog = player.world.GetEntity(entityId) as EntityRebirthDogCompanion;
            if (dog == null || dog.IsDead()) continue;
            float sq = (dog.position - player.position).sqrMagnitude;
            if (sq < bestSq) { bestSq = sq; best = dog; }
        }
        return best;
    }

    private static EntityZombie ResolveZombie(World world, EntityRebirthDogCompanion dog, int requestedId)
    {
        if (requestedId > 0) return world.GetEntity(requestedId) as EntityZombie;
        List<Entity> entities = world.GetEntitiesInBounds(typeof(EntityZombie),
            new Bounds(dog.position, Vector3.one * 80f), new List<Entity>());
        EntityZombie best = null;
        float bestSq = float.MaxValue;
        for (int i = 0; i < entities.Count; i++)
        {
            EntityZombie z = entities[i] as EntityZombie;
            if (z == null || z.IsDead()) continue;
            float sq = (z.position - dog.position).sqrMagnitude;
            if (sq < bestSq) { bestSq = sq; best = z; }
        }
        return best;
    }

    private static bool Owns(EntityPlayer player, EntityRebirthDogCompanion dog)
    {
        string id;
        return player != null && dog != null && dog.RebirthRuntimeState != null &&
               RebirthDogLifecycleService.TryResolveOwnerId(player, out id) &&
               string.Equals(id, dog.RebirthRuntimeState.OwnerId ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    private static EntityPlayer ResolveOwnerForDebug(EntityPlayer fallback, EntityRebirthDogCompanion dog)
    {
        if (fallback != null && Owns(fallback, dog)) return fallback;
        if (dog == null || dog.world == null || dog.RebirthRuntimeState == null) return null;
        List<EntityPlayer> players = dog.world.Players != null ? dog.world.Players.list : null;
        if (players == null) return null;
        for (int i = 0; i < players.Count; i++) if (Owns(players[i], dog)) return players[i];
        return null;
    }

    private static string FormatVector(Vector3 value)
    {
        return value.x.ToString("0.00", CultureInfo.InvariantCulture) + "," +
               value.y.ToString("0.00", CultureInfo.InvariantCulture) + "," +
               value.z.ToString("0.00", CultureInfo.InvariantCulture);
    }

    private static string TargetId(EntityAlive entity)
    {
        if (entity == null) return "none";
        return entity.entityId + ":" + (entity.EntityClass != null ? entity.EntityClass.entityClassName : entity.GetType().Name);
    }
}
