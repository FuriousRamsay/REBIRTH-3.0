using Platform;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting;

#nullable disable

public static class QuickStackCategoryUiService
{
    public const string WindowGroupName = "rebirthQuickStackCategories";
    public static void Open(XUi xui)
    {
        EntityPlayerLocal local = xui != null && xui.playerUI != null ? xui.playerUI.entityPlayer : null;
        if (local == null || local.HitInfo == null) return;
        OpenForContainer(xui, local.HitInfo.hit.blockPos);
    }
    public static void OpenForContainer(XUi xui, Vector3i position)
    {
        EntityPlayerLocal local = xui != null && xui.playerUI != null ? xui.playerUI.entityPlayer : null;
        PersistentPlayerData pp = GameManager.Instance != null ? GameManager.Instance.GetPersistentLocalPlayer() : null;
        if (local == null || pp == null || pp.PrimaryId == null) return;
        ConnectionManager c = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (c.IsServer)
        {
            int mask;
            if (!TryReadAuthorized(GameManager.Instance.World, local, position, out mask)) return;
            XUiC_RebirthQuickStackCategories.Open(xui, position, mask);
        }
        else c.SendToServer(NetPackageManager.GetPackage<NetPackageQuickStackCategoryReadRequest>().Setup(local.entityId, pp, position));
    }

    public static bool TryReadAuthorized(World world, EntityPlayer player, Vector3i position, out int mask)
    {
        mask = 0;
        TileEntity te = QuickStackAcceptedCategoryRegistry.ResolveTileEntity(world, position);
        string reason;
        if (!CanManageContainer(world, player, te, out reason)) return false;

        Vector3i canonical = te.ToWorldPos();
        mask = QuickStackAcceptedCategoryRegistry.GetMask(canonical);
        if (mask == 0 && canonical != position)
        {
            // Compatibility with category files written by older builds against a
            // clicked child/multiblock coordinate.
            mask = QuickStackAcceptedCategoryRegistry.GetMask(position);
        }
        return true;
    }


    /// <summary>
    /// Category assignment is a direct interaction with persistent static storage, not a
    /// remote-resource transaction.  It therefore deliberately does not require ownership,
    /// prior Remote Resource activation, or an unoccupied tile entity.  The latter is
    /// important for the category button inside windowLooting: the same player is actively
    /// using the container while editing its routing categories.
    /// </summary>
    public static bool CanManageContainer(
        World world, EntityPlayer player, TileEntity te, out string reason)
    {
        reason = string.Empty;
        if (world == null || player == null || te == null)
        {
            reason = "missing world, player, or tile entity";
            return false;
        }

        TileEntityComposite composite = te as TileEntityComposite;
        TEFeatureStorage loot;
        if (composite == null ||
            !te.TryGetSelfOrFeature<TEFeatureStorage>(out loot) || loot == null ||
            !RemoteResourceSourcePolicy.IsSupportedStatic(te, loot))
        {
            reason = "unsupported or temporary storage";
            return false;
        }

        Vector3 center = te.ToWorldPos().ToVector3() + Vector3.one * 0.5f;
        const float maximumInteractionDistance = 6f;
        if ((player.position - center).sqrMagnitude >
            maximumInteractionDistance * maximumInteractionDistance)
        {
            reason = "too far away";
            return false;
        }

        if (!RemoteResourceAccess.AuthorizeComposite(composite, player, false, out reason))
            return false;

        reason = string.Empty;
        return true;
    }

    public static void SaveLocal(Vector3i position, int mask)
    {
        EntityPlayerLocal local = GameManager.Instance != null ? GameManager.Instance.World.GetPrimaryPlayer() : null;
        PersistentPlayerData pp = GameManager.Instance != null ? GameManager.Instance.GetPersistentLocalPlayer() : null;
        if (local == null || pp == null || pp.PrimaryId == null) return;
        ConnectionManager c = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (c.IsServer) ProcessSave(GameManager.Instance.World, local.entityId, pp.PrimaryId, position, mask);
        else c.SendToServer(NetPackageManager.GetPackage<NetPackageQuickStackCategorySaveRequest>().Setup(local.entityId, pp, position, mask));
    }

