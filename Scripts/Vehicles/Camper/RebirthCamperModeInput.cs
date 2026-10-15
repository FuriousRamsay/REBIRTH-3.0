using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Scripting;

// An on-foot shortcut to the existing owner-only authoritative mode command.
// It does not open management or mutate the camper's client-side mode.
internal static class RebirthCamperModeInput
{
    private static bool installed;
    private static int consumedFrame = -1, consumedActor = -1;
    private static long sequence;
    private sealed class PeerSequence { internal World World; internal long Sequence; }
    private static readonly ConditionalWeakTable<ClientInfo, PeerSequence> sequences = new ConditionalWeakTable<ClientInfo, PeerSequence>();
    internal static void Install()
    {
        if (installed) return;
        var harmony = new Harmony("rebirth.camper.mode.input.v1");
        harmony.CreateClassProcessor(typeof(RebirthCamperModeInputTick)).Patch();
        harmony.CreateClassProcessor(typeof(RebirthCamperModeInputMap)).Patch();
        harmony.CreateClassProcessor(typeof(RebirthCamperModeInputPrompt)).Patch();
        installed = true;
    }
    private static bool NoWindow(EntityPlayerLocal player)
    {
        var ui = LocalPlayerUI.GetUIForPlayer(player);
        return ui != null && !ui.windowManager.IsCursorWindowOpen() && !ui.windowManager.IsModalWindowOpen()
            && !RebirthConsoleInputGuardRuntime.BlocksGameplayInput();
    }
    internal static bool Allowed(EntityVehicle vehicle, EntityPlayer player)
    {
        if (!ThreadManager.IsMainThread() || !RebirthCamperEnergyAccess.Registered || vehicle?.world == null
            || player == null || player.IsDead() || player.AttachedToEntity != null || vehicle.IsDead()
            || !ReferenceEquals(GameManager.Instance?.World, vehicle.world) || !ReferenceEquals(player.world, vehicle.world)
            || !ReferenceEquals(vehicle.world.GetEntity(vehicle.entityId), vehicle)
            || !ReferenceEquals(vehicle.world.GetEntity(player.entityId), player) || !RebirthCamperRadial.IsCamper(vehicle)
            || (player.position - vehicle.position).sqrMagnitude > 64f
            || RebirthCamperRemoteAdmission.NativeInteractionOwner(vehicle) >= 0
            || !RebirthCamperEnergyRuntime.TryObservePresentation(vehicle, out _, out _, out _, out _)) return false;
        var owner = vehicle.GetOwner();
        var data = GameManager.Instance.GetPersistentPlayerList()?.GetPlayerDataFromEntityID(player.entityId);
        if (owner == null || data == null || (!owner.Equals(data.PrimaryId) && !owner.Equals(data.NativeId))) return false;
        if (player is EntityPlayerLocal local && (!RebirthCamperRadial.Allowed(vehicle, local)
            || LockManager.Instance.IsLockedByLocalPlayer(vehicle, 0))) return false;
        return !vehicle.world.IsRemote() || PackageAvailable();
    }
    internal static bool TryFocused(EntityPlayerLocal player, out EntityVehicle vehicle)
    {
        vehicle = null;
        if (player == null || !NoWindow(player) || player.TPCameraCheckResult == eTPCameraCheckResult.LineOfSightCheckFailed
            || Constants.cCollectItemDistance <= 0) return false;
        if (RebirthCamperPhysicalTargets.TryGetFocused(player, out var physical, out _) && Allowed(physical, player))
        { vehicle = physical; return true; }
        Ray native = player.GetLookRay();
        var ray = new Ray(native.origin - Origin.position, native.direction.normalized);
        var hits = Physics.RaycastAll(ray, Constants.cCollectItemDistance, Physics.AllLayers, QueryTriggerInteraction.Ignore);
        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (var hit in hits)
        {
            var collider = hit.collider;
            if (collider == null || ReferenceEquals(collider.GetComponentInParent<EntityPlayer>(), player)) continue;
            var candidate = collider.GetComponentInParent<EntityVehicle>();
            if (!Allowed(candidate, player)) return false; // The first solid object owns the focus.
            vehicle = candidate; return true;
        }
        return false;
    }
    internal static string Prompt(EntityVehicle vehicle, EntityPlayerLocal player)
    {
        var action = RebirthNativeControls.Actions?.ToggleCamperMode;
        if (action == null || !Allowed(vehicle, player)) return null;
        string activate = player.playerInput.Activate.GetBindingXuiMarkupString()
            + player.playerInput.PermanentActions.Activate.GetBindingXuiMarkupString();
        return string.Format(Localization.Get("xuiRebirthCamperModeShortcutPrompt"), action.GetBindingXuiMarkupString(), activate);
    }
    internal static bool Consume(EntityPlayerLocal player)
    {
        if (player == null) return false;
        if (consumedFrame == Time.frameCount && consumedActor == player.entityId) return true;
        var action = RebirthNativeControls.Actions?.ToggleCamperMode;
        if (action == null || !RebirthNativeControls.Actions.Enabled || !action.WasPressed || !TryFocused(player, out var vehicle)) return false;
        consumedFrame = Time.frameCount; consumedActor = player.entityId;
        Request(vehicle, player);
        return true;
    }
    internal static void Request(EntityVehicle vehicle, EntityPlayerLocal player)
    {
        if (!Allowed(vehicle, player) || !RebirthCamperEnergyRuntime.TryObservePresentation(vehicle, out var mode, out long revision, out _, out _)) return;
        XUiC_RebirthCamperTransitionHud.Track(vehicle, player);
        if (mode != RebirthCamperEnergyMode.Travel && mode != RebirthCamperEnergyMode.Camp)
        { GameManager.ShowTooltip(player, Localization.Get("xuiRebirthCamperModeTransitionBusy")); return; }
        var target = mode == RebirthCamperEnergyMode.Travel ? RebirthCamperEnergyMode.Camp : RebirthCamperEnergyMode.Travel;
        if (!vehicle.world.IsRemote()) { RebirthCamperEnergyAccess.ExecuteModeShortcut(vehicle, player, target, revision); return; }
        var item = vehicle.vehicle.GetUpdatedItemValue();
        if (!item.TryGetMetadata("RebirthCamperIdentity", out string text) || !Guid.TryParse(text, out var identity)) return;
        string creation = RebirthCamperCharacterAccess.CurrentCreation(player);
        if (string.IsNullOrEmpty(creation) || !Guid.TryParse(GamePrefs.GetString(EnumGamePrefs.GameGuidClient), out var worldId)) return;
        var connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (connection == null || !connection.IsClient || connection.IsServer || !PackageAvailable()) return;
        try
        {
            connection.SendToServer(NetPackageManager.GetPackage<NetPackageRebirthCamperModeShortcutV1>().Setup(
                player.entityId, vehicle.entityId, identity, worldId, creation, revision, mode, ++sequence), false);
        }
        catch { /* Unsupported negotiated packet: no local mode change. */ }
    }
    private static bool PackageAvailable()
    { try { NetPackageManager.GetPackageId(typeof(NetPackageRebirthCamperModeShortcutV1)); return true; } catch { return false; } }
    internal static void Receive(World world, ClientInfo sender, int actor, int entity, Guid identity, Guid worldId,
        string creation, long revision, RebirthCamperEnergyMode expected, long serial)
    {
        if (!ThreadManager.IsMainThread() || world == null || world.IsRemote()
            || !ReferenceEquals(GameManager.Instance?.World, world) || SingletonMonoBehaviour<ConnectionManager>.Instance?.IsServer != true
            || sender?.entityId != actor || sender.InternalId == null || identity == Guid.Empty || string.IsNullOrEmpty(creation)
            || revision < 0 || serial <= 0 || (expected != RebirthCamperEnergyMode.Travel && expected != RebirthCamperEnergyMode.Camp)
            || !Guid.TryParse(world.worldState?.Guid, out var currentWorld) || worldId != currentWorld
            || !ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance.Clients.ForEntityId(actor), sender)) return;
        var player = world.GetEntity(actor) as EntityPlayer; var vehicle = world.GetEntity(entity) as EntityVehicle;
        if (!Allowed(vehicle, player) || RebirthCamperCharacterAccess.CurrentCreation(player) != creation
            || !vehicle.vehicle.GetUpdatedItemValue().TryGetMetadata("RebirthCamperIdentity", out string text)
            || !Guid.TryParse(text, out var currentIdentity) || currentIdentity != identity
            || !RebirthCamperEnergyRuntime.TryObservePresentation(vehicle, out var mode, out long currentRevision, out _, out _)
            || mode != expected || currentRevision != revision) return;
        // Existing owner-only RequestMode rechecks authoritative custody and seat/
        // parking requirements; no client projection is accepted as mode authority.
        var seen = sequences.GetValue(sender, _ => new PeerSequence());
        if (ReferenceEquals(seen.World, world) && serial <= seen.Sequence) return;
        seen.World = world; seen.Sequence = serial;
        RebirthCamperEnergyAccess.ExecuteModeShortcut(vehicle, player,
            mode == RebirthCamperEnergyMode.Travel ? RebirthCamperEnergyMode.Camp : RebirthCamperEnergyMode.Travel, revision);
    }
}

