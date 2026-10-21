using System;
using System.Collections.Generic;
using System.Linq;

internal static partial class RebirthNpcSaveQuarantineBundle
{
    private static readonly string[][] NativeRoleMembers={
        new[]{"companion-behavior"},new[]{"companion-item-locks"},new[]{"dog-runtime-cache"},new[]{"native-equipment-projection"},
        new[]{"spatial-index"},new[]{"path-budget"},
        new[]{"network-epoch"},new[]{"network-baseline"},new[]{"equipment-projection-receipts"},new[]{"recovery-projection-mailbox"},
        new[]{"progression-projection-revisions"},new[]{"progression-client-cache"},new[]{"execution-persistence"},
        new[]{"work-assignment-records"},new[]{"work-executor-health"},new[]{"trade-quotes"},new[]{"dog-storage-empty"},
        new[]{"dog-capacity-projection"},new[]{"replication-performance"}
    };
    private static readonly string[][] PlayerRoleMembers={new[]{"dog-ghost-retries"},new[]{"companion-snapshot-callbacks"},
        new[]{"dog-native-observers"},new[]{"dog-auto-loot-callbacks"},new[]{"dog-navigation-markers"}};
    private static readonly string[] TransientRoleMembers={"command-gateway","remote-command-routing","dialogue-conversations","activity-runtime","activity-recovery",
        "network-batches-empty","relationship-batches-empty","serialization-pool-idle","retention-delegates-empty"};

