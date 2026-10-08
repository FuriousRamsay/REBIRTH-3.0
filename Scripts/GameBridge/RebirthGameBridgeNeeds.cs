using System.Collections;
using System.Collections.Generic;
using InControl;
using Newtonsoft.Json.Linq;

#nullable disable

/// <summary>
/// The virtual player's own needs and hands - decisions that must not wait for the agent.
///
/// Hands: one item action at a time, like a player. Equip an item and wait until it is really in hand (the
/// equip animation finished); only use it when the hands are free; wait for the use animation to finish;
/// confirm it was consumed. (Pressing "use" mid-animation or on a stale slot does nothing - or swings the bat.)
///
/// Needs (REBIRTH metabolism): drink when hydration (+ what is still being absorbed) is low, eat when
/// nutrition is low, never overfill the stomach; prefer clean water and cooked food; skip raw/burnt food.
/// Items are moved from the backpack to the equipped toolbelt through the real inventory UI; in
/// test mode missing supplies are added to the backpack (logged).
///
/// Chores: food on a cooking station about to burn (HUD timer) is taken when it is safe to go.
/// </summary>
public static class RebirthGameBridgeNeeds
{
    public static bool Enabled = true, Supply = true;
    private static float nextNeedsCheck, nextDrinkAt, nextEatAt, nextCookCheck, nextPrepareAt;

    // ------------------------------------------------------------------ hands

    public static bool HandsBusy(EntityPlayerLocal p)
    {
        try { return !(!p.inventory.Hand.IsSwitching) || p.inventory.IsHoldingItemActionRunning(); }
        catch { return false; }
    }

    public static IEnumerator WaitHandsFree(EntityPlayerLocal p, float timeout)
    {
        float until = Time.realtimeSinceStartup + timeout;
        while (Time.realtimeSinceStartup < until && HandsBusy(p)) yield return null;
    }

    private static string HeldName(EntityPlayerLocal p)
    {
        ItemClass ic = p.inventory.holdingItem;
        return ic != null ? ic.GetItemName() : null;
    }

    /// <summary>
    /// Equip toolbelt slot (0-based) and wait until it is really in hand. If it is already the selected slot
    /// but its contents changed under it (stale action data), force the game to re-equip it.
    /// </summary>
    public static IEnumerator Equip(EntityPlayerLocal p, int slot, string expectName, Action<bool> done)
    {
        if (p == null || p.IsDead() || slot < 0 || slot >= ToolbeltSize(p)) { done(false); yield break; }
        yield return WaitHandsFree(p, 3f);
        if (p.inventory.holdingItemIdx == slot)
        {
            try { p.inventory.Hand.OnSlotChanged(p.inventory.SelectedSlot); } catch { }
        }
        else
        {
            bool previousShift = RebirthGameBridgeInput.Shift;
            try
            {
                // Native Toolbelt resolves the second bar using SlotsPerBar for
                // physical number-key input. Expanded slots have no InventorySlot11 action.
                int offset = ToolbeltConfig.CurrentConfig.SlotsPerBar;
                bool shifted = slot >= offset;
                int keySlot = shifted ? slot - offset : slot;
                PlayerAction key = RebirthGameBridgeInput.Find("InventorySlot" + (keySlot + 1));
                if (key != null)
                {
                    RebirthGameBridgeInput.Shift = shifted;
                    for (int attempt = 0; attempt < 3 && !p.IsDead() && p.inventory.holdingItemIdx != slot; attempt++)
                    {
                        if (slot >= ToolbeltSize(p)) break;
                        RebirthGameBridgeInput.HoldFrames(key, 3);
                        float t = Time.realtimeSinceStartup + 0.8f;
                        while (Time.realtimeSinceStartup < t && !p.IsDead() && p.inventory.holdingItemIdx != slot) yield return null;
                        // Allow the input's press/release frames to finish before clearing Shift.
                        yield return null;
                        yield return null;
                        yield return null;
                    }
                }
            }
            finally { RebirthGameBridgeInput.Shift = previousShift; }
        }
        yield return WaitHandsFree(p, 2f);              // equip animation
        float settle = Time.realtimeSinceStartup + 0.15f;
        while (Time.realtimeSinceStartup < settle) yield return null;
        done(!p.IsDead() && slot < ToolbeltSize(p) && p.inventory.holdingItemIdx == slot && (expectName == null || HeldName(p) == expectName));
    }

