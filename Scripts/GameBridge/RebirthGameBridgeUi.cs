using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using InControl;
using Newtonsoft.Json.Linq;

#nullable disable

/// <summary>
/// UI perception and interaction for the Game Bridge.
///
/// /ui/tree  walks the XUi controller tree of the open windows and reports each visible view that has
///           text, an item, a recipe, an id or a click handler, with its on-screen rectangle
///           (screenshot pixel coordinates, origin top-left).
/// /ui/click moves the game's soft cursor to a node (or x,y) and presses the real GUI click action, so
///           NGUI hover/press/click and XUi handlers run exactly as for a mouse click.
/// </summary>
public static class RebirthGameBridgeUi
{
    private const int MaxNodes = 2500;

    public static bool TryDispatch(BridgeRequest req)
    {
        switch (req.Path)
        {
            case "/ui/tree": Tree(req); return true;
            case "/ui/find": Find(req); return true;
            case "/ui/click": Click(req); return true;
            case "/ui/hover": Hover(req); return true;
            case "/ui/drag": Drag(req); return true;
            case "/ui/type": TypeText(req); return true;
            case "/ui/scroll": Scroll(req); return true;
            default: return false;
        }
    }

    // ------------------------------------------------------------------ tree model

    private sealed class Node
    {
        public string Path;
        public XUiController Controller;
        public XUiView View;
        public JObject Json;
        public Rect ScreenRect; // Unity screen coords (bottom-left origin)
        public bool HasRect;
        public bool UnderClickable; // inside a clickable ancestor (e.g. a button's label)
    }

    private static GUIWindowManager WindowManager()
    {
        LocalPlayerUI ui = LocalPlayerUI.GetUIForPrimaryPlayer();
        return ui != null ? ui.windowManager : null;
    }

    private static Camera UiCamera()
    {
        LocalPlayerUI ui = LocalPlayerUI.GetUIForPrimaryPlayer();
        return ui != null && ui.uiCamera != null ? ui.uiCamera.cachedCamera : null;
    }

    /// <summary>
    /// Every LocalPlayerUI: the player's copy plus the primary/menu UI. Some screens (e.g. the
    /// death/"Ready to spawn" window) live in a different copy than the player's HUD.
    /// </summary>
    private static List<LocalPlayerUI> AllUis()
    {
        var uis = new List<LocalPlayerUI>();
        LocalPlayerUI player = LocalPlayerUI.GetUIForPrimaryPlayer();
        if (player != null) uis.Add(player);
        if (LocalPlayerUI.PlayerUIs != null)
            foreach (LocalPlayerUI u in LocalPlayerUI.PlayerUIs)
                if (u != null && !uis.Contains(u)) uis.Add(u);
        LocalPlayerUI primary = LocalPlayerUI.primaryUI;
        if (primary != null && !uis.Contains(primary)) uis.Add(primary);
        return uis;
    }

    // ------------------------------------------------------------------ helpers for in-game instincts

    /// <summary>Screen point of a visible item slot holding `itemName` (toolbelt or not), if any.</summary>
    public static bool TryFindItemSlot(string itemName, bool inToolbelt, out Vector2 point, Func<ItemStack, bool> matchStack = null)
    {
        point = default(Vector2);
        foreach (Node n in Collect(null, true))
        {
            if (!(n.Controller is XUiC_ItemStack) || !n.HasRect) continue;
            bool isToolbelt = n.Path.StartsWith("toolbelt/", StringComparison.OrdinalIgnoreCase);
            if (isToolbelt != inToolbelt) continue;
            // Backpack: only real inventory grid slots, not item icons shown elsewhere (recipe ingredients,
            // selected-item previews, the drag-and-drop cursor stack).
            if (!isToolbelt && (n.Path.IndexOf("/inventory/", StringComparison.OrdinalIgnoreCase) < 0 || n.Path.StartsWith("dragAndDrop", StringComparison.OrdinalIgnoreCase))) continue;
            JObject item = n.Json["item"] as JObject;
            if (item == null || (string)item["name"] != itemName) continue;
            if (matchStack != null && !matchStack(((XUiC_ItemStack)n.Controller).ItemStack)) continue;
            point = n.ScreenRect.center;
            return true;
        }
        return false;
    }

    /// <summary>Screen point of the first empty toolbelt slot (index 0-based), if any.</summary>
    public static bool TryFindEmptyToolbeltSlot(out Vector2 point, out int index)
    {
        point = default(Vector2); index = -1;
        foreach (Node n in Collect("toolbelt", true))
        {
            if (!(n.Controller is XUiC_ItemStack) || !n.HasRect || n.Json["item"] is JObject) continue;
            int i;
            string last = n.Path.Substring(n.Path.LastIndexOf('/') + 1);
            if (!int.TryParse(last, out i)) continue;
            point = n.ScreenRect.center; index = i;
            return true;
        }
        return false;
    }

    // Restrict deposits to the actual context backpack, never a matching item in the chest.
    public static bool TryFindDepositSlot(TEFeatureStorage loot, out Vector2 point, Func<ItemStack, bool> retain = null)
    {
        point = default(Vector2);
        foreach (Node n in Collect(null, true))
        {
            var slot = n.Controller as XUiC_RebirthContextSlot;
            if (slot == null || !slot.IsBackpack || !slot.IsPresented || !n.HasRect) continue;
            if (slot.UserLockedSlot) continue;
            if (slot.Grid == null || !slot.Grid.HasVisibleCenter(slot.SlotNumber)) continue;
            ItemStack stack = slot.ItemStack;
            if (stack == null || stack.IsEmpty() || stack.itemValue.ItemClass.GetItemName() == RebirthGameBridgePoiStorage.CrateName) continue;
            if (retain != null && retain(stack)) continue;
            if (!RebirthGameBridgePoiStorage.CanAccept(loot, stack)) continue;
            point = n.ScreenRect.center;
            return true;
        }
        return false;
    }

    public static bool ScrollContextBackpack(float delta)
    {
        foreach (Node n in Collect(null, true))
        {
            var slot = n.Controller as XUiC_RebirthContextSlot;
            if (slot == null || !slot.IsBackpack || !slot.IsPresented || !n.HasRect) continue;
            if (slot.Grid == null || !slot.Grid.HasVisibleCenter(slot.SlotNumber)) continue;
            slot.Scrolled(delta);
            return true;
        }
        return false;
    }

    public static bool TypeInput(string window, string id, string value)
    {
        foreach (Node n in Collect(window, true))
        {
            if ((string)n.Json["id"] != id) continue;
            var input = n.Controller as XUiC_TextInput ?? FindAncestor<XUiC_TextInput>(n.Controller)
                ?? FindDescendant<XUiC_TextInput>(n.Controller);
            if (input == null) continue;
            input.Text = value;
            input.TriggerOnChangeHandler(false);
            return true;
        }
        return false;
    }

