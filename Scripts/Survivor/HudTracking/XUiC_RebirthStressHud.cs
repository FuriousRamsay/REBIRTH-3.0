using System;
using UnityEngine;
using UnityEngine.Scripting;

[Preserve]
public sealed class XUiC_RebirthStressHud : XUiController
{
    private XUiV_Label value;
    private XUiV_Sprite stressIcon, placeIcon;
    private object root, player, world;
    private float nextDiscovery, lastStress = float.NaN;
    private int lastPlace = int.MinValue;
    private long textRevision = long.MinValue;
    private bool initialized;
    public override void Init() { base.Init(); Rebind(); }
    public override void OnOpen() { base.OnOpen(); Rebind(); }
    public override void Cleanup()
    { value = null; stressIcon = placeIcon = null; root = player = world = null; initialized = false; base.Cleanup(); }
    private void Rebind()
    {
        root = ViewComponent != null ? ViewComponent.UiTransform : null;
        value = GetChildById("stressValue")?.ViewComponent as XUiV_Label;
        stressIcon = GetChildById("stressIcon")?.ViewComponent as XUiV_Sprite;
        placeIcon = GetChildById("stressPlaceIcon")?.ViewComponent as XUiV_Sprite;
        nextDiscovery = Time.realtimeSinceStartup + 0.5f;
        initialized = false;
    }
    public override void Update(float dt)
    {
        base.Update(dt);
        var p = xui?.playerUI?.entityPlayer;
        if (ViewComponent == null) return;
        bool visible = p != null && !p.IsDead() && !xui.playerUI.windowManager.IsModalWindowOpen();
        if (ViewComponent.IsVisible != visible) ViewComponent.IsVisible = visible;
        if (!visible) return;
        if (!ReferenceEquals(root, ViewComponent.UiTransform)
            || ((value == null || stressIcon == null || placeIcon == null) && Time.realtimeSinceStartup >= nextDiscovery)) Rebind();
        if (!ReferenceEquals(player, p) || !ReferenceEquals(world, p.world))
        { player = p; world = p.world; initialized = false; }
        float stress = p.Buffs.GetCustomVar("rebirthStress");
        int place = (int)p.Buffs.GetCustomVar("rebirthStressLocation");
        long text = RebirthUiProjectionTextCache.Revision;
        bool stressChanged = !initialized || !stress.Equals(lastStress) || textRevision != text;
        bool placeChanged = !initialized || place != lastPlace || textRevision != text;
        if (stressChanged)
        {
            string number = stress.ToString("0"); // Preserve the native formatting/rounding contract.
            if (value != null && value.Text != number) value.Text = number;
            if (value != null && value.Color != Color.white) value.Color = Color.white;
            if (stressIcon != null)
            {
                if (stressIcon.SpriteName != RebirthStressPresentation.Icon) stressIcon.SpriteName = RebirthStressPresentation.Icon;
                Color color = RebirthStressPresentation.Color(stress);
                if (stressIcon.Color != color) stressIcon.Color = color;
            }
            string title = RebirthUiProjectionTextCache.L("rbStressTitle", "rbStressTitle");
            string tierKey = "rbStress" + RebirthStressPresentation.Tier(stress);
            string tooltip = title + ": " + number + " / 100 · " + RebirthUiProjectionTextCache.L(tierKey, tierKey);
            if (ViewComponent.ToolTip != tooltip) ViewComponent.ToolTip = tooltip;
        }
        if (placeChanged && placeIcon != null)
        {
            string icon = RebirthStressPresentation.PlaceIcon(place), key = RebirthStressPresentation.PlaceKey(place);
            if (placeIcon.SpriteName != icon) placeIcon.SpriteName = icon;
            string tooltip = RebirthUiProjectionTextCache.L(key, key);
            if (placeIcon.ToolTip != tooltip) placeIcon.ToolTip = tooltip;
        }
        lastStress = stress; lastPlace = place; textRevision = text; initialized = true;
    }
}