    public static IEnumerator Equip(EntityPlayerLocal p, int slot, string expectName, Action<bool> done,
        RebirthGameBridgeInput.OwnedInputScope parent)
    {
        bool completed = false;
        using (var scope = new RebirthGameBridgeInput.OwnedInputScope(p, () => parent != null && parent.Admitted, parent))
        {
            try { yield return RebirthGameBridgeUi.RunGuarded(EquipOwned(p, slot, expectName, ok => { completed = true; done(ok); }, scope), scope); }
            finally
            {
                if (scope.CursorCustodyPending) parent?.MarkCursorCustodyPending();
                else if (scope.Failure != null) parent?.Refuse(scope.Failure);
                if (!completed) done(false);
            }
        }
    }
    private static IEnumerator EquipOwned(EntityPlayerLocal p, int slot, string expectName, Action<bool> done,
        RebirthGameBridgeInput.OwnedInputScope scope)
    {
        if (!scope.Admitted || slot < 0 || slot >= ToolbeltSize(p)) { done(false); yield break; }
        yield return WaitHandsFree(p, 3f);
        if (!scope.Admitted) { done(false); yield break; }
        if (p.inventory.holdingItemIdx == slot)
        {
            bool changed = true;
            try { p.inventory.Hand.OnSlotChanged(p.inventory.SelectedSlot); } catch { changed = false; }
            if (!changed) { scope.Refuse("native held item re-equip failed"); done(false); yield break; }
        }
        else
        {
            int offset = ToolbeltConfig.CurrentConfig.SlotsPerBar;
            bool shifted = slot >= offset;
            PlayerAction key = RebirthGameBridgeInput.Find("InventorySlot" + ((shifted ? slot - offset : slot) + 1));
            if (key == null || !scope.TrySetShift(shifted)) { done(false); yield break; }
            for (int attempt = 0; attempt < 3 && p.inventory.holdingItemIdx != slot; attempt++)
            {
                if (!scope.Admitted || slot >= ToolbeltSize(p) || !scope.TryHoldFrames(key, 3)) { done(false); yield break; }
                float until = Time.realtimeSinceStartup + .8f;
                while (Time.realtimeSinceStartup < until && p.inventory.holdingItemIdx != slot) yield return null;
                yield return null; yield return null; yield return null;
            }
        }
        yield return WaitHandsFree(p, 2f);
        float settle = Time.realtimeSinceStartup + .15f;
        while (Time.realtimeSinceStartup < settle) yield return null;
        done(scope.Admitted && slot < ToolbeltSize(p) && p.inventory.holdingItemIdx == slot && (expectName == null || HeldName(p) == expectName));
    }
    public static IEnumerator Prepare(EntityPlayerLocal p, Func<ItemClass, bool> want, string supplyItem, string label,
        Action<string> log, RebirthGameBridgeInput.OwnedInputScope parent, Func<ItemStack, bool> matchStack = null)
    {
        using (var scope = new RebirthGameBridgeInput.OwnedInputScope(p, () => parent != null && parent.Admitted, parent))
        {
            try { yield return RebirthGameBridgeUi.RunGuarded(PrepareOwned(p, want, label, log, matchStack, scope), scope); }
            finally
            {
                if (scope.CursorCustodyPending) parent?.MarkCursorCustodyPending();
                else if (scope.Failure != null) parent?.Refuse(scope.Failure);
            }
        }
    }
    private static IEnumerator DragOwnedInventory(EntityPlayerLocal p, bool sourceBelt, int sourceIndex, bool targetBelt, int targetIndex,
        Vector2 from, Vector2 to, RebirthGameBridgeInput.OwnedInputScope scope)
    {
        ItemStack[] sourceSlots = sourceBelt ? p.inventory.ItemGrid.items : p.bag.ItemGrid.items;
        ItemStack[] targetSlots = targetBelt ? p.inventory.ItemGrid.items : p.bag.ItemGrid.items;
        if (!scope.Admitted || sourceIndex < 0 || sourceIndex >= sourceSlots.Length || targetIndex < 0 || targetIndex >= targetSlots.Length) yield break;
        ItemStack original = sourceSlots[sourceIndex];
        string key = RebirthGameBridgePoiStorage.OwnedItemKey(original); int count = original?.count ?? 0;
        if (key == null || count < 1) { scope.Refuse("drag source fingerprint unavailable"); yield break; }
        bool picked = false, deposited = false;
        Func<ItemStack, bool> custody = cursor =>
        {
            if (!scope.Admitted) return false;
            ItemStack[] currentSource = sourceBelt ? p.inventory.ItemGrid.items : p.bag.ItemGrid.items;
            ItemStack[] currentTarget = targetBelt ? p.inventory.ItemGrid.items : p.bag.ItemGrid.items;
            if (!ReferenceEquals(currentSource, sourceSlots) || !ReferenceEquals(currentTarget, targetSlots)) { scope.Refuse("native inventory grid changed during drag"); return false; }
            ItemStack current = currentSource[sourceIndex], target = currentTarget[targetIndex];
            bool sourceEmpty = current == null || current.IsEmpty(), targetEmpty = target == null || target.IsEmpty();
            if (picked && cursor == null)
            {
                deposited = sourceEmpty && !targetEmpty && target.count == count && RebirthGameBridgePoiStorage.OwnedItemKey(target) == key;
                if (!deposited) scope.Refuse("drag deposit into pinned destination was not witnessed");
                return deposited && scope.Admitted;
            }
            Vector2 targetPoint;
            if (!targetEmpty || !RebirthGameBridgeUi.TryOwnedInventorySlot(targetBelt, targetIndex, out targetPoint)
                || Vector2.Distance(targetPoint, to) > .5f) { scope.Refuse("drag destination changed before native drop"); return false; }
            if (cursor != null)
            {
                if (!sourceEmpty || cursor.count != count || RebirthGameBridgePoiStorage.OwnedItemKey(cursor) != key)
                { scope.Refuse("native picked cursor item differs from pinned source"); return false; }
                picked = true; return scope.Admitted;
            }
            Vector2 sourcePoint;
            if (!ReferenceEquals(current, original) || current.count != count || RebirthGameBridgePoiStorage.OwnedItemKey(current) != key
                || !RebirthGameBridgeUi.TryOwnedInventorySlot(sourceBelt, sourceIndex, out sourcePoint) || Vector2.Distance(sourcePoint, from) > .5f)
            { scope.Refuse("drag source changed before native pickup"); return false; }
            return scope.Admitted;
        };
        yield return RebirthGameBridgeUi.DragItem(from, to, scope, custody);
        if (scope.Admitted && !deposited) { custody(RebirthGameBridgeUi.OwnedCursorStack()); if (!deposited) scope.Refuse("native drag remains incomplete"); }
    }
    private static IEnumerator PrepareOwned(EntityPlayerLocal p, Func<ItemClass, bool> want, string label, Action<string> log,
        Func<ItemStack, bool> matchStack, RebirthGameBridgeInput.OwnedInputScope scope)
    {
        if (!scope.Admitted) yield break;
        string dummyName;
        if (ToolbeltSlot(p, want, out dummyName, matchStack) >= 0) yield break;
        string name = BestInBag(p, want, matchStack);
        // Guarded POI work consumes genuine inventory only; this overload never supplies items.
        if (name == null) { scope.Refuse("need " + label + " but have none"); yield break; }
        PlayerActionsLocal actions = RebirthGameBridgeInput.LocalActions();
        if (actions == null) { scope.Refuse("player input unavailable"); yield break; }
        yield return WaitHandsFree(p, 3f);
        if (!scope.TrySetLockCursor(false) || !scope.TryHoldSeconds(actions.PermanentActions.Inventory, .12f)) yield break;
        float until = Time.realtimeSinceStartup + 2f;
        while (Time.realtimeSinceStartup < until && !RebirthGameBridgeUi.AnyModalOpen()) yield return null;
        yield return RebirthGameBridgeUi.LookAtWindow();
        if (!scope.Admitted) yield break;
        if (!RebirthGameBridgeUi.AnyModalOpen()) { scope.Refuse("inventory did not open"); yield break; }
        Vector2 from, to; int slot;
        if (!RebirthGameBridgeUi.TryFindEmptyToolbeltSlot(out to, out slot) || slot >= ToolbeltSize(p))
        {
            int free = FreeableToolbeltSlot(p, want);
            Vector2 slotPt, bagPt;
            if (free < 0 || !RebirthGameBridgeUi.TryFindToolbeltSlot(free, out slotPt) || !RebirthGameBridgeUi.TryFindEmptyBackpackSlot(out bagPt))
            { scope.Refuse("toolbelt is full and nothing can be moved out"); yield break; }
            int bagIndex = -1;
            for (int i = 0; i < p.bag.ItemGrid.items.Length; i++)
                if ((p.bag.ItemGrid.items[i] == null || p.bag.ItemGrid.items[i].IsEmpty()) && RebirthGameBridgeUi.TryOwnedInventorySlot(false, i, out bagPt)) { bagIndex = i; break; }
            if (bagIndex < 0 || !RebirthGameBridgeUi.TryOwnedInventorySlot(true, free, out slotPt)) { scope.Refuse("pinned eviction slots unavailable"); yield break; }
            yield return DragOwnedInventory(p, true, free, false, bagIndex, slotPt, bagPt, scope);
            if (!scope.Admitted) yield break;
            if (p.inventory.GetItem(free) != null && !p.inventory.GetItem(free).IsEmpty())
            { scope.Refuse("toolbelt item was not moved to the backpack"); yield break; }
        }
        if (!RebirthGameBridgeUi.TryFindEmptyToolbeltSlot(out to, out slot) || slot >= ToolbeltSize(p))
        { scope.Refuse("toolbelt destination unavailable"); yield break; }
        if (!RebirthGameBridgeUi.TryFindItemSlot(name, false, out from, matchStack))
        { scope.Refuse(name + " not visible in the backpack"); yield break; }
        int sourceIndex = -1;
        for (int i = 0; i < p.bag.ItemGrid.items.Length; i++)
        {
            ItemStack candidate = p.bag.ItemGrid.items[i];
            if (candidate == null || candidate.IsEmpty() || candidate.itemValue.ItemClass.GetItemName() != name || (matchStack != null && !matchStack(candidate))) continue;
            if (RebirthGameBridgeUi.TryOwnedInventorySlot(false, i, out from)) { sourceIndex = i; break; }
        }
        if (sourceIndex < 0 || !RebirthGameBridgeUi.TryOwnedInventorySlot(true, slot, out to)) { scope.Refuse("pinned preparation slots unavailable"); yield break; }
        yield return DragOwnedInventory(p, false, sourceIndex, true, slot, from, to, scope);
        yield return Wait(.2f);
        if (!scope.Admitted) yield break;
        if (RebirthGameBridgeUi.HeldItemName() != null) { scope.MarkCursorCustodyPending(); yield break; }
        if (ToolbeltSlot(p, want, out dummyName, matchStack) < 0)
        { scope.Refuse("item did not reach the toolbelt"); yield break; }
        yield return RebirthGameBridgeUi.CloseMenus(scope);
        if (scope.Admitted) log("moved " + name + " to toolbelt slot " + (slot + 1) + " (for " + label + ")");
    }
    /// <summary>Use the held item once (tap), wait for its animation to finish, report whether it was consumed.</summary>
    public static IEnumerator UseHeld(EntityPlayerLocal p, string itemName, Action<bool> done)
    {
        int before = CountItem(p, itemName);
        float fullBefore = StomachMl(p); // containers lose volume per sip, not a whole item
        yield return WaitHandsFree(p, 3f);
        RebirthGameBridgeInput.HoldFrames(RebirthGameBridgeInput.LocalActions().Primary, 3);
        // Wait for the action to start, then to finish (eat/drink/bandage animations take ~1-3 s).
        float until = Time.realtimeSinceStartup + 1f;
        while (Time.realtimeSinceStartup < until && !p.inventory.IsHoldingItemActionRunning() && CountItem(p, itemName) >= before) yield return null;
        until = Time.realtimeSinceStartup + 5f;
        while (Time.realtimeSinceStartup < until && (p.inventory.IsHoldingItemActionRunning() || CountItem(p, itemName) >= before))
        {
            if (!p.inventory.IsHoldingItemActionRunning() && CountItem(p, itemName) >= before && Time.realtimeSinceStartup > until - 3.5f) break;
            yield return null;
        }
        yield return WaitHandsFree(p, 2f);
        done(CountItem(p, itemName) < before || StomachMl(p) > fullBefore + 1f);
    }

