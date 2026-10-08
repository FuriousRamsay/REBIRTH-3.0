using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;

#nullable disable

/// <summary>
/// Generic runtime interpreter for Survivor Trait gameplay components that do not belong to the
/// condition/metabolism resolver. The component target, not the Trait id, selects behavior.
/// This keeps launch Trait mechanics data-driven and preserves server authority for LBD awards.
/// </summary>
public static class RebirthTraitGameplayModifierService
{
    public const string GearCarryCapacityCVar = "$rbSurvivorGearCarryDelta";
    public const string CarryCapacityCVar = "$rbSurvivorTraitCarryDelta";
    public const string BarterBuyingCVar = "$rbSurvivorTraitBarterBuying";
    public const string BarterSellingCVar = "$rbSurvivorTraitBarterSelling";
    public const string HarvestCountCVar = "$rbSurvivorTraitHarvestCount";
    public const string NativePassiveBuff = "RebirthSurvivorTraitNativePassives";
    // Backward-compatible alias used by Chunk-16 diagnostics and any older call sites.
    public const string CarryCapacityBuff = NativePassiveBuff;

    private const float BarterTraitCap = 0.08f;
    private const float HarvestTraitCap = 0.20f;

    public static float GetSkillGainMultiplier(EntityPlayer player, RebirthWorldCharacterRecord record, string skillId, float currentSkill)
    {
        if (record == null || record.Origin == null || string.IsNullOrEmpty(skillId)) return 1f;
        return GetSkillGainMultiplier(record.Origin.TraitIds, skillId, currentSkill);
    }

    // Shared read-only calculation lets the owner UI explain the same threshold and
    // taper rules used by authoritative awards, without needing a server record.
    public static float GetSkillGainMultiplier(IEnumerable<string> traitIds, string skillId, float currentSkill)
    {
        if (traitIds == null || string.IsNullOrEmpty(skillId)) return 1f;
        float result = 1f;
        foreach (string traitId in traitIds)
        {
            RebirthConditionModifierProfileDefinition profile;
            if (!TryGetProfile(traitId, out profile)) continue;
            for (int c = 0; c < profile.Components.Count; c++)
            {
                RebirthConditionModifierComponent component = profile.Components[c];
                if (component == null || !string.Equals(component.Phase, "runtime", StringComparison.OrdinalIgnoreCase)) continue;
                string target = Normalize(component.Target);
                if (!target.StartsWith("skill.gain.", StringComparison.Ordinal)) continue;
                if (!SkillTargetApplies(target, skillId, currentSkill)) continue;
                float value;
                if (!Parse(component.Value, out value)) continue;
                value = GetThresholdBlend(target, currentSkill, value);
                Apply(ref result, component.Operation, value);
            }
        }
        return Clamp(result, 0.50f, 1.50f);
    }

    /// <summary>Returns the authored creation-time native CarryCapacity delta for this character.</summary>
    public static int GetUnencumberedSlotDelta(RebirthWorldCharacterRecord record)
    {
        if (record == null || record.Origin == null) return 0;
        return GetUnencumberedSlotDelta(record.Origin.TraitIds);
    }