[HarmonyPatch(typeof(PlayerMoveController), "Update")]
internal static class RebirthCamperModeInputTick
{ private static void Prefix(PlayerMoveController __instance) => RebirthCamperModeInput.Consume(__instance.entityPlayerLocal); }

// Native Map can run before or after the movement dispatcher. Handle either
// ordering, consuming only the same targeted camper shortcut press/frame.
[HarmonyPatch(typeof(XUiC_WindowSelector), nameof(XUiC_WindowSelector.OpenSelectorAndWindow))]
internal static class RebirthCamperModeInputMap
{
    private static bool Prefix(EntityPlayerLocal __0, string __1)
        => __1 != "map" || !RebirthCamperModeInput.Consume(__0);
}

[HarmonyPatch(typeof(PlayerMoveController), "HandleInteraction")]
internal static class RebirthCamperModeInputPrompt
{
    [HarmonyPriority(Priority.Last)]
    [HarmonyAfter("rebirth.camper.physical.interaction")]
    private static void Postfix(PlayerMoveController __instance)
    {
        var player = __instance.entityPlayerLocal;
        if (!RebirthCamperModeInput.TryFocused(player, out var vehicle)) return;
        string suffix = RebirthCamperModeInput.Prompt(vehicle, player);
        if (string.IsNullOrEmpty(suffix)) return;
        string current = __instance.strTextLabelPointingTo;
        if (!string.IsNullOrEmpty(current) && current.IndexOf(suffix, StringComparison.Ordinal) >= 0) return;
        string text = string.IsNullOrEmpty(current) ? suffix : current + "\n" + suffix;
        XUiC_InteractionPrompt.SetText(__instance.playerUI, text);
        __instance.strTextLabelPointingTo = text;
    }
}

