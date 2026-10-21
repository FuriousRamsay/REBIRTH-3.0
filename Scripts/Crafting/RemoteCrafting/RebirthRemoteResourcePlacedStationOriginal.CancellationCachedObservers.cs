using HarmonyLib;

internal static partial class RebirthRemoteResourcePlacedStationOriginal
{
    private sealed class CancellationCachedPurposes
    {internal object Cancellation,Cached,Recycler,Earned,ActionPayment,Source4,InitialQueue,RecipeOutput;internal bool CachedReadObserved,RecyclerReadObserved,EarnedReadObserved,Source4ReadObserved,InitialQueueReadObserved,RecipeOutputReadObserved;}
    internal static void InstallActionPaymentNativeRegionObservers()
    {
        QualifyCancellationNativeLifecycle();
        var harmony=new Harmony("rebirth.remote.placed.cancellation.native.reconstruction");
        RebirthHarmonyBootstrap.PatchClassOnce(harmony,typeof(CancellationRegionManager));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony,typeof(CancellationLoad));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony,typeof(CancellationLoadStream));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony,typeof(CancellationInstantiate));
    }
    // Observation infrastructure only. Installing B's purpose must not mark
    // the full cancellation transport installed or activate any C effect.
    internal static void InstallCompletionCachedNativeLoadObservers()
    {
        QualifyCancellationNativeLifecycle();
        var harmony=new Harmony("rebirth.remote.placed.cancellation.native.reconstruction");
        RebirthHarmonyBootstrap.PatchClassOnce(harmony,typeof(CancellationLoad));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony,typeof(CancellationLoadStream));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony,typeof(CancellationInstantiate));
        RebirthHarmonyBootstrap.PatchClassOnce(new Harmony("rebirth.remote.placed.accepted.native.reader"),typeof(AcceptedNativeRead));
    }
}
