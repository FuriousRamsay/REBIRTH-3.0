using System;
using System.IO;
using Newtonsoft.Json.Linq;

internal static partial class RebirthRemoteResourceFetchedStationOriginal
{
    internal static JObject CaptureAuditState(World world,RebirthRemoteResourceDurableJournal.Record root)
    {
        var result=new JObject{["mapped"]=root!=null&&IsMappedRoot(root),["producerActive"]=ProtocolActive,["queueHydrationInstalled"]=queueHydrationInstalled};
        if(root==null||world?.IsRemote()!=false||!ThreadManager.IsMainThread()||!ReferenceEquals(world,GameManager.Instance?.World)||!IsMappedRoot(root))return result;
        try
        {
            result["admissionReadable"]=TryReadAdmission(root,out var original);result["planReadable"]=TryReadPlan(root,out var plan);
            if(original==null||plan==null)return result;
            result["job"]=plan.Job.ToString("N");result["address"]=plan.Address;result["originalDeaths"]=plan.Deaths;result["slot"]=plan.QueueSlot;
            result["batches"]=plan.Batches;result["tier"]=plan.Tier;result["requiredTool"]=plan.RequiredTool;
            result["nativePermission"]=MatchesNativePermission(root,plan);
            result["noSourceEffects"]=NoSourceEffects(root);
            result["unusedNoPaymentTerminal"]=root.Phase==4&&(int?)root.Copy().Attribute("outcome")==2&&
                (string)root.Copy().Attribute("disposition")=="unused-no-payment"&&NoSourceEffects(root)&&
                (string)root.Copy().Attribute("ownerProof")==RebirthRemoteResourceDurableJournal.Digest("authenticated-no-payment|"+root.Owner+"|"+root.Binding);
            result["rejecting"]=(int?)root.Copy().Attribute("ownerRejecting")==1;
            result["savedNoUse"]=root.Copy().Attribute("ownerNoUseProof")!=null&&ValidateFetchedNoUseRoot(root.Copy());
            result["ownerBeforeFingerprint"]=RebirthRemoteResourceDurableJournal.Digest(RebirthRemoteResourceClientOriginal.ActorImage(plan.CopyBeforeActor()));
            result["ownerAfterFingerprint"]=RebirthRemoteResourceDurableJournal.Digest(RebirthRemoteResourceClientOriginal.ActorImage(plan.CopyAfterActor()));
            result["planFingerprint"]=RebirthRemoteResourceDurableJournal.Digest(RebirthRemoteResourceFetchedStationPlan.Image(plan).ToString(System.Xml.Linq.SaveOptions.DisableFormatting));
            string directory=Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH","NativeActorPayments");
            if(Directory.Exists(directory))
            {
                if((File.GetAttributes(directory)&FileAttributes.ReparsePoint)!=0){result["nativeUnknown"]=true;return result;}
                var record=new RebirthCamperTransferJournal(directory).Read(plan.Job);result["nativeRecordReadable"]=record!=null;
                if(record!=null)
                {
                    result["nativeState"]=record.State.ToString();result["nativeBeforeFingerprint"]=RebirthRemoteResourceDurableJournal.Digest(Convert.ToBase64String(record.Before));
                    result["nativeAfterFingerprint"]=RebirthRemoteResourceDurableJournal.Digest(Convert.ToBase64String(record.After));
                    result["savedNativeBefore"]=RebirthRemoteResourceStationPaymentWitness.TryReadSavedBefore(world,plan.Job,plan.Address,plan.OwnerKey,plan.Creation,record.Before,record.After,out var beforeProof);
                    result["savedNativeBeforeProof"]=beforeProof??"";
                }
                Guid cancellation=NativeCancellationOperation(plan.Job);result["cancellationOperation"]=cancellation.ToString("N");
                var cancelled=new RebirthCamperTransferJournal(directory).Read(cancellation);result["cancellationRecordReadable"]=cancelled!=null;
                if(cancelled!=null)
                {
                    result["cancellationState"]=cancelled.State.ToString();
                    result["cancellationBeforeFingerprint"]=RebirthRemoteResourceDurableJournal.Digest(Convert.ToBase64String(cancelled.Before));
                    result["cancellationAfterFingerprint"]=RebirthRemoteResourceDurableJournal.Digest(Convert.ToBase64String(cancelled.After));
                    result["savedCancellationPublication"]=RebirthRemoteResourceStationPaymentWitness.TryReadSavedPayment(world,cancellation,plan.Address,plan.OwnerKey,plan.Creation,out var savedCancellation)&&
                        savedCancellation.OriginalDeaths==plan.Deaths&&savedCancellation.InstalledDefinition==plan.InstalledDefinition&&
                        savedCancellation.CopyBeforeFrame().ToString(System.Xml.Linq.SaveOptions.DisableFormatting)==new System.Text.UTF8Encoding(false,true).GetString(cancelled.Before);
                }
            }
            result["savedPublication"]=TryObservePublication(world,root,out var proof);result["savedPublicationProof"]=proof??"";
        }
        catch{result["unknown"]=true;}
        return result;
    }
}
