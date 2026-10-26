using System;
using System.Collections.Generic;
using UnityEngine;

#nullable disable

// G01 (provisional decisions: _Documentation/NPC/Progression/PROVISIONAL_DECISIONS_G01_20261022.md).
// Resolves a human NPC's saved background / trait effects ONCE per NPC and world (immutable origin, so the cache never goes stale)
// and exposes the ones that have a real NPC-side consumer. Read-only: uses TryPeek, never creates, merges or saves a progression row.
// Server, main thread only. A missing row is retried at most every 10 s so there is no per-frame XML parsing.
internal static class RebirthG01NpcBackgroundEffects
{
    internal sealed class Profile
    {
        internal string BackgroundId;
        internal RebirthBackgroundBonusDefinition Bonus;
        internal int BagSlots;            // background storage bonus (physical NPC bag cells), same tuning as the player's backpack bonus
        internal float WoodMultiplier = 1f; // professional logging wood_output_multiplier, >= 1
        internal float TraitHarvest;      // trait harvest.count (native HarvestCount perc_add)
        internal float TraitHeavyStamina; // trait stamina.heavy_tools (native StaminaLoss perc_add with heavy tools held)
    }

    private static readonly Dictionary<RebirthNpcStableId, Profile> Cache = new Dictionary<RebirthNpcStableId, Profile>();
    private static readonly Dictionary<RebirthNpcStableId, float> RetryAt = new Dictionary<RebirthNpcStableId, float>();
    private static World cacheWorld;
    private const string HeavyStaminaCVar = "$rbTraitHeavyStamina";

    internal static bool TryGetProfile(RebirthNpcStableId id, out Profile profile)
    {
        profile = null;
        var world = GameManager.Instance?.World;
        if(id.IsEmpty || world == null || world.IsRemote() || !ThreadManager.IsMainThread()) return false;
        if(!ReferenceEquals(world, cacheWorld)) { cacheWorld = world; Cache.Clear(); RetryAt.Clear(); }
        if(Cache.TryGetValue(id, out profile)) return profile != null;
        if(RetryAt.TryGetValue(id, out float next) && Time.unscaledTime < next) return false;
        if(!RebirthSurvivorMode.IsEnabledForCurrentWorld() || !RebirthBackgroundBonusRegistry.IsReady ||
           !RebirthHumanProgression.TryPeek(id, out var record) || record == null || !record.IsComplete || record.Origin == null)
        { RetryAt[id] = Time.unscaledTime + 10f; return false; }
        profile = Build(record);
        Cache[id] = profile;
        RetryAt.Remove(id);
        return true;
    }

    private static Profile Build(RebirthWorldCharacterRecord record)
    {
        var p = new Profile { BackgroundId = record.Origin.BackgroundId };
        RebirthBackgroundBonusService.TryGetSignatureBonus(record, out p.Bonus);
        p.BagSlots = RebirthBackgroundStorageService.BackpackBonus(record);
        if(p.Bonus != null && string.Equals(p.Bonus.Id, RebirthResourceSignatureService.ProfessionalLoggingBonusId, StringComparison.OrdinalIgnoreCase))
            p.WoodMultiplier = Mathf.Max(1f, RebirthResourceSignatureService.GetTuning(p.Bonus.Id, "wood_output_multiplier", 1.5f));
        p.TraitHarvest = RebirthTraitGameplayModifierService.GetHarvestCountBonus(record);
        p.TraitHeavyStamina = RebirthTraitGameplayModifierService.GetHeavyToolStaminaBonus(record);
        return p;
    }

    internal static bool TryGetSignatureBonus(EntityRebirthHumanoidNPC npc, out RebirthBackgroundBonusDefinition bonus)
    {
        bonus = null;
        return npc != null && npc.RebirthRuntimeState != null && TryGetProfile(npc.RebirthRuntimeState.StableId, out var p) && (bonus = p.Bonus) != null;
    }

    // Extra physical bag cells from the NPC's background (Scavenger). Called by RebirthHumanEquipment.Capacity; 0 while unknown.
    internal static int BagSlotBonus(EntityRebirthHumanoidNPC npc)
    {
        return npc != null && npc.RebirthRuntimeState != null && TryGetProfile(npc.RebirthRuntimeState.StableId, out var p) ? p.BagSlots : 0;
    }

