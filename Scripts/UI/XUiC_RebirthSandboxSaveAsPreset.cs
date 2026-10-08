using System;
using UnityEngine.Scripting;

#nullable disable

[Preserve]
public class XUiC_RebirthSandboxSaveAsPreset : XUiController
{
    [XuiBindComponent("presetName", true)]
    public readonly XUiC_TextInput txtPresetName;

    [XuiBindComponent("description", true)]
    public readonly XUiC_TextInput txtDescription;

    [XuiBindComponent("btnConfirm", true)]
    public readonly XUiC_Button btnConfirm;

    [XuiBindComponent("btnCancel", true)]
    public readonly XUiC_Button btnCancel;

    private string code;
    private string initialDescription;
    private bool nameValid;
    private Action<string> onConfirm;

    [XuiXmlBinding("name_valid")]
    public bool NameValid
    {
        get { return nameValid; }
        private set
        {
            if (nameValid == value)
                return;
            nameValid = value;
            IsDirty = true;
        }
    }

    public override void Init()
    {
        base.Init();
        txtPresetName.UIInput.onValidate = new UIInput.OnValidate(GameUtils.ValidateGameNameInput);
    }

    public override void OnOpen()
    {
        base.OnOpen();
        NameValid = false;
        txtPresetName.Text = string.Empty;
        txtDescription.Text = initialDescription ?? string.Empty;
        txtPresetName.SelectOrVirtualKeyboard(true);
    }

    public override void OnClose()
    {
        base.OnClose();
        initialDescription = string.Empty;
    }

    public override void Update(float dt)
    {
        base.Update(dt);
        if (XUiUtils.HotkeysAllowedFor(viewComponent))
        {
            if (xui.playerUI.playerInput.PermanentActions.Cancel.WasReleased)
                BtnCancel_OnPressed(this, -1);
            if (xui.playerUI.playerInput.GUIActions.Apply.WasReleased)
                BtnConfirm_OnPressed(this, 0);
        }
        handleDirtyUpdateDefault();
    }

    [XuiBindEvent("OnPress", "btnCancel")]
    public void BtnCancel_OnPressed(XUiController sender, int mouseButton)
    {
        xui.playerUI.windowManager.Close((GUIWindow)windowGroup);
        onConfirm = null;
    }

    [XuiBindEvent("OnPress", "btnConfirm")]
    public void BtnConfirm_OnPressed(XUiController sender, int mouseButton)
    {
        if (!NameValid)
            return;

        RebirthSandboxPreset preset = RebirthSandboxOptionManager.Current.SaveUserPreset(
            txtPresetName.Text.Trim(), txtDescription.Text, code);
        if (preset == null)
            return;

        xui.playerUI.windowManager.Close((GUIWindow)windowGroup);
        Action<string> callback = onConfirm;
        onConfirm = null;
        if (callback != null)
            callback(preset.Name);
    }

    [XuiBindEvent("OnSubmitHandler", "txtPresetName")]
    [XuiBindEvent("OnSubmitHandler", "txtDescription")]
    public void OnSubmit(XUiController sender, string text)
    {
        BtnConfirm_OnPressed(this, -1);
    }

    [XuiBindEvent("OnChangeHandler", "txtPresetName")]
    public void PresetName_OnChanged(XUiController sender, string text, bool changeFromCode)
    {
        string trimmed = (text ?? string.Empty).Trim();
        NameValid = trimmed.Length > 0
            && trimmed.IndexOf('.') < 0
            && RebirthSandboxOptionManager.Current.GetPreset(trimmed) == null;
    }

    public static void Open(XUi targetXui, string rebirthCode, string description, Action<string> confirmed)
    {
        XUiC_RebirthSandboxSaveAsPreset controller = targetXui.GetChildByType<XUiC_RebirthSandboxSaveAsPreset>();
        if (controller == null)
        {
            Log.Error("[RebirthSandbox] save-as-preset window controller was not found.");
            return;
        }

        controller.code = rebirthCode;
        controller.initialDescription = description ?? string.Empty;
        controller.onConfirm = confirmed;
        targetXui.playerUI.windowManager.Open((GUIWindow)controller.windowGroup, false);
    }
}