    public static int GetUnencumberedSlotDelta(IEnumerable<string> traitIds)
    {
        if (traitIds == null) return 0;
        float total = 0f;
        foreach (string traitId in traitIds)
        {
            RebirthConditionModifierProfileDefinition profile;
            if (!TryGetProfile(traitId, out profile)) continue;
            for (int c = 0; c < profile.Components.Count; c++)
            {
                RebirthConditionModifierComponent component = profile.Components[c];
                if (component == null || !string.Equals(component.Phase, "creation", StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(component.Target, "inventory.unencumbered_slots", StringComparison.OrdinalIgnoreCase)) continue;
                float value;
                if (!Parse(component.Value, out value)) continue;
                if (string.Equals(component.Operation, "add", StringComparison.OrdinalIgnoreCase)) total += value;
            }
        }
        return Math.Max(-20, Math.Min(20, (int)Math.Round(total)));
    }

    public static float GetBarterBuyingBonus(RebirthWorldCharacterRecord record)
    {
        return record == null || record.Origin == null ? 0f : GetAdditiveRuntimeTotal(record.Origin.TraitIds, "barter.buying", 0f, BarterTraitCap);
    }

    public static float GetBarterSellingBonus(RebirthWorldCharacterRecord record)
    {
        return record == null || record.Origin == null ? 0f : GetAdditiveRuntimeTotal(record.Origin.TraitIds, "barter.selling", 0f, BarterTraitCap);
    }

    public static float GetHarvestCountBonus(RebirthWorldCharacterRecord record)
    {
        return record == null || record.Origin == null ? 0f : GetAdditiveRuntimeTotal(record.Origin.TraitIds, "harvest.count", -0.20f, HarvestTraitCap);
    }

    /// <summary>
    /// Returns a bounded driver-specific vehicle fuel-use multiplier from runtime Trait components.
    /// It only reads the authoritative completed Survivor record for a player currently attached to
    /// the supplied vehicle. No client request path is involved.
    /// </summary>
    public static float GetCraftTimeMultiplier(EntityPlayer player,string skillId)
    {
        if(player==null || string.IsNullOrEmpty(skillId)) return 1f;
        RebirthWorldCharacterRecord record;
        if(!RebirthWorldCharacterService.TryGet(player,out record) || record==null || !record.IsComplete || record.Origin==null) return 1f;
        string target="craft.time."+Normalize(skillId).Replace("skill.","");
        return GetMultiplicativeRuntimeTotal(record.Origin.TraitIds,target,0.75f,1.25f);
    }

    public static float GetPhysicalLiteratureStudyTimeMultiplier(EntityPlayer player)
    {
        if(player==null)return 1f;
        RebirthWorldCharacterRecord record;
        if(!RebirthWorldCharacterService.TryGet(player,out record)||record==null||!record.IsComplete||record.Origin==null)return 1f;
        return GetMultiplicativeRuntimeTotal(record.Origin.TraitIds,"literature.study_time.physical",0.50f,1.75f);
    }

    public static float GetAudioLiteratureStudyTimeMultiplier(EntityPlayer player)
    {
        if(player==null)return 1f;
        RebirthWorldCharacterRecord record;
        if(!RebirthWorldCharacterService.TryGet(player,out record)||record==null||!record.IsComplete||record.Origin==null)return 1f;
        return GetMultiplicativeRuntimeTotal(record.Origin.TraitIds,"literature.study_time.audio",0.50f,1.75f);
    }

    public static float GetVehicleRepairHealthMultiplier(EntityPlayer player)
    {
        if(player==null) return 1f;
        RebirthWorldCharacterRecord record;
        if(!RebirthWorldCharacterService.TryGet(player,out record) || record==null || !record.IsComplete || record.Origin==null) return 1f;
        return GetMultiplicativeRuntimeTotal(record.Origin.TraitIds,"vehicle.repair.health",0.75f,1.25f);
    }

    public static float GetMedicineFirstAidReserveMultiplier(RebirthWorldCharacterRecord record)
    {
        if (record == null || record.Origin == null) return 1f;
        return GetMultiplicativeRuntimeTotal(record.Origin.TraitIds, "medicine.firstaid.reserve", 0.75f, 1.25f);
    }

    public static float GetMedicineFirstAidReserveMultiplier(EntityPlayer player)
    {
        if (player == null) return 1f;
        RebirthWorldCharacterRecord record;
        if (!RebirthWorldCharacterService.TryGet(player, out record) || record == null || !record.IsComplete) return 1f;
        return GetMedicineFirstAidReserveMultiplier(record);
    }

    public static float GetVehicleFuelUseMultiplier(EntityVehicle vehicle)
    {
        if (vehicle == null || vehicle.world == null || vehicle.world.Players == null || vehicle.world.Players.list == null)
            return 1f;

        List<EntityPlayer> players = vehicle.world.Players.list;
        for (int i = 0; i < players.Count; i++)
        {
            EntityPlayer player = players[i];
            if (player == null || player.AttachedToEntity != vehicle) continue;
            RebirthWorldCharacterRecord record;
            if (!RebirthWorldCharacterService.TryGet(player, out record) || record == null || !record.IsComplete || record.Origin == null)
                return 1f;
            return GetMultiplicativeRuntimeTotal(record.Origin.TraitIds, "vehicle.fuel_use", 0.75f, 1.25f);
        }
        return 1f;
    }

    /// <summary>
    /// Mirrors Survivor Trait native-passive values into the vanilla effect system. Physical Bag
    /// length remains owned by Survivor Backpack gear; CarryCapacity changes only unencumbered slots.
    /// Barter and HarvestCount use their native passive-effect contracts and are bounded here.
    /// </summary>
    private sealed class PassiveProjection
    {
        public object World, Definitions, OwnerKey;
        public string DefinitionHash;
        public long Revision;
        public bool Initialized, HasCharacter;
        public int Carry;
        public float Buy, Sell, Harvest, HeavyStamina;
        public PassiveProjection() { }
    }
    private static ConditionalWeakTable<EntityPlayer, PassiveProjection> passiveByPlayer = new ConditionalWeakTable<EntityPlayer, PassiveProjection>();

    public static void ResetPassiveProjections()
    {
        passiveByPlayer = new ConditionalWeakTable<EntityPlayer, PassiveProjection>();
    }

    private static PassiveProjection GetPassiveProjection(EntityPlayer player, out bool changed)
    {
        changed = false;
        if (player == null || player.Buffs == null || player.world == null
            || GameManager.Instance == null || !ReferenceEquals(player.world, GameManager.Instance.World)) return null;
        RebirthSurvivorOwnerScalars remote = null;
        RebirthWorldCharacterRecord record = null;
        bool hasCharacter;
        if (player.world.IsRemote())
        {
            // Do not apply the local owner's values, or clear another entity's native values.
            if (!ReferenceEquals(player.world.GetPrimaryPlayer(), player)) return null;
            hasCharacter = RebirthSurvivorClientState.TryGetOwnerScalars(player, out remote);
        }
        else hasCharacter = RebirthWorldCharacterService.TryGet(player, out record) && record != null && record.IsComplete;
        object ownerKey = remote != null ? (object)remote : record;
        long revision = record != null ? record.Revision : 0L;
        object definitions = RebirthSurvivorDefinitionRegistry.Bundle;
        string definitionHash = RebirthSurvivorDefinitionRegistry.SemanticHash;
        PassiveProjection projection = passiveByPlayer.GetOrCreateValue(player);
        if (projection.Initialized && ReferenceEquals(projection.World, player.world)
            && ReferenceEquals(projection.Definitions, definitions) && ReferenceEquals(projection.OwnerKey, ownerKey)
            && projection.Revision == revision && projection.HasCharacter == hasCharacter
            && string.Equals(projection.DefinitionHash, definitionHash, StringComparison.Ordinal)) return projection;

        projection.World = player.world; projection.Definitions = definitions; projection.DefinitionHash = definitionHash;
        projection.OwnerKey = ownerKey; projection.Revision = revision; projection.HasCharacter = hasCharacter;
        projection.Carry = 0; projection.Buy = projection.Sell = projection.Harvest = projection.HeavyStamina = 0f;
        if (hasCharacter && remote != null)
        {
            projection.Carry = GetUnencumberedSlotDelta(remote.TraitIds);
            projection.HeavyStamina = GetAdditiveRuntimeTotal(remote.TraitIds, "stamina.heavy_tools", 0f, 0.25f);
            projection.Buy = GetAdditiveRuntimeTotal(remote.TraitIds, "barter.buying", 0f, BarterTraitCap);
            projection.Sell = GetAdditiveRuntimeTotal(remote.TraitIds, "barter.selling", 0f, BarterTraitCap);
            projection.Harvest = GetAdditiveRuntimeTotal(remote.TraitIds, "harvest.count", -0.20f, HarvestTraitCap);
        }
        else if (hasCharacter)
        {
            projection.Carry = record.Origin != null ? record.Origin.UnencumberedSlotDelta : 0;
            projection.HeavyStamina = GetAdditiveRuntimeTotal(record.Origin.TraitIds, "stamina.heavy_tools", 0f, 0.25f);
            projection.Buy = GetBarterBuyingBonus(record);
            projection.Sell = GetBarterSellingBonus(record);
            projection.Harvest = GetHarvestCountBonus(record);
        }
        projection.Initialized = true; changed = true;
        return projection;
    }

    private static void ApplyPassiveProjection(EntityPlayer player, PassiveProjection projection)
    {
        SyncGearCarry(player);
        SetCVar(player, CarryCapacityCVar, projection.Carry);
        SetCVar(player, BarterBuyingCVar, projection.Buy);
        SetCVar(player, BarterSellingCVar, projection.Sell);
        SetCVar(player, HarvestCountCVar, projection.Harvest);
        SetCVar(player, "$rbTraitHeavyStamina", projection.HeavyStamina);
        if (projection.HasCharacter)
        {
            if (!player.Buffs.HasBuff(NativePassiveBuff)) player.Buffs.AddBuff(NativePassiveBuff);
        }
        else if (player.Buffs.HasBuff(NativePassiveBuff)) player.Buffs.RemoveBuff(NativePassiveBuff);
    }

    public static void SyncNativePassiveEffects(EntityPlayer player)
    {
        bool changed;
        PassiveProjection projection = GetPassiveProjection(player, out changed);
        if (projection != null) ApplyPassiveProjection(player, projection);
    }

    // Bag needs the carry contract, not repeated barter/harvest interpretation. First load and
    // real revision changes still initialize the complete native projection synchronously.
    public static void SyncNativeCarryCapacityForBag(EntityPlayer player)
    {
        bool changed;
        PassiveProjection projection = GetPassiveProjection(player, out changed);
        if (projection == null) return;
        if (changed || projection.HasCharacter != player.Buffs.HasBuff(NativePassiveBuff)) ApplyPassiveProjection(player, projection);
        else { SyncGearCarry(player); SetCVar(player, CarryCapacityCVar, projection.Carry); }
    }

    // Existing non-getter callers retain their complete passive synchronization contract.
    public static void SyncNativeCarryCapacity(EntityPlayer player) { SyncNativePassiveEffects(player); }

    private static void SyncGearCarry(EntityPlayer player)
    {
        int desired;
        if (RebirthSurvivorGearService.TryGetDesiredPhysicalBagSlots(player, out desired))
            {
            // Offset the native base (27 by default) without overwriting its persistent CVar.
            // Preserve authored trait carry deltas while each physical expansion stays usable.
            float nativeBase = player.Buffs.GetCustomVar(".carryCapacityBase");
            if (nativeBase <= 0f) nativeBase = 27f;
            SetCVar(player, GearCarryCapacityCVar, desired - RebirthSurvivorGearService.BasePhysicalBagSlots
                + RebirthSurvivorGearService.BaseUnencumberedBagSlots - nativeBase);
        }
    }

    private static void SetCVar(EntityPlayer player, string name, float value)
    {
        if (Math.Abs(player.Buffs.GetCustomVar(name) - value) > 0.001f) player.Buffs.SetCustomVar(name, value);
    }

    private static float GetAdditiveRuntimeTotal(IEnumerable<string> traitIds, string exactTarget, float min, float max)
    {
        if (traitIds == null) return 0f;
        float total = 0f;
        foreach (string traitId in traitIds)
        {
            RebirthConditionModifierProfileDefinition profile;
            if (!TryGetProfile(traitId, out profile)) continue;
            for (int c = 0; c < profile.Components.Count; c++)
            {
                RebirthConditionModifierComponent component = profile.Components[c];
                if (component == null || !string.Equals(component.Phase, "runtime", StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(Normalize(component.Target), exactTarget, StringComparison.Ordinal)) continue;
                float value; if (!Parse(component.Value, out value)) continue;
                if (string.Equals(component.Operation, "add", StringComparison.OrdinalIgnoreCase)) total += value;
            }
        }
        return Clamp(total, min, max);
    }

    private static float GetMultiplicativeRuntimeTotal(IEnumerable<string> traitIds, string exactTarget, float min, float max)
    {
        if (traitIds == null) return 1f;
        float total = 1f;
        foreach (string traitId in traitIds)
        {
            RebirthConditionModifierProfileDefinition profile;
            if (!TryGetProfile(traitId, out profile)) continue;
            for (int c = 0; c < profile.Components.Count; c++)
            {
                RebirthConditionModifierComponent component = profile.Components[c];
                if (component == null || !string.Equals(component.Phase, "runtime", StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(Normalize(component.Target), exactTarget, StringComparison.Ordinal)) continue;
                float value; if (!Parse(component.Value, out value)) continue;
                Apply(ref total, component.Operation, value);
            }
        }
        return Clamp(total, min, max);
    }

    public static bool SupportsSkillGainTarget(string target,string skillId,float currentSkill)
    {
        string t=Normalize(target);
        return t.StartsWith("skill.gain.",StringComparison.Ordinal) && SkillTargetApplies(t,skillId,currentSkill);
    }

    public static bool SupportsGenericTraitTarget(string target)
    {
        string t=Normalize(target);
        return t.StartsWith("skill.gain.",StringComparison.Ordinal) ||
            t=="stamina.heavy_tools" || t=="barter.buying" || t=="barter.selling" || t=="harvest.count" ||
            t=="vehicle.fuel_use" || t=="vehicle.repair.health" ||
            t=="inventory.unencumbered_slots" || t=="medicine.firstaid.reserve" ||
            t=="literature.study_time.physical" || t=="literature.study_time.audio" ||
            t.StartsWith("craft.time.",StringComparison.Ordinal);
    }

    private static bool SkillTargetApplies(string target, string skillId, float currentSkill)
    {
        string s = Normalize(skillId);
        string stem = target.Substring("skill.gain.".Length);
        bool below50 = stem.EndsWith(".below50", StringComparison.Ordinal);
        bool taper4060 = stem.EndsWith(".taper40_60", StringComparison.Ordinal);
        if (below50) stem = stem.Substring(0, stem.Length - ".below50".Length);
        else if (taper4060) stem = stem.Substring(0, stem.Length - ".taper40_60".Length);

        if (below50 && currentSkill >= 50f) return false;
        if (taper4060 && currentSkill >= 60f) return false;

        return stem == "all" ||
            (stem == "physical" && IsPhysicalSkill(s)) ||
            (stem == "technical" && IsTechnicalSkill(s)) ||
            (stem == "combat" && IsCombatSkill(s)) ||
            (stem == "ranged" && IsRangedSkill(s)) ||
            string.Equals(stem, s.StartsWith("skill.", StringComparison.Ordinal) ? s.Substring(6) : s, StringComparison.Ordinal);
    }

    public static float GetThresholdBlend(string target, float currentSkill, float authoredValue)
    {
        string t = Normalize(target);
        if (!t.EndsWith(".taper40_60", StringComparison.Ordinal)) return authoredValue;
        if (currentSkill <= 40f) return authoredValue;
        if (currentSkill >= 60f) return 1f;
        float x = (currentSkill - 40f) / 20f;
        return authoredValue + (1f - authoredValue) * x;
    }

    private static bool IsPhysicalSkill(string s)
    {
        return s=="skill.spears"||s=="skill.clubs"||s=="skill.swords"||s=="skill.axes"||s=="skill.batons"||s=="skill.hammers"||
               s=="skill.knives"||s=="skill.scythes"||s=="skill.knuckles"||s=="skill.unarmed"||s=="skill.mining"||s=="skill.logging"||s=="skill.athletics";
    }
    private static bool IsTechnicalSkill(string s)
    {
        return s=="skill.salvage"||s=="skill.mechanics"||s=="skill.maintenance"||s=="skill.construction"||s=="skill.electrical"||
               s=="skill.metalworking"||s=="skill.gunsmithing"||s=="skill.chemistry"||s=="skill.lockpicking"||s=="skill.deployable_turrets"||s=="skill.drone_operations";
    }
    private static bool IsCombatSkill(string s)
    {
        return s=="skill.spears"||s=="skill.clubs"||s=="skill.swords"||s=="skill.axes"||s=="skill.batons"||s=="skill.hammers"||
               s=="skill.knives"||s=="skill.scythes"||s=="skill.knuckles"||s=="skill.unarmed"||s=="skill.explosives"||IsRangedSkill(s);
    }
    private static bool IsRangedSkill(string s)
    {
        return s=="skill.archery"||s=="skill.pistols"||s=="skill.revolvers"||s=="skill.heavy_handguns"||s=="skill.shotguns"||
               s=="skill.assault_rifles"||s=="skill.tactical_rifles"||s=="skill.long_range_rifles"||s=="skill.deployable_turrets"||s=="skill.drone_operations";
    }

    private static bool TryGetProfile(string traitId, out RebirthConditionModifierProfileDefinition profile)
    {
        profile = null;
        RebirthTraitDefinition trait;
        if (!RebirthSurvivorDefinitionRegistry.TryGetTrait(traitId, out trait) || trait == null) return false;
        // Generated Skill Aptitudes are fully resolved into Origin.StartingSkills during creation.
        // They intentionally have no runtime modifier profile and must never be treated as missing.
        if (RebirthSkillAptitudeTraitFactory.IsAptitude(trait)) return false;
        return
               RebirthSurvivorDefinitionRegistry.TryGetModifier(trait.ModifierId, out profile) && profile != null;
    }
    private static string Normalize(string value) { return (value ?? string.Empty).Trim().ToLowerInvariant(); }
    private static bool Parse(string value, out float parsed) { return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed); }
    private static void Apply(ref float current, string operation, float value)
    {
        if (string.Equals(operation, "multiply", StringComparison.OrdinalIgnoreCase)) current *= value;
        else if (string.Equals(operation, "add", StringComparison.OrdinalIgnoreCase)) current += value;
    }
    private static float Clamp(float value, float min, float max) { return value < min ? min : (value > max ? max : value); }
}
