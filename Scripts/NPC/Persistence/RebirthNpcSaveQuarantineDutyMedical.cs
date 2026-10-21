using System;
using System.Collections.Generic;

// Native-held package rows are opaque. No callback, cancellation or restore is
// invoked to manufacture emptiness; foreign/unbound nonempty state is retained.
public static partial class RebirthNpcGuardDutyWorkPackageService
{
    private static RebirthNpcAncillaryOriginalScope packageScope;
    private static bool UseGuardPackageScope(){if(!RebirthNpcWorkReleaseGate.Enabled)return false;lock(Sync)return RebirthNpcAncillaryOriginalScope.Use(ref packageScope,Active.Count==0&&Schedule.Count==0);}
    internal static bool CanRetireEmptyGuardPackage{get{lock(Sync)return ThreadManager.IsMainThread()&&Active.Count==0&&Schedule.Count==0&&RebirthNpcWorkCallbackObservation.IsIdle;}}
    internal static bool TryRetireEmptyObservation(){lock(Sync){if(!CanRetireEmptyGuardPackage)return false;packageScope=null;return true;}}
    internal static RebirthNpcSaveQuarantineDomain PrepareHeldQuarantine(RebirthNpcSaveQuarantineScope scope)
    {
        lock(Sync)
        {
            RebirthNpcAncillaryOriginalScope.RequireScope(scope,packageScope);if(!CanRetireEmptyGuardPackage)return CapturePackageCustody(scope).Domain;
            var records=Active;var original=packageScope;var order=Schedule;var nextOrder=new RebirthNpcFairRoundRobin<Guid>();long c0=requests;long c1=created;long c2=started;long c3=rejected;long c4=reconciliations;long c5=completed;long c6=failed;var doc=RebirthNpcAncillaryOriginalScope.Image("heldWorkEmpty",original,new[]{requests,created,started,rejected,reconciliations,completed,failed});doc.DocumentElement.SetAttribute("emptyOnly","1");var next=new Dictionary<Guid,ActivePackage>();
            return new RebirthNpcSaveQuarantineDomain("guard-packages-empty",doc,
                ()=>{lock(Sync)return CanRetireEmptyGuardPackage&&ReferenceEquals(Active,records)&&ReferenceEquals(packageScope,original)&&ReferenceEquals(Schedule,order)&&requests==c0&&created==c1&&started==c2&&rejected==c3&&reconciliations==c4&&completed==c5&&failed==c6;},
                ()=>{lock(Sync){Active=next;packageScope=null;Schedule=nextOrder;requests=created=started=rejected=reconciliations=completed=failed=0;}},
                ()=>{lock(Sync){Active=records;packageScope=original;Schedule=order;requests=c0;created=c1;started=c2;rejected=c3;reconciliations=c4;completed=c5;failed=c6;}});
        }
    }
}
public static partial class RebirthNpcMedicalTreatmentWorkPackageService
{
    private static RebirthNpcAncillaryOriginalScope packageScope;
    private static bool UseMedicalPackageScope(){if(!RebirthNpcWorkReleaseGate.Enabled)return false;lock(Sync)return RebirthNpcAncillaryOriginalScope.Use(ref packageScope,Active.Count==0&&Schedule.Count==0);}
    internal static bool CanRetireEmptyMedicalPackage{get{lock(Sync)return ThreadManager.IsMainThread()&&Active.Count==0&&Schedule.Count==0&&RebirthNpcWorkCallbackObservation.IsIdle;}}
    internal static bool TryRetireEmptyObservation(){lock(Sync){if(!CanRetireEmptyMedicalPackage)return false;packageScope=null;return true;}}
    internal static RebirthNpcSaveQuarantineDomain PrepareHeldQuarantine(RebirthNpcSaveQuarantineScope scope)
    {
        lock(Sync)
        {
            RebirthNpcAncillaryOriginalScope.RequireScope(scope,packageScope);if(!CanRetireEmptyMedicalPackage)return CapturePackageCustody(scope).Domain;
            var records=Active;var original=packageScope;var order=Schedule;var nextOrder=new RebirthNpcFairRoundRobin<Guid>();long c0=requests;long c1=created;long c2=started;long c3=rejected;long c4=reconciliations;long c5=completed;long c6=failed;var doc=RebirthNpcAncillaryOriginalScope.Image("heldWorkEmpty",original,new[]{requests,created,started,rejected,reconciliations,completed,failed});doc.DocumentElement.SetAttribute("emptyOnly","1");var next=new Dictionary<Guid,ActivePackage>();
            return new RebirthNpcSaveQuarantineDomain("medical-packages-empty",doc,
                ()=>{lock(Sync)return CanRetireEmptyMedicalPackage&&ReferenceEquals(Active,records)&&ReferenceEquals(packageScope,original)&&ReferenceEquals(Schedule,order)&&requests==c0&&created==c1&&started==c2&&rejected==c3&&reconciliations==c4&&completed==c5&&failed==c6;},
                ()=>{lock(Sync){Active=next;packageScope=null;Schedule=nextOrder;requests=created=started=rejected=reconciliations=completed=failed=0;}},
                ()=>{lock(Sync){Active=records;packageScope=original;Schedule=order;requests=c0;created=c1;started=c2;rejected=c3;reconciliations=c4;completed=c5;failed=c6;}});
        }
    }
}
public sealed partial class RebirthNpcGuardPostWorkAdapter
{
    private static RebirthNpcAncillaryOriginalScope nativeScope;
    private static bool UseNativeScope(){if(!RebirthNpcWorkReleaseGate.Enabled)return false;lock(Sync)return RebirthNpcAncillaryOriginalScope.Use(ref nativeScope,States.Count==0);}
    internal static bool CanRetireEmptyNative{get{lock(Sync)return ThreadManager.IsMainThread()&&States.Count==0&&RebirthNpcWorkCallbackObservation.IsIdle;}}
    internal static bool TryRetireEmptyObservation(){lock(Sync){if(!CanRetireEmptyNative)return false;nativeScope=null;return true;}}