    private static float StomachMl(EntityPlayerLocal p)
    {
        try { return RebirthMetabolismService.GetFullnessMl(RebirthMetabolismStateRepository.GetOrCreate(p)); } catch { return 0f; }
    }

    public static int CountItem(EntityPlayerLocal p, string name)
    {
        return CountOwnedItems(p, ic => ic.GetItemName() == name);
    }

    private static int CountOwnedItems(EntityPlayerLocal p, Func<ItemClass, bool> matches)
    {
        if (p == null || p.inventory == null || p.bag == null) return 0;
        return CountMatching(p.inventory.ItemGrid.items, ToolbeltSize(p), matches)
            + CountMatching(p.bag.ItemGrid.items, int.MaxValue, matches);
    }

    private static int CountMatching(ItemStack[] slots, int limit, Func<ItemClass, bool> matches)
    {
        if (slots == null) return 0;
        int count = 0;
        for (int i = 0; i < Math.Min(slots.Length, limit); i++)
        {
            ItemStack stack = slots[i];
            if (stack != null && !stack.IsEmpty() && stack.itemValue.ItemClass != null
                && matches(stack.itemValue.ItemClass)) count += stack.count;
        }
        return count;
    }

    // ------------------------------------------------------------------ toolbelt

    /// <summary>Unlocked native capacity, including equipment/background expansion; excludes dummy slots.</summary>
    public static int ToolbeltSize(EntityPlayerLocal p)
    {
        return p == null || p.inventory == null ? 0
            : RebirthToolbeltCapacity.GetOwnedSlotCount(p, p.inventory.ItemGrid.items.Length);
    }

    /// <summary>Toolbelt slot (0-based) holding an item matching `want`, or -1.</summary>
    public static int ToolbeltSlot(EntityPlayerLocal p, Func<ItemClass, bool> want, out string name, Func<ItemStack, bool> matchStack = null)
    {
        name = null;
        ItemStack[] slots = p.inventory.ItemGrid.items;
        int n = Mathf.Min(ToolbeltSize(p), slots.Length);
        int best = -1; float bestScore = float.MinValue;
        for (int i = 0; i < n; i++)
        {
            ItemStack s = slots[i];
            if (s == null || s.IsEmpty() || s.itemValue.ItemClass == null || !want(s.itemValue.ItemClass) || (matchStack != null && !matchStack(s))) continue;
            float score = Preference(s.itemValue.ItemClass);
            if (score > bestScore) { bestScore = score; best = i; name = s.itemValue.ItemClass.GetItemName(); }
        }
        return best;
    }

    private static string BestInBag(EntityPlayerLocal p, Func<ItemClass, bool> want, Func<ItemStack, bool> matchStack = null)
    {
        string best = null; float bestScore = float.MinValue;
        foreach (ItemStack s in p.bag.ItemGrid.items)
        {
            if (s == null || s.IsEmpty() || s.itemValue.ItemClass == null || !want(s.itemValue.ItemClass) || (matchStack != null && !matchStack(s))) continue;
            float score = Preference(s.itemValue.ItemClass);
            if (score > bestScore) { bestScore = score; best = s.itemValue.ItemClass.GetItemName(); }
        }
        return best;
    }

