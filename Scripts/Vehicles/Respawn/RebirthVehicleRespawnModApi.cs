using UnityEngine.Scripting;

#nullable disable

/// <summary>
/// Binds the concrete carsRandomHelper targets only after blocks and placeholder maps
/// are fully populated. The server then has a direct marker downgrade on every block
/// that the native vehicle respawner can produce.
/// </summary>
[Preserve]
public sealed class RebirthVehicleRespawnModApi : IModApi
{
    private static bool initialized;

    public void InitMod(Mod modInstance)
    {
        if (initialized)
            return;

        initialized = true;
        ModEvents.GameStartDone.RegisterHandler(
            new ModEvents.ModEventHandlerDelegate<ModEvents.SGameStartDoneData>(OnGameStartDone));
    }

    private static void OnGameStartDone(ref ModEvents.SGameStartDoneData data)
    {
        RebirthVehicleRespawnRuntimeRegistry.BindingReport report =
            RebirthVehicleRespawnRuntimeRegistry.BindCarsRandomHelperTargets();

        if (!report.MarkerResolved || !report.HelperResolved || !report.PlaceholderResolved ||
            report.MissingTargetBlocks > 0 || report.WrongAfterBinding > 0)
            Log.Warning("[REBIRTH Vehicle Respawn] runtime helper binding " + report);
        else if (RebirthLogSettings.RuntimeInstallLoggingEnabled)
            Log.Out("[REBIRTH Vehicle Respawn] runtime helper binding " + report);
    }
}
