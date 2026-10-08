/// <summary>
/// Lightweight HUD binding controller for the client-local cruise-control state.
/// It reads the dispatcher state; it does not own input or vehicle movement logic.
/// </summary>
public sealed class RebirthCruiseControlHud : XUiController
{
    private const float RefreshInterval = 0.05f;
    private EntityPlayerLocal localPlayer;
    private float refreshAccumulator;
    private string stateText = string.Empty;
    private string stateColor = "242,236,167,128";
    private string statePosition = "0,-10000";

    public override void OnOpen()
    {
        base.OnOpen();
        localPlayer = xui == null || xui.playerUI == null ? null : xui.playerUI.entityPlayer;
        RefreshState();
        IsDirty = true;
        RefreshBindings();
    }

    public override void Update(float _dt)
    {
        refreshAccumulator += _dt;
        if (refreshAccumulator < RefreshInterval)
            return;

        refreshAccumulator = 0f;
        base.Update(_dt);
        RefreshState();
        RefreshBindings();
    }

    private void RefreshState()
    {
        int state = RebirthNativeControls.GetCruiseState(localPlayer);
        stateText = string.Empty;
        stateColor = "242,236,167,128";
        statePosition = "0,-10000";

        if (state == 1)
        {
            stateText = Localization.Get("ttCruiseNormal");
            stateColor = "242,236,167,128";
        }
        else if (state == 2)
        {
            stateText = Localization.Get("ttCruiseFast");
            stateColor = "86,145,78,128";
        }

        if (state > 0)
            statePosition = "0,50";
    }

    public override bool GetBindingValueInternal(ref string value, string bindingName)
    {
        switch (bindingName)
        {
            case "rebirth_cruise_text":
                value = stateText;
                return true;
            case "rebirth_cruise_color":
                value = stateColor;
                return true;
            case "rebirth_cruise_position":
                value = statePosition;
                return true;
            default:
                return base.GetBindingValueInternal(ref value, bindingName);
        }
    }
}
