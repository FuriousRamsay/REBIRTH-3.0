using System;
using UnityEngine;
using UnityEngine.Scripting;

#nullable disable

/// <summary>One reusable card in the 2x3 visible Requirements viewport.</summary>
[Preserve]
public sealed class XUiC_RebirthCraftingRequirementEntry : XUiController
{
    private XUiV_Sprite icon;
    private XUiV_Sprite frame;
    private XUiV_Label nameLabel;
    private XUiV_Label haveTitleLabel;
    private XUiV_Label haveValueLabel;
    private XUiV_Label needTitleLabel;
    private XUiV_Label needValueLabel;
    private RebirthCraftingRequirementProjectionService.Requirement requirement;

    public override void Init()
    {
        base.Init();
        icon = GetView<XUiV_Sprite>("rebirthCraftingRequirementIcon");
        frame = GetView<XUiV_Sprite>("rebirthCraftingRequirementFrame");
        nameLabel = GetView<XUiV_Label>("rebirthCraftingRequirementName");
        haveTitleLabel = GetView<XUiV_Label>("rebirthCraftingRequirementHaveLabel");
        haveValueLabel = GetView<XUiV_Label>("rebirthCraftingRequirementHaveValue");
        needTitleLabel = GetView<XUiV_Label>("rebirthCraftingRequirementNeedLabel");
        needValueLabel = GetView<XUiV_Label>("rebirthCraftingRequirementNeedValue");
        Clear();
    }

    public void Bind(RebirthCraftingRequirementProjectionService.Requirement value)
    {
        requirement = value;
        if (ViewComponent != null) ViewComponent.IsVisible = value != null;
        if (value == null) { Clear(); return; }

        if (icon != null)
        {
            icon.IsVisible = !string.IsNullOrEmpty(value.Icon);
            icon.SpriteName = value.Icon;
            icon.Color = value.IconTint;
        }
        if (nameLabel != null) nameLabel.Text = value.Name ?? string.Empty;
        if (haveTitleLabel != null)
        {
            haveTitleLabel.Text = Localize("xuiRebirthHave", "Have").ToUpperInvariant();
            haveTitleLabel.Color = new Color32(240, 240, 242, 255);
        }
        if (haveValueLabel != null)
        {
            haveValueLabel.Text = value.Have.ToString();
            haveValueLabel.Color = value.HasEnough
                ? new Color32(112, 196, 126, 255)
                : new Color32(228, 92, 92, 255);
        }
        if (needTitleLabel != null)
        {
            needTitleLabel.Text = Localize("xuiRebirthNeed", "Need").ToUpperInvariant();
            needTitleLabel.Color = new Color32(240, 240, 242, 255);
        }
        if (needValueLabel != null)
        {
            needValueLabel.Text = value.Need.ToString();
            needValueLabel.Color = new Color32(240, 240, 242, 255);
        }
        if (frame != null)
            frame.Color = value.HasEnough ? new Color32(74, 118, 80, 255) : new Color32(150, 66, 66, 255);

        RefreshBindings();
    }

    public void Clear()
    {
        requirement = null;
        if (icon != null) icon.IsVisible = false;
        if (nameLabel != null) nameLabel.Text = string.Empty;
        if (haveTitleLabel != null) haveTitleLabel.Text = string.Empty;
        if (haveValueLabel != null) haveValueLabel.Text = string.Empty;
        if (needTitleLabel != null) needTitleLabel.Text = string.Empty;
        if (needValueLabel != null) needValueLabel.Text = string.Empty;
        if (ViewComponent != null) ViewComponent.IsVisible = false;
    }

    [PublicizedFrom(EAccessModifier.Protected)]
    public override bool GetBindingValueInternal(ref string value, string bindingName)
    {
        if (bindingName == "rebirthsourcebreakdown")
        {
            value = requirement != null ? requirement.SourceBreakdown : " ";
            return true;
        }
        return base.GetBindingValueInternal(ref value, bindingName);
    }

    private T GetView<T>(string id) where T : XUiView
    {
        XUiController child = GetChildById(id);
        return child != null ? child.ViewComponent as T : null;
    }

    private static string Localize(string key, string fallback)
    {
        string value = Localization.Get(key ?? string.Empty);
        return string.IsNullOrWhiteSpace(value) || string.Equals(value, key, StringComparison.Ordinal) ? fallback : value;
    }
}
