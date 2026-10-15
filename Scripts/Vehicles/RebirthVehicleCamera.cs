using System;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;

// Local view choice for every native vehicle rider. Seat membership, walking
// exclusion, controls, pose, networking and vehicle ownership remain native.
public static class RebirthVehicleCamera
{
    private sealed class View
    {
        internal EntityVehicle Vehicle;
        internal Transform Anchor, Model;
        internal Quaternion SeatRotation;
        internal int Seat = -1, LastToggleFrame = -1;
        internal float AnchorYaw;
        internal bool FirstPerson, Active;
    }
    internal struct SteeringScope { internal bool Active, TurnTowardsLook; }
    internal struct CameraScope { internal bool Active, Collision; }
    private static ConditionalWeakTable<EntityPlayerLocal, View> views = new ConditionalWeakTable<EntityPlayerLocal, View>();
    private static bool installed;
    public static void Install()
    {
        if (installed) return;
        var harmony = new Harmony("rebirth.vehicle.camera.v1");
        harmony.CreateClassProcessor(typeof(RebirthVehicleCameraInput)).Patch();
        harmony.CreateClassProcessor(typeof(RebirthVehicleCameraAttach)).Patch();
        harmony.CreateClassProcessor(typeof(RebirthVehicleCameraDetach)).Patch();
        harmony.CreateClassProcessor(typeof(RebirthVehicleCameraLate)).Patch();
        harmony.CreateClassProcessor(typeof(RebirthVehicleCameraPhysics)).Patch();
        harmony.CreateClassProcessor(typeof(RebirthVehicleCameraTurnInput)).Patch();
        harmony.CreateClassProcessor(typeof(RebirthVehicleCameraHeading)).Patch();
        ModEvents.WorldShuttingDown.RegisterHandler(Shutdown);
        installed = true;
    }
    private static void Shutdown(ref ModEvents.SWorldShuttingDownData data)
        => views = new ConditionalWeakTable<EntityPlayerLocal, View>();
    private static bool Rider(EntityPlayerLocal player, out EntityVehicle vehicle)
    {
        vehicle = player?.AttachedToEntity as EntityVehicle;
        return ThreadManager.IsMainThread() && !GameManager.IsDedicatedServer && player != null && !player.IsDead()
            && vehicle?.vehicle != null && vehicle.world != null && !vehicle.IsDead()
            && ReferenceEquals(GameManager.Instance?.World, vehicle.world) && ReferenceEquals(player.world, vehicle.world)
            && ReferenceEquals(vehicle.world.GetEntity(player.entityId), player)
            && ReferenceEquals(vehicle.world.GetEntity(vehicle.entityId), vehicle) && vehicle.FindAttachSlot(player) >= 0
            && player.vp_FPCamera != null && player.vp_FPController != null && player.vp_FPController.Player.Driving.Active;
    }
    private static bool CameraPermitted()
        => GameManager.Instance != null && (GameManager.Instance.IsEditMode() || GameStats.GetInt(EnumGameStats.CameraRestrictionMode) == 0);
    private static Transform Head(EntityPlayerLocal player)
    {
        var model = player.emodel;
        var head = model?.GetHeadTransform();
        // EModelBase returns its model transform when no head bone exists. That
        // is not an authored seated eye and must not become a guessed camera.
        return model != null && head != null && head != model.transform ? head : null;
    }
    private static bool Bind(EntityPlayerLocal player, EntityVehicle vehicle, View view)
    {
        int seat = vehicle.FindAttachSlot(player);
        if (view.Vehicle == vehicle && view.Seat == seat && view.Model == vehicle.ModelTransform && view.Anchor != null) return true;
        var info = vehicle.GetAttachedToInfo(seat);
        if (info?.enterParentTransform == null) return false;
        view.Vehicle = vehicle; view.Seat = seat; view.Model = vehicle.ModelTransform;
        view.Anchor = info.enterParentTransform; view.SeatRotation = Quaternion.Euler(info.enterRotation);
        return true;
    }
    private static float SeatYaw(View view) => (view.Anchor.rotation * view.SeatRotation).eulerAngles.y;
    private static void SetNativeView(EntityPlayerLocal player, bool first)
    {
        // Native IsFirstPerson's camera event rotates its Parent (the player
        // root). Preserve the seat's actual orientation around this view event.
        var root = player.RootTransform;
        Quaternion rotation = root != null ? root.rotation : Quaternion.identity;
        try { if (player.bFirstPersonView != first) player.SetFirstPersonView(first, true); }
        finally { if (root != null) root.rotation = rotation; }
    }
    private static void Apply(EntityPlayerLocal player, EntityVehicle vehicle, View view)
    {
        if (!Bind(player, vehicle, view)) return;
        bool first = view.FirstPerson && CameraPermitted() && Head(player) != null;
        SetNativeView(player, first);
        view.Active = true; view.AnchorYaw = SeatYaw(view);
        if (first) { player.vp_FPCamera.Yaw = view.AnchorYaw; player.vp_FPCamera.Pitch = 0; }
    }
    internal static void Attached(EntityPlayerLocal player)
    {
        if (!Rider(player, out var vehicle)) return;
        var view = views.GetValue(player, _ => new View());
        Apply(player, vehicle, view);
    }
    internal static void Detached(EntityPlayerLocal player)
    {
        if (!views.TryGetValue(player, out var view)) return;
        view.Active = false; view.Vehicle = null; view.Anchor = null; view.Model = null; view.Seat = -1;
        // Native Detach restores the on-foot preference, which this feature
        // never changes. Retain only the rider's session-local vehicle choice.
    }
    internal static void Poll(PlayerMoveController controller)
    {
        var player = controller.entityPlayerLocal;
        if (!Rider(player, out var vehicle)) { if (player != null) Detached(player); return; }
        var view = views.GetValue(player, _ => new View());
        if (!view.Active || view.Vehicle != vehicle) Apply(player, vehicle, view);
        var actions = RebirthNativeControls.Actions;
        var windows = player.PlayerUI?.windowManager;
        if (!controller.bAllowPlayerInput || actions == null || !actions.Enabled || actions.ToggleVehicleCamera == null
            || !actions.ToggleVehicleCamera.WasPressed || windows == null || windows.IsCursorWindowOpen()
            || windows.IsModalWindowOpen() || RebirthConsoleInputGuardRuntime.BlocksGameplayInput()
            || view.LastToggleFrame == Time.frameCount) return;
        view.LastToggleFrame = Time.frameCount;
        if (!CameraPermitted()) { GameManager.ShowTooltip(player, Localization.Get("ttCameraRestricted")); return; }
        if (!view.FirstPerson && Head(player) == null)
        { GameManager.ShowTooltip(player, Localization.Get("xuiRebirthVehicleCameraUnavailable")); return; }
        view.FirstPerson = !view.FirstPerson;
        Apply(player, vehicle, view);
    }
    private static bool Active(EntityPlayerLocal player, out View view)
    {
        view = null;
        return Rider(player, out var vehicle) && views.TryGetValue(player, out view)
            && view.Active && view.FirstPerson && player.bFirstPersonView && view.Vehicle == vehicle;
    }
    internal static bool Active(EntityVehicle vehicle)
    {
        var player = vehicle?.GetAttachedPlayerLocal();
        return player != null && Active(player, out _) && ReferenceEquals(player.AttachedToEntity, vehicle);
    }
    internal static CameraScope BeforeCamera(vp_FPCamera camera)
    {
        var player = camera.FPController?.localPlayer;
        if (!Active(player, out var view)) return default;
        if (Head(player) == null || !Bind(player, view.Vehicle, view))
        {
            view.FirstPerson = false;
            SetNativeView(player, false);
            return default;
        }
        float yaw = SeatYaw(view);
        camera.Yaw += Mathf.DeltaAngle(view.AnchorYaw, yaw); view.AnchorYaw = yaw;
        camera.DrivingPosition = Head(player).position;
        // The native first-person collision probe starts at the standing capsule
        // and can pull a seated camera out of the cab. Scope it to this one view
        // update; native player/vehicle collision and third-person probes remain.
        var scope = new CameraScope { Active = true, Collision = camera.HasCollision };
        camera.HasCollision = false;
        return scope;
    }
    internal static void AfterCamera(vp_FPCamera camera, CameraScope scope)
    {
        if (!scope.Active) return;
        var player = camera.FPController?.localPlayer;
        if (!Active(player, out _)) return;
        var head = Head(player); if (head == null) return;
        // Keep native yaw/pitch/free-look rotation and use the real posed seat
        // eye instead of the standing spring offset or vehicle-center chase point.
        camera.transform.position = head.position;
        camera.DrivingPosition = head.position;
        camera.LookPoint = camera.GetLookPoint();
    }
    internal static void RestoreCamera(vp_FPCamera camera, CameraScope scope)
    { if (scope.Active) camera.HasCollision = scope.Collision; }
    internal static SteeringScope BeforePhysics(EntityVehicle vehicle)
    {
        var player = vehicle?.GetAttachedPlayerLocal();
        if (!Active(vehicle) || !ReferenceEquals(vehicle.GetAttached(0), player)) return default;
        var scope = new SteeringScope { Active = true, TurnTowardsLook = EntityVehicle.isTurnTowardsLook };
        EntityVehicle.isTurnTowardsLook = false;
        return scope;
    }
    internal static SteeringScope BeforeTurnInput(EntityVehicle vehicle, EntityPlayerLocal player)
        => Active(vehicle) && ReferenceEquals(vehicle.GetAttached(0), player)
            ? new SteeringScope { Active = true, TurnTowardsLook = EntityVehicle.isTurnTowardsLook } : default;
    internal static void RestoreSteering(SteeringScope scope)
    { if (scope.Active) EntityVehicle.isTurnTowardsLook = scope.TurnTowardsLook; }
}

