using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;
using UnityEngine;

// N10: per-job "what is short / why blocked" rows for the Assignments report, built on the server from data
// the executors already hold (P7 recipe shape + ingredient counts, P8 maintainer status, P10 planner hold reasons).
// Read-only: it never reserves, debits or writes anything, and it is recomputed for each Desk read.
internal static class RebirthCompanionJobShortfall
{
    private const int MaxPerJob = 4;
    private const long PlanThrottleTicks = TimeSpan.TicksPerSecond * 5;
    private sealed class PlanEntry { internal long Next; internal string[] Lines = new string[0]; }
    private static readonly Dictionary<RebirthNpcStableId, PlanEntry> PlanCache = new Dictionary<RebirthNpcStableId, PlanEntry>();

    private static string Text(string key, string fallback)
    {
        string value = Localization.Get(key);
        return string.IsNullOrEmpty(value) || value == key ? fallback : value;
    }

    private static string Clean(string value) =>
        new string((value ?? "").Where(ch => !char.IsControl(ch)).Take(160).ToArray());

    private static XElement Row(string job, string kind, string line) =>
        new XElement("shortfall", new XAttribute("job", job), new XAttribute("kind", kind), Clean(line));

    // Rows for the saved Desk jobs of one worker (companionId is the Desk "npc" key of the worker).
    internal static IEnumerable<XElement> Build(XElement desk, RebirthNpcStableId id, string companionId, string actorId)
    {
        var rows = new List<XElement>();
        try
        {
            if (desk == null || string.IsNullOrEmpty(companionId) || !ThreadManager.IsMainThread()) return rows;
            EntityRebirthHumanoidNPC npc = null;
            int entityId;
            if (RebirthNpcRuntimeRegistry.TryGetEntityId(id, out entityId))
                npc = GameManager.Instance?.World?.GetEntity(entityId) as EntityRebirthHumanoidNPC;
            foreach (var job in desk.Elements("job").Where(j => (string)j.Attribute("npc") == companionId).Take(12))
            {
                string jobId = (string)job.Attribute("id") ?? "", kind = (string)job.Attribute("kind") ?? "";
                IEnumerable<string> lines = null;
                if (kind == "Craft") lines = CraftLines(id, npc, (string)job.Attribute("recipe"));
                else if (kind == "Food") lines = FoodLines(npc, job);
                else if (kind == "Farm") lines = FarmLines(id);
                else if (kind == "Organize") lines = OrganizeLines(id, actorId);
                if (lines != null)
                    foreach (string line in lines.Where(l => !string.IsNullOrEmpty(l)).Take(MaxPerJob)) rows.Add(Row(jobId, kind, line));
            }
        }
        catch { }
        return rows;
    }

    private static int Carried(EntityRebirthHumanoidNPC npc, int itemType)
    {
        int total = 0;
        var grid = npc?.bag?.ItemGrid;
        if (grid == null) return 0;
        for (int i = 0; i < npc.bag.SlotCount && i < grid.Length; i++)
        {
            var stack = grid[i];
            if (stack != null && !stack.IsEmpty() && stack.itemValue.type == itemType) total += stack.count;
        }
        return total;
    }

    // P7: the recipe shape refusal (single source) and, for a supported shape, ingredient counts the worker lacks.
    private static IEnumerable<string> CraftLines(RebirthNpcStableId id, EntityRebirthHumanoidNPC npc, string name)
    {
        var lines = new List<string>();
        var recipe = string.IsNullOrEmpty(name) ? null : CraftingManager.GetRecipe(name);
        if (recipe == null || !RebirthHumanRecipeReading.Knows(id, name)) { lines.Add(RebirthCompanionDeskStore.L("CraftNotKnown")); return lines; }
        string refusal = RebirthNpcRecipeQueueWork.RefusalReason(recipe);
        if (refusal != null) { lines.Add(refusal); return lines; }
        if (npc == null || recipe.ingredients == null) return lines;
        string recipeName = Localization.Get(name);
        if (string.IsNullOrEmpty(recipeName)) recipeName = name;
        foreach (var ingredient in recipe.ingredients)
        {
            if (ingredient == null || ingredient.itemValue == null || ingredient.count <= 0) continue;
            int carried = Carried(npc, ingredient.itemValue.type), missing = ingredient.count - carried;
            if (missing <= 0) continue;
            var item = ItemClass.GetForId(ingredient.itemValue.type);
            string itemName = item != null ? item.GetLocalizedItemName() : ingredient.itemValue.type.ToString(CultureInfo.InvariantCulture);
            lines.Add(string.Format(CultureInfo.CurrentCulture,
                Text("rbNpcShortCraftIngredient", "Needs {0} more {1} for {2}; carrying {3}."), missing, itemName, recipeName, carried));
        }
        return lines;
    }

