using System;
using System.Collections.Generic;

internal static partial class RebirthNpcSaveQuarantineBundle
{
    private static bool retryingServerNativeAdmission;
    internal static void RetryServerNativeAdmission(World world,string directory)
    {
        if(retryingServerNativeAdmission||!AdmissionEnabled||transferring||!ThreadManager.IsMainThread()||world?.IsRemote()!=false||
            !ReferenceEquals(world,GameManager.Instance?.World)||!SameDirectory(directory,GameIO.GetSaveGameDir()))return;
        retryingServerNativeAdmission=true;
        try
        {
            var sources=new List<RebirthNpcRuntimeNativeAdoption.NativeReentry>();var seen=new HashSet<EntityRebirthNPC>();
            foreach(var value in retained)
            {
                if(sources.Count>=RebirthNpcSaveQuarantineIds.MaximumIds)break;
                if(!value.Detached||value.RollbackIncomplete||!SameDirectory(value.Directory,directory)||value.ServerRuntimeReentries==null)continue;
                foreach(var source in value.ServerRuntimeReentries.Values)
                {
                    if(sources.Count>=RebirthNpcSaveQuarantineIds.MaximumIds)break;
                    if(source?.Current()==true&&ReferenceEquals(source.World,world)&&seen.Add(source.Actor))sources.Add(source);
                }
            }
            foreach(var source in sources)
            {
                // A first OnAdded observation precedes the native actor's
                // existing deferred-registration branch. Never resume inside
                // that original activation stack or call OnAdded a second time.
                if(!source.Current()||!RebirthNpcLiveRecoveryRuntime.IsNativeActivationDeferred(source.Actor))continue;
                try
                {
                    if(RebirthNpcRuntimeNativeAdoption.IsIncomingHeld(source.Actor))
                    {
                        if(!CanReleaseServerRuntimeNativeReentry(source)||!RebirthNpcRuntimeNativeAdoption.ReleaseIncoming(source))continue;
                    }
                    RebirthNpcLiveRecoveryRuntime.TryResumeAdmittedNativeActivation(source);
                }
                catch{/* Keep this exact source/deferred actor; retry other independent owners. */}
            }
        }
        finally{retryingServerNativeAdmission=false;}
    }
}
