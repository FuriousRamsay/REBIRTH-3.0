using System;
using System.IO;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Scripting;

#nullable disable

/// <summary>
/// 2.6 propane refill, as a feature on the native composite tank so saved tanks and the native
/// explosion keep working. The server moves propane; the lock holder changes the slot.
/// </summary>
[Preserve]
public sealed class TEFeatureRebirthPropaneFiller : TEFeatureAbs
{
    public const string FeatureName = "TEFeatureRebirthPropaneFiller";
    public const string OpenCommand = FeatureName + ":open";
    private const ushort SaveVersion = 1;
    private const double TickSeconds = 0.25, SyncSeconds = 0.5;

    private int unitsMax = 1000000, unitsLeft;
    private float unitsPerSecond = 200f, initMin = 0.30f, initMax = 0.65f;
    private bool initialized, filling;
    private ItemStack tank = ItemStack.Empty.Clone();
    private double lastTick, nextSync;

    internal string TankItemName { get; private set; } = "FuriousRamsayPropaneTank";
    internal bool Filling => filling;
    // 2.6 kept the tank in a tool slot. Here it is the block's one-slot storage, so it is dragged in and out
    // like any container slot, and saving and multiplayer sync are the native storage's.
    private TEFeatureStorage Slot => Parent?.GetFeature<TEFeatureStorage>();
    internal ItemStack Tank
    {
        get
        {
            var grid = Slot?.ItemGrid;
            if (grid == null || grid.items == null || grid.items.Length == 0) return tank;
            var stack = grid.items[0];
            return stack != null && !stack.IsEmpty() && stack.itemValue.ItemClass?.GetItemName() == TankItemName ? stack : ItemStack.Empty;
        }
    }
    internal float StationFraction => unitsMax > 0 ? (float)unitsLeft / unitsMax : 0f;
    internal float TankFraction
    {
        get
        {
            var current = Tank;
            if (current.IsEmpty() || current.itemValue.MaxUseTimes <= 0) return 0f;
            return 1f - current.itemValue.UseTimes / current.itemValue.MaxUseTimes;
        }
    }

    public override void Init(TileEntityComposite parent, TileEntityFeatureData featureData)
    {
        base.Init(parent, featureData);
        var props = featureData.Props;
        props.ParseInt("UnitsMax", ref unitsMax);
        props.ParseFloat("UnitsPerSecond", ref unitsPerSecond);
        props.ParseFloat("UnitsInitMin", ref initMin);
        props.ParseFloat("UnitsInitMax", ref initMax);
        string item = props.GetString("AcceptItem");
        if (!string.IsNullOrEmpty(item)) TankItemName = item;
    }

    public override void CopyFromInternal(TileEntityComposite other)
    {
        var source = other?.GetFeature<TEFeatureRebirthPropaneFiller>();
        if (source == null) return;
        unitsLeft = source.unitsLeft;
        initialized = source.initialized;
        filling = source.filling;
        tank = source.tank.Clone();
    }

    internal bool CanFill => !Tank.IsEmpty() && unitsLeft > 0 && Tank.itemValue.UseTimes > 0f;

    internal void SetTank(ItemStack stack)
    {
        tank = stack == null || stack.IsEmpty() ? ItemStack.Empty.Clone() : stack.Clone();
        if (tank.IsEmpty()) filling = false;
        SetModified();
    }

    internal void SetFilling(bool value)
    {
        filling = value && CanFill;
        SetModified();
    }

    public override void UpdateTick(World world)
    {
        base.UpdateTick(world);
        if (!SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer) return;
        if (!initialized) InitializeReservoir(world);
        MoveLegacyTank();
        if (!filling) { lastTick = 0; return; }

        double now = Time.timeAsDouble;
        if (lastTick <= 0) lastTick = now;
        double dt = now - lastTick;
        if (dt < TickSeconds) return;
        lastTick = now;

        if (!CanFill) { filling = false; SetModified(); return; }
        var current = Tank;
        int delta = Mathf.Min(Mathf.FloorToInt(unitsPerSecond * (float)dt), Mathf.Min(unitsLeft, Mathf.CeilToInt(current.itemValue.UseTimes)));
        if (delta <= 0) return;
        current.itemValue.UseTimes = Mathf.Max(0f, current.itemValue.UseTimes - delta);
        Slot?.SetModified();
        unitsLeft -= delta;
        if (!CanFill) filling = false;
        if (!filling || now >= nextSync)
        {
            nextSync = now + SyncSeconds;
            SetModified();
        }
    }