    public static void ProcessSave(World world, int entityId, PlatformUserIdentifierAbs userId, Vector3i position, int mask)
    {
        EntityPlayer player = world != null ? world.GetEntity(entityId) as EntityPlayer : null;
        PersistentPlayerData pp = GameManager.Instance.GetPersistentPlayerList().GetPlayerDataFromEntityID(entityId);
        if (player == null || pp == null || pp.PrimaryId == null || userId == null || !pp.PrimaryId.Equals(userId)) return;
        int ignored;
        if (!TryReadAuthorized(world, player, position, out ignored)) return;

        TileEntity te = QuickStackAcceptedCategoryRegistry.ResolveTileEntity(world, position);
        if (te == null) return;
        Vector3i canonical = te.ToWorldPos();

        // Assigning categories is an explicit Quick Stack activation. Register the
        // tile entity immediately instead of waiting for an item deposit/slot update.
        RemoteResourceRegistry.Register(te);

        // Remove a possible legacy alias and persist the canonical tile position.
        if (canonical != position) QuickStackAcceptedCategoryRegistry.SetMask(position, 0);
        QuickStackAcceptedCategoryRegistry.SetMask(canonical, mask);
    }


}

[Preserve]
public sealed class XUiC_RebirthQuickStackCategories : XUiController
{
    private Vector3i position;
    private int mask;
    private readonly Dictionary<QuickStackCategory, XUiC_ToggleButton> toggles = new Dictionary<QuickStackCategory, XUiC_ToggleButton>();
    private bool openedOverLoot;
    private GUIWindow underlyingLootWindow;
    private bool underlyingLootWasEscClosable;

    public override void Init()
    {
        base.Init();
        foreach (QuickStackCategory category in Enum.GetValues(typeof(QuickStackCategory)))
        {
            if (category == QuickStackCategory.None) continue;

            XUiController toggleRoot = GetChildById("cat" + category);
            XUiC_ToggleButton toggle = toggleRoot != null
                ? toggleRoot.GetChildByType<XUiC_ToggleButton>()
                : null;
            if (toggle == null) continue;

            toggles[category] = toggle;
            QuickStackCategory captured = category;
            toggle.OnValueChanged += delegate(XUiC_ToggleButton sender, bool value)
            {
                SetCategory(captured, value);
            };
        }

        XUiController all = GetChildById("btnSelectAll"); if (all != null) all.OnPress += delegate { SelectAll(); };
        XUiController clear = GetChildById("btnClearAll"); if (clear != null) clear.OnPress += delegate { ClearAll(); };
        XUiController save = GetChildById("btnSave"); if (save != null) save.OnPress += delegate { Save(); };
        XUiController cancel = GetChildById("btnCancel"); if (cancel != null) cancel.OnPress += delegate { Close(); };
    }


    public override void OnOpen()
    {
        base.OnOpen();
        windowGroup.isEscClosable = false;

        CursorControllerAbs cursor = xui.playerUI.CursorController;
        EntityPlayerLocal player = xui.playerUI.entityPlayer;

        if (openedOverLoot)
        {
            // This is an overlay on the already-open Looting workflow.  Do not
            // touch cursor visibility/locking, player controllability, navigation,
            // or action-set state at all.  The player just clicked this button from
            // Looting, so Looting already has the exact usable mouse/input state we
            // need.  Our own GUI action set is pushed above it by GUIWindowManager
            // and will be popped by GUIWindowManager after OnClose returns.
        }
        else
        {
            // Standalone launch (for example from Hold-E) owns its own temporary
            // mouse/gameplay state because the launcher modal was closed first.
            if (cursor != null)
            {
                cursor.Locked = false;
                cursor.SetCursorHidden(false);
                cursor.ResetToCenter();
            }

            if (player != null)
            {
                player.SetControllable(false);
                player.ClearMovementInputs();
            }
        }

        Refresh();
    }

