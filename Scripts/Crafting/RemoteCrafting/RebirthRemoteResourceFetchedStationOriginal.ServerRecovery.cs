using System;
using System.IO;
using System.Linq;
using System.Text;

internal static partial class RebirthRemoteResourceFetchedStationOriginal
{
    internal static bool CanRebindPreparedServer(EntityPlayer actor,RebirthCamperStationState station,Guid job,byte[] before,byte[] after)
    {
        try
        {
            if(!HasOnlyNeverEnteredNativeFiles(job,false)||!RebirthCamperStationNativeActorPayment.AreCanonicalFetchedFrames(before,after)||!RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var owner)||station?.BoundVehicle==null)return false;
            var installed=RebirthCamperModuleRuntime.For(station.BoundVehicle).Stations.Find(station.InstallationId)?.InstalledItem;if(installed==null)return false;
            var frame=RebirthCookingRemoteAdmission.ReadProtocolXml(new UTF8Encoding(false,true).GetString(after),8*1024*1024);
            var model=RebirthCamperStationSnapshot.Restore(station.InstallationId,installed,Convert.FromBase64String(frame.Element("stationImage").Value));
            int slot=(int)frame.Attribute("queueSlot");if(slot<0||slot>=model.Queue.Length)return false;
            var marker=model.Queue[slot]?.Recipe?.ingredients?.FirstOrDefault(item=>item?.itemValue?.HasMetadata(RebirthRemoteResourceFetchedStationPlan.SourceKey)==true)?.itemValue;
            if(marker==null||!marker.TryGetMetadata(RebirthRemoteResourceFetchedStationPlan.SourceKey,out string text)||!Guid.TryParseExact(text,"N",out var source)||
                !marker.TryGetMetadata(RebirthRemoteResourceFetchedStationPlan.BindingKey,out string binding)||
                !RebirthRemoteResourceDurableJournal.TryRead(owner.StorageKey,source,out var root,out bool unknown)||unknown||root==null||root.Binding!=binding||!NoSourceEffects(root)||
                !TryReadAdmission(root,out var original)||!Scope(actor,original)||!TryReadPlan(root,out var plan)||plan.Job!=job||
                !CurrentStationOwner(actor,plan,out var actual,out _)||!ReferenceEquals(actual,station)||
                !RetainedFrameMatchesPlan(before,plan,false)||!RetainedFrameMatchesPlan(after,plan,true)||
                RebirthRemoteResourceSourceCustody.HasForeignFetchedVehicleHold(station.BoundVehicle,station,plan.OwnerKey,plan.Source,plan.Binding)||RebirthCookingRemoteAdmissionBackend.Held(station))return false;
            // Terminal no-effect ACK allows later legitimate owner movement;
            // this branch can only retire the exact never-entered station hold.
            if(root.Phase==3||root.Phase==4)return (int)root.Copy().Attribute("outcome")==2&&
                (root.Phase==3||(string)root.Copy().Attribute("disposition")=="unused-no-payment");
            if(root.Phase>1||!RebirthRemoteResourceStationPaymentWitness.TryReadSavedBefore(actor.world,job,plan.Address,plan.OwnerKey,plan.Creation,before,after,out _,out var definition)||definition!=plan.InstalledDefinition||
                !RebirthCamperEnergySavedOwner.TryRead(owner,out var image,out var saved)||saved.id!=actor.entityId||saved.deaths!=plan.Deaths||
                !plan.CopyBeforeActor().Matches(image)||!RebirthCookingRemoteAdmissionBackend.NativePhase(saved,RebirthRemoteResourceOwnerAction.Receipt(job),out float sourcePhase,out bool present)||!present||sourcePhase!=10f||
                !RebirthCookingRemoteAdmissionBackend.NativePhase(saved,"rbCamperNativePayment_"+job.ToString("N"),out float native,out bool nativePresent))return false;
            return !nativePresent||native==0f;
        }
        catch{return false;}
    }
    // Read-only authentication of the retained enqueue, not a new admission.
    // The native caller additionally verifies its private Frame and receipt.
    internal static bool CanRebindNativeServer(EntityPlayer actor,RebirthCamperStationState station,Guid job,byte[] before,byte[] after,out bool used)
        =>CanRebindNativeServer(actor,station,job,before,after,out used,out _,out _);
    internal static bool CanRebindNativeServer(EntityPlayer actor,RebirthCamperStationState station,Guid job,byte[] before,byte[] after,out bool used,
        out RebirthCamperEnergyActorImage actual,out PlayerDataFile saved)
    {
        used=false;actual=null;saved=null;
        try
        {
            string directory=Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH","NativeActorPayments");
            if(!RebirthCamperStationNativeActorPayment.AreCanonicalFetchedFrames(before,after)||!Directory.Exists(directory)||(File.GetAttributes(directory)&FileAttributes.ReparsePoint)!=0||
                Directory.EnumerateFileSystemEntries(directory,job.ToString("N")+"*").Any(path=>
                    (File.GetAttributes(path)&(FileAttributes.Directory|FileAttributes.ReparsePoint))!=0||
                    !new[]{".0.rcj",".1.rcj",".2.rcj"}.Any(suffix=>Path.GetFileName(path)==job.ToString("N")+suffix)))return false;
            if(!ThreadManager.IsMainThread()||actor?.world?.IsRemote()!=false||!actor.IsSpawned()||actor.IsDead()||
                !ReferenceEquals(GameManager.Instance?.World,actor.world)||!ReferenceEquals(actor.world.GetEntity(actor.entityId),actor)||
                !RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var owner)||station?.BoundVehicle==null||
                !RebirthCamperModuleRuntime.TryGetExisting(station.BoundVehicle,out var runtime)||runtime.TransferPending||RebirthCookingRemoteAdmissionBackend.Held(station))return false;
            var installation=runtime.Stations.Find(station.InstallationId);
            if(installation?.InstalledItem==null||!ReferenceEquals(installation.State,station))return false;
            var frame=RebirthCookingRemoteAdmission.ReadProtocolXml(new UTF8Encoding(false,true).GetString(after),8*1024*1024);
            var model=RebirthCamperStationSnapshot.Restore(station.InstallationId,installation.InstalledItem,Convert.FromBase64String(frame.Element("stationImage").Value));
            int slot=(int)frame.Attribute("queueSlot");if(slot<0||slot>=model.Queue.Length)return false;
            var marker=model.Queue[slot]?.Recipe?.ingredients?.FirstOrDefault(item=>item?.itemValue?.HasMetadata(RebirthRemoteResourceFetchedStationPlan.SourceKey)==true)?.itemValue;
            if(marker==null||!marker.TryGetMetadata(RebirthRemoteResourceFetchedStationPlan.SourceKey,out string text)||!Guid.TryParseExact(text,"N",out var source)||
                !marker.TryGetMetadata(RebirthRemoteResourceFetchedStationPlan.BindingKey,out string binding)||
                !RebirthRemoteResourceDurableJournal.TryRead(owner.StorageKey,source,out var root,out bool unknown)||unknown||root==null||root.Binding!=binding||
                !TryReadAdmission(root,out var original)||!Scope(actor,original)||!TryReadPlan(root,out var plan)||plan.Job!=job||
                plan.Deaths!=actor.Died||plan.InstalledDefinition!=RebirthNativeItemCodec.Encode(installation.InstalledItem.itemValue)||
                !RebirthCamperStationAddress.TryParse(plan.Address,out var address)||runtime.Identity!=address.Vehicle||station.InstallationId!=address.Installation||
                !RetainedFrameMatchesPlan(before,plan,false)||!RetainedFrameMatchesPlan(after,plan,true)||!MatchesNativePermission(root,plan)||
                !RebirthRemoteResourceStationPaymentWitness.TryReadSavedBefore(actor.world,job,plan.Address,plan.OwnerKey,plan.Creation,before,after,out _,out var definition)||definition!=plan.InstalledDefinition||
                RebirthRemoteResourceSourceCustody.HasForeignFetchedVehicleHold(station.BoundVehicle,station,plan.OwnerKey,plan.Source,plan.Binding))return false;
            if(!RebirthCamperEnergySavedOwner.TryRead(owner,out actual,out saved)||saved.id!=actor.entityId||saved.deaths!=plan.Deaths||
                !RebirthCookingRemoteAdmissionBackend.NativePhase(saved,RebirthRemoteResourceOwnerAction.Receipt(job),out float sourcePhase,out bool sourcePresent)||!sourcePresent||sourcePhase!=10f)return false;
            // Cursor and locks are unchanged across the original payment.
            // The native caller separately classifies only its planned cells.
            var nonInventory=plan.CopyBeforeActor();
            if(nonInventory.Bag.Length!=actual.Bag.Length||nonInventory.Belt.Length!=actual.Belt.Length)return false;
            Array.Copy(actual.Bag,nonInventory.Bag,actual.Bag.Length);Array.Copy(actual.Belt,nonInventory.Belt,actual.Belt.Length);
            if(!nonInventory.Matches(actual))return false;
            if(root.Phase==2)return (int)root.Copy().Attribute("outcome")==1&&(int?)root.Copy().Attribute("ownerRejecting")!=1&&
                root.Copy().Element("ownerRefund")==null&&root.Copy().Attribute("ownerNoUseProof")==null;
            used=root.Phase==4&&(string)root.Copy().Attribute("disposition")=="used"&&
                TryObservePublication(actor.world,root,out var proof)&&(string)root.Copy().Attribute("ownerProof")==proof;
            return used;
        }
        catch{used=false;actual=null;saved=null;return false;}
    }
}
