using System;
using System.Collections.Generic;

// These named owners already require positive emptiness in their typed
// preparation. Admission rechecks their incoming private owners after detach;
// it never interprets an opaque nonempty domain as an empty continuation.
internal static class RebirthNpcEmptyNativeAdoption
{
    internal static RebirthNpcRuntimeContinuationAdoption Capture(RebirthNpcSaveQuarantineScope scope,
        RebirthNpcSaveQuarantineDomain[] domains)
    {
        var owners=new Dictionary<string,Func<bool>>(StringComparer.Ordinal)
        {
            {"work-navigation-empty",()=>RebirthNpcWorkNavigationService.CanRetireEmptyNavigation},
            {"work-resolution-empty",()=>RebirthNpcWorkExecutorRegistry.CanRetireEmptyResolution},
            {"work-targets-empty",()=>RebirthNpcWorkTargetRegistry.CanRetireEmptyTargets},
            {"guard-packages-empty",()=>RebirthNpcGuardDutyWorkPackageService.CanRetireEmptyGuardPackage},
            {"medical-packages-empty",()=>RebirthNpcMedicalTreatmentWorkPackageService.CanRetireEmptyMedicalPackage},
            {"guard-native-empty",()=>RebirthNpcGuardPostWorkAdapter.CanRetireEmptyNative},
            {"medical-native-empty",()=>RebirthNpcMedicalTreatmentAdapter.CanRetireEmptyNative},
            {"work-reservations-empty",()=>RebirthNpcWorkReservationService.CanRetireEmptyReservations},
            {"work-assignment-controller-empty",()=>RebirthNpcWorkAssignmentCoordinator.CanRetireEmptyAssignmentCoordinator},
            {"workstation-jobs-empty",()=>RebirthNpcWorkstationExecutionService.CanRetireEmptyWorkstations}
        };
        var captured=new List<RebirthNpcSaveQuarantineDomain>();
        foreach(var domain in domains)if(domain!=null&&owners.ContainsKey(domain.Name))captured.Add(domain);
        if(captured.Count!=owners.Count)return null;
        bool Current()
        {
            foreach(var domain in captured)if(!domain.IsCurrent||!owners[domain.Name]())return false;
            return true;
        }
        if(!Current())return null;
        bool Adopt(World incoming,Dictionary<RebirthNpcStableId,RebirthNpcRuntimeNativeAdoption.NativeReentry> native)
        {
            foreach(var domain in captured)if(!domain.IsDetached||!owners[domain.Name]())return false;
            return ReferenceEquals(incoming,GameManager.Instance?.World)&&incoming?.IsRemote()==false;
        }
        return new RebirthNpcRuntimeContinuationAdoption(scope,new HashSet<RebirthNpcStableId>(),Current,Adopt);
    }
}
