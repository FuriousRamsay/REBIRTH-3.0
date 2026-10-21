using System;
using System.Collections.Generic;
using System.Linq;

internal static partial class RebirthNpcSaveQuarantineBundle
{
    internal static bool IsDetachedServerRuntimeMembership(RebirthNpcRuntimeNativeAdoption.Outgoing source)
    {
        if(!ThreadManager.IsMainThread()||source==null)return false;
        foreach(var value in retained)
            if(value.Detached&&!value.RollbackIncomplete&&ReferenceEquals(value.Original,source.World)&&
                SameDirectory(value.Directory,source.Directory)&&RebirthNpcSaveQuarantinePreparation.IsDetachedServerMembership(value.Domains)&&
                value.ServerRuntimeOriginals!=null&&Array.Exists(value.ServerRuntimeOriginals,item=>ReferenceEquals(item,source)))return true;
        return false;
    }
    internal static bool NeedsIncomingNativeOwnerReentry(World incoming,string directory,string account)
    {
        if(!AdmissionEnabled||!ThreadManager.IsMainThread()||transferring||string.IsNullOrEmpty(account)||
            incoming?.IsRemote()!=false||!ReferenceEquals(incoming,GameManager.Instance?.World)||
            !SameDirectory(directory,GameIO.GetSaveGameDir())||
            !(serverBoundary?.ObservesIncomingServer(incoming,directory)==true||clientBoundary?.ObservesIncomingServer(incoming,directory)==true))return false;
        foreach(var value in retained)
            if(value.Detached&&!value.RollbackIncomplete&&value.Original?.IsRemote()==false&&SameDirectory(value.Directory,directory)&&
                RebirthNpcSaveQuarantinePreparation.IsDetachedServerMembership(value.Domains)&&
                (value.ServerRuntimeOriginals?.Length==0||value.ServerPlayerOwnerAdoptions!=null&&
                Array.Exists(value.ServerPlayerOwnerAdoptions,owner=>owner?.RequiresAccount(account)==true)))return true;
        return false;
    }
    internal static void ObserveServerNativeOwnerReentry(RebirthDogLifecycleService.NativeOwnerReentry source)
    {
        if(source?.Current()!=true)return;
        ObserveServerNativeOwnerScope(source);
        if(!NeedsIncomingNativeOwnerReentry(source.World,source.Directory,source.Account))return;
        foreach(var value in retained)
        {
            if(!value.Detached||value.RollbackIncomplete||!SameDirectory(value.Directory,source.Directory)||
                !RebirthNpcSaveQuarantinePreparation.IsDetachedServerMembership(value.Domains)||value.ServerPlayerOwnerAdoptions==null)continue;
            foreach(var owner in value.ServerPlayerOwnerAdoptions)owner?.Observe(source);
        }
        RetryServerNativeAdmission(source.World,source.Directory);
    }
    internal static void ObserveServerRuntimeNativeReentry(RebirthNpcRuntimeNativeAdoption.NativeReentry source)
    {
        if(!ThreadManager.IsMainThread()||transferring||source?.Current()!=true)return;
        foreach(var value in retained)
        {
            if(!value.Detached||value.RollbackIncomplete||value.Original?.IsRemote()!=false||
                !SameDirectory(value.Directory,source.Directory))continue;
            // Hold the actual incoming actor even if its native body fails to
            // match. Identity equality alone cannot replace an old incarnation.
            RebirthNpcRuntimeNativeAdoption.HoldIncoming(source);
            // A committed incoming namespace keeps its receipt after lifecycle
            // clears the boundary. Later genuine native loads use that current
            // namespace and remain subject to every retained owner fence.
            if(ResolveServerIncomingAdmission(value,source.World,source.Directory))
            {
                if(TryReleaseQualifiedRuntime(source))RebirthNpcRuntimeNativeAdoption.ReleaseIncoming(source);
                continue;
            }
            if(!AdmissionEnabled||!(serverBoundary?.ObservesIncomingServer(source.World,source.Directory)==true||
                clientBoundary?.ObservesIncomingServer(source.World,source.Directory)==true)||
                !RebirthNpcSaveQuarantinePreparation.IsDetachedServerMembership(value.Domains)||
                value.ServerRuntimeOriginals==null||!value.ServerRuntimeOriginals.Any(source.Matches)&&
                value.ServerRuntimeOriginals.Any(original=>original.StableId==source.StableId))continue;
            if(value.ServerRuntimeReentries.Count>=RebirthNpcSaveQuarantineIds.MaximumIds&&
                !value.ServerRuntimeReentries.ContainsKey(source.StableId))continue;
            value.ServerRuntimeReentries[source.StableId]=source;
            if(value.ServerTransientAdoptions!=null&&Array.TrueForAll(value.ServerTransientAdoptions,owner=>owner?.CanAdopt(source)==true))
                foreach(var owner in value.ServerTransientAdoptions)if(!owner.TryAdopt(source))break;
            if(value.ServerIdentityAdoption?.CanAdopt(source)==true&&value.ServerWorldAdoption?.CanAdopt(source)==true)
            {
                if(!value.ServerIdentityAdoption.TryAdopt(source)||!value.ServerWorldAdoption.TryAdopt(source))continue;
            }
            // Independent private receipts qualify each persistent/runtime
            // family. None may release the actor or resume its old executor.
            value.ServerSavedAdoption?.TryAdopt(source);
            value.ServerAdvancedAdoption?.Observe(source);
            value.ServerCheckpointAdoption?.Observe(source);
            value.ServerDecisionAdoption?.Observe(source);
            value.ServerSignalsAdoption?.Observe(source);
            value.ServerOutcomeAdoption?.Observe(source);
            if(value.ServerNativeOwnerAdoptions!=null)
                foreach(var owner in value.ServerNativeOwnerAdoptions)owner?.Observe(source);
            if(RebirthNpcPersistenceTimelineService.TryAdoptNativeAlignment(source))
                RebirthNpcLiveRecoveryRuntime.TryAdoptNativeDriver(source);
            if(TryReleaseQualifiedRuntime(source))RebirthNpcRuntimeNativeAdoption.ReleaseIncoming(source);
        }
        RetryServerNativeAdmission(source.World,source.Directory);
    }
    // The native frame producer does not qualify remaining scheduler, work,
    // financial or projection owners. Whole-domain admission supplies this
    // permission only after those independent receipts are implemented.
    internal static bool CanReleaseServerRuntimeNativeReentry(RebirthNpcRuntimeNativeAdoption.NativeReentry source)=>TryReleaseQualifiedRuntime(source);

