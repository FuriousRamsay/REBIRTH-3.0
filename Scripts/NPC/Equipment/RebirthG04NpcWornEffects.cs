using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

#nullable disable

// G04: worn legacy armor mods on NPCs (docs: _Documentation/NPC/Equipment/G04_WORN_EFFECTS_20261022.md).
// Native EffectManager.GetValue already walks equipment -> installed ItemValue.Modifications for any EntityAlive it is given,
// so passive effects need no copy. Two things are NOT guaranteed on an NPC and are projected here from the worn mods:
//  1. the equip-update CVars ($ModStrength, $RebirthArmorDodgeChance, $RampageIncreaseChance ...) that the dodge / Rampage
//     consumers read: the XML sets them from onSelfEquipUpdate, which is only a player-driven trigger path;
//  2. a movement scale from the native RunSpeed passive (no native NPC locomotion consumer) used by the follow controller.
// Read-only on gear: never mutates equipment. Server, main thread, bounded per tick, idempotent (writes only when a value
// differs, removes a CVar when its mod is gone). Values mirror Config/_Rebirth/legacy_armor_modifiers.xml.
internal static class RebirthG04NpcWornEffects
{
    private const string StrengthCVar = "$ModStrength", DexterityCVar = "$ModDexterity", ConstitutionCVar = "$ModConstitution",
        IntelligenceCVar = "$ModIntelligence", CharismaCVar = "$ModCharisma", DodgeCVar = "$RebirthArmorDodgeChance",
        GeneticsDodgeCVar = "$RebirthArmorGeneticsDodgeChance", RampageChanceCVar = "$RampageIncreaseChance",
        RampageDurationCVar = "$RampageIncreaseDuration";
    private static readonly float[] RampageTier = { 0.2f, 0.4f, 0.6f, 0.8f, 1f };
    private static readonly string[] Slots = { "Head", "Chest", "Hands", "Feet" };

    private struct Projection
    {
        internal float Strength, Dexterity, Constitution, Intelligence, Charisma, Dodge, GeneticsDodge, Rampage;
    }

    private static readonly Dictionary<int, float> RunScale = new Dictionary<int, float>();
    private static World scaleWorld;
    private static float nextSync, nextPrune;
    private static int cursor;

    // Cached movement scale (1 when no RunSpeed mod). O(1); never evaluates effects.
    internal static float RunSpeedScale(EntityAlive npc) =>
        npc != null && RunScale.TryGetValue(npc.entityId, out float scale) ? scale : 1f;

    internal static void Tick()
    {
        var world = GameManager.Instance?.World;
        if(world == null || world.IsRemote() || !ThreadManager.IsMainThread() || Time.unscaledTime < nextSync) return;
        nextSync = Time.unscaledTime + 1f;
        if(!ReferenceEquals(world, scaleWorld)) { scaleWorld = world; RunScale.Clear(); }
        if(Time.unscaledTime >= nextPrune) { nextPrune = Time.unscaledTime + 30f; Prune(world); }
        var actors = RebirthNpcRuntimeRegistry.GetSnapshot();
        if(actors.Length == 0) { cursor = 0; return; }
        int budget = Math.Min(16, actors.Length);
        for(int i = 0; i < budget; i++)
        {
            var runtime = actors[(cursor + i) % actors.Length];
            if(!RebirthNpcRuntimeRegistry.TryGetEntityId(runtime.StableId, out var entityId)) continue;
            Sync(world.GetEntity(entityId) as EntityRebirthHumanoidNPC);
        }
        cursor = (cursor + budget) % actors.Length;
    }

    // Immediate refresh after an authorized armor write (the 1 s tick would otherwise lag one second).
    internal static void SyncNow(EntityRebirthHumanoidNPC npc)
    {
        var world = npc?.world;
        if(world == null || world.IsRemote() || !ThreadManager.IsMainThread()) return;
        try { Sync(npc); } catch(Exception) { }
    }

    private static void Sync(EntityRebirthHumanoidNPC npc)
    {
        if(npc == null || npc.IsDead() || npc.IsPreparedRestorationPending || npc.Buffs == null || npc.equipment == null) return;
        var p = new Projection();
        try
        {
            for(int slot = 0; slot < Slots.Length; slot++)
            {
                var mods = npc.equipment.ItemGrid[slot]?.itemValue?.modifications;
                if(mods == null) continue;
                for(int m = 0; m < mods.Length; m++)
                {
                    string name = mods[m] != null && !mods[m].IsEmpty() ? mods[m].ItemClass?.Name : null;
                    if(name != null) Accumulate(name, ref p);
                }
            }
        }
        catch(Exception) { return; } // unreadable frame: keep the previous projection rather than clearing it
        var b = npc.Buffs;
        Apply(b, StrengthCVar, p.Strength); Apply(b, DexterityCVar, p.Dexterity); Apply(b, ConstitutionCVar, p.Constitution);
        Apply(b, IntelligenceCVar, p.Intelligence); Apply(b, CharismaCVar, p.Charisma);
        Apply(b, DodgeCVar, p.Dodge); Apply(b, GeneticsDodgeCVar, p.GeneticsDodge);
        Apply(b, RampageChanceCVar, p.Rampage); Apply(b, RampageDurationCVar, p.Rampage);
        float scale = Mathf.Clamp(RebirthP11NpcLootEffects.Eval(PassiveEffects.RunSpeed, npc, 1f), 1f, 1.25f);
        if(scale > 1.0005f) RunScale[npc.entityId] = scale; else RunScale.Remove(npc.entityId);
    }