    // Tanks saved inside the station before it had a storage slot move into that slot once.
    private void MoveLegacyTank()
    {
        var storage = Slot;
        if (storage?.ItemGrid != null && !storage.ItemGrid.PlayerOwned) { storage.ItemGrid.PlayerOwned = true; storage.SetModified(); }
        var grid = Slot?.ItemGrid;
        if (tank.IsEmpty() || grid?.items == null || grid.items.Length == 0 || (grid.items[0] != null && !grid.items[0].IsEmpty())) return;
        grid.items[0] = tank.Clone();
        tank = ItemStack.Empty.Clone();
        Slot.SetModified();
        SetModified();
    }

    private void InitializeReservoir(World world)
    {
        // 2.6: trader tanks hold much less than tanks found in the world.
        bool atTrader = world.GetTraderAreaAt(ToWorldPos()) != null;
        float low = atTrader ? 0.10f : initMin, high = atTrader ? 0.15f : initMax;
        unitsLeft = Mathf.Clamp((int)(unitsMax * UnityEngine.Random.Range(low, high)), 0, unitsMax);
        initialized = true;
        SetModified();
    }

    public override void OnRemove(World world)
    {
        // The tank inside survives the block.
        if (SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer && !tank.IsEmpty())
        {
            GameManager.Instance.ItemDropServer(tank.Clone(), ToWorldPos().ToVector3() + Vector3.one * 0.5f, Vector3.zero, -1, 60f, false);
            tank = ItemStack.Empty.Clone();
        }
        base.OnRemove(world);
    }

    public override string GetActivationText(WorldBase world, Vector3i blockPos, BlockValue blockValue, EntityAlive entityFocusing, string activateHotkeyMarkup, string focusedTileEntityName)
    {
        return focusedTileEntityName + System.Environment.NewLine + string.Format(Localization.Get("useWorkstation"), activateHotkeyMarkup);
    }

    public override void InitBlockActivationCommands(Action<BlockActivationCommand, TileEntityComposite.EBlockCommandOrder, TileEntityFeatureData> addCallback)
    {
        base.InitBlockActivationCommands(addCallback);
        addCallback(new BlockActivationCommand("open", "campfire", true), TileEntityComposite.EBlockCommandOrder.Normal, FeatureData);
    }

    public override bool OnLockResponseLocal(bool success, PooledBinaryReader serverContext, ushort channel)
    {
        var ui = LocalPlayerUI.GetUIForPrimaryPlayer();
        if (!success)
        {
            GameManager.ShowTooltip(ui.entityPlayer, Localization.Get("ttNoInteractItem"), string.Empty, "ui_denied");
            return false;
        }
        return true;
    }

    public override void Write(PooledBinaryWriter writer, StreamModeWrite mode)
    {
        base.Write(writer, mode);
        var bw = (BinaryWriter)writer;
        if (mode == StreamModeWrite.Persistency) bw.Write(SaveVersion);
        bw.Write(unitsLeft);
        bw.Write(initialized);
        bw.Write(filling);
        tank.Write(writer);
    }

    public override void Read(PooledBinaryReader reader, StreamModeRead mode)
    {
        base.Read(reader, mode);
        var br = (BinaryReader)reader;
        if (mode == StreamModeRead.Persistency) br.ReadUInt16();
        int left = br.ReadInt32();
        bool init = br.ReadBoolean();
        bool wantFilling = br.ReadBoolean();
        var incoming = ItemStack.Empty.Clone();
        incoming.Read(reader);

        if (mode != StreamModeRead.FromClient)
        {
            unitsLeft = Mathf.Clamp(left, 0, unitsMax);
            initialized = init;
            filling = wantFilling;
            tank = incoming;
            return;
        }

        // A client only changes the slot and the switch. It can be a moment behind the server's
        // filling, so it never resets the reservoir or takes back propane already in the tank.
        if (!incoming.IsEmpty() && !tank.IsEmpty() && incoming.itemValue.type == tank.itemValue.type)
            incoming.itemValue.UseTimes = Mathf.Min(incoming.itemValue.UseTimes, tank.itemValue.UseTimes);
        tank = incoming;
        filling = wantFilling && CanFill;
        SetModified();
    }
}

[Preserve]
public sealed class XUiC_RebirthPropanePanel : XUiController
{
    public const string WindowGroup = "rebirthPropaneFiller";
    private static TEFeatureRebirthPropaneFiller opening;

    private TEFeatureRebirthPropaneFiller filler;
    private XUiC_SimpleButton toggle;
    private XUiV_Label stationLabel, tankLabel;
    private XUiV_Sprite stationBar, tankBar, tankIcon;
    private float nextPoll;

    internal static void Open(LocalPlayerUI ui, TEFeatureRebirthPropaneFiller value)
    {
        opening = value;
        ui.windowManager.Open(WindowGroup, false, true);
    }