[Preserve]
public sealed class NetPackageRebirthCamperModeShortcutV1 : NetPackage
{
    private int actor, entity; private Guid identity, worldId; private string creation;
    private long revision, sequence; private RebirthCamperEnergyMode mode;
    public override NetPackageDirection PackageDirection => NetPackageDirection.ToServer;
    internal NetPackageRebirthCamperModeShortcutV1 Setup(int a, int e, Guid i, Guid w, string c, long r, RebirthCamperEnergyMode m, long s)
    { actor = a; entity = e; identity = i; worldId = w; creation = c; revision = r; mode = m; sequence = s; return this; }
    public override void read(PooledBinaryReader reader)
    {
        if (reader.ReadByte() != 1) throw new InvalidDataException("Camper mode shortcut version changed.");
        actor = reader.ReadInt32(); entity = reader.ReadInt32(); identity = new Guid(reader.ReadBytes(16)); worldId = new Guid(reader.ReadBytes(16));
        int count = reader.ReadUInt16(); if (count > 2048) throw new InvalidDataException("Camper mode identity too large.");
        var bytes = reader.ReadBytes(count); if (bytes.Length != count) throw new EndOfStreamException();
        creation = new UTF8Encoding(false, true).GetString(bytes); revision = reader.ReadInt64(); mode = (RebirthCamperEnergyMode)reader.ReadByte(); sequence = reader.ReadInt64();
    }
    public override void write(PooledBinaryWriter writer)
    {
        base.write(writer); writer.Write((byte)1); writer.Write(actor); writer.Write(entity); writer.Write(identity.ToByteArray()); writer.Write(worldId.ToByteArray());
        var bytes = Encoding.UTF8.GetBytes(creation ?? string.Empty); if (bytes.Length > 2048) throw new InvalidDataException("Camper mode identity too large.");
        writer.Write((ushort)bytes.Length); writer.Write(bytes); writer.Write(revision); writer.Write((byte)mode); writer.Write(sequence);
    }
    public override void ProcessPackage(World world, GameManager callbacks)
    { if (ValidEntityIdForSender(actor)) RebirthCamperModeInput.Receive(world, Sender, actor, entity, identity, worldId, creation, revision, mode, sequence); }
    public int GetLength() => 0;
}
