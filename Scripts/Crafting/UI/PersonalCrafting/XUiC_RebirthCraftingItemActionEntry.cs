using System;
using UnityEngine.Scripting;

#nullable disable

/// <summary>
/// Personal-Crafting presentation wrapper around the native ItemActionEntry.
/// Native ItemActionEntry writes keyboard shortcuts using the angle-bracket display style
/// (for example &lt;W&gt;) when a new action is assigned. The Rebirth surface intentionally
/// shows the same binding without brackets. Normalizing inside the entry itself means the
/// native assignment and the Rebirth presentation settle in the same update pass, before
/// the frame is rendered; no outer controller has to fight the value on later frames.
/// </summary>
[Preserve]
public sealed class XUiC_RebirthCraftingItemActionEntry : XUiC_ItemActionEntry
{
    private XUiC_RebirthScrapPreview scrapPreview;
    private bool scrapHovered;
    private float scrapHoverStarted;
    private bool scrapPreviewShown;
    private string lastShortcut;
    public override void Init()
    {
        base.Init();
        scrapPreview = GetParentByType<XUiC_RebirthCraftingItemContext>()?.GetChildByType<XUiC_RebirthScrapPreview>();
        GetChildById("background").OnHover += ScrapHover;
    }

    private void ScrapHover(XUiController sender, bool over)
    {
        if (scrapPreview == null) return;
        if (!over && scrapHovered) scrapPreview.Hide();
        scrapHovered = over && ItemActionEntry is ItemActionEntryScrap;
        scrapPreviewShown = false;
        scrapHoverStarted = UnityEngine.Time.realtimeSinceStartup;
    }
    public override void Update(float dt)
    {
        // Parent XUiC_ItemActionList assigns ItemActionEntry before it updates its children.
        // Normalize immediately, then let all native hover/input/action behavior run unchanged.
        NormalizeShortcutText();
        base.Update(dt);
        // Native hover/press replaces the authored sprite with menu_empty, whose
        // border is not the complete REBIRTH outline. Keep the sliced outline.
        if (background != null && background.SpriteName != "menu_empty2px" && !isOver)
            background.SpriteName = "menu_empty2px";
        // Input-style/binding work inside the native update may have touched presentation state.
        // This is change-aware and therefore a no-op unless the text actually changed.
        NormalizeShortcutText();
        if (scrapHovered && !(ItemActionEntry is ItemActionEntryScrap)) { scrapHovered = false; scrapPreview?.Hide(); }
        if (scrapHovered && !scrapPreviewShown && UnityEngine.Time.realtimeSinceStartup - scrapHoverStarted >= 1f) {
            scrapPreview?.Show((ItemActionEntryScrap)ItemActionEntry);
            scrapPreviewShown = true;
        }
    }

    public override void OnClose()
    {
        scrapHovered = false;
        scrapPreviewShown = false;
        scrapPreview?.Hide();
        base.OnClose();
    }

    private void NormalizeShortcutText()
    {
        XUiV_Label keyboard = keyboardButton;
        if (keyboard == null)
            return;

        string current = keyboard.Text ?? string.Empty;
        if (current == lastShortcut) return;
        string normalized = NormalizeShortcut(current);
        if (!string.Equals(current, normalized, StringComparison.Ordinal))
            keyboard.SetTextImmediately(normalized);
        lastShortcut = normalized;
    }

    private static string NormalizeShortcut(string value)
    {
        string text = (value ?? string.Empty).Trim().ToUpperInvariant();
        if (text.Length >= 2 && text[0] == '<' && text[text.Length - 1] == '>')
            text = text.Substring(1, text.Length - 2);
        return text;
    }
}
