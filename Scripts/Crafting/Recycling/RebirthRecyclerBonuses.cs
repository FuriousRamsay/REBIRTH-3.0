using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

// Calculations only. The native grouped-queue coordinator owns consumption,
// tool-array publication, hidden reward custody and final-completion delivery.
public static class RebirthRecyclerBonuses
{
    private static readonly string[] LegacyPanelParts =
    {
        "partRetrofitHousing", "partTuningBoard", "partFieldMechanismPack", "partRegulatorPack", "partPurgeCanister",
        "partServiceHousing", "partStabilityClampSet", "partRetuningCartridge", "partAdjustmentMatrix", "partControlShunt", "partProfileBridge",
        "partReinforcedHousing", "partReinforcementCore", "partStabilityFrame", "partControlLattice", "partRegulatorAssembly",
        "partMasterHousing", "partComprehensiveCore", "partControlArray", "partMasterRegulator", "partFieldArray"
    };
    public static float WearRatePerSecond(Block block)
    {
        string raw = block?.Properties?.GetString("ScrapToolWearPerMinute");
        if (!float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out float perMinute)
            || float.IsNaN(perMinute) || float.IsInfinity(perMinute)) return 1f / 60f;
        return perMinute <= 0 ? 0 : perMinute / 60f;
    }
    private static bool Valid(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0;
    private static float Remaining(ItemStack stack)
    {
        if (stack == null || stack.IsEmpty() || stack.itemValue?.ItemClass == null) return -1;
        int max = stack.itemValue.MaxUseTimes;
        return max <= 0 ? float.MaxValue : Math.Max(0, max - stack.itemValue.UseTimes);
    }
    private static List<int> WearTargets(ItemStack[] tools, IReadOnlyList<string> required)
    {
        var result = new List<int>(); var taken = new HashSet<int>();
        if (tools == null || required == null) return result;
        foreach (string token in required)
        {
            if (string.IsNullOrEmpty(token)) continue;
            int chosen = -1; float best = -1;
            for (int i = 0; i < tools.Length; i++)
            {
                if (taken.Contains(i) || Remaining(tools[i]) < 0
                    || !string.Equals(tools[i].itemValue.ItemClass.GetItemName(), token, StringComparison.OrdinalIgnoreCase)) continue;
                float left = Remaining(tools[i]);
                if (chosen < 0 || left > best) { chosen = i; best = left; }
            }
            // Exact legacy fallback. Normal queue admission rejects missing tools,
            // so this path is for the historical unknown-token wear behavior only.
            if (chosen < 0)
            {
                for (int i = 0; i < tools.Length; i++)
                {
                    float left = Remaining(tools[i]);
                    if (taken.Contains(i) || left <= 0 || tools[i].itemValue.MaxUseTimes <= 0) continue;
                    if (chosen < 0 || left > best) { chosen = i; best = left; }
                }
            }
            if (chosen >= 0) { taken.Add(chosen); result.Add(chosen); }
        }
        // Nondegradable and exhausted selected slots do not share total wear.
        result.RemoveAll(i => tools[i].itemValue.MaxUseTimes <= 0 || Remaining(tools[i]) <= 0);
        return result;
    }
    public static float MaxProcessingSeconds(ItemStack[] tools, IReadOnlyList<string> required, float rate)
    {
        if (!Valid(rate)) return 0;
        // Presence/broken gating remains mandatory even when configured wear is zero.
        if (!Healthy(tools, required)) return 0;
        if (rate <= 0) return float.PositiveInfinity;
        var targets = WearTargets(tools, required);
        if (targets.Count == 0) return float.PositiveInfinity;
        float limit = float.PositiveInfinity;
        foreach (int i in targets) limit = Math.Min(limit, Remaining(tools[i]) * targets.Count / rate);
        return limit;
    }
    private static bool Healthy(ItemStack[] tools, IReadOnlyList<string> required)
    {
        if (required == null || required.Count == 0) return true;
        foreach (string token in required)
        {
            if (string.IsNullOrEmpty(token)) continue;
            bool healthy = false;
            if (tools != null) foreach (var stack in tools)
                if (Remaining(stack) > 0 && string.Equals(stack.itemValue.ItemClass.GetItemName(), token, StringComparison.OrdinalIgnoreCase)) { healthy = true; break; }
            if (!healthy) return false;
        }
        return true;
    }
    public static ItemStack[] ApplyWear(ItemStack[] tools, IReadOnlyList<string> required, float rate, float seconds)
    {
        if (!Valid(rate) || !Valid(seconds)) throw new ArgumentOutOfRangeException("Finite nonnegative wear values required.");
        if (tools == null) return new ItemStack[0];
        var copy = new ItemStack[tools.Length];
        for (int i = 0; i < tools.Length; i++) copy[i] = tools[i]?.Clone();
        var targets = WearTargets(copy, required);
        if (rate <= 0 || seconds <= 0 || targets.Count == 0) return copy;
        float delta = rate * seconds / targets.Count;
        if (!Valid(delta)) throw new ArgumentOutOfRangeException("Tool wear overflow.");
        foreach (int i in targets)
        {
            var value = copy[i].itemValue;
            value.UseTimes = Math.Min(value.MaxUseTimes, value.UseTimes + delta);
        }
        return copy;
    }
    // The old preview estimator considered every duplicate, unlike actual wear.
    // Keep it separate from the authoritative break-time calculation above.
    public static float LegacyPreviewSecondsLeft(ItemStack[] tools, IReadOnlyList<string> required, float rate)
    {
        if (!Valid(rate)) return 0;
        if (required == null || required.Count == 0 || rate <= 0) return float.PositiveInfinity;
        var names = new HashSet<string>(required, StringComparer.OrdinalIgnoreCase);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase); float result = float.PositiveInfinity;
        if (tools != null) foreach (var stack in tools)
        {
            if (Remaining(stack) < 0) continue;
            string name = stack.itemValue.ItemClass.GetItemName(); if (!names.Contains(name)) continue;
            seen.Add(name);
            if (stack.itemValue.MaxUseTimes > 0) result = Math.Min(result, Remaining(stack) / rate);
        }
        return seen.Count == names.Count ? result : 0;
    }
    private static bool ItemExists(string name)
    {
        var item = ItemClass.GetItem(name, true);
        return item != null && item.type > 0 && item.ItemClass != null
            && string.Equals(item.ItemClass.GetItemName(), name, StringComparison.OrdinalIgnoreCase);
    }
    public static IReadOnlyList<string> UnavailableLegacyFeatures(ItemValue source)
    {
        var unavailable = new List<string>(); string name = source?.ItemClass?.GetItemName() ?? string.Empty;
        if (source != null && source.ItemClass != null && source.ItemClass.HasQuality
            && !(source.GetMetadata("crafted_by") is string crafted && !string.IsNullOrEmpty(crafted)))
            foreach (string hint in new[] { "helmet", "outfit", "gloves", "boots" })
                if (name.IndexOf(hint, StringComparison.OrdinalIgnoreCase) >= 0) { unavailable.Add("legacy-armor-mastery-progression"); break; }
        if (source != null && source.TryGetMetadata("ench_count", out int count) && count > 0)
            unavailable.Add("legacy-enchantment-catalog-and-completion-parts");
        if (name.StartsWith("controlPanel", StringComparison.OrdinalIgnoreCase))
            foreach (string part in LegacyPanelParts) if (!ItemExists(part)) unavailable.Add("legacy-control-panel-reward-item:" + part);
        if (name.StartsWith("armor", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(RebirthVariables.customScenario, "hive", StringComparison.OrdinalIgnoreCase) && !ItemExists("armorParts"))
            unavailable.Add("armorParts");
        return unavailable.AsReadOnly();
    }
    public static bool TryFreezeBonus(ItemValue source, Guid group, out IReadOnlyList<ItemStack> rewards, out IReadOnlyList<string> unavailable)
    {
        var result = new List<ItemStack>(); rewards = result.AsReadOnly(); unavailable = UnavailableLegacyFeatures(source);
        if (source == null || source.IsEmpty() || group == Guid.Empty) return false;
        // Unported mastery is reported independently; never manufacture old CVars.
        foreach (string issue in unavailable) if (issue != "legacy-armor-mastery-progression") return false;
        string name = source.ItemClass?.GetItemName() ?? string.Empty;
        if (!name.StartsWith("armor", StringComparison.OrdinalIgnoreCase)
            || string.Equals(RebirthVariables.customScenario, "hive", StringComparison.OrdinalIgnoreCase)) return true;
        byte[] roll;
        using (var hash = SHA256.Create()) roll = hash.ComputeHash(Encoding.UTF8.GetBytes("rebirth.recycler.armor.parts.v1|" + group.ToString("N")));
        // One 50% roll per grouped input, including stacked inputs, as in 2.6.
        // Frozen hidden custody makes retries/reload/cancel unable to reroll.
        if (roll[0] < 128) result.Add(new ItemStack(ItemClass.GetItem("armorParts", true).Clone(), 1));
        return true;
    }
    public static IReadOnlyList<ItemStack> FreezeBonus(ItemValue source, Guid group)
    {
        if (TryFreezeBonus(source, group, out var rewards, out var unavailable)) return rewards;
        throw new InvalidOperationException("Recycler bonus dependency unavailable: " + string.Join(",", unavailable));
    }
    public static List<ItemStack> CompletionRewards(ItemValue source, Guid group)
    {
        var result = new List<ItemStack>();
        foreach (var stack in FreezeBonus(source, group)) result.Add(stack.Clone());
        return result;
    }
}