    /// <summary>
    /// Get an item matching `want` into the toolbelt through the inventory UI (Tab, pick, place, Esc), making
    /// room if the toolbelt is full; supply `supplyItem` to the backpack first in test mode if there is none.
    /// </summary>
    public static IEnumerator Prepare(EntityPlayerLocal p, Func<ItemClass, bool> want, string supplyItem, string label, Action<string> log, Func<ItemStack, bool> matchStack = null)
    {
        string dummyName;
        if (ToolbeltSlot(p, want, out dummyName, matchStack) >= 0) yield break;
        PlayerActionsLocal a = RebirthGameBridgeInput.LocalActions();

        string name = BestInBag(p, want, matchStack);
        if (name == null && matchStack == null && Supply && supplyItem != null)
        {
            ItemValue iv = ItemClass.GetItem(supplyItem, true);
            if (iv != null && !iv.IsEmpty() && p.bag.AddItem(new ItemStack(new ItemValue(iv.type, 1, 1, false), 2)))
            {
                log("no " + (label.StartsWith("a ") ? label.Substring(2) : label) + " anywhere - added 2 " + supplyItem + " to the backpack (test supply)");
                name = supplyItem;
            }
        }
        if (name == null) { log("need " + label + " but have none"); yield break; }

        yield return WaitHandsFree(p, 3f);
        RebirthGameBridgeInput.HoldSeconds(a.PermanentActions.Inventory, 0.12f);
        float until = Time.realtimeSinceStartup + 2f;
        while (Time.realtimeSinceStartup < until && !RebirthGameBridgeUi.AnyModalOpen()) yield return null;
        yield return RebirthGameBridgeUi.LookAtWindow();
        RebirthGameBridgeInput.LockCursor = true;

        string outcome = null;
        Vector2 from, to; int slot;
        if (!RebirthGameBridgeUi.AnyModalOpen()) outcome = "inventory did not open";
        else
        {
            if (!RebirthGameBridgeUi.TryFindEmptyToolbeltSlot(out to, out slot) || slot >= ToolbeltSize(p))
            {
                int free = FreeableToolbeltSlot(p, want);
                Vector2 slotPt, bagPt;
                if (free >= 0 && RebirthGameBridgeUi.TryFindToolbeltSlot(free, out slotPt) && RebirthGameBridgeUi.TryFindEmptyBackpackSlot(out bagPt))
                {
                    string evicted = p.inventory.GetItem(free).itemValue.ItemClass.GetItemName();
                    yield return RebirthGameBridgeUi.DragItem(slotPt, bagPt);
                    log("toolbelt full - moved " + evicted + " from slot " + (free + 1) + " to the backpack");
                }
            }
            if (!RebirthGameBridgeUi.TryFindEmptyToolbeltSlot(out to, out slot) || slot >= ToolbeltSize(p)) outcome = "toolbelt is full and nothing can be moved out";
            else
            {
                for (int attempt = 0; attempt < 2 && outcome == null; attempt++)
                {
                    if (!RebirthGameBridgeUi.TryFindItemSlot(name, false, out from, matchStack)) { outcome = name + " not visible in the backpack"; break; }
                    yield return RebirthGameBridgeUi.DragItem(from, to); // press, drag, release (a click only selects)
                    yield return Wait(0.2f);
                    if (ToolbeltSlot(p, want, out dummyName, matchStack) >= 0) outcome = "ok: moved " + name + " to toolbelt slot " + (slot + 1);
                    else if (RebirthGameBridgeUi.HeldItemName() != null) { yield return RebirthGameBridgeUi.ClickAt(from); outcome = "place failed (put it back)"; }
                }
                if (outcome == null) outcome = "could not pick up " + name;
            }
        }
        RebirthGameBridgeInput.LockCursor = false;
        RebirthGameBridgeInput.ReleaseCursor();
        yield return RebirthGameBridgeUi.CloseMenus(); // verified close (an item selection can eat the first Escape)
        log(outcome.StartsWith("ok:") ? outcome.Substring(4) + " (for " + label + ")" : "failed to get " + label + " into the toolbelt: " + outcome);
    }

    /// <summary>Toolbelt slot that can be emptied: not a weapon, not healing, not food/drink, not in hand.</summary>
    private static int FreeableToolbeltSlot(EntityPlayerLocal p, Func<ItemClass, bool> want)
    {
        ItemStack[] slots = p.inventory.ItemGrid.items;
        int n = Mathf.Min(ToolbeltSize(p), slots.Length);
        // First choice: something that isn't a weapon, healing or a meal. Second: a consumable of the other
        // kind (food when fetching a drink, and vice versa); it goes back to the backpack, not away.
        for (int pass = 0; pass < 2; pass++)
            for (int i = 0; i < n; i++)
            {
                ItemStack s = slots[i];
                if (s == null || s.IsEmpty() || (pass == 0 && i == p.inventory.holdingItemIdx)) continue;
                ItemClass ic = s.itemValue.ItemClass;
                if (ic == null || IsWeapon(ic) || IsHealing(ic)) continue;
                bool consumable = IsFood(ic) || IsDrink(ic);
                if (pass == 0 && consumable) continue;
                if (pass == 1 && (!consumable || want(ic))) continue;
                return i;
            }
        return -1;
    }

    // ------------------------------------------------------------------ classification

    public static bool IsWeapon(ItemClass ic)
    {
        return ic.Actions != null && ic.Actions.Length > 0 && (ic.Actions[0] is ItemActionRanged || ic.Actions[0] is ItemActionMelee || ic.Actions[0] is ItemActionDynamicMelee)
            && !(ic.GetItemName().StartsWith("melee", StringComparison.OrdinalIgnoreCase) && ic.GetItemName().IndexOf("Hand", StringComparison.OrdinalIgnoreCase) >= 0);
    }

    public static bool IsHealing(ItemClass ic)
    {
        string n = ic.GetItemName();
        return n == "medicalFirstAidKit" || n == "medicalFirstAidBandage" || n == "medicalBandage";
    }

    private static bool Resolve(ItemClass ic, out RebirthConsumableDefinition def)
    {
        def = null;
        try { return RebirthConsumableResolver.TryResolve(ic, out def) && def != null; } catch { return false; }
    }