    public override void Init()
    {
        base.Init();
        stationLabel = GetChildById("lblStation")?.ViewComponent as XUiV_Label;
        tankLabel = GetChildById("lblTank")?.ViewComponent as XUiV_Label;
        stationBar = GetChildById("barStation")?.ViewComponent as XUiV_Sprite;
        tankBar = GetChildById("barTank")?.ViewComponent as XUiV_Sprite;
        tankIcon = GetChildById("iconTank")?.ViewComponent as XUiV_Sprite;
        toggle = GetChildById("btnFill") as XUiC_SimpleButton;
        if (toggle != null) toggle.OnPressed += delegate { Toggle(); };
    }

    public override void OnOpen()
    {
        base.OnOpen();
        filler = opening;
        opening = null;
        nextPoll = 0f;
    }

    public override void OnClose()
    {
        filler = null;
        base.OnClose();
    }

    private void Deny(string key)
    {
        GameManager.ShowTooltip(xui.playerUI.entityPlayer, Localization.Get(key), string.Empty, "ui_denied");
    }

    private void Toggle()
    {
        if (filler == null) return;
        if (!filler.Filling && !filler.CanFill)
        {
            Deny(filler.Tank.IsEmpty() ? "xuiRebirthPropaneNoTank" : filler.TankFraction >= 1f ? "xuiRebirthPropaneTankFull" : "xuiRebirthPropaneEmpty");
            return;
        }
        filler.SetFilling(!filler.Filling);
        nextPoll = 0f;
    }

    public override void Update(float dt)
    {
        base.Update(dt);
        if (filler == null || Time.unscaledTime < nextPoll) return;
        nextPoll = Time.unscaledTime + 0.25f;
        Set(stationLabel, stationBar, "xuiRebirthPropaneStation", filler.StationFraction);
        Set(tankLabel, tankBar, "xuiRebirthPropaneTank", filler.TankFraction);
        if (tankIcon != null) tankIcon.Color = filler.Tank.IsEmpty() ? new Color(1f, 1f, 1f, 0.25f) : Color.white;
        if (toggle != null) toggle.Text = Localization.Get(filler.Filling ? "xuiRebirthPropaneStop" : "xuiRebirthPropaneStart");
    }

    private static void Set(XUiV_Label label, XUiV_Sprite bar, string key, float fraction)
    {
        if (label != null) label.Text = Localization.Get(key) + ": " + (fraction * 100f).ToString("0.00") + "%";
        if (bar != null && !Mathf.Approximately(bar.Fill, fraction)) bar.Fill = fraction;
    }
}

[HarmonyPatch(typeof(BlockCompositeTileEntity), nameof(BlockCompositeTileEntity.OnBlockActivated), new[] { typeof(string), typeof(WorldBase), typeof(Vector3i), typeof(BlockValue), typeof(EntityPlayerLocal) })]
internal static class RebirthPropaneFillerActivation
{
    private static bool Prefix(string _commandName, WorldBase _world, Vector3i _blockPos, BlockValue _blockValue, EntityPlayerLocal _player, ref bool __result)
    {
        if (!string.Equals(_commandName, TEFeatureRebirthPropaneFiller.OpenCommand, StringComparison.OrdinalIgnoreCase)) return true;
        if (_blockValue.ischild)
        {
            _blockPos = _blockValue.Block.multiBlockPos.GetParentPos(_blockPos, _blockValue);
        }
        var filler = (_world.GetTileEntity(_blockPos) as TileEntityComposite)?.GetFeature<TEFeatureRebirthPropaneFiller>();
        if (filler == null) return true;
        _player.AimingGun = false;
        var storage = filler.Parent.GetFeature<TEFeatureStorage>();
        // 2.6 had no search timer here: the slot is storage the player uses, never loot.
        if (storage?.ItemGrid != null) storage.ItemGrid.PlayerOwned = true;
        if (storage != null) LockManager.Instance.LockRequestLocal(storage); else return true;
        __result = true;
        return false;
    }
}


/// <summary>When the tank's storage opens, show the fill controls beside it; close them with it.</summary>
[HarmonyPatch(typeof(XUiC_LootWindowGroup), "openContainer")]
internal static class RebirthPropaneFillerLootOpen
{
    private static void Postfix(XUiC_LootWindowGroup __instance, object _te)
    {
        var filler = (_te as TEFeatureStorage)?.Parent?.GetFeature<TEFeatureRebirthPropaneFiller>();
        if (filler != null) XUiC_RebirthPropanePanel.Open(__instance.xui.playerUI, filler);
    }
}