    /// <summary>How many toolbelt slots the UI actually shows (REBIRTH: 4). 0 if the toolbelt isn't visible.</summary>
    public static int VisibleToolbeltSlotCount()
    {
        int n = 0;
        foreach (Node node in Collect("toolbelt", true))
            if (node.Controller is XUiC_ItemStack && node.HasRect) n++;
        return n;
    }

    private static readonly System.Text.RegularExpressions.Regex BurnTimer =
        new System.Text.RegularExpressions.Regex(@"(\d+):(\d\d)\s*to burning", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    /// <summary>Seconds until cooking food burns, read from the HUD alert ("01:12 to burning"), if shown.</summary>
    public static bool TryReadBurnTimer(out float seconds)
    {
        seconds = float.MaxValue;
        bool found = false;
        foreach (Node n in Collect(null, false))
        {
            string text = (string)n.Json["text"];
            if (string.IsNullOrEmpty(text)) continue;
            var m = BurnTimer.Match(text);
            if (!m.Success) continue;
            float s = int.Parse(m.Groups[1].Value) * 60 + int.Parse(m.Groups[2].Value);
            if (s < seconds) { seconds = s; found = true; }
        }
        return found;
    }

    /// <summary>All visible label texts of a window, in UI order (for reading panels like REBIRTH's Metabolism page).</summary>
    public static List<string> Texts(string window)
    {
        var list = new List<string>();
        foreach (Node n in Collect(window, false))
        {
            string t = (string)n.Json["text"];
            if (!string.IsNullOrEmpty(t)) list.Add(t.Trim());
        }
        return list;
    }

    /// <summary>Screen point of the first visible node whose text equals `text` (case-insensitive) in a window.</summary>
    public static bool TryFindByText(string window, string text, out Vector2 point)
    {
        point = default(Vector2);
        foreach (Node n in Collect(window, true))
            if (n.HasRect && string.Equals(((string)n.Json["text"] ?? "").Trim(), text, StringComparison.OrdinalIgnoreCase)) { point = n.ScreenRect.center; return true; }
        return false;
    }

    public static bool TryFindByTextAny(string text, out Vector2 point) { return TryFindByText(null, text, out point); }

    public static bool IsWindowOpen(string id)
    {
        foreach (JToken t in OpenWindowIds()) if (string.Equals((string)t, id, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }
    /// <summary>Screen point of a visible recipe row for `recipe` (recipe name) in a window.</summary>
    public static bool TryFindRecipe(string window, string recipe, out Vector2 point)
    {
        point = default(Vector2);
        foreach (Node n in Collect(window, true))
            if (n.HasRect && string.Equals((string)n.Json["recipe"], recipe, StringComparison.OrdinalIgnoreCase)) { point = n.ScreenRect.center; return true; }
        return false;
    }

    /// <summary>Id of the open workstation window (workstation_campfire, ...), or null.</summary>
    public static string OpenStationWindow()
    {
        foreach (JToken t in OpenWindowIds()) { string id = (string)t; if (id != null && id.StartsWith("workstation_", StringComparison.OrdinalIgnoreCase)) return id; }
        return null;
    }

    /// <summary>Does any visible label in the window contain `text` (case-insensitive)?</summary>
    public static bool WindowContains(string window, string text)
    {
        foreach (string t in Texts(window)) if (t.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0) return true;
        return false;
    }
    /// <summary>HUD says cooked food is ready or overcooking ("READY TO TAKE", "OVERCOOKING - TAKE NOW").</summary>
    public static bool CookingReadyAlert()
    {
        foreach (Node n in Collect(null, false))
        {
            string t = (string)n.Json["text"];
            if (t != null && (t.IndexOf("TAKE NOW", StringComparison.OrdinalIgnoreCase) >= 0 || t.IndexOf("READY TO TAKE", StringComparison.OrdinalIgnoreCase) >= 0)) return true;
        }
        return false;
    }

    /// <summary>Texts/ids of clickable controls in a window (diagnostics when an expected button is missing).</summary>
    public static List<string> ClickableTexts(string window)
    {
        var l = new List<string>();
        if (window == null) return l;
        foreach (Node n in Collect(window, true))
        {
            if (!n.HasRect || n.Json["clickable"] == null) continue;
            string label = (string)n.Json["text"] ?? (string)n.Json["id"];
            if (!string.IsNullOrEmpty(label) && !l.Contains(label) && l.Count < 20) l.Add(label);
        }
        return l;
    }
    /// <summary>Screen point of the first visible node with this id.</summary>
    /// <summary>Find a control by id inside one window (several windows reuse ids like btnMoveAll).</summary>
    public static bool TryFindById(string window, string id, out Vector2 point)
    {
        point = default(Vector2);
        foreach (Node n in Collect(window, true))
            if (n.HasRect && string.Equals((string)n.Json["id"], id, StringComparison.OrdinalIgnoreCase)) { point = n.ScreenRect.center; return true; }
        return false;
    }

    public static bool TryFindById(string id, out Vector2 point)
    {
        point = default(Vector2);
        foreach (Node n in Collect(null, true))
            if (n.HasRect && string.Equals((string)n.Json["id"], id, StringComparison.OrdinalIgnoreCase)) { point = n.ScreenRect.center; return true; }
        return false;
    }

    /// <summary>Screen point of toolbelt slot `index` (0-based), if visible.</summary>
    public static bool TryFindToolbeltSlot(int index, out Vector2 point)
    {
        point = default(Vector2);
        foreach (Node n in Collect("toolbelt", true))
        {
            if (!(n.Controller is XUiC_ItemStack) || !n.HasRect) continue;
            if (n.Path.EndsWith("/" + index, StringComparison.Ordinal)) { point = n.ScreenRect.center; return true; }
        }
        return false;
    }

    /// <summary>Screen point of an empty backpack slot (any visible non-toolbelt item slot), if any.</summary>
    public static bool TryFindEmptyBackpackSlot(out Vector2 point)
    {
        point = default(Vector2);
        foreach (Node n in Collect(null, true))
        {
            if (!(n.Controller is XUiC_ItemStack) || !n.HasRect || n.Json["item"] is JObject) continue;
            if (n.Path.StartsWith("toolbelt/", StringComparison.OrdinalIgnoreCase) || n.Path.StartsWith("dragAndDrop", StringComparison.OrdinalIgnoreCase)) continue;
            if (n.Path.IndexOf("/inventory/", StringComparison.OrdinalIgnoreCase) < 0) continue; // real backpack grid only
            point = n.ScreenRect.center;
            return true;
        }
        return false;
    }

    // ------------------------------------------------------------------ paced (human-speed) UI actions

    private static IEnumerator Pause(float seconds)
    {
        float end = Time.realtimeSinceStartup + seconds * RebirthGameBridgeInput.Pace;
        while (Time.realtimeSinceStartup < end) yield return null;
    }

    /// <summary>Glide the cursor to a point with an ease-in/out, like a hand on a mouse (paced).</summary>
    public static IEnumerator GlideTo(Vector2 to)
    {
        Vector2 from = RebirthGameBridgeInput.CursorActive ? RebirthGameBridgeInput.CursorPosition : (Vector2)Input.mousePosition;
        float duration = Mathf.Clamp(Vector2.Distance(from, to) / 1200f, 0.18f, 0.45f) * RebirthGameBridgeInput.Pace;
        float start = Time.realtimeSinceStartup;
        while (duration > 0.01f && Time.realtimeSinceStartup - start < duration)
        {
            float t = (Time.realtimeSinceStartup - start) / duration;
            RebirthGameBridgeInput.SetCursor(Vector2.Lerp(from, to, t * t * (3f - 2f * t))); // smoothstep
            yield return null;
        }
        for (int i = 0; i < 2; i++) { RebirthGameBridgeInput.SetCursor(to); yield return null; }
    }

    /// <summary>
    /// Move an item stack like a player: move to it, press, drag, release on the destination (and click there if
    /// the game uses click-to-place and the stack is still on the cursor). In REBIRTH's crafting-screen
    /// backpack a plain click only SELECTS an item, so picking up needs this drag gesture.
    /// </summary>
    public static IEnumerator DragItem(Vector2 from, Vector2 to)
    {
        PlayerAction click = RebirthGameBridgeInput.Find("gui.LeftClick");
        if (click == null) yield break;
        yield return GlideTo(from);
        yield return Pause(0.15f);
        RebirthGameBridgeInput.HoldSeconds(click, 0f);
        for (int i = 0; i < 3; i++) { RebirthGameBridgeInput.SetCursor(from); yield return null; }
        yield return GlideTo(to);                        // carry it across (button held)
        yield return Pause(0.1f);
        RebirthGameBridgeInput.Release(click);
        for (int i = 0; i < 4; i++) yield return null;
        if (HeldItem() != null) yield return ClickAt(to);
        yield return Pause(0.2f);
    }

    /// <summary>Close open menus like a player (Escape), verifying they closed; retries a few times.</summary>
    public static IEnumerator CloseMenus()
    {
        if (AnyModalOpen()) yield return Pause(0.35f); // a beat before closing, so it's visible
        for (int attempt = 0; attempt < 3 && AnyModalOpen(); attempt++)
        {
            Vector2 resume;
            if (IsWindowOpen("ingameMenu") && (TryFindById("btnRebirthResume", out resume) || TryFindByText("ingameMenu", "Resume", out resume)))
            { yield return ClickAt(resume); continue; } // the pause menu: Escape toggles it, click Resume
            foreach (PlayerAction a in RebirthGameBridgeInput.FindByKey("Escape")) RebirthGameBridgeInput.HoldSeconds(a, 0.1f);
            float until = Time.realtimeSinceStartup + 0.6f;
            while (Time.realtimeSinceStartup < until && AnyModalOpen()) yield return null;
        }
    }

    /// <summary>Wait after a window opens so a watcher can see it (paced).</summary>
    public static IEnumerator LookAtWindow() { yield return Pause(0.4f); }

    /// <summary>One real left click at a screen point: glide there, a short pause, press, release (paced).</summary>
    public static IEnumerator ClickAt(Vector2 point, Func<bool> beforePress = null)
    {
        PlayerAction click = RebirthGameBridgeInput.Find("gui.LeftClick");
        if (click == null) yield break;
        yield return GlideTo(point);
        yield return Pause(0.12f);
        if (beforePress != null && !beforePress()) yield break;
        RebirthGameBridgeInput.HoldFrames(click, 2);
        while (RebirthGameBridgeInput.IsHeld(click)) { RebirthGameBridgeInput.SetCursor(point); yield return null; }
        for (int i = 0; i < 4; i++) yield return null;
        yield return Pause(0.15f);
    }
    /// <summary>Advance nested iterators with admission at every yield and dispose deepest first.</summary>
    public static IEnumerator RunGuarded(IEnumerator routine, RebirthGameBridgeInput.OwnedInputScope scope)
    {
        var stack = new Stack<IEnumerator>();
        if (routine != null) stack.Push(routine);
        try
        {
            while (stack.Count > 0 && scope != null && scope.Admitted)
            {
                IEnumerator current = stack.Peek();
                if (!current.MoveNext()) { stack.Pop(); (current as IDisposable)?.Dispose(); continue; }
                IEnumerator child = current.Current as IEnumerator;
                if (child != null) { stack.Push(child); continue; }
                yield return current.Current;
            }
        }
        finally
        {
            while (stack.Count > 0)
            {
                try { (stack.Pop() as IDisposable)?.Dispose(); }
                catch { scope?.Refuse("nested iterator cleanup failed"); }
            }
            try { if (scope != null && scope.ContextCurrent && HeldItem() != null) scope.MarkCursorCustodyPending(); }
            catch { scope?.Refuse("native cursor custody inspection failed"); }
            finally { scope?.Dispose(); }
        }
    }
    public static IEnumerator GlideTo(Vector2 to, RebirthGameBridgeInput.OwnedInputScope scope)
    {
        if (scope == null || !scope.Admitted) yield break;
        Vector2 from = RebirthGameBridgeInput.CursorActive ? RebirthGameBridgeInput.CursorPosition : (Vector2)Input.mousePosition;
        float duration = Mathf.Clamp(Vector2.Distance(from, to) / 1200f, .18f, .45f) * RebirthGameBridgeInput.Pace;
        float start = Time.realtimeSinceStartup;
        while (duration > .01f && Time.realtimeSinceStartup - start < duration)
        {
            float t = (Time.realtimeSinceStartup - start) / duration;
            if (!scope.TrySetCursor(Vector2.Lerp(from, to, t * t * (3f - 2f * t)))) yield break;
            yield return null;
        }
        for (int i = 0; i < 2; i++) { if (!scope.TrySetCursor(to)) yield break; yield return null; }
    }
    public static IEnumerator ClickAt(Vector2 point, RebirthGameBridgeInput.OwnedInputScope parent, Func<bool> beforePress = null)
    {
        bool pressed = false;
        using (var scope = new RebirthGameBridgeInput.OwnedInputScope(parent.Player, () => parent.Admitted && (pressed || beforePress == null || beforePress()), parent))
        {
            try { yield return RunGuarded(ClickOwned(point, scope, () => pressed = true, () => pressed), scope); }
            finally
            {
                if (scope.CursorCustodyPending) parent.MarkCursorCustodyPending();
                else if (scope.Failure != null) parent.Refuse(scope.Failure);
            }
        }
    }
    private static IEnumerator ClickOwned(Vector2 point, RebirthGameBridgeInput.OwnedInputScope scope, Action pressed, Func<bool> injected)
    {
        if (!scope.Admitted) yield break;
        PlayerAction click = RebirthGameBridgeInput.Find("gui.LeftClick");
        if (click == null) { scope.Refuse("mouse action unavailable"); yield break; }
        yield return GlideTo(point, scope);
        yield return Pause(.12f);
        if (!scope.TryHoldFrames(click, 2, 1f, null, pressed)) yield break;
        while (RebirthGameBridgeInput.IsHeld(click)) { if (!scope.TrySetCursor(point)) yield break; yield return null; }
        if (!injected()) { scope.Refuse("owned click ended without native injection"); yield break; }
        for (int i = 0; i < 4; i++) yield return null;
        yield return Pause(.15f);
    }
    internal static ItemStack OwnedCursorStack()
    {
        EntityPlayerLocal p = GameManager.Instance?.World?.GetPrimaryPlayer();
        foreach (Node node in Collect("dragAndDrop", true))
        {
            var slot = node.Controller as XUiC_ItemStack;
            if (slot != null && ReferenceEquals(slot.xui?.playerUI?.entityPlayer, p) && slot.ItemStack != null && !slot.ItemStack.IsEmpty()) return slot.ItemStack;
        }
        return null;
    }
    internal static bool TryOwnedInventorySlot(bool toolbelt, int index, out Vector2 point)
    {
        point = default(Vector2);
        EntityPlayerLocal p = GameManager.Instance?.World?.GetPrimaryPlayer();
        World world = p?.world;
        ItemStack[] native = toolbelt ? p?.inventory?.ItemGrid?.items : p?.bag?.ItemGrid?.items;
        if (native == null || index < 0 || index >= native.Length) return false;
        ItemStack actual = native[index];
        foreach (Node node in Collect(null, true))
        {
            var slot = node.Controller as XUiC_ItemStack;
            if (slot == null || !node.HasRect || slot.SlotNumber != index || !ReferenceEquals(slot.xui?.playerUI?.entityPlayer, p)) continue;
            if (slot.StackLocation != (toolbelt ? XUiC_ItemStack.StackLocationTypes.ToolBelt : XUiC_ItemStack.StackLocationTypes.Backpack)) continue;
            var context = slot as XUiC_RebirthContextSlot;
            if (!toolbelt)
            {
                if (context != null) { if (!context.IsBackpack || !context.IsPresented || context.Grid == null || !context.Grid.HasVisibleCenter(index)) continue; }
                else if (slot.GetParentByType<XUiC_Backpack>() == null) continue;
                var scroll = slot.GetParentByType<XUiC_RebirthCraftingInventoryScroll>();
                if (scroll != null && !scroll.IsSlotVisible(index)) continue;
            }
            bool belt = node.Path.StartsWith("toolbelt/", StringComparison.OrdinalIgnoreCase);
            if (belt != toolbelt || (!belt && (node.Path.IndexOf("/inventory/", StringComparison.OrdinalIgnoreCase) < 0 || node.Path.StartsWith("dragAndDrop", StringComparison.OrdinalIgnoreCase)))) continue;
            ItemStack shown = slot.ItemStack;
            bool emptyActual = actual == null || actual.IsEmpty(), emptyShown = shown == null || shown.IsEmpty();
            if (emptyActual != emptyShown || (!emptyActual && (actual.count != shown.count || RebirthGameBridgePoiStorage.OwnedItemKey(actual) == null
                || RebirthGameBridgePoiStorage.OwnedItemKey(actual) != RebirthGameBridgePoiStorage.OwnedItemKey(shown)))) continue;
            if (p == null || world == null || p.IsDead() || !ReferenceEquals(p.world, world)
                || GameManager.Instance == null || !ReferenceEquals(GameManager.Instance.World, world) || !ReferenceEquals(world.GetPrimaryPlayer(), p)) return false;
            ItemStack[] current = toolbelt ? p.inventory.ItemGrid.items : p.bag.ItemGrid.items;
            if (!ReferenceEquals(native, current) || !ReferenceEquals(current[index], actual)) return false;
            point = node.ScreenRect.center; return true;
        }
        return false;
    }
    public static IEnumerator DragItem(Vector2 from, Vector2 to, RebirthGameBridgeInput.OwnedInputScope parent, Func<ItemStack, bool> beforePickup = null)
    {
        using (var scope = new RebirthGameBridgeInput.OwnedInputScope(parent.Player, () => parent.Admitted && (beforePickup == null || beforePickup(OwnedCursorStack())), parent))
        {
            try { yield return RunGuarded(DragOwned(from, to, scope, beforePickup != null), scope); }
            finally
            {
                if (scope.CursorCustodyPending) parent.MarkCursorCustodyPending();
                else if (scope.Failure != null) parent.Refuse(scope.Failure);
            }
        }
    }
    private static IEnumerator DragOwned(Vector2 from, Vector2 to, RebirthGameBridgeInput.OwnedInputScope scope, bool requirePickup)
    {
        if (!scope.Admitted) yield break;
        PlayerAction click = RebirthGameBridgeInput.Find("gui.LeftClick");
        if (click == null) { scope.Refuse("mouse action unavailable"); yield break; }
        yield return GlideTo(from, scope);
        yield return Pause(.15f);
        if (!scope.TryHoldSeconds(click, 0)) yield break;
        try
        {
            for (int i = 0; i < 3; i++) { if (!scope.TrySetCursor(from)) yield break; yield return null; }
            if (requirePickup)
            {
                float until = Time.realtimeSinceStartup + 1f;
                while (OwnedCursorStack() == null && Time.realtimeSinceStartup < until)
                { if (!scope.TrySetCursor(from)) yield break; yield return null; }
                if (OwnedCursorStack() == null || !scope.Admitted) { scope.Refuse("native cursor pickup was not witnessed"); yield break; }
            }
            yield return GlideTo(to, scope);
            yield return Pause(.1f);
        }
        finally { scope.Release(click); }
        for (int i = 0; i < 4; i++) yield return null;
        if (HeldItem() != null) yield return ClickAt(to, scope);
        yield return Pause(.2f);
    }
    public static IEnumerator CloseMenus(RebirthGameBridgeInput.OwnedInputScope parent)
    {
        using (var scope = new RebirthGameBridgeInput.OwnedInputScope(parent.Player, () => parent.Admitted, parent))
        {
            try { yield return RunGuarded(CloseOwned(scope), scope); }
            finally
            {
                if (scope.CursorCustodyPending) parent.MarkCursorCustodyPending();
                else if (scope.Failure != null) parent.Refuse(scope.Failure);
            }
        }
    }
    private static IEnumerator CloseOwned(RebirthGameBridgeInput.OwnedInputScope scope)
    {
        if (!scope.Admitted) yield break;
        if (HeldItem() != null) { scope.MarkCursorCustodyPending(); yield break; }
        if (AnyModalOpen()) yield return Pause(.35f);
        for (int attempt = 0; attempt < 3 && AnyModalOpen(); attempt++)
        {
            if (!scope.Admitted) yield break;
            if (HeldItem() != null) { scope.MarkCursorCustodyPending(); yield break; }
            Vector2 resume;
            if (IsWindowOpen("ingameMenu") && (TryFindById("btnRebirthResume", out resume) || TryFindByText("ingameMenu", "Resume", out resume)))
            { yield return ClickAt(resume, scope); continue; }
            foreach (PlayerAction action in RebirthGameBridgeInput.FindByKey("Escape")) if (!scope.TryHoldSeconds(action, .1f)) yield break;
            float until = Time.realtimeSinceStartup + .6f;
            while (Time.realtimeSinceStartup < until && AnyModalOpen()) yield return null;
        }
    }
    public static List<string> OpenWindowIdList()
    {
        var l = new List<string>();
        foreach (JToken t in OpenWindowIds()) l.Add((string)t);
        return l;
    }

    /// <summary>True when a window that blocks gameplay input (inventory, workstation, loot...) is open.</summary>
    public static bool AnyModalOpen()
    {
        LocalPlayerUI ui = LocalPlayerUI.GetUIForPrimaryPlayer();
        return ui != null && ui.windowManager != null && ui.windowManager.IsModalWindowOpen();
    }
    public static JArray OpenWindowIds()
    {
        var arr = new JArray();
        foreach (LocalPlayerUI ui in AllUis())
            foreach (XUiWindowGroup g in VisibleGroups(ui))
                if (!arr.Any(t => (string)t == g.Id)) arr.Add(g.Id);
        return arr;
    }

    private static List<Node> Collect(string windowFilter, bool includeAll)
    {
        var nodes = new List<Node>();
        var seen = new HashSet<XUiWindowGroup>();
        foreach (LocalPlayerUI ui in AllUis())
        {
            Camera cam = ui.uiCamera != null ? ui.uiCamera.cachedCamera : null;
            foreach (XUiWindowGroup group in VisibleGroups(ui))
            {
                if (!seen.Add(group)) continue;
                if (windowFilter != null && !group.Id.Equals(windowFilter, StringComparison.OrdinalIgnoreCase)) continue;
                Walk(group.Controller, group.Id, cam, includeAll, nodes, false);
            }
        }
        return nodes;
    }

    /// <summary>
    /// Window groups the UI's window manager lists as open, plus any other XUi group that is showing.
    /// </summary>
    private static List<XUiWindowGroup> VisibleGroups(LocalPlayerUI ui)
    {
        var groups = new List<XUiWindowGroup>();
        GUIWindowManager wm = ui != null ? ui.windowManager : null;
        if (wm != null && wm.openWindows != null)
            foreach (GUIWindow w in new List<GUIWindow>(wm.openWindows))
            {
                var g = w as XUiWindowGroup;
                if (g != null && g.Controller != null) groups.Add(g);
            }
        XUi xui = ui != null ? ui.xui : null;
        if (xui != null && xui.WindowGroups != null)
            foreach (XUiWindowGroup g in xui.WindowGroups)
                if (g != null && g.Controller != null && g.isShowing && !groups.Contains(g)) groups.Add(g);
        return groups;
    }

    private static void Walk(XUiController c, string path, Camera cam, bool includeAll, List<Node> nodes, bool underClickable)
    {
        if (c == null || nodes.Count >= MaxNodes) return;
        XUiView v = c.ViewComponent;
        if (v != null && (!v.IsVisible || v.UiTransform == null || !v.UiTransform.gameObject.activeInHierarchy)) return;

        Node n = Describe(c, v, path, cam, includeAll);
        if (n != null) { n.UnderClickable = underClickable; nodes.Add(n); }
        bool childUnderClickable = underClickable || (v != null && v.EventOnPress);

        List<XUiController> kids = c.Children;
        if (kids == null) return;
        var idCounts = new Dictionary<string, int>();
        foreach (XUiController k in kids)
        {
            string id = k.ViewComponent != null ? k.ViewComponent.ID : null;
            if (!string.IsNullOrEmpty(id)) { int cnt; idCounts.TryGetValue(id, out cnt); idCounts[id] = cnt + 1; }
        }
        for (int i = 0; i < kids.Count; i++)
        {
            XUiController k = kids[i];
            string id = k.ViewComponent != null ? k.ViewComponent.ID : null;
            string seg = !string.IsNullOrEmpty(id) && idCounts[id] == 1 ? id : (id ?? "") + "#" + i;
            Walk(k, path + "/" + seg, cam, includeAll, nodes, childUnderClickable);
        }
    }

    private static Node Describe(XUiController c, XUiView v, string path, Camera cam, bool includeAll)
    {
        var o = new JObject { ["path"] = path };
        bool interesting = false;

        if (v != null)
        {
            o["view"] = v.GetType().Name;
            if (!string.IsNullOrEmpty(v.ID)) { o["id"] = v.ID; interesting |= includeAll; }
            var label = v as XUiV_Label;
            if (label != null && !string.IsNullOrEmpty(label.Text)) { o["text"] = RebirthGameBridgePlayer.StripColors(label.Text); interesting = true; }
            var sprite = v as XUiV_Sprite;
            if (sprite != null && includeAll && !string.IsNullOrEmpty(sprite.SpriteName)) o["sprite"] = sprite.SpriteName;
            if (!string.IsNullOrEmpty(v.ToolTip)) o["tooltip"] = RebirthGameBridgePlayer.StripColors(v.ToolTip);
            if (v.EventOnPress) { o["clickable"] = true; interesting = true; }
            if (!v.Enabled) o["enabled"] = false;
        }

        string ctrl = c.GetType().Name;
        if (ctrl != "XUiController") { o["ctrl"] = ctrl; }

        var btn = c as XUiC_SimpleButton;
        if (btn != null && !string.IsNullOrEmpty(btn.Text)) { o["text"] = RebirthGameBridgePlayer.StripColors(btn.Text); interesting = true; }

        var input = c as XUiC_TextInput;
        if (input != null) { o["input"] = input.Text ?? ""; interesting = true; }

        var stack = c as XUiC_ItemStack;
        if (stack != null)
        {
            ItemStack s = stack.ItemStack;
            if (s != null && !s.IsEmpty())
            {
                var item = new JObject { ["name"] = s.itemValue.ItemClass != null ? s.itemValue.ItemClass.GetItemName() : "#" + s.itemValue.type, ["count"] = s.count };
                if (s.itemValue.Quality > 0) item["quality"] = (int)s.itemValue.Quality;
                o["item"] = item;
            }
            else o["item"] = null;
            interesting = true;
        }

        var recipeEntry = c as XUiC_RecipeEntry;
        if (recipeEntry != null && recipeEntry.Recipe != null)
        {
            o["recipe"] = recipeEntry.Recipe.GetName();
            o["hasIngredients"] = recipeEntry.HasIngredients;
            interesting = true;
        }

        var queued = c as XUiC_RecipeStack;
        if (queued != null && queued.recipe != null)
        {
            o["queued"] = new JObject
            {
                ["recipe"] = queued.recipe.GetName(),
                ["count"] = queued.recipeCount,
                ["crafting"] = queued.isCrafting,
                ["timeLeft"] = (float)Math.Round(queued.craftingTimeLeft, 1),
                ["totalTimeLeft"] = (float)Math.Round(queued.totalCraftTimeLeft, 1)
            };
            interesting = true;
        }

        if (!interesting && !includeAll) return null;

        var node = new Node { Path = path, Controller = c, View = v, Json = o };
        if (v != null && cam != null)
        {
            try
            {
                Bounds b = NGUIMath.CalculateAbsoluteWidgetBounds(v.UiTransform);
                if (b.size.sqrMagnitude > 0f)
                {
                    Vector3 min = cam.WorldToScreenPoint(b.min), max = cam.WorldToScreenPoint(b.max);
                    node.ScreenRect = Rect.MinMaxRect(Mathf.Min(min.x, max.x), Mathf.Min(min.y, max.y), Mathf.Max(min.x, max.x), Mathf.Max(min.y, max.y));
                    node.HasRect = node.ScreenRect.width >= 1f && node.ScreenRect.height >= 1f;
                    if (node.HasRect)
                    {
                        Rect r = node.ScreenRect;
                        o["rect"] = new JArray(Mathf.RoundToInt(r.xMin), Mathf.RoundToInt(Screen.height - r.yMax), Mathf.RoundToInt(r.width), Mathf.RoundToInt(r.height));
                    }
                }
            }
            catch { }
        }
        return node;
    }

    private static bool Matches(Node n, BridgeRequest req)
    {
        string text = req.QueryString("text"), id = req.QueryString("id"), item = req.QueryString("item"),
               recipe = req.QueryString("recipe"), ctrl = req.QueryString("ctrl"), path = req.QueryString("path");
        if (path != null && !n.Path.Equals(path, StringComparison.OrdinalIgnoreCase)) return false;
        if (text != null && !ContainsToken(n.Json["text"], text) && !ContainsToken(n.Json["tooltip"], text)) return false;
        if (id != null && !string.Equals((string)n.Json["id"], id, StringComparison.OrdinalIgnoreCase)) return false;
        if (item != null && !(n.Json["item"] is JObject && ContainsToken(n.Json["item"]["name"], item))) return false;
        if (recipe != null && !ContainsToken(n.Json["recipe"], recipe) && !(n.Json["queued"] is JObject && ContainsToken(n.Json["queued"]["recipe"], recipe))) return false;
        if (ctrl != null && !ContainsToken(n.Json["ctrl"], ctrl) && !ContainsToken(n.Json["view"], ctrl)) return false;
        if (req.QueryBool("clickable", false) && n.Json["clickable"] == null) return false;
        return true;
    }

    private static bool ContainsToken(JToken t, string sub)
    {
        string s = t != null && t.Type == JTokenType.String ? (string)t : null;
        return s != null && s.IndexOf(sub, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool HasSelector(BridgeRequest req)
    {
        foreach (string k in new[] { "path", "text", "id", "item", "recipe", "ctrl" })
            if (req.QueryString(k) != null) return true;
        return false;
    }

    // ------------------------------------------------------------------ endpoints

    private static void Tree(BridgeRequest req)
    {
        List<Node> nodes = Collect(req.QueryString("window"), req.QueryBool("all", false));
        var arr = new JArray();
        foreach (Node n in nodes) arr.Add(n.Json);
        req.Complete(new JObject
        {
            ["openWindows"] = OpenWindowIds(),
            ["screen"] = new JArray(Screen.width, Screen.height),
            ["count"] = arr.Count,
            ["truncated"] = nodes.Count >= MaxNodes,
            ["nodes"] = arr
        });
    }

    private static void Find(BridgeRequest req)
    {
        var arr = new JArray();
        foreach (Node n in Collect(req.QueryString("window"), true))
            if (Matches(n, req)) arr.Add(n.Json);
        req.Complete(new JObject { ["count"] = arr.Count, ["nodes"] = arr });
    }

    /// <summary>Resolve the click point: ?x&amp;y (screenshot pixels, top-left origin; &amp;scale= if the
    /// screenshot was downscaled) or a node selector (path/text/id/item/recipe/ctrl, &amp;index=n).</summary>
    private static bool ResolveScreenPoint(BridgeRequest req, string prefix, out Vector2 point, out JObject node, out string error)
    {
        point = default(Vector2); node = null; error = null;
        string xs = req.QueryString(prefix + "x"), ys = req.QueryString(prefix + "y");
        if (xs != null && ys != null)
        {
            float scale = req.QueryFloat("scale", 1f);
            if (scale <= 0f) scale = 1f;
            float x = req.QueryFloat(prefix + "x", 0f) / scale, y = req.QueryFloat(prefix + "y", 0f) / scale;
            point = new Vector2(x, Screen.height - y);
            return true;
        }

        BridgeRequest selector = req;
        if (prefix.Length > 0)
        {
            // drag uses from.path / to.text etc.; re-map to plain keys for matching
            selector = new BridgeRequest();
            foreach (var kv in req.Query)
                if (kv.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    selector.Query[kv.Key.Substring(prefix.Length)] = kv.Value;
        }
        if (!HasSelector(selector)) { error = "give a node selector (" + prefix + "path|text|id|item|recipe|ctrl) or " + prefix + "x&" + prefix + "y"; return false; }

        var matches = new List<Node>();
        foreach (Node n in Collect(req.QueryString("window"), true))
            if (n.HasRect && Matches(n, selector)) matches.Add(n);
        if (matches.Count == 0) { error = "no visible UI node matches " + prefix + " selector"; return false; }

        // Without an explicit index, pick the best match: exact text beats partial text, and something
        // clickable (or inside a button, like its label) beats plain text such as a status line.
        int index = selector.QueryInt("index", -1);
        Node chosen = null;
        if (index >= 0) chosen = index < matches.Count ? matches[index] : null;
        else
        {
            string wanted = selector.QueryString("text");
            int bestScore = int.MinValue;
            foreach (Node m in matches)
            {
                int score = 0;
                if (m.Json["clickable"] != null) score += 4;
                else if (m.UnderClickable) score += 3;
                if (wanted != null && string.Equals(((string)m.Json["text"] ?? "").Trim(), wanted.Trim(), StringComparison.OrdinalIgnoreCase)) score += 5;
                if (score > bestScore) { bestScore = score; chosen = m; }
            }
        }
        if (chosen == null) { error = "index out of range (" + matches.Count + " matches)"; return false; }

        point = chosen.ScreenRect.center;
        node = chosen.Json;
        return true;
    }

    // ---- respawn: never leave the player on the death / "Ready to spawn" screen (that wastes a whole run) ----
    private static float spawnWaitStart = -1f, nextAutoSpawn;
    public static bool AutoRespawn = true;

    /// <summary>Called every frame: when the player has been dead or waiting to spawn for 6 s, press the spawn button like a player would.</summary>
    public static void AutoRespawnTick()
    {
        string st = RebirthGameBridge.CachedState;
        float now = Time.realtimeSinceStartup;
        if (!AutoRespawn || (st != "spawning" && st != "dead")) { spawnWaitStart = -1f; return; }
        if (spawnWaitStart < 0f) spawnWaitStart = now;
        if (now - spawnWaitStart < 6f || now < nextAutoSpawn) return;
        Vector2 pt;
        if (TryFindById("spawnselection", "btnNearBackpack", out pt) || TryFindById("spawnselection", "btnRandom", out pt) || TryFindById("spawnselection", "btnSpawn", out pt))
        {
            nextAutoSpawn = now + 8f;
            Log.Out("[REBIRTH GameBridge] respawn: pressing the spawn button");
            RebirthGameBridgePump.Instance.Run(ClickAt(pt));
        }
    }

    private static void Click(BridgeRequest req)
    {
        Vector2 point; JObject node; string error;
        if (!ResolveScreenPoint(req, "", out point, out node, out error)) { req.Fail(error, 404); return; }
        string button = req.QueryString("button", "left").ToLowerInvariant();
        PlayerAction action = RebirthGameBridgeInput.Find(button == "right" ? "gui.RightClick" : button == "middle" ? "gui.MiddleClick" : "gui.LeftClick");
        if (action == null) { req.Fail("GUI click action unavailable", 409); return; }
        RebirthGameBridgePump.Instance.Run(ClickRoutine(req, point, node, action,
            req.QueryBool("shift", false), req.QueryBool("ctrl", false), req.QueryBool("alt", false),
            req.QueryBool("double", false) ? 2 : Mathf.Clamp(req.QueryInt("clicks", 1), 1, 20)));
    }

    private static IEnumerator ClickRoutine(BridgeRequest req, Vector2 point, JObject node, PlayerAction click, bool shift, bool ctrl, bool alt, int clicks)
    {
        yield return GlideTo(point);                     // move there like a hand, not a teleport
        yield return Pause(0.12f);
        RebirthGameBridgeInput.Shift = shift; RebirthGameBridgeInput.Control = ctrl; RebirthGameBridgeInput.Alt = alt;
        for (int c = 0; c < clicks; c++)
        {
            RebirthGameBridgeInput.SetCursor(point);
            RebirthGameBridgeInput.HoldFrames(click, 2);
            while (RebirthGameBridgeInput.IsHeld(click)) { RebirthGameBridgeInput.SetCursor(point); yield return null; }
            yield return null;
            yield return null;
        }
        RebirthGameBridgeInput.Shift = RebirthGameBridgeInput.Control = RebirthGameBridgeInput.Alt = false;
        for (int i = 0; i < 4; i++) yield return null; // let windows open/close and bindings refresh
        req.Complete(new JObject
        {
            ["clicked"] = node ?? new JObject { ["screen"] = new JArray(Mathf.RoundToInt(point.x), Mathf.RoundToInt(Screen.height - point.y)) },
            ["hovered"] = UICamera.hoveredObject != null ? UICamera.hoveredObject.name : null,
            ["openWindows"] = OpenWindowIds()
        });
    }

    private static void Hover(BridgeRequest req)
    {
        Vector2 point; JObject node; string error;
        if (!ResolveScreenPoint(req, "", out point, out node, out error)) { req.Fail(error, 404); return; }
        RebirthGameBridgePump.Instance.Run(HoverRoutine(req, point, node, req.QueryFloat("seconds", 0.8f)));
    }

    private static IEnumerator HoverRoutine(BridgeRequest req, Vector2 point, JObject node, float seconds)
    {
        RebirthGameBridgeInput.SetCursor(point);
        float end = Time.realtimeSinceStartup + seconds;
        while (Time.realtimeSinceStartup < end) yield return null;
        var tooltip = new JArray();
        foreach (Node n in Collect("toolTip", false))
            if (n.Json["text"] != null) tooltip.Add(n.Json["text"]);
        req.Complete(new JObject
        {
            ["hoveredNode"] = node,
            ["hovered"] = UICamera.hoveredObject != null ? UICamera.hoveredObject.name : null,
            ["tooltip"] = tooltip
        });
    }

    /// <summary>Press on from.*, move to to.*, release. Selectors: from.path / from.item / from.x,from.y etc.</summary>
    private static void Drag(BridgeRequest req)
    {
        Vector2 from, to; JObject fromNode, toNode; string error;
        if (!ResolveScreenPoint(req, "from.", out from, out fromNode, out error)) { req.Fail(error, 404); return; }
        if (!ResolveScreenPoint(req, "to.", out to, out toNode, out error)) { req.Fail(error, 404); return; }
        PlayerAction click = RebirthGameBridgeInput.Find(req.QueryString("button", "left") == "right" ? "gui.RightClick" : "gui.LeftClick");
        RebirthGameBridgePump.Instance.Run(DragRoutine(req, from, to, fromNode, toNode, click));
    }

    private static IEnumerator DragRoutine(BridgeRequest req, Vector2 from, Vector2 to, JObject fromNode, JObject toNode, PlayerAction click)
    {
        RebirthGameBridgeInput.SetCursor(from);
        for (int i = 0; i < 3; i++) yield return null;
        RebirthGameBridgeInput.HoldSeconds(click, 0f);
        for (int i = 0; i < 3; i++) yield return null;
        const int steps = 12;
        for (int s = 1; s <= steps; s++)
        {
            // Removing an equipped pack can resize/reposition the destination grid.
            // Follow the selected cell as a player would, rather than swapping the item
            // that happens to move under its obsolete screen coordinates.
            Vector2 liveTo; JObject liveNode; string liveError;
            if (ResolveScreenPoint(req, "to.", out liveTo, out liveNode, out liveError)) to = liveTo;
            RebirthGameBridgeInput.SetCursor(Vector2.Lerp(from, to, s / (float)steps));
            yield return null;
        }
        for (int i = 0; i < 3; i++) yield return null;
        RebirthGameBridgeInput.Release(click);
        for (int i = 0; i < 4; i++) yield return null;

        // 7DTD item slots use click-to-pick / click-to-place: if the stack is still held on the cursor
        // after the release, click the destination to drop it there.
        bool placedByClick = false;
        if (HeldItem() != null)
        {
            Vector2 liveTo; JObject liveNode; string liveError;
            if (ResolveScreenPoint(req, "to.", out liveTo, out liveNode, out liveError)) to = liveTo;
            RebirthGameBridgeInput.SetCursor(to);
            yield return null;
            RebirthGameBridgeInput.HoldFrames(click, 2);
            while (RebirthGameBridgeInput.IsHeld(click)) { RebirthGameBridgeInput.SetCursor(to); yield return null; }
            for (int i = 0; i < 4; i++) yield return null;
            placedByClick = true;
        }
        req.Complete(new JObject
        {
            ["from"] = fromNode, ["to"] = toNode, ["placedByClick"] = placedByClick,
            ["stillHeld"] = HeldItem(), ["openWindows"] = OpenWindowIds()
        });
    }

    /// <summary>Name of the item currently held on the cursor, or null.</summary>
    public static string HeldItemName()
    {
        JToken t = HeldItem();
        return t is JObject ? (string)t["name"] : null;
    }

    /// <summary>The item stack currently held on the cursor (drag-and-drop window), or null.</summary>
    private static JToken HeldItem()
    {
        foreach (Node n in Collect("dragAndDrop", true))
            if (n.Controller is XUiC_ItemStack && n.Json["item"] is JObject) return n.Json["item"];
        return null;
    }

    public static bool TypeInput(string window, string id, string value, RebirthGameBridgeInput.OwnedInputScope scope)
    {
        if (scope == null || !scope.Admitted) return false;
        foreach (Node node in Collect(window, true))
        {
            if ((string)node.Json["id"] != id) continue;
            var input = node.Controller as XUiC_TextInput ?? FindAncestor<XUiC_TextInput>(node.Controller) ?? FindDescendant<XUiC_TextInput>(node.Controller);
            if (input == null || !ReferenceEquals(input.xui?.playerUI?.entityPlayer, scope.Player)) continue;
            var expectedView = input.ViewComponent; var expectedUi = input.xui;
            if (expectedView == null || !expectedView.IsVisible || !scope.Admitted) return false;
            if (!ReferenceEquals(input.ViewComponent, expectedView) || !expectedView.IsVisible || !ReferenceEquals(input.xui, expectedUi)
                || !ReferenceEquals(input.xui?.playerUI?.entityPlayer, scope.Player) || !scope.OwnershipCurrent) return false;
            input.Text = value;
            if (!scope.Admitted) return false;
            if (!ReferenceEquals(input.ViewComponent, expectedView) || !expectedView.IsVisible || !ReferenceEquals(input.xui, expectedUi)
                || !ReferenceEquals(input.xui?.playerUI?.entityPlayer, scope.Player) || !scope.OwnershipCurrent) return false;
            input.TriggerOnChangeHandler(false);
            return scope.OwnershipCurrent;
        }
        return false;
    }
    public static bool ScrollContextBackpack(float delta, RebirthGameBridgeInput.OwnedInputScope scope)
    {
        if (scope == null || !scope.Admitted) return false;
        foreach (Node node in Collect(null, true))
        {
            var slot = node.Controller as XUiC_RebirthContextSlot;
            if (slot == null || !slot.IsBackpack || !slot.IsPresented || !node.HasRect || !ReferenceEquals(slot.xui?.playerUI?.entityPlayer, scope.Player)) continue;
            var expectedGrid = slot.Grid; var expectedUi = slot.xui;
            if (expectedGrid == null || !expectedGrid.HasVisibleCenter(slot.SlotNumber) || !scope.Admitted) return false;
            if (!ReferenceEquals(slot.Grid, expectedGrid) || !ReferenceEquals(slot.xui, expectedUi) || !slot.IsBackpack || !slot.IsPresented
                || !ReferenceEquals(slot.xui?.playerUI?.entityPlayer, scope.Player) || !scope.OwnershipCurrent) return false;
            slot.Scrolled(delta);
            return scope.OwnershipCurrent;
        }
        return false;
    }
    /// <summary>Set the text of a text input (search boxes, counts, names): selector + &amp;value=...&amp;submit=0|1.</summary>
    private static void TypeText(BridgeRequest req)
    {
        string value = req.QueryString("value") ?? req.Body ?? "";
        XUiC_TextInput input = null;
        JObject nodeJson = null;
        foreach (Node n in Collect(req.QueryString("window"), true))
        {
            if (!Matches(n, req)) continue;
            input = n.Controller as XUiC_TextInput ?? FindAncestor<XUiC_TextInput>(n.Controller) ?? FindDescendant<XUiC_TextInput>(n.Controller);
            if (input != null) { nodeJson = n.Json; break; }
        }
        if (input == null) { req.Fail("no text input matches the selector (try /ui/find?ctrl=TextInput)", 404); return; }

        input.Text = value;
        input.TriggerOnChangeHandler(false);
        if (req.QueryBool("submit", false))
        {
            FieldInfo f = typeof(XUiC_TextInput).GetField("OnSubmitHandler", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            var handler = f != null ? f.GetValue(input) as Delegate : null;
            if (handler != null) handler.DynamicInvoke(input, value);
        }
        req.Complete(new JObject { ["node"] = nodeJson, ["text"] = input.Text });
    }

    private static void Scroll(BridgeRequest req)
    {
        float delta = req.QueryFloat("delta", -1f);
        foreach (Node n in Collect(req.QueryString("window"), true))
        {
            if (!Matches(n, req)) continue;
            n.Controller.Scrolled(delta);
            req.Complete(new JObject { ["scrolled"] = n.Json, ["delta"] = delta });
            return;
        }
        req.Fail("no UI node matches the selector", 404);
    }

    private static T FindAncestor<T>(XUiController c) where T : XUiController
    {
        for (XUiController p = c != null ? c.Parent : null; p != null; p = p.Parent)
            if (p is T) return (T)p;
        return null;
    }

    private static T FindDescendant<T>(XUiController c) where T : XUiController
    {
        if (c == null || c.Children == null) return null;
        foreach (XUiController k in c.Children)
        {
            if (k is T) return (T)k;
            T d = FindDescendant<T>(k);
            if (d != null) return d;
        }
        return null;
    }
}




