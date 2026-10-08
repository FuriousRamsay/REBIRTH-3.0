using System;
using UnityEngine.Scripting;

#nullable disable

/// <summary>
/// Small native Character-window bridge. The native Character frame remains authoritative for
/// vanilla equipment/stats; this button opens the dedicated Survivor presentation window only
/// when the owning client has a committed REBIRTH character.
/// </summary>
[Preserve]
public sealed class XUiC_RebirthSurvivorCharacterLauncher : XUiController
{
    private XUiController button;
    private bool lastAvailable;
    private float refresh;

    public override void Init()
    {
        base.Init();
        button = GetChildById("btnRebirthSurvivorCharacter");
        if (button != null) button.OnPress += OnPressed;
        RefreshAvailability(true);
    }

    public override void Update(float dt)
    {
        base.Update(dt);
        refresh += Math.Max(0f, dt);
        if (refresh < 0.25f) return;
        refresh = 0f;
        RefreshAvailability(false);
    }

    private void RefreshAvailability(bool force)
    {
        RebirthSurvivorOwnerHeader state = RebirthSurvivorClientState.GetOwnerHeader();
        bool available = state.Available && state.RebirthModeEnabled && state.HasCharacter
            && state.CreationState == RebirthSurvivorOwnerCreationState.Ready;
        if (!force && available == lastAvailable) return;
        lastAvailable = available;
        if (ViewComponent != null) ViewComponent.IsVisible = available;
        if (button != null && button.ViewComponent != null) button.ViewComponent.IsVisible = available;
    }

    private static void OnPressed(XUiController sender, int mouseButton)
    {
        if (sender == null || sender.xui == null || sender.xui.playerUI == null || sender.xui.playerUI.windowManager == null) return;
        RebirthSurvivorOwnerHeader state = RebirthSurvivorClientState.GetOwnerHeader();
        if (!state.Available || !state.RebirthModeEnabled || !state.HasCharacter) return;
        sender.xui.playerUI.windowManager.Open(XUiC_RebirthSurvivorCharacter.WindowGroupId, true);
    }
}