[HarmonyPatch(typeof(XUiC_LootWindowGroup), nameof(XUiC_LootWindowGroup.OnClose))]
internal static class RebirthPropaneFillerLootClose
{
    private static void Prefix(XUiC_LootWindowGroup __instance)
    {
        var manager = __instance.xui?.playerUI?.windowManager;
        if (manager != null && manager.IsWindowOpen(XUiC_RebirthPropanePanel.WindowGroup)) manager.Close(XUiC_RebirthPropanePanel.WindowGroup);
    }
}

/// <summary>2.6 accepted only propane tanks in that slot.</summary>
[HarmonyPatch(typeof(XUiC_ItemStack), nameof(XUiC_ItemStack.CanSwap))]
internal static class RebirthPropaneFillerSlotFilter
{
    private static void Postfix(XUiC_ItemStack __instance, ItemStack _stack, ref bool __result)
    {
        if (!__result || _stack == null || _stack.IsEmpty() || __instance.StackLocation != XUiC_ItemStack.StackLocationTypes.LootContainer) return;
        var loot = __instance.xui?.FindWindowGroupByName("looting") as XUiC_LootWindowGroup;
        var filler = (loot?.te as TEFeatureStorage)?.Parent?.GetFeature<TEFeatureRebirthPropaneFiller>();
        if (filler != null && _stack.itemValue.ItemClass?.GetItemName() != filler.TankItemName) __result = false;
    }
}

[Preserve]
public sealed class RebirthPropaneFillerModApi : IModApi
{
    public void InitMod(Mod modInstance)
    {
        var harmony = new Harmony("rebirth.workstations.propanefiller");
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthPropaneFillerActivation));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthPropaneFillerLootOpen));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthPropaneFillerLootClose));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthPropaneFillerSlotFilter));
    }
}

/// <summary>Test helper: empties propane tanks in the toolbelt and backpack so refilling can be checked.</summary>
[Preserve]
public sealed class ConsoleCmdRebirthPropane : ConsoleCmdAbstract
{
    public override bool IsExecuteOnClient => true;
    public override int DefaultPermissionLevel => 1000;
    public override string[] getCommands() => new[] { "rbpropane" };
    public override string getDescription() => "Empties your carried propane tanks, for testing the refill station.";

    public override void Execute(System.Collections.Generic.List<string> args, CommandSenderInfo sender)
    {
        var console = SingletonMonoBehaviour<SdtdConsole>.Instance;
        var player = GameManager.Instance?.World?.GetPrimaryPlayer();
        if (player == null) { console.Output("No local player."); return; }
        if (args.Count > 1 && args[0] == "place")
        {
            var hit = player.HitInfo;
            var block = Block.GetBlockValue(args[1]);
            if (block.isair || !hit.bHitValid) { console.Output("Aim at the ground and name a real block."); return; }
            var position = hit.hit.blockPos + Vector3i.up;
            GameManager.Instance.World.SetBlockRPC(position, block);
            console.Output("Placed " + args[1] + " at " + position);
            return;
        }
        if (args.Count > 3 && args[0] == "clear")
        {
            // Test setup: an empty tank slot at that station and no tanks carried, so a scenario starts the same way.
            var at = new Vector3i(int.Parse(args[1]), int.Parse(args[2]), int.Parse(args[3]));
            var slot = (GameManager.Instance.World.GetTileEntity(at) as TileEntityComposite)?.GetFeature<TEFeatureStorage>();
            if (slot?.ItemGrid?.items != null && slot.ItemGrid.items.Length > 0) { slot.ItemGrid.items[0] = ItemStack.Empty.Clone(); slot.SetModified(); }
            int removed = 0;
            for (int i = 0; i < player.bag.SlotCount; i++)
                if (player.bag.ItemGrid.GetItemClone(i).itemValue.ItemClass?.GetItemName() == "FuriousRamsayPropaneTank") { player.bag.SetSlot(i, ItemStack.Empty.Clone()); removed++; }
            for (int i = 0; i < player.inventory.ItemGrid.items.Length; i++)
                if (player.inventory.ItemGrid.items[i]?.itemValue?.ItemClass?.GetItemName() == "FuriousRamsayPropaneTank") { player.inventory.SetItem(i, ItemStack.Empty.Clone()); removed++; }
            console.Output("Cleared the tank slot" + (slot == null ? " (no station there)" : "") + " and removed " + removed + " carried tanks.");
            return;
        }
        int count = 0;
        for (int i = 0; i < player.bag.SlotCount; i++)
        {
            var stack = player.bag.ItemGrid.GetItemClone(i);
            if (stack.IsEmpty() || stack.itemValue.ItemClass?.GetItemName() != "FuriousRamsayPropaneTank") continue;
            stack.itemValue.UseTimes = stack.itemValue.MaxUseTimes;
            player.bag.SetSlot(i, stack);
            count++;
        }
        console.Output("Emptied " + count + " propane tanks.");
    }
}