    private static bool Unsafe(string n)
    {
        return n.IndexOf("Empty", StringComparison.OrdinalIgnoreCase) >= 0 // an empty container is not a drink
            || n.IndexOf("Raw", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("Burnt", StringComparison.OrdinalIgnoreCase) >= 0
            || n.IndexOf("Murky", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("Rotting", StringComparison.OrdinalIgnoreCase) >= 0
            || n.StartsWith("foodCrop", StringComparison.OrdinalIgnoreCase) || n.IndexOf("Egg", StringComparison.Ordinal) >= 0 && n.IndexOf("Boiled", StringComparison.Ordinal) < 0;
    }

    public static bool IsDrink(ItemClass ic)
    {
        RebirthConsumableDefinition d;
        return Resolve(ic, out d) && d.IsDrink && !Unsafe(ic.GetItemName());
    }

    public static bool IsFood(ItemClass ic)
    {
        RebirthConsumableDefinition d;
        return Resolve(ic, out d) && d.IsFood && !d.IsDrink && !Unsafe(ic.GetItemName());
    }

    /// <summary>Higher is better: most nutrition for food; clean water first for drinks.</summary>
    private static float Preference(ItemClass ic)
    {
        RebirthConsumableDefinition d;
        if (!Resolve(ic, out d)) return 0f;
        if (d.IsDrink) return ic.GetItemName().IndexOf("Water", StringComparison.OrdinalIgnoreCase) >= 0 ? 10f : 5f;
        return d.NutritionUnits;
    }

    // ------------------------------------------------------------------ needs tick

    private static IEnumerator Wait(float s)
    {
        float end = Time.realtimeSinceStartup + s;
        while (Time.realtimeSinceStartup < end) yield return null;
    }

    public static JObject Status(EntityPlayerLocal p)
    {
        var o = new JObject { ["enabled"] = Enabled, ["supply"] = Supply };
        try
        {
            RebirthMetabolismState st = RebirthMetabolismStateRepository.GetOrCreate(p);
            RebirthMetabolismModifiers mods = RebirthMetabolismModifierResolver.Resolve(p, st);
            o["hydration"] = Mathf.Round(p.Stats.Water.Value);
            o["hydrationPending"] = Mathf.Round(RebirthMetabolismService.GetPendingHydrationPoints(st, mods));
            o["nutrition"] = Mathf.Round(p.Stats.Food.Value);
            o["nutritionPending"] = Mathf.Round(RebirthMetabolismService.GetPendingFoodPoints(st, mods));
            o["stomachMl"] = Mathf.Round(RebirthMetabolismService.GetFullnessMl(st));
            o["stomachCapacityMl"] = Mathf.Round(Mathf.Max(100f, mods.StomachCapacity));
        }
        catch (Exception ex) { o["error"] = ex.Message; }
        return o;
    }

    /// <summary>
    /// Called by the instincts when nothing more urgent is going on (no threat close, not hurt). Returns a
    /// routine to run (drink, eat, rescue cooking), or null when there is nothing to do.
    /// </summary>
    public static IEnumerator Tick(EntityPlayerLocal p, Action<string> log)
    {
        float now = Time.realtimeSinceStartup;
        if (!Enabled || now < nextNeedsCheck) return null;
        nextNeedsCheck = now + 0.5f;

        float water, pendW, food, pendF, full, cap, stomachFluid, stomachSolid;
        try
        {
            RebirthMetabolismState st = RebirthMetabolismStateRepository.GetOrCreate(p);
            RebirthMetabolismModifiers mods = RebirthMetabolismModifierResolver.Resolve(p, st);
            water = p.Stats.Water.Value; food = p.Stats.Food.Value;
            pendW = RebirthMetabolismService.GetPendingHydrationPoints(st, mods);
            pendF = RebirthMetabolismService.GetPendingFoodPoints(st, mods);
            full = RebirthMetabolismService.GetFullnessMl(st);
            cap = Mathf.Max(100f, mods.StomachCapacity);
            stomachFluid = RebirthMetabolismService.GetFluidMl(st);
            stomachSolid = RebirthMetabolismService.GetSolidMl(st);
        }
        catch { return null; }

        // Project where the levels are heading: absorbed + being absorbed + what is still in the stomach.
        // Drinking: sip, don't chug - never more than ~300 ml of fluid waiting in the stomach.
        float hydrationProjected = water + pendW + stomachFluid / RebirthMetabolismConfig.MlPerHydrationPoint;
        // What's on the way: from the Metabolism page when recently read, otherwise a conservative guess.
        float nutritionProjected = food + pendF + (stomachSolid > 50f ? Mathf.Max(digestNutritionOnTheWay, 10f) : 0f);
        // Cooked food ready on a station: grab it first (quick, and it may be the meal itself).
        if (now >= nextCookCheck)
        {
            nextCookCheck = now + 2f;
            float readyLeft;
            if (RebirthGameBridgeUi.TryReadBurnTimer(out readyLeft) || RebirthGameBridgeUi.CookingReadyAlert())
                return RescueCooking(p, readyLeft, log);
        }
        // Low food/water drags the stats down: keep both reasonably topped up (doesn't need to be perfect).
        // When either drops to ~50 (and the stomach has room), have a MEAL: eat and drink back-to-back until it
        // feels like enough, check digestion on the Metabolism page, think, top up.
        if ((nutritionProjected < 50f || hydrationProjected < 50f) && full < cap * 0.85f - 100f && now >= nextMealAt && now >= nextPrepareAt)
            return MealSession(p, log);
        if (now >= nextCookCheck)
        {
            nextCookCheck = now + 2f;
            float secondsLeft;
            // Take cooked food as soon as it is ready, like a player (not at the last minute).
            if (RebirthGameBridgeUi.TryReadBurnTimer(out secondsLeft) || RebirthGameBridgeUi.CookingReadyAlert())
                return RescueCooking(p, secondsLeft, log);
        }
        return null;
    }

    private static float nextDigestCheckAt = float.MaxValue, digestNutritionOnTheWay, digestHydrationOnTheWay;
    private static readonly System.Text.RegularExpressions.Regex StomachRx = new System.Text.RegularExpressions.Regex(@"(\d+)\s*/\s*(\d+)\s*mL\s*\((\d+)%\)");
    private static readonly System.Text.RegularExpressions.Regex IntakeRx = new System.Text.RegularExpressions.Regex(@"H\s*\+([\d.]+).*N\s*\+([\d.]+)");
    private static readonly System.Text.RegularExpressions.Regex LevelRx = new System.Text.RegularExpressions.Regex(@"^([\d.]+)\s*/\s*100$");

    /// <summary>
    /// Like a player: open the REBIRTH screen -> Character -> Metabolism, read how digestion is going (stomach,
    /// what's on the way, hydration, nutrition), log it, cross-check against the metabolism's own numbers, close.
    /// The reading also feeds the eat/drink decisions ("is what I ate enough?").
    /// </summary>
    private static IEnumerator CheckDigestion(EntityPlayerLocal p, Action<string> log, float thinkSeconds = 1.2f)
    {
        yield return WaitHandsFree(p, 3f);
        RebirthGameBridgeInput.HoldSeconds(RebirthGameBridgeInput.LocalActions().PermanentActions.Inventory, 0.12f);
        float until = Time.realtimeSinceStartup + 2f;
        while (Time.realtimeSinceStartup < until && !RebirthGameBridgeUi.AnyModalOpen()) yield return null;
        yield return RebirthGameBridgeUi.LookAtWindow();
        Vector2 pt;
        if (RebirthGameBridgeUi.TryFindById("rebirthCraftingTabCharacterLabel", out pt)) yield return RebirthGameBridgeUi.ClickAt(pt);
        until = Time.realtimeSinceStartup + 1.5f;
        while (Time.realtimeSinceStartup < until && !RebirthGameBridgeUi.IsWindowOpen("rebirthSurvivorCharacter")) yield return null;
        if (RebirthGameBridgeUi.TryFindByText("rebirthSurvivorCharacter", "METABOLISM", out pt)) yield return RebirthGameBridgeUi.ClickAt(pt);
        yield return RebirthGameBridgeUi.LookAtWindow();

        List<string> texts = RebirthGameBridgeUi.Texts("rebirthSurvivorCharacter");
        yield return Wait(thinkSeconds); // read it and work out what's still missing
        string stomach = null, hydration = null, nutrition = null;
        float onTheWayH = 0f, onTheWayN = 0f;
        for (int i = 0; i < texts.Count; i++)
        {
            var sm = StomachRx.Match(texts[i]);
            if (stomach == null && sm.Success && texts[i].Contains("1500") || stomach == null && sm.Success && i > 0 && texts.IndexOf("STOMACH") >= 0 && i > texts.IndexOf("STOMACH")) stomach = texts[i];
            var im = IntakeRx.Match(texts[i]);
            if (im.Success) { onTheWayH += float.Parse(im.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture); onTheWayN += float.Parse(im.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture); }
            if (texts[i] == "HYDRATION" && i + 1 < texts.Count && LevelRx.IsMatch(texts[i + 1])) hydration = texts[i + 1];
            if (texts[i] == "NUTRITION" && i + 1 < texts.Count && LevelRx.IsMatch(texts[i + 1])) nutrition = texts[i + 1];
        }
        yield return RebirthGameBridgeUi.CloseMenus();

        if (stomach == null && nutrition == null) { log("opened the Metabolism page but could not read it"); yield break; }
        digestNutritionOnTheWay = onTheWayN;
        digestHydrationOnTheWay = onTheWayH;
        log("checked digestion (Metabolism page): stomach " + (stomach ?? "?") + ", on the way H +" + onTheWayH.ToString("0.#") + " N +" + onTheWayN.ToString("0.#")
            + ", hydration " + (hydration ?? "?") + ", nutrition " + (nutrition ?? "?"));

        // Cross-check the page against the metabolism itself (a mismatch is a UI bug worth reporting).
        try
        {
            var m = stomach != null ? StomachRx.Match(stomach) : null;
            float shown = m != null && m.Success ? float.Parse(m.Groups[1].Value) : -1f;
            float actual = RebirthMetabolismService.GetFullnessMl(RebirthMetabolismStateRepository.GetOrCreate(p));
            if (shown >= 0f && Mathf.Abs(shown - actual) > 60f)
                log("UI MISMATCH: Metabolism page shows stomach " + shown.ToString("0") + " mL but the metabolism has " + actual.ToString("0") + " mL");
        }
        catch { }
    }
    private static float nextMealAt;
    private const float NutritionTarget = 80f, HydrationTarget = 80f;

    private static void Projections(EntityPlayerLocal p, out float nutrition, out float hydration, out float full, out float cap)
    {
        RebirthMetabolismState st = RebirthMetabolismStateRepository.GetOrCreate(p);
        RebirthMetabolismModifiers mods = RebirthMetabolismModifierResolver.Resolve(p, st);
        full = RebirthMetabolismService.GetFullnessMl(st);
        cap = Mathf.Max(100f, mods.StomachCapacity);
        float solid = RebirthMetabolismService.GetSolidMl(st), fluid = RebirthMetabolismService.GetFluidMl(st);
        hydration = p.Stats.Water.Value + RebirthMetabolismService.GetPendingHydrationPoints(st, mods) + fluid / RebirthMetabolismConfig.MlPerHydrationPoint;
        nutrition = p.Stats.Food.Value + RebirthMetabolismService.GetPendingFoodPoints(st, mods) + (solid > 50f ? Mathf.Max(digestNutritionOnTheWay, solid / 20f) : 0f);
    }

    /// <summary>Rough value of one bite/sip, from REBIRTH's own item definition.</summary>
    private static float ItemValue(ItemClass ic, bool drink)
    {
        RebirthConsumableDefinition d;
        if (!Resolve(ic, out d)) return drink ? 5f : 12f;
        if (drink) return Mathf.Max(2f, (d.ManualSipMl > 0f ? d.ManualSipMl : 125f) * Mathf.Max(0.5f, d.LiquidHydrationYield) / RebirthMetabolismConfig.MlPerHydrationPoint);
        return d.NutritionUnits > 0f ? d.NutritionUnits : 12f;
    }

    /// <summary>
    /// A meal like a player has one: get food and drink ready, then eat and drink back-to-back (alternating,
    /// no pauses) until it feels like enough; check digestion on the Metabolism page and think a moment; top
    /// up whatever is still short.
    /// </summary>
    private static IEnumerator MealSession(EntityPlayerLocal p, Action<string> log)
    {
        nextMealAt = Time.realtimeSinceStartup + 60f;
        float nut, hyd, full, cap;
        Projections(p, out nut, out hyd, out full, out cap);
        bool needFood = nut < NutritionTarget - 5f, needDrink = hyd < HydrationTarget - 5f;
        log("meal time: nutrition ~" + nut.ToString("0") + ", hydration ~" + hyd.ToString("0") + " (aiming for ~" + NutritionTarget.ToString("0") + ")");

        // Get everything ready first (one trip to the inventory each).
        if (needFood) yield return Prepare(p, IsFood, "foodGrilledMeat", "food", log);
        if (needDrink) yield return Prepare(p, IsDrink, "drinkJarBoiledWater", "a drink", log);

        int previous = p.inventory.holdingItemIdx;
        int items = 0;
        float gotN = 0f, gotH = 0f;
        yield return EatDrinkRun(p, NutritionTarget - nut, HydrationTarget - hyd, 10, (n, h, k) => { gotN += n; gotH += h; items += k; }, log);
        log("had " + items + " item(s): about +" + gotN.ToString("0") + " nutrition, +" + gotH.ToString("0") + " hydration on the way - checking digestion");

        // Check the Metabolism page and take a moment to work out what's still missing.
        yield return CheckDigestion(p, log, 2.2f);
        Projections(p, out nut, out hyd, out full, out cap);
        float shortN = NutritionTarget - nut, shortH = HydrationTarget - hyd;
        if ((shortN > 8f || shortH > 8f) && full < cap * 0.85f - 100f)
        {
            log("still short on " + (shortN > 8f && shortH > 8f ? "food and water" : shortN > 8f ? "food" : "water") + " (nutrition ~" + nut.ToString("0") + ", hydration ~" + hyd.ToString("0") + ") - topping up");
            int k2 = 0;
            yield return EatDrinkRun(p, shortN, shortH, 4, (n, h, k) => { k2 += k; }, log);
            log(k2 > 0 ? "topped up with " + k2 + " more" : "nothing more fits / nothing left to top up with");
        }
        else log("that's enough for now");
        if (previous >= 0 && previous != p.inventory.holdingItemIdx) yield return Equip(p, previous, null, ok => { });
        nextDigestCheckAt = Time.realtimeSinceStartup + 240f;
    }

    /// <summary>Eat/drink back-to-back, alternating, until the wanted amounts are covered or the stomach is nearly full.</summary>
    private static IEnumerator EatDrinkRun(EntityPlayerLocal p, float wantN, float wantH, int maxItems, Action<float, float, int> report, Action<string> log)
    {
        float gotN = 0f, gotH = 0f; int count = 0;
        bool foodOk = true, drinkOk = true, lastWasFood = false;
        while (count < maxItems)
        {
            float full = StomachMl(p), cap = 1500f;
            try { cap = Mathf.Max(100f, RebirthMetabolismModifierResolver.Resolve(p, RebirthMetabolismStateRepository.GetOrCreate(p)).StomachCapacity); } catch { }
            if (full > cap * 0.85f) { log("stomach nearly full (" + full.ToString("0") + "/" + cap.ToString("0") + " mL) - stopping"); break; }
            bool f = foodOk && wantN - gotN > 4f, d = drinkOk && wantH - gotH > 4f;
            if (!f && !d) break;
            bool doFood = f && d ? !lastWasFood : f;
            Func<ItemClass, bool> want = doFood ? (Func<ItemClass, bool>)IsFood : IsDrink;
            string name;
            int slot = ToolbeltSlot(p, want, out name);
            if (slot < 0)
            {
                yield return Prepare(p, want, doFood ? "foodGrilledMeat" : "drinkJarBoiledWater", doFood ? "food" : "a drink", log);
                slot = ToolbeltSlot(p, want, out name);
                if (slot < 0) { if (doFood) foodOk = false; else drinkOk = false; continue; }
            }
            bool equipped = false, consumed = false;
            yield return Equip(p, slot, name, ok => equipped = ok);
            if (!equipped) { if (doFood) foodOk = false; else drinkOk = false; continue; }
            yield return UseHeld(p, name, ok => consumed = ok);
            if (!consumed) { if (doFood) foodOk = false; else drinkOk = false; continue; }
            float v = ItemValue(p.inventory.holdingItem ?? ItemClass.GetItemClass(name, true), !doFood);
            if (doFood) gotN += v; else gotH += v;
            count++;
            lastWasFood = doFood;
        }
        report(gotN, gotH, count);
    }
    private static IEnumerator Consume(EntityPlayerLocal p, string label, Func<ItemClass, bool> want, string supply, float level, float target, Action cooldown, Action<string> log)
    {
        cooldown();
        string name;
        int slot = ToolbeltSlot(p, want, out name);
        if (slot < 0)
        {
            yield return Prepare(p, want, supply, label, log);
            slot = ToolbeltSlot(p, want, out name);
            if (slot < 0) { nextPrepareAt = Time.realtimeSinceStartup + 20f; yield break; } // only a FAILED trip backs off
        }
        int previous = p.inventory.holdingItemIdx;
        bool equipped = false;
        yield return Equip(p, slot, name, ok => equipped = ok);
        if (!equipped) { log("couldn't get " + name + " in hand (holding " + (HeldName(p) ?? "nothing") + ")"); yield break; }
        bool consumed = false;
        yield return UseHeld(p, name, ok => consumed = ok);
        if (consumed) nextDigestCheckAt = Time.realtimeSinceStartup + 25f;
        log(consumed ? (label == "drink" ? "drank " : "ate ") + name + " (" + label + " level " + level.ToString("0") + ", aiming for " + target.ToString("0") + ")"
                     : "tried to " + (label == "drink" ? "drink " : "eat ") + name + " but nothing was consumed");
        if (previous != slot && previous >= 0) yield return Equip(p, previous, null, ok => { });
    }

    /// <summary>Food on a station is about to burn: walk to the nearest cooking station, take it, close.</summary>
    private static float rescueBackoffUntil;

    /// <summary>Food about to burn: close any open menu first, then go and take it (bypasses the back-off).</summary>
    public static IEnumerator UrgentRescue(EntityPlayerLocal p, float secondsLeft, Action<string> log)
    {
        if (!Enabled) yield break;   // chores are off (tests that top up by hand): do not wander off to a stove
        if (RebirthGameBridgeUi.AnyModalOpen()) { log("food burns in " + secondsLeft.ToString("0") + "s - leaving the menu"); yield return RebirthGameBridgeUi.CloseMenus(); }
        yield return RescueCooking(p, secondsLeft, log); // respects the back-off: a failing rescue must not loop
    }

    /// <summary>
    /// Food on a station is about to burn: walk to the nearest cooking station, wait for its window, click TAKE,
    /// and VERIFY (timer gone / food count up). Failed attempts back off instead of looping open/close.
    /// </summary>
    /// <summary>Ready food on one station (REBIRTH cooking state on the queue, or plain output): seconds until it burns.</summary>
    private static bool StationHasReadyFood(TileEntityWorkstation te, out float burnIn)
    {
        burnIn = float.MaxValue;
        bool ready = false;
        try
        {
            RecipeQueueItem[] q = te.Queue;
            if (q != null)
                foreach (RecipeQueueItem it in q)
                {
                    if (it == null || it.Recipe == null || it.Multiplier <= 0 || !RebirthCookingHeat.Managed(it.Recipe)) continue;
                    if (!RebirthCookingHeat.Ready(it.Recipe) || RebirthCookingHeat.Burnt(it.Recipe)) continue;
                    ready = true;
                    float left = RebirthCookingHeatRules.BurnAfter(RebirthCookingHeat.Text(it.Recipe, "method")) - RebirthCookingHeat.Number(it.Recipe, "overdue");
                    burnIn = Mathf.Min(burnIn, Mathf.Max(0f, left));
                }
            if (!te.OutputEmpty()) ready = true;
        }
        catch { }
        return ready;
    }

    /// <summary>Stations within range that have ready food, the one about to burn first (then the nearest).</summary>
    private static List<Vector3i> StationsWithReadyFood(EntityPlayerLocal p, int radius)
    {
        var found = new List<KeyValuePair<Vector3i, float>>();
        Vector3i c = p.GetBlockPosition();
        for (int cx = World.toChunkXZ(c.x - radius); cx <= World.toChunkXZ(c.x + radius); cx++)
            for (int cz = World.toChunkXZ(c.z - radius); cz <= World.toChunkXZ(c.z + radius); cz++)
            {
                var chunk = p.world.GetChunkSync(cx, cz) as Chunk;
                if (chunk == null) continue;
                foreach (TileEntity t in chunk.GetTileEntities().list)
                {
                    var te = t as TileEntityWorkstation;
                    float burnIn;
                    if (te == null || !StationHasReadyFood(te, out burnIn)) continue;
                    Vector3i pos = te.ToWorldPos();
                    float d = (pos.ToVector3() - p.position).magnitude;
                    if (d > radius) continue;
                    found.Add(new KeyValuePair<Vector3i, float>(pos, burnIn + d * 0.5f)); // urgency first, distance breaks ties
                }
            }
        found.Sort((a, b) => a.Value.CompareTo(b.Value));
        return found.ConvertAll(kv => kv.Key);
    }

    private static bool StillReady(EntityPlayerLocal p, Vector3i pos)
    {
        var te = p.world.GetTileEntity(pos) as TileEntityWorkstation;
        float ignored;
        return te != null && StationHasReadyFood(te, out ignored);
    }

    private static IEnumerator RescueCooking(EntityPlayerLocal p, float secondsLeft, Action<string> log)
    {
        if (Time.realtimeSinceStartup < rescueBackoffUntil) yield break;
        // Go to the station whose food is actually ready (the one about to burn first), not just the nearest fire.
        List<Vector3i> stations = StationsWithReadyFood(p, 60);
        if (stations.Count == 0)
        {
            Vector3i? any = NearestCookingStation(p, 60);
            if (!any.HasValue) { log("food about to burn (" + secondsLeft.ToString("0") + "s) but no cooking station within 60 m"); rescueBackoffUntil = Time.realtimeSinceStartup + 60f; yield break; }
            stations.Add(any.Value);
        }
        log((secondsLeft < 3600f ? "food burns in " + secondsLeft.ToString("0") + "s" : "cooked food is ready")
            + (stations.Count > 1 ? " on " + stations.Count + " stations" : "") + " - going to take it");

        int emptied = 0;
        string problem = null;
        var visitedStations = new HashSet<Vector3i>();
        for (int round = 0; round < 4 && stations.Count > 0; round++)
        {
            Vector3i station = stations[0];
            visitedStations.Add(station);
            yield return RebirthGameBridgePlayer.ApproachAndActivate(p, station, log);

            // Wait for the station window and its TAKE button to be built.
            Vector2 pt = default(Vector2);
            bool found = false;
            float until = Time.realtimeSinceStartup + 1.5f;
            while (Time.realtimeSinceStartup < until && !(found = RebirthGameBridgeUi.AnyModalOpen() && RebirthGameBridgeUi.TryFindById("rebirthCookingTake", out pt)))
                yield return null;
            if (found) yield return RebirthGameBridgeUi.LookAtWindow();

            if (!RebirthGameBridgeUi.AnyModalOpen()) problem = "the station window did not open";
            else if (!found) problem = "no TAKE button in the station window; visible buttons: " + string.Join(", ", RebirthGameBridgeUi.ClickableTexts(RebirthGameBridgeUi.OpenStationWindow()).ToArray());
            else
            {
                for (int i = 0; i < 8 && RebirthGameBridgeUi.TryFindById("rebirthCookingTake", out pt); i++)
                {
                    yield return RebirthGameBridgeUi.ClickAt(pt);
                    yield return Wait(0.35f);
                }
            }
            RebirthGameBridgeInput.ReleaseCursor();
            yield return RebirthGameBridgeUi.CloseMenus();
            if (problem != null) break;
            // Verify on the station itself (the HUD alert lingers a moment after taking).
            if (StillReady(p, station)) { problem = "food is still waiting on the station after pressing TAKE"; break; }
            emptied++;
            // Anything else ready nearby? A player grabs it in the same trip instead of walking off and coming back.
            stations = StationsWithReadyFood(p, 60).FindAll(s => !visitedStations.Contains(s));
            if (stations.Count > 0) log("more food ready on another station - taking that too");
        }
        if (problem == null)
        {
            log("took the cooked food (" + emptied + " station" + (emptied == 1 ? "" : "s") + ", nothing left waiting)");
            nextCookCheck = Time.realtimeSinceStartup + 10f; // the HUD alert lingers a moment
        }
        else { log("could not take the food: " + problem + " - will retry in 30 s"); rescueBackoffUntil = Time.realtimeSinceStartup + 30f; }
    }

    private static int CountFoodItems(EntityPlayerLocal p)
    {
        return CountOwnedItems(p, ic => ic.GetItemName().StartsWith("food", StringComparison.Ordinal));
    }
    private static bool StationActive(EntityPlayerLocal p, Vector3i pos)
    {
        try
        {
            var te = p.world.GetTileEntity(pos) as TileEntityWorkstation;
            if (te == null) return false;
            if (te.IsBurning || te.IsCrafting || !te.OutputEmpty()) return true;
            RecipeQueueItem[] q = te.Queue;
            if (q != null) foreach (RecipeQueueItem it in q) if (it != null && it.Recipe != null && it.Multiplier > 0) return true;
        }
        catch { }
        return false;
    }

    /// <summary>The cooking station a player would use: the lit/active one nearby, else the nearest one.</summary>
    public static Vector3i? ActiveCookingStation(EntityPlayerLocal p, int radius) { return NearestCookingStation(p, radius); }

    private static Vector3i? NearestCookingStation(EntityPlayerLocal p, int radius)
    {
        Vector3i c = p.GetBlockPosition();
        Vector3i? best = null; float bestD = float.MaxValue;
        var checkedTypes = new Dictionary<int, bool>();
        for (int x = -radius; x <= radius; x++)
            for (int z = -radius; z <= radius; z++)
                for (int y = -6; y <= 6; y++)
                {
                    BlockValue bv = p.world.GetBlock(c.x + x, c.y + y, c.z + z);
                    if (bv.isair || bv.ischild) continue;
                    bool isStation;
                    if (!checkedTypes.TryGetValue(bv.type, out isStation))
                    {
                        string n = bv.Block.GetBlockName();
                        isStation = n.IndexOf("campfire", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("stove", StringComparison.OrdinalIgnoreCase) >= 0
                            || n.IndexOf("grill", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("oven", StringComparison.OrdinalIgnoreCase) >= 0;
                        checkedTypes[bv.type] = isStation;
                    }
                    if (!isStation) continue;
                    var pos = new Vector3i(c.x + x, c.y + y, c.z + z);
                    // The fire that is actually cooking (lit, or something queued / ready) wins over a cold one
                    // that happens to be closer - that's the one a player walks to.
                    float d = x * x + y * y + z * z;
                    if (!StationActive(p, pos)) d += 1e6f;
                    if (d < bestD) { bestD = d; best = pos; }
                }
        return best;
    }
}


