using UnityEngine;
using UnityEngine.Scripting;

#nullable disable

/// <summary>
/// Quick Stack category shortcut inside the normal loot-container toolbar.  The radial
/// command remains available too; this button makes category editing possible without
/// closing the container first.
/// </summary>
[Preserve]
public sealed class XUiC_RebirthQuickStackCategoryButton : XUiController
{
    private XUiV_Button button;
    private XUiC_LootWindow lootWindow;
    private Vector3i position;
    private bool hasPosition;
    private float nextRefreshAt;
    private readonly RebirthLootTitleProjection titleState = new RebirthLootTitleProjection();

    public override void Init()
    {
        base.Init();
        button = viewComponent as XUiV_Button;
        OnPress += OnButtonPressed;
    }

    public override void OnOpen()
    {
        base.OnOpen();
        lootWindow = GetParentByType<XUiC_LootWindow>();
        titleState.Reset();
        hasPosition = false;
        nextRefreshAt = 0f;
        SetHidden();
        Refresh();
    }

    public override void OnClose()
    {
        titleState.Reset();
        hasPosition = false;
        SetHidden();
        base.OnClose();
    }

    public override void Update(float dt)
    {
        base.Update(dt);
        if (Time.time < nextRefreshAt)
            return;

        nextRefreshAt = Time.time + 0.35f;
        Refresh();
    }

    private void Refresh()
    {
        if (button == null)
            return;
        if (lootWindow == null)
            lootWindow = GetParentByType<XUiC_LootWindow>();

        TEFeatureStorage loot = lootWindow != null ? lootWindow.te : null;
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        EntityPlayerLocal player = xui != null && xui.playerUI != null
            ? xui.playerUI.entityPlayer
            : null;
        if (loot == null || world == null || player == null)
        {
            hasPosition = false;
            SetHidden();
            return;
        }

        // Viewing a name is independent of permission to edit Quick Stack categories.
        RebirthContainerRenameService.ApplyCustomNameToLootWindow(lootWindow, titleState);

        Vector3i current = loot.ToWorldPos();
        TileEntity tileEntity = world.GetTileEntity(current);
        string reason;
        bool manageable = QuickStackCategoryUiService.CanManageContainer(
            world, player, tileEntity, out reason);
        if (!manageable)
        {
            hasPosition = false;
            SetHidden();
            return;
        }

        position = tileEntity.ToWorldPos();
        hasPosition = true;
        button.IsVisible = true;
        button.Enabled = true;


    }

    private void OnButtonPressed(XUiController sender, int mouseButton)
    {
        if (!hasPosition || button == null || !button.Enabled)
            return;

        QuickStackCategoryUiService.OpenForContainer(xui, position);
    }

    private void SetHidden()
    {
        if (button == null)
            return;
        button.IsVisible = false;
        button.Enabled = false;
    }
}
