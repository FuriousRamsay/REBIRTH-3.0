using System;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;

// Native notifications own contents, timer text, removal and clicks; this owns compact geometry.
public static class RebirthCompactBuffLayout
{
    private static bool installed;
    private sealed class ListState
    {
        public int Frame = -1;
        public object Root, World, Window;
        public XUiController Sneak, Stress;
        public float NextDiscovery;
        public bool Owned;
        public Vector2i BeforePosition;
        public Vector2 BeforeSpriteSize, BeforeBarSize;
        public bool BeforeVisible, WrittenVisible;
        public readonly CardPosition SneakPosition = new CardPosition(), StressPosition = new CardPosition();
        public ListState() { }
    }
    private sealed class CardPosition
    {
        private XUiView view;
        private Vector2i before, written;
        public void Apply(XUiController card, float bottom)
        {
            XUiView next = card?.ViewComponent;
            if (!ReferenceEquals(view, next)) { Restore(); view = next; if (view != null) before = view.Position; }
            if (view == null) return;
            written = new Vector2i(14, 20 + Mathf.CeilToInt(bottom) + 26);
            if (view.Position != written) view.Position = written;
        }
        public void Restore()
        {
            if (view != null && view.Position == written) view.Position = before;
            view = null;
        }
    }
    private sealed class WidgetGeometry
    {
        private UIWidget widget;
        private Vector3 before, written;
        private int beforeWidth, beforeHeight, width, height;
        public void Apply(UIWidget next, Vector3 position, int nextWidth, int nextHeight)
        {
            if (widget != next)
            {
                Restore(); widget = next;
                if (widget != null) { before = widget.transform.localPosition; beforeWidth = widget.width; beforeHeight = widget.height; }
            }
            if (widget == null) return;
            written = position; width = nextWidth; height = nextHeight;
            if (widget.transform.localPosition != position) widget.transform.localPosition = position;
            if (widget.width != width || widget.height != height) widget.SetDimensions(width, height);
        }
        public void Restore()
        {
            if (widget != null)
            {
                if (widget.transform.localPosition == written) widget.transform.localPosition = before;
                if (widget.width == width && widget.height == height) widget.SetDimensions(beforeWidth, beforeHeight);
            }
            widget = null;
        }
    }
    private sealed class EntryState
    {
        public Transform Root;
        public TweenPosition Tween;
        public UISprite Background;
        public UILabel Label;
        public BoxCollider Collider;
        public int ChildCount = -1;
        public float NextDiscovery;
        public bool Captured, BeforeTween, BeforeActive, AppliedActive, OwnsActive;
        public Vector3 BeforePosition, AppliedPosition, BeforeColliderSize, BeforeColliderCenter;
        public string BeforeBackgroundName;
        public Color BeforeBackgroundColor, BeforeLabelColor;
        public int BeforeBackgroundWidth, BeforeBackgroundHeight, BeforeFontSize, BeforeLabelWidth, BeforeLabelHeight;
        public NGUIText.Alignment BeforeAlignment;
        public Vector3 BeforeLabelPosition;
        public readonly WidgetGeometry IconGeometry = new WidgetGeometry(), BarGeometry = new WidgetGeometry();
        private UISprite capturedBackground;
        private UILabel capturedLabel;
        private BoxCollider capturedCollider;
        private TweenPosition capturedTween;
        public EntryState() { }
        public void Discover(GameObject item)
        {
            Transform transform = item.transform;
            bool structure = Root != transform || ChildCount != transform.childCount;
            if (Captured && !structure && Time.realtimeSinceStartup < NextDiscovery) return;
            // Stable positive references require no hierarchy lookup; missing parts retry boundedly.
            if (Captured && !structure && Background != null && Label != null && Collider != null) return;
            Root = transform; ChildCount = transform.childCount; NextDiscovery = Time.realtimeSinceStartup + 1f;
            Tween = item.GetComponent<TweenPosition>();
            Background = transform.Find("Background")?.GetComponent<UISprite>();
            Label = transform.Find("TextContent")?.GetComponent<UILabel>();
            Collider = item.GetComponent<BoxCollider>();
            if (!Captured) { BeforePosition = transform.localPosition; BeforeActive = item.activeSelf; }
            if (Tween != capturedTween || !Captured) { capturedTween = Tween; BeforeTween = Tween != null && Tween.enabled; }
            if (Background != null && (Background != capturedBackground || !Captured)) { capturedBackground = Background; BeforeBackgroundName = Background.spriteName; BeforeBackgroundColor = Background.color; BeforeBackgroundWidth = Background.width; BeforeBackgroundHeight = Background.height; }
            if (Label != null && (Label != capturedLabel || !Captured)) { capturedLabel = Label; BeforeLabelColor = Label.color; BeforeAlignment = Label.alignment; BeforeFontSize = Label.fontSize; BeforeLabelWidth = Label.width; BeforeLabelHeight = Label.height; BeforeLabelPosition = Label.transform.localPosition; }
            if (Collider != null && (Collider != capturedCollider || !Captured)) { capturedCollider = Collider; BeforeColliderCenter = Collider.center; BeforeColliderSize = Collider.size; }
            Captured = true;
        }
        public void Visibility(GameObject item, bool visible)
        {
            if (item.activeSelf == visible) return;
            if (!OwnsActive) BeforeActive = item.activeSelf;
            item.SetActive(visible); AppliedActive = visible; OwnsActive = true;
        }
        public void Restore(GameObject item)
        {
            if (!Captured) return;
            if (OwnsActive && item.activeSelf == AppliedActive) item.SetActive(BeforeActive);
            if (Root != null && Root.localPosition == AppliedPosition) Root.localPosition = BeforePosition;
            if (Tween != null && !Tween.enabled) Tween.enabled = BeforeTween;
            if (Background != null)
            {
                if (Background.spriteName == "menu_empty") Background.spriteName = BeforeBackgroundName;
                if (Background.width == 118 && Background.height == 26) Background.SetDimensions(BeforeBackgroundWidth, BeforeBackgroundHeight);
                if (Background.color == (Color)new Color32(18,18,18,205)) Background.color = BeforeBackgroundColor;
            }
            if (Label != null)
            {
                if (Label.color == Color.white) Label.color = BeforeLabelColor;
                if (Label.alignment == NGUIText.Alignment.Right) Label.alignment = BeforeAlignment;
                if (Label.fontSize == 16) Label.fontSize = BeforeFontSize;
                if (Label.width == 82) Label.width = BeforeLabelWidth;
                if (Label.height == 22) Label.height = BeforeLabelHeight;
                if (Label.transform.localPosition == new Vector3(-30,0,0)) Label.transform.localPosition = BeforeLabelPosition;
            }
            if (Collider != null)
            {
                if (Collider.center == Vector3.zero) Collider.center = BeforeColliderCenter;
                if (Collider.size == new Vector3(118,26,1)) Collider.size = BeforeColliderSize;
            }
            IconGeometry.Restore(); BarGeometry.Restore();
            Captured = OwnsActive = false;
        }
    }
    private static readonly ConditionalWeakTable<XUiC_BuffPopoutList, ListState> Lists = new ConditionalWeakTable<XUiC_BuffPopoutList, ListState>();
    private static readonly ConditionalWeakTable<GameObject, EntryState> Entries = new ConditionalWeakTable<GameObject, EntryState>();
    public static void Install()
    {
        if (installed) return;
        var harmony = new Harmony("rebirth.compact-buff-layout");
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthBuffEntriesLayoutPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthBuffUpdateLayoutPatch));
        installed = true;
    }
    public static bool Active => RebirthSurvivorMode.ConfiguredMode == RebirthPlayerProgressionMode.Rebirth;
    // Null Buff is a native non-buff notification, not an invalid slot; preserve that content.
    public static bool IsActiveNotification(BuffValue buff)
    { return buff == null || (!buff.Paused && !buff.Finished && !buff.Invalid && !buff.Remove); }
    public static int CountActive(XUiC_BuffPopoutList list)
    {
        if (list == null || list.items == null) return 0;
        int count = 0;
        foreach (var entry in list.items) if (entry.Item != null && IsActiveNotification(entry.Notification.Buff)) count++;
        return count;
    }
    // Only the native Update postfix calls this. The envelope controller never performs a
    // second layout pass; updateEntries only suppresses competing native vertical tweens.
    public static void Apply(XUiC_BuffPopoutList list)
    {
        if (list == null || list.ViewComponent == null || list.items == null) return;
        ListState state = Lists.GetOrCreateValue(list);
        if (!Active)
        {
            if (!state.Owned) return;
            foreach (var data in list.items)
                if (data.Item != null) { EntryState entry; if (Entries.TryGetValue(data.Item, out entry)) entry.Restore(data.Item); }
            if (list.ViewComponent.Position.x == 14 && list.ViewComponent.Position.y == 20) list.ViewComponent.Position = state.BeforePosition;
            if (list.spriteSize == new Vector2(20,20)) list.spriteSize = state.BeforeSpriteSize;
            if (list.barSpriteSize == new Vector2(116,2)) list.barSpriteSize = state.BeforeBarSize;
            if (list.ViewComponent.IsVisible == state.WrittenVisible) list.ViewComponent.IsVisible = state.BeforeVisible;
            state.SneakPosition.Restore(); state.StressPosition.Restore();
            state.Owned = false; state.Frame = -1;
            return;
        }
        if (state.Frame == Time.frameCount) return;
        state.Frame = Time.frameCount;
        if (!state.Owned)
        {
            state.BeforePosition = list.ViewComponent.Position; state.BeforeVisible = list.ViewComponent.IsVisible;
            state.BeforeSpriteSize = list.spriteSize; state.BeforeBarSize = list.barSpriteSize; state.Owned = true;
        }
        bool hidden = RebirthPersonalCraftingHudSuppressionInstaller.ShouldSuppress || list.xui.playerUI.windowManager.IsModalWindowOpen();
        if (list.ViewComponent.IsVisible == hidden) list.ViewComponent.IsVisible = !hidden;
        state.WrittenVisible = !hidden;
        foreach (var data in list.items)
        {
            if (data.Item == null) continue;
            EntryState entry = Entries.GetOrCreateValue(data.Item); entry.Discover(data.Item);
            entry.Visibility(data.Item, !hidden && IsActiveNotification(data.Notification.Buff));
        }
        if (hidden) return;
        if (list.ViewComponent.Position.x != 14 || list.ViewComponent.Position.y != 20) list.ViewComponent.Position = new Vector2i(14,20);
        if (list.spriteSize != new Vector2(20,20)) list.spriteSize = new Vector2(20,20);
        if (list.barSpriteSize != new Vector2(116,2)) list.barSpriteSize = new Vector2(116,2);
        object root = list.xui.transform;
        object world = GameManager.Instance != null ? GameManager.Instance.World : null;
        var window = list.xui.FindWindowGroupByName("toolbelt"); // Direct registered-window lookup, not scene discovery.
        if (!ReferenceEquals(state.Root, root) || !ReferenceEquals(state.World, world) || !ReferenceEquals(state.Window, window)
            || ((state.Sneak == null || state.Stress == null) && Time.realtimeSinceStartup >= state.NextDiscovery))
        {
            state.Root = root; state.World = world; state.Window = window;
            state.Sneak = window?.GetChildById("rebirthSneakCard"); state.Stress = window?.GetChildById("rebirthStressCards");
            state.NextDiscovery = Time.realtimeSinceStartup + 1f;
        }
        float bottom = RebirthHudTrackingLayoutMetrics.RenderedLowerHudHeight;
        if (bottom > 0f) bottom += RebirthHudTrackingLayoutMetrics.TrackerBuffGap;
        state.SneakPosition.Apply(state.Sneak, bottom);
        if (list.xui.playerUI?.entityPlayer?.IsCrouching == true) bottom += 34f;
        state.StressPosition.Apply(state.Stress, bottom); bottom += 28f;
        int index = 0;
        foreach (var data in list.items)
        {
            if (data.Item == null || !IsActiveNotification(data.Notification.Buff)) continue;
            EntryState entry = Entries.GetOrCreateValue(data.Item);
            Transform transform = data.Item.transform;
            if (entry.Tween != null && entry.Tween.enabled) entry.Tween.enabled = false;
            Vector3 position = new Vector3(59 + index % 2 * 122, bottom + 13 + index / 2 * 28, transform.localPosition.z);
            if (transform.localPosition != position) transform.localPosition = position;
            entry.AppliedPosition = position;
            UISprite bg = entry.Background;
            if (bg != null)
            {
                if (bg.spriteName != "menu_empty") bg.spriteName = "menu_empty";
                if (bg.width != 118 || bg.height != 26) bg.SetDimensions(118,26);
                Color color = new Color32(18,18,18,205); if (bg.color != color) bg.color = color;
            }
            entry.IconGeometry.Apply(data.Sprite, new Vector3(-45,0,0), 20, 20);
            entry.BarGeometry.Apply(data.BarSprite, new Vector3(0,-12,0), 116, 2);
            UILabel label = entry.Label;
            if (label != null)
            {
                if (label.color != Color.white) label.color = Color.white;
                if (label.alignment != NGUIText.Alignment.Right) label.alignment = NGUIText.Alignment.Right;
                if (label.fontSize != 16) label.fontSize = 16;
                if (label.width != 82) label.width = 82;
                if (label.height != 22) label.height = 22;
                if (label.transform.localPosition != new Vector3(-30,0,0)) label.transform.localPosition = new Vector3(-30,0,0);
            }
            BoxCollider collider = entry.Collider;
            if (collider != null)
            {
                if (collider.center != Vector3.zero) collider.center = Vector3.zero;
                if (collider.size != new Vector3(118,26,1)) collider.size = new Vector3(118,26,1);
            }
            index++;
        }
    }
}
[HarmonyPatch(typeof(XUiC_BuffPopoutList), "updateEntries")]
internal static class RebirthBuffEntriesLayoutPatch
{
    [HarmonyPrefix] private static bool Prefix(XUiC_BuffPopoutList __instance)
    { return !RebirthCompactBuffLayout.Active; }
}
[HarmonyPatch(typeof(XUiC_BuffPopoutList), nameof(XUiC_BuffPopoutList.Update))]
internal static class RebirthBuffUpdateLayoutPatch
{
    [HarmonyPostfix] private static void Postfix(XUiC_BuffPopoutList __instance) => RebirthCompactBuffLayout.Apply(__instance);
}