    public override void OnClose()
    {
        bool wasOpenedOverLoot = openedOverLoot;
        CursorControllerAbs cursor = xui.playerUI.CursorController;
        EntityPlayerLocal player = xui.playerUI.entityPlayer;

        if (underlyingLootWindow != null)
            underlyingLootWindow.isEscClosable = underlyingLootWasEscClosable;

        // IMPORTANT: do not call GUIWindowManager.ResetActionSets() here.
        // GUIWindowManager.Close() invokes this OnClose() first and only AFTER it
        // returns does it DisableWindowActionSet(windowGroup).  Resetting action
        // sets inside OnClose therefore rebuilds the stack and then makes Close()
        // pop this window from the freshly rebuilt stack in the wrong order.  That
        // is what could leave both UI and gameplay input unusable.  Let the native
        // Close() sequence pop our action set normally.
        base.OnClose();

        if (!wasOpenedOverLoot)
        {
            // Remove only an actual focused text input on the standalone path.
            // When layered over Looting we intentionally preserve every piece of
            // the underlying window's focus/navigation state.
            UIInput selectedInput = UIInput.selection;
            if (selectedInput != null)
            {
                selectedInput.RemoveFocus();
                selectedInput.isSelected = false;
            }

            // Standalone Categories is returning to gameplay, so restore only the
            // state it explicitly took in OnOpen.  The window manager will pop this
            // window's GUI action set immediately after OnClose returns.
            if (cursor != null)
            {
                cursor.HoverTarget = null;
                cursor.SetNavigationTarget((XUiView)null);
                cursor.SetNavigationLockView((XUiView)null);
                cursor.Locked = false;

                // Match base 3.1 XUiC_Radial.OnClose.  The keyboard/mouse radial
                // deliberately leaves the XUi cursor in the non-hidden state after
                // it closes, and its next OnOpen does not explicitly show the cursor.
                // Hiding it here poisoned the next Hold-E radial: the radial opened
                // normally, but its pointer remained invisible.
                cursor.SetCursorHidden(false);
            }

            if (player != null)
                player.SetControllable(true);
        }
        // When opened over Looting, deliberately do NOTHING to cursor visibility,
        // controllability, navigation, or action sets.  Looting is still open and
        // remains authoritative for all of those states.

        openedOverLoot = false;
        underlyingLootWindow = null;
        underlyingLootWasEscClosable = false;
    }

    public override void Update(float dt)
    {
        base.Update(dt);

        // Only the standalone window needs to hold gameplay input itself.  When
        // Categories is layered over Looting, Looting already owns that state.
        if (!openedOverLoot)
        {
            EntityPlayerLocal player = xui.playerUI.entityPlayer;
            if (player != null)
                player.SetControllable(false);
        }

        if (XUiUtils.HotkeysAllowedFor(viewComponent) &&
            (xui.playerUI.playerInput.PermanentActions.Cancel.WasReleased ||
             xui.playerUI.playerInput.GUIActions.Cancel.WasReleased)) Close();
    }

    private void SetCategory(QuickStackCategory category, bool enabled)
    {
        int bit = 1 << ((int)category - 1);
        if (enabled) mask |= bit;
        else mask &= ~bit;
    }

    private void SelectAll()
    {
        mask = (1 << (Enum.GetValues(typeof(QuickStackCategory)).Length - 1)) - 1;
        Refresh();
    }

    private void ClearAll()
    {
        mask = 0;
        Refresh();
    }

    private void Refresh()
    {
        foreach (KeyValuePair<QuickStackCategory, XUiC_ToggleButton> pair in toggles)
            pair.Value.Value = (mask & (1 << ((int)pair.Key - 1))) != 0;
    }

    private void Save()
    {
        QuickStackCategoryUiService.SaveLocal(position, mask);
        Close();
    }

    private void Close()
    {
        xui.playerUI.windowManager.Close((GUIWindow)windowGroup);
    }

    public static void Open(XUi xui, Vector3i pos, int value)
    {
        XUiController group = xui != null ? xui.FindWindowGroupByName(QuickStackCategoryUiService.WindowGroupName) : null;
        XUiC_RebirthQuickStackCategories controller = group != null
            ? group.GetChildByType<XUiC_RebirthQuickStackCategories>()
            : null;

        if (controller == null)
        {
            Log.Error("[QuickStack] category UI controller not found");
            return;
        }

        controller.position = pos;
        controller.mask = value;

        GUIWindowManager manager = xui.playerUI.windowManager;
        controller.openedOverLoot = manager.IsWindowOpen(XUiC_LootWindowGroup.ID);
        controller.underlyingLootWindow = controller.openedOverLoot
            ? manager.GetWindow(XUiC_LootWindowGroup.ID)
            : null;
        if (controller.underlyingLootWindow != null)
        {
            // Keep the loot/container window alive behind the Categories overlay.
            // It remains the modal window, but making it temporarily non-ESC-closable
            // prevents GUIWindowManager from closing it when Categories handles ESC.
            controller.underlyingLootWasEscClosable =
                controller.underlyingLootWindow.isEscClosable;
            controller.underlyingLootWindow.isEscClosable = false;
            manager.Open((GUIWindow)controller.windowGroup, false, true);
        }
        else
        {
            controller.underlyingLootWasEscClosable = false;
            manager.Open((GUIWindow)controller.windowGroup, true, true);
        }
    }
}

