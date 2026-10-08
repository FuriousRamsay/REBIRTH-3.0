using System.Collections.Generic;
using UnityEngine;

// Shared ownership for temporary HUD suppression. Overlapping Rebirth windows take one
// original snapshot per XUi and restoration happens only when the final owner releases it.
public sealed class RebirthWindowHudScope
{
    private sealed class Ownership
    {
        public int RefCount;
        public readonly Dictionary<XUiController, bool[]> States = new Dictionary<XUiController, bool[]>();
        public readonly Dictionary<Transform, Vector3> Scales = new Dictionary<Transform, Vector3>();
        public XUiController Toolbelt;
        public XUiController[] Targets;
    }

    private static readonly Dictionary<XUi, Ownership> Owners = new Dictionary<XUi, Ownership>();
    private static readonly string[] Ids = { "rebirthTrackedProgressionHud", "HUDLeftStatBars", "HUDRightStatBars", "windowEntering", "windowLocation", "windowQuestTracker", "windowRecipeTracker", "windowGroupBars", "rebirthPartyCompanionHud", "rebirthHeatMapHud", "rebirthStudyHud", "rebirthOreSenseHud", "windowCompass" };

    private XUi ownedUi;
    private bool acquired;

    public void Maintain(XUi ui)
    {
        if (ui == null) return;
        if (acquired && ownedUi != ui) Restore();

        Ownership ownership;
        if (!Owners.TryGetValue(ui, out ownership))
        {
            ownership = new Ownership();
            Owners.Add(ui, ownership);
        }
        if (!acquired)
        {
            acquired = true;
            ownedUi = ui;
            ownership.RefCount++;
        }

        XUiController toolbelt = ui.FindWindowGroupByName("toolbelt");
        // These authored HUD descendants are stable while a window owns the scope.
        // Keep enforcing suppression every frame, without thirteen recursive tree walks.
        // A rebuilt toolbelt gets a new lookup; final release discards the whole cache.
        if (ownership.Targets == null || !ReferenceEquals(ownership.Toolbelt, toolbelt))
        {
            ownership.Toolbelt = toolbelt;
            ownership.Targets = new XUiController[Ids.Length];
            for (int i = 0; i < Ids.Length; i++)
                ownership.Targets[i] = toolbelt?.GetChildById(Ids[i]);
        }
        for (int i = 0; i < ownership.Targets.Length; i++) Hide(ownership, ownership.Targets[i]);
        Hide(ownership, ui.BuffPopoutList);
    }

    private static void Hide(Ownership ownership, XUiController controller)
    {
        XUiView view = controller != null ? controller.ViewComponent : null;
        if (view == null) return;
        if (!ownership.States.ContainsKey(controller)) ownership.States.Add(controller, new[] { view.IsVisible, view.Enabled });
        Transform transform = view.UiTransform;
        if (transform == null) return;
        if (!ownership.Scales.ContainsKey(transform)) ownership.Scales.Add(transform, transform.localScale);
        if (transform.localScale != Vector3.zero) transform.localScale = Vector3.zero;
    }

    public void Restore()
    {
        if (!acquired) return;
        XUi ui = ownedUi;
        acquired = false;
        ownedUi = null;

        Ownership ownership;
        if (ui == null || !Owners.TryGetValue(ui, out ownership)) return;
        ownership.RefCount = Mathf.Max(0, ownership.RefCount - 1);
        if (ownership.RefCount != 0) return;

        foreach (KeyValuePair<Transform, Vector3> pair in ownership.Scales)
            if (pair.Key != null) pair.Key.localScale = pair.Value;
        foreach (KeyValuePair<XUiController, bool[]> pair in ownership.States)
        {
            if (pair.Key == null || pair.Key.ViewComponent == null) continue;
            pair.Key.ViewComponent.IsVisible = pair.Value[0];
            pair.Key.ViewComponent.Enabled = pair.Value[1];
        }
        Owners.Remove(ui);
    }
}