    // Native passive projection (same CVars and buff the player uses), idempotent: only writes when a value differs.
    internal static void SyncPassives(EntityRebirthHumanoidNPC npc)
    {
        if(npc == null || npc.Buffs == null || npc.RebirthRuntimeState == null) return;
        string buff = RebirthTraitGameplayModifierService.NativePassiveBuff;
        if(!TryGetProfile(npc.RebirthRuntimeState.StableId, out var p))
        {
            return;
        }
        bool any = Math.Abs(p.TraitHarvest) > 0.0005f || Math.Abs(p.TraitHeavyStamina) > 0.0005f;
        if(!any && !npc.Buffs.HasBuff(buff)) return;
        Set(npc, RebirthTraitGameplayModifierService.HarvestCountCVar, p.TraitHarvest);
        Set(npc, HeavyStaminaCVar, p.TraitHeavyStamina);
        if(any) { if(!npc.Buffs.HasBuff(buff)) npc.Buffs.AddBuff(buff); }
        else npc.Buffs.RemoveBuff(buff);
    }

    // Professional Logging for an NPC: tree wood yield multiplier, fractional part resolved with the world random (same rule as the player).
    internal static void ScaleLoggingWood(EntityAlive entity, Block block, List<ItemStack> emitted, GameRandom random)
    {
        var npc = entity as EntityRebirthHumanoidNPC;
        if(npc == null || npc.RebirthRuntimeState == null || emitted == null || random == null || !RebirthResourceSignatureService.IsTree(block)) return;
        if(!TryGetProfile(npc.RebirthRuntimeState.StableId, out var p) || p.WoodMultiplier <= 1f) return;
        for(int i = 0; i < emitted.Count; i++)
        {
            var stack = emitted[i];
            if(stack == null || stack.IsEmpty() || !RebirthResourceSignatureService.IsWood(stack.itemValue)) continue;
            float raw = stack.count * p.WoodMultiplier;
            int count = Mathf.FloorToInt(raw);
            if(raw - count > 0f && random.RandomFloat < raw - count) count++;
            if(count > stack.count) stack.count = count;
        }
    }

    // Bounded recovery for the bag-capacity bonus: progression can be installed after the loadout / UI reconcile ran. Only the existing
    // journaled RebirthP2NpcBagCapacityMigration.TryReconcile is used (never a direct resize). Per NPC: first attempt as soon as the
    // profile exists, then every 30 s, at most 6 attempts; stops when bag size == expected (base + bonuses). Cheap pass every 5 s, 8 NPCs max.
    private sealed class BagState { internal int Attempts; internal float Next; internal bool Done; }
    private static readonly Dictionary<RebirthNpcStableId, BagState> BagStates = new Dictionary<RebirthNpcStableId, BagState>();
    private static World bagWorld;
    private static float bagNextPass;
    internal static void TickBagReconcile()
    {
        var world = GameManager.Instance?.World;
        if(world == null || world.IsRemote() || !ThreadManager.IsMainThread() || Time.unscaledTime < bagNextPass) return;
        bagNextPass = Time.unscaledTime + 5f;
        if(!ReferenceEquals(world, bagWorld)) { bagWorld = world; BagStates.Clear(); }
        int budget = 8;
        foreach(var runtime in RebirthNpcRuntimeRegistry.GetSnapshot())
        {
            if(budget <= 0) break;
            if(runtime.PreparedRestorationPending || !RebirthNpcRuntimeRegistry.TryGetEntityId(runtime.StableId, out var entityId)) continue;
            if(BagStates.TryGetValue(runtime.StableId, out var state) ? state.Done || state.Attempts >= 6 || Time.unscaledTime < state.Next : false) continue;
            var npc = world.GetEntity(entityId) as EntityRebirthHumanoidNPC;
            if(npc == null || npc.IsDead() || npc.bag == null || !RebirthHumanEquipment.Human(npc)) continue;
            if(!TryGetProfile(runtime.StableId, out var p)) continue;
            if(state == null) BagStates[runtime.StableId] = state = new BagState();
            if(p.BagSlots <= 0) { state.Done = true; continue; }
            budget--;
            try
            {
                int baseSlots = (int?)RebirthHumanEquipment.Read(npc).Attribute("base") ?? 0;
                if(baseSlots >= 1 && RebirthHumanEquipment.Capacity(npc, RebirthHumanEquipment.Get(npc, 9), RebirthHumanEquipment.Get(npc, 1), baseSlots) == npc.bag.SlotCount)
                { state.Done = true; continue; }
                state.Attempts++; state.Next = Time.unscaledTime + 30f;
                if(RebirthP2NpcBagCapacityMigration.TryReconcile(npc) && baseSlots >= 1 &&
                   RebirthHumanEquipment.Capacity(npc, RebirthHumanEquipment.Get(npc, 9), RebirthHumanEquipment.Get(npc, 1), baseSlots) == npc.bag.SlotCount) state.Done = true;
            }
            catch(Exception) { state.Attempts++; state.Next = Time.unscaledTime + 30f; }
        }
    }

    private static void Set(EntityAlive npc, string key, float value)
    { if(Math.Abs(npc.Buffs.GetCustomVar(key) - value) > 0.0005f) npc.Buffs.SetCustomVar(key, value); }
}
