using System;
using HarmonyLib;
using UnityEngine.Scripting;

// D-14: NPC-worn loot effects for the NPC-owned open. LootContainer.Spawn only accepts an
// EntityPlayer; its internal EffectManager.GetValue calls (getProbability: PassiveEffects 79
// LootProb; SpawnItem: 81 LootQuantity) bind that player. With player == null the native
// code evaluates GetValue with a null entity (no worn gear). While RebirthP11NpcLootOpen
// has an actor context set, these two prefixes re-evaluate the SAME GetValue calls with the
// NPC as the entity, so Equipment.ModifyValue traverses the NPC's worn gear. One evaluation
// per roll; the owner's effects are never added (the owner only supplies the base stage).
// Transcribed from b259 IL (getProbability 0x0000-0x01e8, SpawnItem 0x0000-0x0058). The
// context is thread-static and set only inside TryOpen, so player rolls are untouched.
internal static class RebirthP11NpcLootEffects
{
    [ThreadStatic] internal static EntityAlive Actor;

    private static float Value(PassiveEffects type, ItemValue item, float value, EntityAlive actor,
        FastTags<TagGroup.Global> tags) =>
        EffectManager.GetValue(type, item, value, actor, null, tags, true, true, true, true, true, 1, true, false);

    internal static float Eval(PassiveEffects type, EntityAlive actor, float value) =>
        Value(type, null, value, actor, default(FastTags<TagGroup.Global>));

    // Native pattern: tags = item tags when the entry has a tagged item, else the entry tags.
    internal static float Probability(EntityAlive actor, LootContainer.LootEntry entry, float lootStage, bool ignoreLootProb)
    {
        float prob = entry.prob;
        if (entry.lootProbTemplate != string.Empty && LootContainer.lootProbTemplates.ContainsKey(entry.lootProbTemplate))
        {
            var template = LootContainer.lootProbTemplates[entry.lootProbTemplate];
            for (int i = 0; i < template.templates.Count; i++)
            {
                var candidate = template.templates[i];
                if (candidate.minLevel > lootStage || candidate.maxLevel < lootStage) continue;
                prob = candidate.prob;
                break;
            }
        }
        if (entry.item != null && !entry.item.itemValue.ItemClass.ItemTags.IsEmpty)
        {
            float sandbox = LootContainer.GetSandboxProb(entry.item.itemValue.ItemClass);
            return ignoreLootProb ? prob * sandbox
                : Value(PassiveEffects.LootProb, null, prob, actor, entry.item.itemValue.ItemClass.ItemTags) * sandbox;
        }
        if (entry.tags.IsEmpty) return prob;
        return Value(PassiveEffects.LootProb, null, prob, actor, entry.tags);
    }

    [HarmonyPatch(typeof(LootContainer), nameof(LootContainer.getProbability))]
    private static class ProbabilityPatch
    {
        private static bool Prefix(EntityPlayer __0, LootContainer.LootEntry __1, float __2, bool __3, ref float __result)
        {
            var actor = Actor;
            if (actor == null || __0 != null || __1 == null) return true;
            try { __result = Probability(actor, __1, __2, __3); return false; }
            catch { return true; }
        }
    }

    [HarmonyPatch(typeof(LootContainer), nameof(LootContainer.SpawnItem))]
    private static class QuantityPatch
    {
        private static void Prefix(ref int countToSpawn, ItemValue lootItemValue,
            FastTags<TagGroup.Global> containerTags, EntityPlayer player)
        {
            var actor = Actor;
            if (actor == null || player != null || lootItemValue?.ItemClass == null) return;
            try
            {
                countToSpawn = Math.Min((int)Value(PassiveEffects.LootQuantity, actor.inventory?.holdingItemItemValue,
                    countToSpawn, actor, lootItemValue.ItemClass.ItemTags | containerTags), lootItemValue.ItemClass.MaxCount);
            }
            catch { }
        }
    }

    private static bool installed;
    internal static void Install()
    {
        if (installed || !RebirthP11SmartLootGate.NpcAttributedOpenQualified) return;
        var harmony = new Harmony("rebirth.smartloot.npc.effects.v1");
        try
        {
            RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(ProbabilityPatch));
            RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(QuantityPatch));
            installed = true;
        }
        catch { harmony.UnpatchSelf(); throw; }
    }
    internal static bool Installed => installed;
}

[Preserve]
public sealed class RebirthP11NpcLootEffectsModApi : IModApi
{
    public void InitMod(Mod mod) => RebirthP11NpcLootEffects.Install();
}
