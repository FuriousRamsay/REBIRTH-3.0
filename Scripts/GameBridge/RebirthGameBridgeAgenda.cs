using System.Collections;
using System.Collections.Generic;

#nullable disable

/// <summary>
/// The virtual player's default agenda: useful things a player does with downtime (e.g. while a meal digests)
/// when nothing more urgent is going on and the agent hasn't given it a goal. Threats, healing, needs and
/// chores (burning food) always come first; any agent command takes over immediately.
///
///   1. Keep healing stocked: fewer than 3 healing items -> craft bandages at the crafting screen.
///   2. Cook ahead: few ready-to-eat meals and raw meat on hand -> put Grilled Meat on the lit campfire.
/// Each task verifies its result and backs off after a failure (no loops).
/// </summary>
public static class RebirthGameBridgeAgenda
{
    public static bool Enabled = true;
    private static float nextCheck, craftBackoffUntil, cookBackoffUntil;

    private static IEnumerator Wait(float s)
    {
        float end = Time.realtimeSinceStartup + s;
        while (Time.realtimeSinceStartup < end) yield return null;
    }

    private static int Count(EntityPlayerLocal p, Func<ItemClass, bool> match)
    {
        int n = 0;
        foreach (ItemStack s in p.inventory.ItemGrid.items)
            if (s != null && !s.IsEmpty() && s.itemValue.ItemClass != null && match(s.itemValue.ItemClass)) n += s.count;
        foreach (ItemStack s in p.bag.ItemGrid.items)
            if (s != null && !s.IsEmpty() && s.itemValue.ItemClass != null && match(s.itemValue.ItemClass)) n += s.count;
        return n;
    }

    /// <summary>Returns a routine for the next agenda task, or null when there is nothing to do right now.</summary>
    public static IEnumerator Tick(EntityPlayerLocal p, Action<string> log)
    {
        float now = Time.realtimeSinceStartup;
        if (!Enabled || now < nextCheck) return null;
        nextCheck = now + 1.5f;

        if (now >= craftBackoffUntil && Count(p, RebirthGameBridgeNeeds.IsHealing) < 3)
            return CraftRoutine(p, "medicalBandage", 3, "Bandage", log);

        int meals = Count(p, RebirthGameBridgeNeeds.IsFood);
        int raw = Count(p, ic => ic.GetItemName() == "foodRawMeat");
        if (now >= cookBackoffUntil && meals < 4 && raw >= 1)
            return CookRoutine(p, "Grilled Meat", log);
        // Nothing to prepare: go find a tier-1 POI, clear it and loot it.
        return RebirthGameBridgeWorld.ExploreTick(p, log);
    }

    /// <summary>Crafting screen: pick the recipe, set the batch, press Craft, wait for the items, close.</summary>
    private static IEnumerator CraftRoutine(EntityPlayerLocal p, string recipe, int count, string label, Action<string> log)
    {
        log("downtime: crafting " + count + " " + label + " (keeping healing stocked)");
        Func<ItemClass, bool> isIt = ic => ic.GetItemName() == recipe;
        int before = Count(p, isIt);
        yield return RebirthGameBridgeNeeds.WaitHandsFree(p, 3f);
        RebirthGameBridgeInput.HoldSeconds(RebirthGameBridgeInput.LocalActions().PermanentActions.Inventory, 0.12f);
        float until = Time.realtimeSinceStartup + 2f;
        while (Time.realtimeSinceStartup < until && !RebirthGameBridgeUi.IsWindowOpen("crafting")) yield return null;
        yield return RebirthGameBridgeUi.LookAtWindow();

        string outcome = null;
        Vector2 pt;
        if (!RebirthGameBridgeUi.IsWindowOpen("crafting")) outcome = "crafting screen did not open";
        else if (!RebirthGameBridgeUi.TryFindRecipe("crafting", recipe, out pt)) outcome = "recipe " + label + " not in the list";
        else
        {
            yield return RebirthGameBridgeUi.ClickAt(pt);
            for (int i = 1; i < count && RebirthGameBridgeUi.TryFindById("countUp", out pt); i++) yield return RebirthGameBridgeUi.ClickAt(pt);
            if (!RebirthGameBridgeUi.TryFindById("btnRebirthCraftingCraft", out pt)) outcome = "no Craft button";
            else
            {
                yield return RebirthGameBridgeUi.ClickAt(pt);
                until = Time.realtimeSinceStartup + 4f + count * 3f;           // craft time + margin
                while (Time.realtimeSinceStartup < until && Count(p, isIt) < before + count) yield return null;
                int made = Count(p, isIt) - before;
                outcome = made > 0 ? "ok: crafted " + made + " " + label : "Craft pressed but nothing was made (missing ingredients?)";
            }
        }
        RebirthGameBridgeInput.ReleaseCursor();
        yield return RebirthGameBridgeUi.CloseMenus();
        if (outcome.StartsWith("ok:")) log(outcome.Substring(4));
        else { log("couldn't craft " + label + ": " + outcome + " - trying again later"); craftBackoffUntil = Time.realtimeSinceStartup + 180f; }
    }

    /// <summary>Lit campfire: choose the dish, pull ingredients, cook, make sure the heat is on, leave it cooking.</summary>
    private static IEnumerator CookRoutine(EntityPlayerLocal p, string dish, Action<string> log)
    {
        Vector3i? station = RebirthGameBridgeNeeds.ActiveCookingStation(p, 40);
        if (!station.HasValue) { cookBackoffUntil = Time.realtimeSinceStartup + 120f; yield break; }
        log("downtime: cooking " + dish + " ahead (few ready meals left)");
        yield return RebirthGameBridgePlayer.ApproachAndActivate(p, station.Value, log);
        float until = Time.realtimeSinceStartup + 1.5f;
        while (Time.realtimeSinceStartup < until && !RebirthGameBridgeUi.AnyModalOpen()) yield return null;
        yield return RebirthGameBridgeUi.LookAtWindow();

        string outcome = null;
        Vector2 pt;
        string window = RebirthGameBridgeUi.OpenStationWindow();
        if (window == null) outcome = "the station window did not open";
        else if (!RebirthGameBridgeUi.TryFindByText(window, dish, out pt)) outcome = dish + " is not in this station's recipes";
        else
        {
            yield return RebirthGameBridgeUi.ClickAt(pt);
            if (RebirthGameBridgeUi.TryFindByText(window, "PULL INGREDIENTS", out pt)) yield return RebirthGameBridgeUi.ClickAt(pt);
            if (!RebirthGameBridgeUi.TryFindByText(window, "COOK", out pt)) outcome = "no COOK button";
            else
            {
                yield return RebirthGameBridgeUi.ClickAt(pt);
                if (RebirthGameBridgeUi.TryFindByText(window, "Turn On", out pt)) yield return RebirthGameBridgeUi.ClickAt(pt);
                yield return Wait(0.4f);
                outcome = RebirthGameBridgeUi.WindowContains(window, "COOKING") ? "ok: " + dish + " is cooking" : "pressed COOK but nothing is cooking (missing ingredients or fuel?)";
            }
        }
        RebirthGameBridgeInput.ReleaseCursor();
        yield return RebirthGameBridgeUi.CloseMenus();
        if (outcome.StartsWith("ok:")) { log(outcome.Substring(4) + " - will take it when it's done"); cookBackoffUntil = Time.realtimeSinceStartup + 120f; }
        else { log("couldn't cook " + dish + ": " + outcome + " - trying again later"); cookBackoffUntil = Time.realtimeSinceStartup + 240f; }
    }
}