[Preserve]
public sealed class NetPackageQuickStackCategoryReadRequest : NetPackage
{
    private int entityId; private PlatformUserIdentifierAbs userId; private Vector3i position;
    public override NetPackageDirection PackageDirection { get { return NetPackageDirection.ToServer; } }
    public NetPackageQuickStackCategoryReadRequest Setup(int id, PersistentPlayerData pp, Vector3i pos) { entityId=id; userId=pp.PrimaryId; position=pos; return this; }
    public override void read(PooledBinaryReader r) { var b=(System.IO.BinaryReader)r; entityId=b.ReadInt32(); userId=PlatformUserIdentifierAbs.FromStream(b); position=new Vector3i(b.ReadInt32(),b.ReadInt32(),b.ReadInt32()); }
    public override void write(PooledBinaryWriter w) { base.write(w); var b=(System.IO.BinaryWriter)w; b.Write(entityId); userId.ToStream(b); b.Write(position.x); b.Write(position.y); b.Write(position.z); }
    public override void ProcessPackage(World world, GameManager callbacks) { EntityPlayer p=world.GetEntity(entityId) as EntityPlayer; int mask; if(p!=null && ValidEntityIdForSender(entityId) && ValidUserIdForSender(userId) && QuickStackCategoryUiService.TryReadAuthorized(world,p,position,out mask)) SingletonMonoBehaviour<ConnectionManager>.Instance.SendPackage(NetPackageManager.GetPackage<NetPackageQuickStackCategoryReadResult>().Setup(position,mask), _attachedToEntityId:entityId); }
    public int GetLength() { return 48; }
}

[Preserve]
public sealed class NetPackageQuickStackCategoryReadResult : NetPackage
{
    private Vector3i position; private int mask;
    public override NetPackageDirection PackageDirection { get { return NetPackageDirection.ToClient; } }
    public NetPackageQuickStackCategoryReadResult Setup(Vector3i pos,int value) { position=pos; mask=value; return this; }
    public override void read(PooledBinaryReader r) { var b=(System.IO.BinaryReader)r; position=new Vector3i(b.ReadInt32(),b.ReadInt32(),b.ReadInt32()); mask=b.ReadInt32(); }
    public override void write(PooledBinaryWriter w) { base.write(w); var b=(System.IO.BinaryWriter)w; b.Write(position.x); b.Write(position.y); b.Write(position.z); b.Write(mask); }
    public override void ProcessPackage(World world, GameManager callbacks) { EntityPlayerLocal p=world.GetPrimaryPlayer(); if(p!=null) XUiC_RebirthQuickStackCategories.Open(p.PlayerUI.xui,position,mask); }
    public int GetLength() { return 20; }
}

[Preserve]
public sealed class NetPackageQuickStackCategorySaveRequest : NetPackage
{
    private int entityId; private PlatformUserIdentifierAbs userId; private Vector3i position; private int mask;
    public override NetPackageDirection PackageDirection { get { return NetPackageDirection.ToServer; } }
    public NetPackageQuickStackCategorySaveRequest Setup(int id, PersistentPlayerData pp, Vector3i pos,int value) { entityId=id; userId=pp.PrimaryId; position=pos; mask=value; return this; }
    public override void read(PooledBinaryReader r) { var b=(System.IO.BinaryReader)r; entityId=b.ReadInt32(); userId=PlatformUserIdentifierAbs.FromStream(b); position=new Vector3i(b.ReadInt32(),b.ReadInt32(),b.ReadInt32()); mask=b.ReadInt32(); }
    public override void write(PooledBinaryWriter w) { base.write(w); var b=(System.IO.BinaryWriter)w; b.Write(entityId); userId.ToStream(b); b.Write(position.x); b.Write(position.y); b.Write(position.z); b.Write(mask); }
    public override void ProcessPackage(World world, GameManager callbacks) { if(ValidEntityIdForSender(entityId)&&ValidUserIdForSender(userId)) QuickStackCategoryUiService.ProcessSave(world,entityId,userId,position,mask); }
    public int GetLength() { return 52; }
}