    internal static RebirthNpcSaveQuarantineDomain PrepareHeldQuarantine(RebirthNpcSaveQuarantineScope scope)
    {
        lock(Sync)
        {
            RebirthNpcAncillaryOriginalScope.RequireScope(scope,nativeScope);if(!CanRetireEmptyNative)return CapturePackageCustody(scope).Domain;
            var records=States;var original=nativeScope;long c0=begun;long c1=ticks;long c2=completed;long c3=cancelled;long c4=restored;long c5=orderFailures;long c6=driftCorrections;var doc=RebirthNpcAncillaryOriginalScope.Image("heldWorkEmpty",original,new[]{begun,ticks,completed,cancelled,restored,orderFailures,driftCorrections});doc.DocumentElement.SetAttribute("emptyOnly","1");var next=new Dictionary<ulong,DutyState>();
            return new RebirthNpcSaveQuarantineDomain("guard-native-empty",doc,
                ()=>{lock(Sync)return CanRetireEmptyNative&&ReferenceEquals(States,records)&&ReferenceEquals(nativeScope,original)&&begun==c0&&ticks==c1&&completed==c2&&cancelled==c3&&restored==c4&&orderFailures==c5&&driftCorrections==c6;},
                ()=>{lock(Sync){States=next;nativeScope=null;begun=ticks=completed=cancelled=restored=orderFailures=driftCorrections=0;}},
                ()=>{lock(Sync){States=records;nativeScope=original;begun=c0;ticks=c1;completed=c2;cancelled=c3;restored=c4;orderFailures=c5;driftCorrections=c6;}});
        }
    }
}
public sealed partial class RebirthNpcMedicalTreatmentAdapter
{
    private static RebirthNpcAncillaryOriginalScope nativeScope;
    private static bool UseNativeScope(){if(!RebirthNpcWorkReleaseGate.Enabled)return false;lock(sync)return RebirthNpcAncillaryOriginalScope.Use(ref nativeScope,states.Count==0);}
    internal static bool CanRetireEmptyNative{get{lock(sync)return ThreadManager.IsMainThread()&&states.Count==0&&RebirthNpcWorkCallbackObservation.IsIdle;}}
    internal static bool TryRetireEmptyObservation(){lock(sync){if(!CanRetireEmptyNative)return false;nativeScope=null;return true;}}
    internal static RebirthNpcSaveQuarantineDomain PrepareHeldQuarantine(RebirthNpcSaveQuarantineScope scope)
    {
        lock(sync)
        {
            RebirthNpcAncillaryOriginalScope.RequireScope(scope,nativeScope);if(!CanRetireEmptyNative)return CapturePackageCustody(scope).Domain;
            var records=states;var original=nativeScope;long c0=begun;long c1=ticks;long c2=treatments;long c3=cancelled;long c4=overlapDenied;long c5=supplyFailures;long c6=patientFailures;long c7=refunds;var doc=RebirthNpcAncillaryOriginalScope.Image("heldWorkEmpty",original,new[]{begun,ticks,treatments,cancelled,overlapDenied,supplyFailures,patientFailures,refunds});doc.DocumentElement.SetAttribute("emptyOnly","1");var next=new Dictionary<ulong,State>();
            return new RebirthNpcSaveQuarantineDomain("medical-native-empty",doc,
                ()=>{lock(sync)return CanRetireEmptyNative&&ReferenceEquals(states,records)&&ReferenceEquals(nativeScope,original)&&begun==c0&&ticks==c1&&treatments==c2&&cancelled==c3&&overlapDenied==c4&&supplyFailures==c5&&patientFailures==c6&&refunds==c7;},
                ()=>{lock(sync){states=next;nativeScope=null;begun=ticks=treatments=cancelled=overlapDenied=supplyFailures=patientFailures=refunds=0;}},
                ()=>{lock(sync){states=records;nativeScope=original;begun=c0;ticks=c1;treatments=c2;cancelled=c3;overlapDenied=c4;supplyFailures=c5;patientFailures=c6;refunds=c7;}});
        }
    }
}

internal static class RebirthNpcHeldDutyMedicalRetirement
{
    internal static bool TryRetireEmpty()
    {
        if(!RebirthNpcGuardDutyWorkPackageService.CanRetireEmptyGuardPackage||!RebirthNpcMedicalTreatmentWorkPackageService.CanRetireEmptyMedicalPackage||!RebirthNpcGuardPostWorkAdapter.CanRetireEmptyNative||!RebirthNpcMedicalTreatmentAdapter.CanRetireEmptyNative)return false;
        return RebirthNpcGuardDutyWorkPackageService.TryRetireEmptyObservation()&&RebirthNpcMedicalTreatmentWorkPackageService.TryRetireEmptyObservation()&&RebirthNpcGuardPostWorkAdapter.TryRetireEmptyObservation()&&RebirthNpcMedicalTreatmentAdapter.TryRetireEmptyObservation();
    }
}
