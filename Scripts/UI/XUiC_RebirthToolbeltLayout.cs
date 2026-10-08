using System;
using UnityEngine;
using UnityEngine.Scripting;

// Native Toolbelt owns all inventory behavior; only slot positions change.
[Preserve]
public class XUiC_RebirthToolbeltLayout : XUiController
{
    public const int Pitch = 54, BuckleWidth = 234, DesignWidth = 1206;
    private static readonly RebirthVitalHudKind[] Kinds = {
        RebirthVitalHudKind.Health, RebirthVitalHudKind.Stamina, RebirthVitalHudKind.Food, RebirthVitalHudKind.Water };
    private readonly XUiController[] buttons = new XUiController[4];
    private readonly XUiV_FilledSprite[] fills = new XUiV_FilledSprite[4];
    private XUiController cachedRoot, firstInventory, secondInventory, firstClear, secondClear;
    private XUiC_Toolbelt belt;
    private Transform rootTransform;
    private object lastSlots;
    private int lastCount = -1, lastColorRevision = -1;
    private float lastScale = -1f, next;

    public static int SlotX(int index, int count)
    {
        int left = count / 2;
        return index < left ? 486 - (left - index) * Pitch : 720 + (index - left) * Pitch;
    }

    public override void Init() { base.Init(); ResetCache(); Apply(); }
    public override void OnOpen() { base.OnOpen(); lastCount = -1; Apply(); }
    public override void Update(float dt)
    {
        base.Update(dt);
        XUiC_RebirthVersionLabel.RefreshVersion();
        if (Time.realtimeSinceStartup < next) return;
        next = Time.realtimeSinceStartup + 0.1f;
        Apply();
    }

    private void ResetCache()
    {
        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] != null) buttons[i].OnPress -= OnVitalColorPressed;
            buttons[i] = null;
            fills[i] = null;
        }
        cachedRoot = null;
        rootTransform = null;
        belt = null;
        firstInventory = secondInventory = firstClear = secondClear = null;
        lastSlots = null;
        lastCount = lastColorRevision = -1;
        lastScale = -1f;
    }

    private void Apply()
    {
        XUiController root = Parent;
        var player = xui?.playerUI?.entityPlayer;
        if (root?.ViewComponent == null || player?.inventory == null) return;
        if (!ReferenceEquals(root, cachedRoot) || root.ViewComponent.UiTransform != rootTransform)
        {
            ResetCache();
            cachedRoot = root;
            rootTransform = root.ViewComponent.UiTransform;
        }
        if (belt == null || belt.ViewComponent?.UiTransform == null)
        {
            belt = root.GetChildById("toolbelt") as XUiC_Toolbelt;
            lastCount = -1;
        }
        if (belt == null) return;
        if (firstInventory?.ViewComponent?.UiTransform == null) firstInventory = belt.GetChildById("inventory");
        if (secondInventory?.ViewComponent?.UiTransform == null) secondInventory = belt.GetChildById("inventory2");
        if (firstClear?.ViewComponent?.UiTransform == null) firstClear = belt.GetChildById("btnClearInventory1");
        if (secondClear?.ViewComponent?.UiTransform == null) secondClear = belt.GetChildById("btnClearInventory2");
        int count = RebirthToolbeltCapacity.GetOwnedSlotCount(player, player.inventory.Length);
        var slots = belt.GetItemStackControllers();
        float scale = Mathf.Min(1f, Mathf.Max(1, xui.GetXUiScreenSize().x - 120) / (float)DesignWidth);
        // Reading the native pool also detects pool replacement at unchanged slot capacity.
        bool slotLayoutChanged = lastCount != count || lastScale != scale || !ReferenceEquals(lastSlots, slots);
        if (!slotLayoutChanged && slots != null) for (int i = 0; i < slots.Length; i++)
        {
            var view = slots[i]?.ViewComponent;
            if (view == null) continue;
            if (view.IsVisible != (i < count) || view.Position.x != SlotX(i, count) || view.Position.y != -4 ||
                (view.UiTransform != null && view.UiTransform.localScale != new Vector3(54f / 62f, 54f / 62f, 1f)))
            { slotLayoutChanged = true; break; }
        }
        if (slotLayoutChanged)
        {
            if (slots != null) for (int i = 0; i < slots.Length; i++)
            {
                var view = slots[i]?.ViewComponent;
                if (view == null) continue;
                Vector3 slotScale = new Vector3(54f / 62f, 54f / 62f, 1);
                if (view.UiTransform != null && view.UiTransform.localScale != slotScale) view.UiTransform.localScale = slotScale;
                if (view.IsVisible != (i < count)) view.IsVisible = i < count;
                Vector2i position = new Vector2i(SlotX(i, count), -4);
                if (view.Position != position) view.Position = position;
            }
            if (rootTransform != null) rootTransform.localScale = new Vector3(scale, scale, 1);
            SetPosition(root, -Mathf.RoundToInt(DesignWidth * scale / 2f), Mathf.RoundToInt(70 * scale));
            lastSlots = slots;
            lastCount = count;
            lastScale = scale;
        }
        if (rootTransform != null && rootTransform.localScale != new Vector3(scale, scale, 1f)) rootTransform.localScale = new Vector3(scale, scale, 1f);
        SetPosition(root, -Mathf.RoundToInt(DesignWidth * scale / 2f), Mathf.RoundToInt(70 * scale));
        // Guarded setters preserve origin/late-child behavior without rediscovery or needless writes.
        SetPosition(firstInventory, 0, 0);
        SetPosition(secondInventory, 0, 0);
        if (secondInventory?.ViewComponent != null && !secondInventory.ViewComponent.IsVisible) secondInventory.ViewComponent.IsVisible = true;
        SetPosition(firstClear, SlotX(count - 1, count) + 80, -31);
        SetPosition(secondClear, SlotX(count - 1, count) + 116, -31);
        bool colorsChanged = lastColorRevision != RebirthVitalHudColors.Revision;
        for (int i = 0; i < Kinds.Length; i++)
        {
            bool resolved = false;
            if (fills[i]?.UiTransform == null)
            {
                fills[i] = root.GetChildById("rebirthVital" + Kinds[i])?.GetChildById("rebirthVitalFill")?.ViewComponent as XUiV_FilledSprite;
                resolved = fills[i] != null;
            }
            if ((colorsChanged || resolved) && fills[i] != null) fills[i].Color = RebirthVitalHudColors.Get(Kinds[i]);
            if (buttons[i]?.ViewComponent?.UiTransform == null)
            {
                var button = GetChildById("btnRebirthVital" + Kinds[i] + "Color");
                if (!ReferenceEquals(button, buttons[i]))
                {
                    if (buttons[i] != null) buttons[i].OnPress -= OnVitalColorPressed;
                    buttons[i] = button;
                    if (button != null) button.OnPress += OnVitalColorPressed;
                }
            }
        }
        lastColorRevision = RebirthVitalHudColors.Revision;
    }

    private void OnVitalColorPressed(XUiController sender, int mouseButton)
    {
        if (mouseButton != 0 && mouseButton != -1) return;
        for (int i = 0; i < buttons.Length; i++)
            if (ReferenceEquals(sender, buttons[i])) { XUiC_RebirthVitalColorPicker.Open(xui, Kinds[i]); return; }
    }

    private static void SetPosition(XUiController controller, int x, int y)
    {
        if (controller?.ViewComponent == null) return;
        Vector2i position = new Vector2i(x, y);
        if (controller.ViewComponent.Position != position) controller.ViewComponent.Position = position;
    }
}
