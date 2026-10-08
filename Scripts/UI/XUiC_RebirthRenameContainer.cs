using Platform;
using UnityEngine.Scripting;

#nullable disable

[Preserve]
public sealed class XUiC_RebirthRenameContainer : XUiController
{
    [XuiBindComponent("containerName", true)]
    public readonly XUiC_TextInput txtContainerName;

    [XuiBindComponent("btnConfirm", true)]
    public readonly XUiC_Button btnConfirm;

    [XuiBindComponent("btnCancel", true)]
    public readonly XUiC_Button btnCancel;

    private Vector3i blockPos;
    private string initialName;
    private string defaultName;
    private bool wasCursorHidden;

    public override void OnOpen()
    {
        base.OnOpen();

        // Rename is opened from a Hold-E radial. Never inherit that radial's
        // navigation lock/cursor state: the player must be able to move the mouse
        // immediately and click/type in this window.
        CursorControllerAbs cursor = xui.playerUI.CursorController;
        if (cursor != null)
        {
            wasCursorHidden = cursor.GetCursorHidden();
            cursor.Locked = false;
            cursor.HoverTarget = null;
            cursor.SetNavigationTarget((XUiView)null);
            cursor.SetNavigationLockView((XUiView)null);
            cursor.SetCursorHidden(false);
            cursor.ResetToCenter();
        }

        EntityPlayerLocal player = xui.playerUI.entityPlayer;
        if (player != null)
        {
            player.SetControllable(false);
            player.ClearMovementInputs();
        }

        txtContainerName.Text = !string.IsNullOrEmpty(initialName)
            ? initialName
            : (defaultName ?? string.Empty);
        txtContainerName.SelectOrVirtualKeyboard(true);
    }

    public override void OnClose()
    {
        UIInput selectedInput = UIInput.selection;
        if (selectedInput != null)
        {
            selectedInput.RemoveFocus();
            selectedInput.isSelected = false;
        }

        CursorControllerAbs cursor = xui.playerUI.CursorController;
        if (cursor != null)
        {
            cursor.HoverTarget = null;
            cursor.SetNavigationTarget((XUiView)null);
            cursor.SetNavigationLockView((XUiView)null);
            cursor.SetCursorHidden(wasCursorHidden);
            cursor.Locked = false;
        }

        EntityPlayerLocal player = xui.playerUI.entityPlayer;
        if (player != null) player.SetControllable(true);

        base.OnClose();

        if (UIInput.selection != null) UIInput.selection.isSelected = false;
        if (cursor != null)
        {
            cursor.HoverTarget = null;
            cursor.ResetNavigationTarget();
        }
        xui.playerUI.windowManager.ResetActionSets();

        initialName = string.Empty;
        defaultName = string.Empty;
    }

    public override void Update(float dt)
    {
        base.Update(dt);
        if (!XUiUtils.HotkeysAllowedFor(viewComponent))
            return;

        if (xui.playerUI.playerInput.PermanentActions.Cancel.WasReleased)
            BtnCancel_OnPressed(this, -1);
        else if (xui.playerUI.playerInput.GUIActions.Apply.WasReleased)
            BtnConfirm_OnPressed(this, 0);
    }

    [XuiBindEvent("OnPress", "btnCancel")]
    public void BtnCancel_OnPressed(XUiController sender, int mouseButton)
    {
        xui.playerUI.windowManager.Close((GUIWindow)windowGroup);
    }

    [XuiBindEvent("OnPress", "btnConfirm")]
    public void BtnConfirm_OnPressed(XUiController sender, int mouseButton)
    {
        EntityPlayerLocal player = xui != null && xui.playerUI != null
            ? xui.playerUI.entityPlayer
            : null;
        PersistentPlayerData persistent = GameManager.Instance != null
            ? GameManager.Instance.GetPersistentLocalPlayer()
            : null;
        if (player == null || persistent == null || persistent.PrimaryId == null)
            return;

        string name = RebirthContainerRenameService.NormalizeName(txtContainerName.Text);
        World world = player.world as World;
        if (world == null)
            return;

        var connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (connection == null || GameManager.Instance == null ||
            !object.ReferenceEquals(GameManager.Instance.World, world) ||
            (!world.IsRemote() && !connection.IsServer))
        {
            ShowUnavailable(player);
            return;
        }

        if (!world.IsRemote())
        {
            RebirthContainerRenameService.ProcessServerRequest(
                world, blockPos, player.entityId, persistent.PrimaryId, name);
        }
        else
        {
            NetPackageRebirthRenameContainer package = NetPackageManager
                .GetPackage<NetPackageRebirthRenameContainer>()
                .Setup(blockPos, player.entityId, persistent, name);
            if (!LogisticsTransferService.ClientChannelReady(connection, package))
            {
                ShowUnavailable(player);
                return;
            }
            connection.SendToServer(package);
        }

        xui.playerUI.windowManager.Close((GUIWindow)windowGroup);
    }

    private static void ShowUnavailable(EntityPlayerLocal player)
    {
        GameManager.ShowTooltip(player, Localization.Get("xuiRebirthContainerRenameUnavailable"), string.Empty, "ui_denied");
    }

    [XuiBindEvent("OnSubmitHandler", "txtContainerName")]
    public void ContainerName_OnSubmit(XUiController sender, string text)
    {
        BtnConfirm_OnPressed(this, 0);
    }

    public static void Open(
        XUi targetXui,
        Vector3i position,
        string currentName,
        string localizedDefaultName)
    {
        XUiC_RebirthRenameContainer controller =
            targetXui != null ? targetXui.GetChildByType<XUiC_RebirthRenameContainer>() : null;
        if (controller == null)
        {
            Log.Error("[REBIRTH BlockPickup] rename-container XUI controller was not found.");
            return;
        }

        controller.blockPos = position;
        controller.initialName = currentName ?? string.Empty;
        controller.defaultName = localizedDefaultName ?? string.Empty;
        // Open as a real modal so the Hold-E radial is closed first instead of
        // remaining underneath with its navigation lock still active.
        targetXui.playerUI.windowManager.Open((GUIWindow)controller.windowGroup, true);
    }
}
