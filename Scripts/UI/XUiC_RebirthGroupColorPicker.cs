using System;
using UnityEngine;
using UnityEngine.Scripting;

#nullable disable

[Preserve]
public sealed class XUiC_RebirthGroupColorPicker : XUiController
{
    private XUiC_ColorPicker colorPicker;
    private Color32 initialColor;
    private Color32 selectedColor;
    private Action<Color32> onApply;
    private bool suppressPickerEvent;

    [XuiXmlBinding("rebirth_picker_color")]
    public string PickerColor { get { return selectedColor.r + "," + selectedColor.g + "," + selectedColor.b + ",255"; } }

    [XuiXmlBinding("rebirth_picker_hex")]
    public string PickerHex { get { return "#" + selectedColor.r.ToString("X2") + selectedColor.g.ToString("X2") + selectedColor.b.ToString("X2"); } }

    public override void Init()
    {
        base.Init();
        colorPicker = GetChildById("targetNameColorPicker") as XUiC_ColorPicker;
        if (colorPicker != null)
            colorPicker.OnSelectedColorChanged += ColorPicker_OnSelectedColorChanged;

        // Wire these modal buttons directly. 3.1 exposes XUiController.OnPress and
        // this avoids relying on generated XuiBindEvent hookup for a dynamically
        // opened window group. This is the same native event path used by base 3.1 UI.
        XUiController applyButton = GetChildById("btnApplyTargetNameColor");
        if (applyButton != null)
            applyButton.OnPress += new XUiEvent_OnPressEventHandler(BtnApply_OnPressed);

        XUiController cancelButton = GetChildById("btnCancelTargetNameColor");
        if (cancelButton != null)
            cancelButton.OnPress += new XUiEvent_OnPressEventHandler(BtnCancel_OnPressed);
    }

    public override void OnOpen()
    {
        base.OnOpen();
        selectedColor = initialColor;
        if (colorPicker != null)
        {
            suppressPickerEvent = true;
            colorPicker.SelectedColor = (Color)selectedColor;
            suppressPickerEvent = false;
        }
        IsDirty = true;
        RefreshBindings();
    }

    private void ColorPicker_OnSelectedColorChanged(Color color)
    {
        if (suppressPickerEvent) return;
        selectedColor = (Color32)color;
        selectedColor.a = 255;
        IsDirty = true;
        RefreshBindings();
    }

    public void BtnApply_OnPressed(XUiController sender, int mouseButton)
    {
        Action<Color32> callback = onApply;
        onApply = null;
        xui.playerUI.windowManager.Close((GUIWindow)windowGroup);
        if (callback != null) callback(selectedColor);
    }

    public void BtnCancel_OnPressed(XUiController sender, int mouseButton)
    {
        onApply = null;
        xui.playerUI.windowManager.Close((GUIWindow)windowGroup);
    }

    public override void Update(float dt)
    {
        base.Update(dt);
        if (XUiUtils.HotkeysAllowedFor(viewComponent))
        {
            if (xui.playerUI.playerInput.PermanentActions.Cancel.WasReleased) BtnCancel_OnPressed(this, -1);
            if (xui.playerUI.playerInput.GUIActions.Apply.WasReleased) BtnApply_OnPressed(this, 0);
        }
        handleDirtyUpdateDefault();
    }

    public static void Open(XUi targetXui, Color32 color, Action<Color32> applied)
    {
        XUiC_RebirthGroupColorPicker controller = targetXui.GetChildByType<XUiC_RebirthGroupColorPicker>();
        if (controller == null)
        {
            Log.Error("[RebirthSandbox] group color picker controller was not found.");
            return;
        }
        color.a = 255;
        controller.initialColor = color;
        controller.selectedColor = color;
        controller.onApply = applied;
        targetXui.playerUI.windowManager.Open((GUIWindow)controller.windowGroup, false);
    }
}
