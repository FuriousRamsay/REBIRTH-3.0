using System;
using UnityEngine;
using UnityEngine.Scripting;

#nullable disable

public enum RebirthVitalHudKind
{
    Health = 0,
    Stamina = 1,
    Water = 2,
    Food = 3,
    Energy = 4
}

/// <summary>
/// Default colors for the five compact player-vital rails.
/// Players may open the picker by clicking any vital bar whenever a UI cursor is available.
/// Apply changes the current-session HUD color and logs the exact selected value.
/// </summary>
public static class RebirthVitalHudColors
{
    private static readonly Color32[] Colors =
    {
        new Color32(186, 115, 115, 255),
        new Color32(211, 185, 113, 255),
        new Color32(115, 162, 194, 255),
        new Color32(143, 186, 128, 255),
        new Color32(174, 88, 238, 255)
    };

    public static int Revision { get; private set; }

    public static Color32 Get(RebirthVitalHudKind kind)
    {
        int i = Mathf.Clamp((int)kind, 0, Colors.Length - 1);
        return Colors[i];
    }

    public static void Set(RebirthVitalHudKind kind, Color32 color)
    {
        color.a = 255;
        int i = Mathf.Clamp((int)kind, 0, Colors.Length - 1);
        if (Colors[i].r == color.r && Colors[i].g == color.g && Colors[i].b == color.b && Colors[i].a == color.a) return;
        Colors[i] = color;
        Revision++;
    }

    public static string ToRgbaString(Color32 color)
    {
        return color.r + "," + color.g + "," + color.b + "," + color.a;
    }

    public static string ToHex(Color32 color)
    {
        return "#" + color.r.ToString("X2") + color.g.ToString("X2") + color.b.ToString("X2");
    }
}

[Preserve]
public sealed class XUiC_RebirthVitalColorPicker : XUiController
{
    private XUiC_ColorPicker colorPicker;
    private RebirthVitalHudKind kind;
    private Color32 initialColor;
    private Color32 selectedColor;
    private bool suppressPickerEvent;

    [XuiXmlBinding("rebirth_vital_picker_title")]
    public string PickerTitle { get { return kind.ToString().ToUpperInvariant() + " BAR FILL"; } }

    [XuiXmlBinding("rebirth_vital_picker_color")]
    public string PickerColor { get { return RebirthVitalHudColors.ToRgbaString(selectedColor); } }

    [XuiXmlBinding("rebirth_vital_picker_hex")]
    public string PickerHex { get { return RebirthVitalHudColors.ToHex(selectedColor); } }

    public override void Init()
    {
        base.Init();

        colorPicker = GetChildById("rebirthVitalColorPicker") as XUiC_ColorPicker;
        if (colorPicker != null)
            colorPicker.OnSelectedColorChanged += ColorPicker_OnSelectedColorChanged;

        XUiController applyButton = GetChildById("btnApplyRebirthVitalColor");
        if (applyButton != null)
            applyButton.OnPress += new XUiEvent_OnPressEventHandler(BtnApply_OnPressed);

        XUiController cancelButton = GetChildById("btnCancelRebirthVitalColor");
        if (cancelButton != null)
            cancelButton.OnPress += new XUiEvent_OnPressEventHandler(BtnCancel_OnPressed);
    }

    public override void OnOpen()
    {
        base.OnOpen();

        initialColor = RebirthVitalHudColors.Get(kind);
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
        if (suppressPickerEvent)
            return;

        selectedColor = (Color32)color;
        selectedColor.a = 255;
        IsDirty = true;
        RefreshBindings();
    }

    public void BtnApply_OnPressed(XUiController sender, int mouseButton)
    {
        RebirthVitalHudColors.Set(kind, selectedColor);
        { if (RebirthLogSettings.AutomaticLoggingDefault) Log.Out("[REBIRTH HUD] vital-fill-color selected stat=" + kind
            + " rgba=" + RebirthVitalHudColors.ToRgbaString(selectedColor)
            + " hex=" + RebirthVitalHudColors.ToHex(selectedColor)); }

        xui.playerUI.windowManager.Close((GUIWindow)windowGroup);
    }

    public void BtnCancel_OnPressed(XUiController sender, int mouseButton)
    {
        selectedColor = initialColor;
        xui.playerUI.windowManager.Close((GUIWindow)windowGroup);
    }

    public override void Update(float dt)
    {
        base.Update(dt);
        if (XUiUtils.HotkeysAllowedFor(viewComponent))
        {
            if (xui.playerUI.playerInput.PermanentActions.Cancel.WasReleased)
                BtnCancel_OnPressed(this, -1);
            if (xui.playerUI.playerInput.GUIActions.Apply.WasReleased)
                BtnApply_OnPressed(this, 0);
        }
        handleDirtyUpdateDefault();
    }

    public static void Open(XUi targetXui, RebirthVitalHudKind targetKind)
    {
        if (targetXui == null || targetXui.playerUI == null)
            return;

        XUiC_RebirthVitalColorPicker controller = targetXui.GetChildByType<XUiC_RebirthVitalColorPicker>();
        if (controller == null)
        {
            Log.Error("[REBIRTH HUD] vital color picker controller was not found.");
            return;
        }

        controller.kind = targetKind;
        controller.initialColor = RebirthVitalHudColors.Get(targetKind);
        controller.selectedColor = controller.initialColor;
        controller.IsDirty = true;
        controller.RefreshBindings();
        targetXui.playerUI.windowManager.Open((GUIWindow)controller.windowGroup, false);
    }
}