[HarmonyPatch(typeof(PlayerMoveController), "Update")]
internal static class RebirthVehicleCameraInput
{ private static void Prefix(PlayerMoveController __instance) => RebirthVehicleCamera.Poll(__instance); }

[HarmonyPatch(typeof(EntityPlayerLocal), nameof(EntityPlayerLocal.AttachToEntity))]
internal static class RebirthVehicleCameraAttach
{ private static void Postfix(EntityPlayerLocal __instance, int __result) { if (__result >= 0) RebirthVehicleCamera.Attached(__instance); } }

[HarmonyPatch(typeof(EntityPlayerLocal), nameof(EntityPlayerLocal.Detach))]
internal static class RebirthVehicleCameraDetach
{ private static void Prefix(EntityPlayerLocal __instance) => RebirthVehicleCamera.Detached(__instance); }

[HarmonyPatch(typeof(vp_FPCamera), "LateUpdate")]
internal static class RebirthVehicleCameraLate
{
    private static void Prefix(vp_FPCamera __instance, out RebirthVehicleCamera.CameraScope __state)
        => __state = RebirthVehicleCamera.BeforeCamera(__instance);
    private static void Postfix(vp_FPCamera __instance, RebirthVehicleCamera.CameraScope __state)
        => RebirthVehicleCamera.AfterCamera(__instance, __state);
    private static void Finalizer(vp_FPCamera __instance, RebirthVehicleCamera.CameraScope __state)
        => RebirthVehicleCamera.RestoreCamera(__instance, __state);
}

