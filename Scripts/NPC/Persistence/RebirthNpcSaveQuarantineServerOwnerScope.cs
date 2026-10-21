using System;
using System.Collections.Generic;
internal static partial class RebirthNpcSaveQuarantineBundle
{
    private static Retained stagingEmptyRuntime;
    private static RebirthDogLifecycleService.NativeOwnerReentry stagingEmptyOwner;
    internal static bool CanStageEmptyServerRuntimeNativeScope(RebirthDogLifecycleService.NativeOwnerReentry source)
    {
        var value=stagingEmptyRuntime;
        return AdmissionEnabled&&!transferring&&ThreadManager.IsMainThread()&&source?.Current()==true&&
            ReferenceEquals(source,stagingEmptyOwner)&&value!=null&&retained.Contains(value)&&value.Detached&&!value.RollbackIncomplete&&
            value.Fence==null&&value.Original?.IsRemote()==false&&!ReferenceEquals(value.Original,source.World)&&
            value.ServerRuntimeOriginals!=null&&value.ServerRuntimeOriginals.Length==0&&SameDirectory(value.Directory,source.Directory)&&
            RebirthNpcSaveQuarantinePreparation.IsDetachedServerMembership(value.Domains)&&
            (serverBoundary?.ObservesIncomingServer(source.World,source.Directory)==true||clientBoundary?.ObservesIncomingServer(source.World,source.Directory)==true);
    }
    private static bool StageQualifiedEmptyRuntime(Retained value,RebirthDogLifecycleService.NativeOwnerReentry source)
    {
        if(stagingEmptyRuntime!=null||stagingEmptyOwner!=null)return false;
        stagingEmptyRuntime=value;stagingEmptyOwner=source;
        try{return RebirthNpcRuntimeRegistry.StageEmptyIncomingNativeRuntime(source)&&RebirthNpcNetworkEpoch.StageIncomingNativeOwnerEpoch(source);}
        finally{stagingEmptyRuntime=null;stagingEmptyOwner=null;}
    }
    internal static void ObserveServerNativeOwnerScope(RebirthDogLifecycleService.NativeOwnerReentry source)
    {
        if(!AdmissionEnabled||transferring||!ThreadManager.IsMainThread()||source?.Current()!=true)return;
        foreach(var value in retained)
        {
            if(!value.Detached||value.RollbackIncomplete||value.Original?.IsRemote()!=false||
                ReferenceEquals(value.Original,source.World)||!SameDirectory(value.Directory,source.Directory)||
                value.ServerRuntimeOriginals==null||value.ServerRuntimeOriginals.Length!=0)continue;
            TryQualifyIncomingOwnerScope(value,source);
        }
    }
    private static bool TryQualifyIncomingOwnerScope(Retained value,RebirthDogLifecycleService.NativeOwnerReentry source)
    {
        if(!AdmissionEnabled||source?.Current()!=true||value==null||!value.Detached||value.RollbackIncomplete||value.Fence!=null||
            value.Original?.IsRemote()!=false||!SameDirectory(value.Directory,source.Directory)||
            !RebirthNpcSaveQuarantinePreparation.IsDetachedServerMembership(value.Domains))return false;
        if(ResolveServerIncomingAdmission(value,source.World,source.Directory))return true;
        if(value.ServerRuntimeOriginals==null||value.ServerRuntimeOriginals.Length!=0||
            !(serverBoundary?.ObservesIncomingServer(source.World,source.Directory)==true||clientBoundary?.ObservesIncomingServer(source.World,source.Directory)==true))return false;
        if(value.ServerNativeOwnerAdoptions?.Length!=NativeRoleMembers.Length||value.ServerPlayerOwnerAdoptions?.Length!=PlayerRoleMembers.Length||
            value.ServerTransientAdoptions?.Length!=TransientRoleMembers.Length)return false;
        var roles=new List<RebirthNpcServerNativeAdmission.Role>();
        void Add(string[] members,Func<bool> action)=>roles.Add(new RebirthNpcServerNativeAdmission.Role(members,action));
        Add(new[]{"inventory-service","inventory-binding","equipment-service","equipment-binding","social-service","faction-service","social-binding",
            "progression-service","progression-binding","economy-service","economy-binding","combat-service","combat-binding",
            "settlement-service","settlement-binding","lifecycle-gameplay","identity-audit","aggregate"},()=>value.ServerSavedAdoption?.TryAdopt(source)==true);
        Add(new[]{"stable-identity","world-integration"},()=>value.ServerIdentityAdoption?.CanAdoptOwnerScope(source)==true&&value.ServerWorldAdoption?.CanAdoptOwnerScope(source)==true&&
            value.ServerIdentityAdoption.TryAdoptOwnerScope(source)&&value.ServerWorldAdoption.TryAdoptOwnerScope(source));
        // The complete roster has been preflighted before any role runs.
        // Saved backing loads precede semantic native publication; dependent
        // projection/spatial owners run only after that publication succeeds.
        Add(new[]{"runtime-registry"},()=>StageQualifiedEmptyRuntime(value,source));
        Add(new[]{"advanced-service","advanced-binding"},()=>value.ServerAdvancedAdoption?.ObserveOwnerScope(source)==true);
        Add(new[]{"work-checkpoint-binding"},()=>value.ServerCheckpointAdoption?.ObserveOwnerScope(source)==true);
        Add(new[]{"work-outcome-service","work-outcome-binding"},()=>value.ServerOutcomeAdoption?.ObserveOwnerScope(source)==true);
        Add(new[]{"decision-runtime"},()=>value.ServerDecisionAdoption?.ObserveOwnerScope(source)==true);
        Add(new[]{"decision-signals"},()=>value.ServerSignalsAdoption?.ObserveOwnerScope(source)==true);
        Add(new[]{"execution-leases","work-telemetry"},()=>value.ServerWorkCustody?.ObserveOwnerScope(source)==true&&value.ServerWorkCustody.IncomingSafe(source.World,source.Directory));
        Add(new[]{"work-navigation-empty","work-resolution-empty","work-targets-empty","guard-packages-empty","medical-packages-empty","guard-native-empty",
            "medical-native-empty","work-reservations-empty","work-assignment-controller-empty","workstation-jobs-empty"},
            ()=>value.ServerWorkPackages?.ObserveOwnerScope(source)==true&&value.ServerWorkPackages.IncomingSafe(source.World,source.Directory));
        Add(new[]{"follow-controller-empty","travel-controller-empty","guard-controller-empty","patrol-controller-empty","stationary-controller-empty",
            "mission-controller-empty","work-controller-empty","navigation-runtime-empty"},()=>value.ServerControllerCustody?.ObserveOwnerScope(source)==true&&
            value.ServerControllerCustody.IncomingSafe(source.World,source.Directory));
        Add(new[]{"timeline"},()=>RebirthNpcPersistenceTimelineService.TryAdoptNativeOwnerAlignment(source));
        Add(new[]{"recovery-driver"},()=>RebirthNpcLiveRecoveryRuntime.TryAdoptNativeOwnerDriver(source));
        for(int index=0;index<NativeRoleMembers.Length;index++)
        {
            var owner=value.ServerNativeOwnerAdoptions[index];
            Add(NativeRoleMembers[index],()=>owner?.ObserveOwnerScope(source)==true);
        }
        for(int index=0;index<PlayerRoleMembers.Length;index++)
        {
            var owner=value.ServerPlayerOwnerAdoptions[index];
            Add(PlayerRoleMembers[index],()=>owner?.Observe(source)==true);
        }
        for(int index=0;index<TransientRoleMembers.Length;index++)
        {
            var owner=value.ServerTransientAdoptions[index];
            Add(new[]{TransientRoleMembers[index]},()=>owner?.CanAdoptOwnerScope(source)==true&&owner.TryAdoptOwnerScope(source));
        }
        if(!RebirthNpcServerNativeAdmission.TryQualifyOwnerScope(source,value.Domains,roles.ToArray(),out var receipt))return false;
        value.ServerIncomingAdmission=receipt;return true;
    }
}