    internal static void ObserveServerTradeNativeReentry(RebirthNpcTradeLocalOwnerLoad.NativeReentry source)
    {
        if(!AdmissionEnabled||!ThreadManager.IsMainThread()||transferring||source?.Current()!=true||
            !(serverBoundary?.ObservesIncomingServer(source.World,source.Directory)==true||
              clientBoundary?.ObservesIncomingServer(source.World,source.Directory)==true))return;
        foreach(var value in retained)
        {
            if(!value.Detached||value.RollbackIncomplete||value.Original?.IsRemote()!=false||
                !SameDirectory(value.Directory,source.Directory)||!RebirthNpcSaveQuarantinePreparation.IsDetachedServerMembership(value.Domains)||
                value.ServerLocalReentryOriginals==null||value.ServerHoldReentryOriginals==null)continue;
            bool matches=value.ServerLocalReentryOriginals.Any(source.Matches)||value.ServerHoldReentryOriginals.Any(source.Matches)||
                value.ServerPreparationRetirement?.MatchesNativeReentry(source)==true;
            if(!matches||!source.Current()||value.ServerNativeReentries.Count>=512&&!value.ServerNativeReentries.ContainsKey(source.Operation))continue;
            value.ServerNativeReentries[source.Operation]=source;
            // This native owner observation permits only the seven independently
            // pinned, final-primary store loaders. Runtime/native owners and
            // the remaining domain receipts still gate whole-world admission.
            // A refusal retains both the old graph and the incoming observation.
            value.ServerSavedAdoption?.TryAdopt(source);
        }
        RetryServerNativeAdmission(source.World,source.Directory);
    }
    // Native epoch/load staging only. This never restores an old domain,
    // consumes a terminal, admits NPC gameplay or marks a bundle retired.
    private static bool CanStageRetainedServerDestination(World incoming,string directory)
    {
        if(!ThreadManager.IsMainThread()||transferring||incoming?.IsRemote()!=false||!IncomingRoleCurrent(incoming)||
            !ReferenceEquals(incoming,GameManager.Instance?.World)||!SameDirectory(directory,GameIO.GetSaveGameDir()))return false;
        bool found=false;
        foreach(var value in retained)
        {
            if(value.RollbackIncomplete)return false;
            if(!value.Detached||!SameDirectory(value.Directory,directory))continue;
            if(value.Fence!=null||value.Original==null)return false;
            if(value.Original.IsRemote())
            {
                if(value.Capture!=null||value.ClientRetirement==null||value.Domains?.Length!=ClientProjectionMembers.Length)return false;
                var names=new HashSet<string>(StringComparer.Ordinal);
                foreach(var domain in value.Domains)if(domain==null||!domain.IsDetached||!names.Add(domain.Name))return false;
                if(!Array.TrueForAll(ClientProjectionMembers,names.Contains))return false;
            }
            else if(value.Capture==null||!ReferenceEquals(value.Capture.Scope.World,value.Original)||
                !SameDirectory(value.Capture.Scope.Directory,value.Directory)||
                !RebirthNpcSaveQuarantinePreparation.IsDetachedServerMembership(value.Domains))return false;
            found=true;
        }
        return found;
    }
    internal static bool TryStageIncomingServerEpoch(World outgoing,string outgoingDirectory,World incoming,string incomingDirectory)
    {
        if(!AdmissionEnabled||!ThreadManager.IsMainThread()||transferring||outgoing==null||incoming?.IsRemote()!=false||
            ReferenceEquals(outgoing,incoming)||!ReferenceEquals(incoming,GameManager.Instance?.World)||!IncomingRoleCurrent(incoming)||
            !SameDirectory(incomingDirectory,GameIO.GetSaveGameDir()))return false;
        if(serverBoundary?.CanInstallIncomingServerEpoch(outgoing,outgoingDirectory,incoming,incomingDirectory)==true||
            clientBoundary?.CanInstallIncomingServerEpoch(outgoing,outgoingDirectory,incoming,incomingDirectory)==true)return true;
        // An ordinary different-save boundary uses the normal full transaction.
        // This arm exists for a native server return whose destination custody
        // would otherwise prevent the Load/Read/ToPlayer producer from running.
        if(!SameDirectory(outgoingDirectory,incomingDirectory)&&!HasRetainedOriginal(incoming,incomingDirectory))return false;
        bool detached=outgoing.IsRemote()?TryTransferClientCore(outgoing,outgoingDirectory,incoming,true,out _,out _):
            TryTransferServerCore(outgoing,outgoingDirectory,incoming,false,out _,out _,true);
        return detached&&(serverBoundary?.CanInstallIncomingServerEpoch(outgoing,outgoingDirectory,incoming,incomingDirectory)==true||
            clientBoundary?.CanInstallIncomingServerEpoch(outgoing,outgoingDirectory,incoming,incomingDirectory)==true);
    }
}
