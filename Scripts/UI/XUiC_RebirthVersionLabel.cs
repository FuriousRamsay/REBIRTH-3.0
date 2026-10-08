using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting;

[Preserve]
public sealed class XUiC_RebirthVersionLabel : XUiController
{
    private sealed class LabelOwner
    {
        internal NGUIWindowManager Manager;
        internal UILabel Label;
        internal GameObject Window;
    }

    private static float nextRetry;
    private static readonly List<LabelOwner> labels = new List<LabelOwner>();
    private static LocalPlayerUI lastPlayerUi;
    private static World lastWorld;
    private static string versionText;

    public override void Init()
    {
        base.Init();
        InvalidateDiscovery();
    }

    public override void OnOpen()
    {
        base.OnOpen();
        InvalidateDiscovery();
        RefreshVersion();
    }

    private static void InvalidateDiscovery()
    {
        labels.Clear();
        versionText = null;
        nextRetry = 0f;
    }

    public override void Update(float dt) { base.Update(dt); RefreshVersion(); }

    public static void RefreshVersion()
    {
        LocalPlayerUI ui = LocalPlayerUI.GetUIForPrimaryPlayer();
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (!ReferenceEquals(lastPlayerUi, ui) || !ReferenceEquals(lastWorld, world))
        {
            lastPlayerUi = ui;
            lastWorld = world;
            InvalidateDiscovery();
        }

        bool visible = !(ui?.windowManager?.IsModalWindowOpen() ?? false) &&
            !RebirthPersonalCraftingHudSuppressionInstaller.ShouldSuppress;
        bool invalidated = false;
        for (int i = labels.Count - 1; i >= 0; i--)
        {
            LabelOwner entry = labels[i];
            // Check the actual window-map association, not just a still-live old label.
            if (entry.Manager == null || entry.Label == null || entry.Manager.windowMap == null ||
                !entry.Manager.windowMap.TryGetValue(EnumNGUIWindow.Version, out var current) ||
                current == null || current.transform.gameObject != entry.Window)
            {
                labels.RemoveAt(i);
                invalidated = true;
                continue;
            }
            if (entry.Label.enabled != visible) entry.Label.enabled = visible;
        }
        if (invalidated) nextRetry = 0f;
        // Successful discovery is retained. Global discovery is only a lifecycle/missing-view retry.
        if (!invalidated && labels.Count != 0) return;
        if (Time.unscaledTime < nextRetry) return;
        nextRetry = Time.unscaledTime + 1f;
        string modVersion = ModManager.GetMod("zzz_REBIRTH__3_0")?.VersionString;
        if (string.IsNullOrEmpty(modVersion)) return;
        var version = Constants.cVersionInformation;
        versionText = "[BC84E7]REBIRTH[-]  " + modVersion + " / 7DTD v " + version.Major + "." +
            version.Minor / 10 + "." + version.Minor % 10 + " (b" + version.Build + ")";
        labels.Clear();
        foreach (var manager in Resources.FindObjectsOfTypeAll<NGUIWindowManager>())
        {
            if (manager.windowMap == null || !manager.windowMap.TryGetValue(EnumNGUIWindow.Version, out var window) || window == null) continue;
            var label = window.GetComponent<UILabel>();
            if (label == null) continue;
            labels.Add(new LabelOwner { Manager = manager, Label = label, Window = window.transform.gameObject });
            label.enabled = visible;
            label.supportEncoding = true;
            label.fontSize = 18;
            label.overflowMethod = UILabel.Overflow.ResizeFreely;
            if (label.text != versionText) label.text = versionText;
        }
    }
}
