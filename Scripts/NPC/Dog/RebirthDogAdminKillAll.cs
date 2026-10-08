using UnityEngine.Scripting;

#nullable disable

/// <summary>
/// Compatibility shell for the retired v230-v234 administrative killall special case.
/// 2.6 did not erase hired ownership for killall; all lethal dog damage now flows through
/// RebirthDogLifecycleService's non-terminal AwaitingRespawn path. Keep this no-op type so
/// older bootstraps cannot accidentally re-enable the destructive Harmony patch.
/// </summary>
[Preserve]
public static class RebirthDogAdminKillAllInstaller
{
    public static void Install() { }
}