    private static bool TryQualifyIncomingServer(Retained value,RebirthNpcRuntimeNativeAdoption.NativeReentry source,Func<bool> stageRuntime)
    {
        if(!AdmissionEnabled||source?.Current()!=true||stageRuntime==null||value==null||!value.Detached||value.RollbackIncomplete||value.Fence!=null||
            value.Original?.IsRemote()!=false||!SameDirectory(value.Directory,source.Directory)||
            !RebirthNpcSaveQuarantinePreparation.IsDetachedServerMembership(value.Domains))return false;
        if(ResolveServerIncomingAdmission(value,source.World,source.Directory))return true;
        if(value.ServerRuntimeOriginals==null||!value.ServerRuntimeOriginals.Any(source.Matches)&&
            value.ServerRuntimeOriginals.Any(original=>original.StableId==source.StableId)||
            !(serverBoundary?.ObservesIncomingServer(source.World,source.Directory)==true||clientBoundary?.ObservesIncomingServer(source.World,source.Directory)==true))return false;
        if(value.ServerNativeOwnerAdoptions?.Length!=NativeRoleMembers.Length||value.ServerPlayerOwnerAdoptions?.Length!=PlayerRoleMembers.Length||
            value.ServerTransientAdoptions?.Length!=TransientRoleMembers.Length)return false;
        var roles=new List<RebirthNpcServerNativeAdmission.Role>();
        void Add(string[] members,Func<bool> action)=>roles.Add(new RebirthNpcServerNativeAdmission.Role(members,action));
        Add(new[]{"inventory-service","inventory-binding","equipment-service","equipment-binding","social-service","faction-service","social-binding",
            "progression-service","progression-binding","economy-service","economy-binding","combat-service","combat-binding",
            "settlement-service","settlement-binding","lifecycle-gameplay","identity-audit","aggregate"},()=>value.ServerSavedAdoption?.TryAdopt(source)==true);
        Add(new[]{"stable-identity","world-integration"},()=>value.ServerIdentityAdoption?.CanAdopt(source)==true&&value.ServerWorldAdoption?.CanAdopt(source)==true&&
            value.ServerIdentityAdoption.TryAdopt(source)&&value.ServerWorldAdoption.TryAdopt(source));
        // The complete roster has been preflighted before any role runs.
        // Saved backing loads precede semantic native publication; dependent
        // projection/spatial owners run only after that publication succeeds.
        Add(new[]{"runtime-registry"},stageRuntime);
        Add(new[]{"advanced-service","advanced-binding"},()=>value.ServerAdvancedAdoption?.Observe(source)==true);
        Add(new[]{"work-checkpoint-binding"},()=>value.ServerCheckpointAdoption?.Observe(source)==true);
        Add(new[]{"work-outcome-service","work-outcome-binding"},()=>value.ServerOutcomeAdoption?.Observe(source)==true);
        Add(new[]{"decision-runtime"},()=>value.ServerDecisionAdoption?.Observe(source)==true);
        Add(new[]{"decision-signals"},()=>value.ServerSignalsAdoption?.Observe(source)==true);
        Add(new[]{"execution-leases","work-telemetry"},()=>value.ServerWorkCustody?.ObserveIncoming(source)==true&&value.ServerWorkCustody.IncomingSafe(source.World,source.Directory));
        Add(new[]{"work-navigation-empty","work-resolution-empty","work-targets-empty","guard-packages-empty","medical-packages-empty","guard-native-empty",
            "medical-native-empty","work-reservations-empty","work-assignment-controller-empty","workstation-jobs-empty"},
            ()=>value.ServerWorkPackages?.ObserveIncoming(source)==true&&value.ServerWorkPackages.IncomingSafe(source.World,source.Directory));
        Add(new[]{"follow-controller-empty","travel-controller-empty","guard-controller-empty","patrol-controller-empty","stationary-controller-empty",
            "mission-controller-empty","work-controller-empty","navigation-runtime-empty"},()=>value.ServerControllerCustody?.ObserveIncoming(source)==true&&
            value.ServerControllerCustody.IncomingSafe(source.World,source.Directory));
        Add(new[]{"timeline"},()=>RebirthNpcPersistenceTimelineService.TryAdoptNativeAlignment(source));
        Add(new[]{"recovery-driver"},()=>RebirthNpcLiveRecoveryRuntime.TryAdoptNativeDriver(source));
        for(int index=0;index<NativeRoleMembers.Length;index++)
        {
            var owner=value.ServerNativeOwnerAdoptions[index];
            Add(NativeRoleMembers[index],()=>owner?.Observe(source)==true);
        }
        for(int index=0;index<PlayerRoleMembers.Length;index++)
        {
            var owner=value.ServerPlayerOwnerAdoptions[index];
            Add(PlayerRoleMembers[index],()=>owner?.TryAdopt(source)==true);
        }
        for(int index=0;index<TransientRoleMembers.Length;index++)
        {
            var owner=value.ServerTransientAdoptions[index];
            Add(new[]{TransientRoleMembers[index]},()=>owner?.CanAdopt(source)==true&&owner.TryAdopt(source));
        }
        if(!RebirthNpcServerNativeAdmission.TryQualify(source,value.Domains,roles.ToArray(),out var receipt))return false;
        value.ServerIncomingAdmission=receipt;return true;
    }
    private static bool StageQualifiedRuntime(Retained value,RebirthNpcRuntimeNativeAdoption.NativeReentry source)
    {
        var original=value.ServerRuntimeOriginals?.FirstOrDefault(source.Matches);
        if(value.ServerRuntimeOriginals==null||original==null&&value.ServerRuntimeOriginals.Any(item=>item.StableId==source.StableId)||
            value.ServerRuntimeStagingSource!=null)return false;
        value.ServerRuntimeStagingSource=source;value.ServerRuntimeStagingOriginal=original;
        try{return original==null?RebirthNpcRuntimeRegistry.StageNewIncomingNativeRuntime(source):RebirthNpcRuntimeRegistry.StageIncomingNativeRuntime(source,original);}
        finally{value.ServerRuntimeStagingSource=null;value.ServerRuntimeStagingOriginal=null;}
    }
    internal static bool CanStageServerRuntimeNativeReentry(RebirthNpcRuntimeNativeAdoption.NativeReentry source,
        RebirthNpcRuntimeNativeAdoption.Outgoing original)
    {
        if(!AdmissionEnabled||!ThreadManager.IsMainThread()||transferring||source?.Current()!=true||original?.IsDetached!=true||!source.Matches(original))return false;
        foreach(var value in retained)
            if(value.Detached&&!value.RollbackIncomplete&&ReferenceEquals(value.ServerRuntimeStagingSource,source)&&
                ReferenceEquals(value.ServerRuntimeStagingOriginal,original)&&IsDetachedServerRuntimeMembership(original))return true;
        return false;
    }
    internal static bool CanStageNewServerRuntimeNativeReentry(RebirthNpcRuntimeNativeAdoption.NativeReentry source)
    {
        if(!AdmissionEnabled||!ThreadManager.IsMainThread()||transferring||source?.Current()!=true)return false;
        foreach(var value in retained)
            if(value.Detached&&!value.RollbackIncomplete&&ReferenceEquals(value.ServerRuntimeStagingSource,source)&&value.ServerRuntimeStagingOriginal==null&&
                value.ServerRuntimeOriginals!=null&&!value.ServerRuntimeOriginals.Any(original=>original.StableId==source.StableId)&&
                SameDirectory(value.Directory,source.Directory)&&RebirthNpcSaveQuarantinePreparation.IsDetachedServerMembership(value.Domains))return true;
        return false;
    }
    private static bool HasQualifiedIncomingServer(World incoming,string directory)
    {
        if(!AdmissionEnabled||transferring||incoming?.IsRemote()!=false||!ReferenceEquals(incoming,GameManager.Instance?.World))return false;
        bool found=false;
        foreach(var value in retained)
        {
            if(value.RollbackIncomplete)return false;
            if(!value.Detached||!SameDirectory(value.Directory,directory))continue;
            if(value.Original?.IsRemote()!=false||!ResolveServerIncomingAdmission(value,incoming,directory))return false;
            found=true;
        }
        return found;
    }
    private static bool TryReleaseQualifiedRuntime(RebirthNpcRuntimeNativeAdoption.NativeReentry source)
    {
        if(!AdmissionEnabled||transferring||source?.Current()!=true)return false;
        bool found=false;
        foreach(var value in retained)
        {
            if(value.RollbackIncomplete)return false;
            if(!value.Detached||!SameDirectory(value.Directory,source.Directory))continue;
            if(value.Original?.IsRemote()!=false)return false;
            if(!TryQualifyIncomingServer(value,source,()=>StageQualifiedRuntime(value,source)))return false;
            if(value.ServerTransientAdoptions!=null&&Array.Exists(value.ServerTransientAdoptions,owner=>owner?.BlocksOwner(source.StableId)==true))return false;
            found=true;
        }
        return found&&HasQualifiedIncomingServer(source.World,source.Directory)&&
            !RebirthNpcRetainedWorkCustody.BlocksOwner(source.StableId)&&!RebirthNpcRetainedControllerCustody.BlocksOwner(source.StableId)&&
            !RebirthNpcRetainedWorkPackages.BlocksOwner(source.StableId)&&
            !BlocksRetainedTrade(source.StableId);
    }
}
