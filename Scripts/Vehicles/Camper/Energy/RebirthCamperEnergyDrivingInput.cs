using System;
using HarmonyLib;
using System.Runtime.CompilerServices;

// Camp keeps native seat admission, camera, horn, lights and exit controls.
// Suppress only vehicle driving input, on both the authority and remote driver.
internal static class RebirthCamperEnergyDrivingInput
{
    private sealed class Notice { internal float Next; }
    private static readonly ConditionalWeakTable<EntityPlayerLocal,Notice> notices = new ConditionalWeakTable<EntityPlayerLocal,Notice>();
    internal static void NotifyAttempt(EntityVehicle vehicle, EntityPlayerLocal player)
    {
        if (player == null || !RebirthCamperRadial.IsDriver(vehicle, player)
            || RebirthConsoleInputGuardRuntime.BlocksGameplayInput() || !Blocked(vehicle)) return;
        var ui = LocalPlayerUI.GetUIForPlayer(player);
        if (ui?.playerInput == null || ui.windowManager.IsCursorWindowOpen() || ui.windowManager.IsModalWindowOpen()) return;
        var move = ui.playerInput.VehicleActions.Move;
        if (Math.Abs(move.X) < .05f && Math.Abs(move.Y) < .05f) return;
        var notice = notices.GetValue(player, _ => new Notice());
        float now = UnityEngine.Time.realtimeSinceStartup;
        if (now < notice.Next) return;
        notice.Next = now + 3f;
        GameManager.ShowTooltip(player, Localization.Get("xuiRebirthCamperDriveCampBlocked"));
    }
    internal static bool Blocked(EntityVehicle vehicle)
        =>RebirthCamperEnergyRuntime.TryObserveMode(vehicle,out var mode,out _,out _)
            &&mode!=RebirthCamperEnergyMode.Travel;

    internal static void Clear(EntityVehicle vehicle)
    {
        var input=vehicle.movementInput;
        if(input!=null)
        {
            input.moveForward=0f;input.moveStrafe=0f;
            input.running=false;input.jump=false;input.down=false;
        }
        vehicle.wheelMotor=0f;vehicle.wheelDir=0f;vehicle.motorTorque=0f;
        vehicle.vehicle.CurrentIsAccel=false;
        vehicle.vehicle.CurrentMotorTorquePercent=0f;
        vehicle.vehicle.CurrentSteeringPercent=0f;
    }
}

[HarmonyPatch(typeof(EntityVehicle),nameof(EntityVehicle.MoveByAttachedEntity),typeof(EntityPlayerLocal))]
internal static class RebirthCamperEnergyDrivingInputRead
{
    private static void Prefix(EntityVehicle __instance, EntityPlayerLocal __0)
        => RebirthCamperEnergyDrivingInput.NotifyAttempt(__instance, __0);
    // Do not skip the native body: it also owns camera rotation/zoom and the
    // driver's turn-mode preference. Never clear the player's shared input.
    private static void Postfix(EntityVehicle __instance)
    {if(RebirthCamperEnergyDrivingInput.Blocked(__instance))RebirthCamperEnergyDrivingInput.Clear(__instance);}
}

[HarmonyPatch(typeof(EntityVehicle),nameof(EntityVehicle.PhysicsFixedUpdate))]
internal static class RebirthCamperEnergyDrivingInputPhysics
{
    private static void Prefix(EntityVehicle __instance,out MovementInput __state)
    {
        __state=null;
        if(!RebirthCamperEnergyDrivingInput.Blocked(__instance))return;
        RebirthCamperEnergyDrivingInput.Clear(__instance);
        __state=__instance.movementInput;
        // Native physics still executes. Its existing no-input path prevents
        // torque, hop forces and look-to-steer without changing camera settings,
        // attachment, Rigidbody ownership, simulation or collision lifecycle.
        __instance.movementInput=null;
    }
    private static void Postfix(EntityVehicle __instance,MovementInput __state)
    {Restore(__instance,__state);}
    private static Exception Finalizer(EntityVehicle __instance,MovementInput __state,Exception __exception)
    {Restore(__instance,__state);return __exception;}
    private static void Restore(EntityVehicle vehicle,MovementInput input)
    {
        if(input!=null&&vehicle.movementInput==null)vehicle.movementInput=input;
        if(RebirthCamperEnergyDrivingInput.Blocked(vehicle))RebirthCamperEnergyDrivingInput.Clear(vehicle);
    }
}
