[UnityEngine.Scripting.Preserve]
public sealed class XUiC_RebirthGameSenseOption : XUiController
{
    private XUiC_ComboBoxList<string> selector;
    private bool refreshing;

    public override void Init()
    {
        base.Init();
        selector = GetChildById("gameSenseSelector") as XUiC_ComboBoxList<string>;
        if (selector != null) selector.OnValueChanged += OnValueChanged;
    }

    public override void OnOpen()
    {
        base.OnOpen();
        if (selector == null) return;
        refreshing = true;
        try
        {
            selector.Elements.Clear();
            selector.Elements.Add(Localization.Get("xuiRebirthMinimapOff"));
            selector.Elements.Add(Localization.Get("xuiRebirthMinimapOn"));
            selector.SelectedIndex = RebirthGameSenseOption.Enabled ? 1 : 0;
        }
        finally { refreshing = false; }
    }

    private void OnValueChanged(XUiController sender, string oldValue, string newValue)
    {
        if (!refreshing && selector != null)
            RebirthGameSenseOption.SetEnabled(selector.SelectedIndex == 1);
    }
}
