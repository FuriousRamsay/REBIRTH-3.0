using HarmonyLib;

public static partial class RemoteResourceTransactions
{
    private static bool actionPaymentObserversInstalled;
    internal static void InstallActionPaymentNativeObservers()
    {
        if(actionPaymentObserversInstalled)return;
        if(!ThreadManager.IsMainThread())throw new System.InvalidOperationException("Action payment native observers require main thread installation");
        if(actionClientReadAllocation==null&&!RebirthProductiveEarnedObservationBudget.TryRetainDetached(12L*1024*1024,out actionClientReadAllocation))
            throw new System.InvalidOperationException("Action payment bounded native read allocation unavailable");
        RebirthDogLifecycleService.InstallActionPaymentNativeOwnerLoadObservers();
        RebirthRemoteResourcePlacedStationOriginal.InstallActionPaymentNativeRegionObservers();
        if(!RebirthCamperEnergyMaintenanceMarkers.InstallNativeReadObserver())throw new System.InvalidOperationException("Powered native read observation unavailable");
        RebirthHarmonyBootstrap.PatchClassOnce(new Harmony("rebirth.action-payment.native-target-read"),typeof(ActionPaymentDerivedPoweredRead));
        // Install the existing sole native read/save/mutation observers before
        // any purpose source can hold an inventory or invoke a native setter.
        // Electrical input uses this payment owner; crafting/trade retain their
        // own distinct protocols and originals.
        RebirthRemoteResourceSourceCustody.Install();
        RebirthProductiveEarnedUiOwnerFile.Install();
        var harmony=new Harmony("rebirth.camper.native.actor-payment");
        RebirthHarmonyBootstrap.PatchClassOnce(harmony,typeof(RebirthCamperNativeActorMutationGuard));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony,typeof(RebirthCamperNativeActorCursorGuard));
        RebirthTeachingOutcomeStore.RegisterReceiptProtection(ProtectActionTrainingReceipts);
        actionPaymentObserversInstalled=true;
    }
}