    private static void Accumulate(string name, ref Projection p)
    {
        if(Is(name, "ModGenetics")) { p.GeneticsDodge = 0.05f; return; }
        if(name.StartsWith("ModStrength", StringComparison.OrdinalIgnoreCase)) p.Strength = 1f;
        else if(name.StartsWith("ModDexterity", StringComparison.OrdinalIgnoreCase))
        {
            p.Dexterity = 1f;
            if(Is(name, "ModDexterity") || Is(name, "ModDexterityDodge")) p.Dodge = 0.05f;
        }
        else if(name.StartsWith("ModConstitution", StringComparison.OrdinalIgnoreCase)) p.Constitution = 1f;
        else if(name.StartsWith("ModIntelligence", StringComparison.OrdinalIgnoreCase)) p.Intelligence = 1f;
        else if(name.StartsWith("ModCharisma", StringComparison.OrdinalIgnoreCase)) p.Charisma = 1f;
        else if(name.StartsWith("ModRampageIncreaseTier", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(name.Substring("ModRampageIncreaseTier".Length), out int tier) && tier >= 1 && tier <= RampageTier.Length)
            p.Rampage = Math.Max(p.Rampage, RampageTier[tier - 1]);
    }

    private static bool Is(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    private static void Apply(EntityBuffs buffs, string key, float value)
    {
        float current = buffs.GetCustomVar(key);
        if(value <= 0f) { if(current != 0f) buffs.RemoveCustomVar(key); }
        else if(Math.Abs(current - value) > 0.0005f) buffs.SetCustomVar(key, value);
    }

    private static void Prune(World world)
    {
        List<int> gone = null;
        foreach(var id in RunScale.Keys)
        {
            var entity = world.GetEntity(id) as EntityAlive;
            if(entity == null || entity.IsDead()) (gone ?? (gone = new List<int>())).Add(id);
        }
        if(gone != null) for(int i = 0; i < gone.Count; i++) RunScale.Remove(gone[i]);
    }

    // Read-only diagnostic for the Game Bridge npcgear handler: worn mods per armor slot, the projected CVars and the native
    // values EffectManager yields for THIS NPC (base 1; stage/time/quantity use base 100). Not called per frame.
    internal static JObject Describe(EntityRebirthHumanoidNPC npc)
    {
        var o = new JObject();
        try
        {
            var worn = new JArray();
            for(int slot = 0; slot < Slots.Length; slot++)
            {
                var mods = npc.equipment?.ItemGrid[slot]?.itemValue?.modifications;
                var names = new JArray();
                if(mods != null)
                    for(int m = 0; m < mods.Length; m++)
                        if(mods[m] != null && !mods[m].IsEmpty() && mods[m].ItemClass != null) names.Add(mods[m].ItemClass.Name);
                worn.Add(new JObject { ["slot"] = Slots[slot], ["mods"] = names });
            }
            o["worn"] = worn;
            var cvars = new JObject();
            foreach(string key in new[] { StrengthCVar, DexterityCVar, ConstitutionCVar, IntelligenceCVar, CharismaCVar, DodgeCVar,
                GeneticsDodgeCVar, RampageChanceCVar, RampageDurationCVar })
                cvars[key] = npc.Buffs?.GetCustomVar(key) ?? 0f;
            o["cvars"] = cvars;
            var native = new JObject();
            foreach(var type in new[] { PassiveEffects.EntityDamage, PassiveEffects.DamageModifier, PassiveEffects.DismemberChance,
                PassiveEffects.AttacksPerMinute, PassiveEffects.RoundsPerMinute, PassiveEffects.RunSpeed, PassiveEffects.BagSize,
                PassiveEffects.StaminaLoss, PassiveEffects.StaminaChangeOT, PassiveEffects.HealthMax, PassiveEffects.DegradationPerUse })
                native[type.ToString()] = RebirthP11NpcLootEffects.Eval(type, npc, 1f);
            foreach(var type in new[] { PassiveEffects.ScavengingTime, PassiveEffects.LootQuantity, PassiveEffects.LootStage,
                PassiveEffects.GameStage })
                native[type.ToString() + "@100"] = RebirthP11NpcLootEffects.Eval(type, npc, 100f);
            o["native"] = native;
            o["runSpeedScale"] = RunSpeedScale(npc);
            o["lootAttribution"] = RebirthG04LootSearchAttribution.Describe(npc, RebirthCompanionLeadership.DirectingPlayer(npc.RebirthRuntimeState?.StableId ?? default), null, 0f);
        }
        catch(Exception ex) { o["error"] = ex.Message; }
        return o;
    }
}