    // Food stock target versus what the worker carries (same resolver the cooking catalogue uses).
    private static IEnumerable<string> FoodLines(EntityRebirthHumanoidNPC npc, XElement job)
    {
        var lines = new List<string>();
        if (npc == null || npc.bag == null || npc.bag.ItemGrid == null) return lines;
        int meals = 0, drinks = 0;
        for (int i = 0; i < npc.bag.SlotCount && i < npc.bag.ItemGrid.Length; i++)
        {
            var stack = npc.bag.ItemGrid[i];
            RebirthConsumableDefinition d;
            if (stack == null || stack.IsEmpty() || !RebirthConsumableResolver.TryResolve(stack.itemValue, out d)) continue;
            if (d.IsDrink) drinks += stack.count; else if (d.IsFood) meals += stack.count;
        }
        int wantMeals, wantDrinks;
        if ((string)job.Attribute("meals") == "true" && int.TryParse((string)job.Attribute("mealCount"), NumberStyles.Integer, CultureInfo.InvariantCulture, out wantMeals) && meals < wantMeals)
            lines.Add(string.Format(CultureInfo.CurrentCulture, Text("rbNpcShortFoodMeals", "Meals: carrying {0} of {1}."), meals, wantMeals));
        if ((string)job.Attribute("drinks") == "true" && int.TryParse((string)job.Attribute("drinkCount"), NumberStyles.Integer, CultureInfo.InvariantCulture, out wantDrinks) && drinks < wantDrinks)
            lines.Add(string.Format(CultureInfo.CurrentCulture, Text("rbNpcShortFoodDrinks", "Drinks: carrying {0} of {1}."), drinks, wantDrinks));
        return lines;
    }

    // P8: the maintainer's own verdict (provider below the minimum, blocked reason, cooldown) plus a held refill's detail.
    private static IEnumerable<string> FarmLines(RebirthNpcStableId id)
    {
        var lines = new List<string>();
        string status = RebirthP8NpcWaterMaintainer.Status(id);
        if (!string.IsNullOrEmpty(status) && !status.StartsWith("All providers", StringComparison.Ordinal))
            lines.Add(Text("rbNpcShortFarmWater", "Water: ") + status);
        foreach (var a in RebirthNpcWorkAssignmentService.GetForNpc(id, false))
            if (RebirthP8NpcWaterRefillWork.IsDefinition(a.DefinitionId) && a.Status == RebirthNpcWorkAssignmentStatus.Suspended && !string.IsNullOrEmpty(a.Detail))
            { lines.Add(a.Detail); break; }
        return lines;
    }

    // P10: why the planner holds items back (B09 hold reasons), cached per worker for a few seconds.
    private static IEnumerable<string> OrganizeLines(RebirthNpcStableId id, string actorId)
    {
        long now = DateTime.UtcNow.Ticks;
        PlanEntry entry;
        if (PlanCache.TryGetValue(id, out entry) && now < entry.Next) return entry.Lines;
        if (PlanCache.Count >= 64) PlanCache.Clear();
        entry = new PlanEntry { Next = now + PlanThrottleTicks };
        PlanCache[id] = entry;
        RebirthNpcB09Policy policy; bool enabled;
        var player = string.IsNullOrEmpty(actorId) ? null : RebirthNpcInteractionMutationHandlers.FindActorPlayer(actorId);
        if (player == null || !RebirthNpcB09PolicyStore.TryRead(id, out policy, out enabled) || !enabled) return entry.Lines;
        var plan = RebirthNpcB09OrganizationPlanner.Plan(player, policy, RebirthNpcB09SelectionAdapter.ClaimedCells(), 12);
        if (plan == null) return entry.Lines;
        entry.Lines = plan.Held.Take(MaxPerJob).Select(h => string.Format(CultureInfo.CurrentCulture,
            Text("rbNpcShortOrganizeHold", "Held {0}: {1}"), h.ItemName ?? "", RebirthCompanionDeskStore.L("B09Hold" + h.Reason))).ToArray();
        return entry.Lines;
    }
}