[HarmonyPatch(typeof(EntityVehicle), nameof(EntityVehicle.PhysicsFixedUpdate))]
internal static class RebirthVehicleCameraPhysics
{
    private static void Prefix(EntityVehicle __instance, out RebirthVehicleCamera.SteeringScope __state)
        => __state = RebirthVehicleCamera.BeforePhysics(__instance);
    private static void Finalizer(RebirthVehicleCamera.SteeringScope __state) => RebirthVehicleCamera.RestoreSteering(__state);
}

[HarmonyPatch(typeof(EntityVehicle), nameof(EntityVehicle.MoveByAttachedEntity), typeof(EntityPlayerLocal))]
internal static class RebirthVehicleCameraTurnInput
{
    private static void Prefix(EntityVehicle __instance, EntityPlayerLocal __0, out RebirthVehicleCamera.SteeringScope __state)
        => __state = RebirthVehicleCamera.BeforeTurnInput(__instance, __0);
    private static void Finalizer(RebirthVehicleCamera.SteeringScope __state) => RebirthVehicleCamera.RestoreSteering(__state);
}

// Native heading correction targets only the first locally attached rider and
// assumes the chase camera. Seat-relative yaw above already handles this view.
[HarmonyPatch(typeof(EntityVehicle), nameof(EntityVehicle.CameraChangeRotation))]
internal static class RebirthVehicleCameraHeading
{ private static bool Prefix(EntityVehicle __instance) => !RebirthVehicleCamera.Active(__instance); }
