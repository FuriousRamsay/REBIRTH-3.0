using System;
using System.IO;
using System.Collections.Generic;

public sealed partial class RebirthVehicleTransferPlanStore
{
    internal bool HasTrainingCompletion(RebirthVehicleTransferPlan plan)
    {
        AssertScope();if(plan?.Training==null)return false;
        var record=RebirthCamperTransferJournal.ReadExisting(Path.Combine(saveDirectory,"RebirthVehicleTrainingCompletion"),plan.TransactionId);
        return Same(record,plan)&&record.State==RebirthCamperTransferJournal.Phase.Applied;
    }
    internal bool TryCompleteTraining(RebirthVehicleTransferPlan plan,RebirthStablePlayerIdentity owner)
    {
        lock(Sync)
        {
            try
            {
                AssertScope();if(plan?.Training==null||owner?.CanonicalId!=plan.ActorId||!HasCompletion(plan)||HasRejectedCompletion(plan))return false;
                if(HasTrainingCompletion(plan))return true;
                bool Saved()=>isCurrentSession()&&RebirthWorldCharacterRepository.HasSavedVehicleTraining(owner,plan.CreationId,plan.TransactionId);
                if(!Saved()||Read(plan.TransactionId)?.ToXml().ToString()!=plan.ToXml().ToString())return false;
                var journal=new RebirthCamperTransferJournal(Path.Combine(saveDirectory,"RebirthVehicleTrainingCompletion"));var image=Image(plan);
                var record=journal.Prepare(plan.TransactionId,plan.ReadBefore().AssemblyId,plan.OwnerScope,plan.ReadBefore().Revision,image,image);
                if(!Same(record,plan)||!Saved())return false;
                if(record.State==RebirthCamperTransferJournal.Phase.Prepared)record=journal.Advance(plan.TransactionId,record.State,RebirthCamperTransferJournal.Phase.Uncertain);
                AssertScope();if(!Saved())return false;
                if(record.State==RebirthCamperTransferJournal.Phase.Uncertain)record=journal.Advance(plan.TransactionId,record.State,RebirthCamperTransferJournal.Phase.Applied);
                return Same(record,plan)&&record.State==RebirthCamperTransferJournal.Phase.Applied&&HasTrainingCompletion(plan)&&Saved();
            }
            catch{return false;}
        }
    }
    internal static void RegisterTrainingReceiptProtection()
        =>RebirthTeachingOutcomeStore.RegisterReceiptProtection(ProtectedTrainingReceipts);
    private static ISet<string> ProtectedTrainingReceipts(HashSet<string> receipts)
    {
        var retained=new HashSet<string>(StringComparer.Ordinal);
        foreach(string receipt in receipts)
        {
            if(receipt==null||!receipt.StartsWith("vehicle:",StringComparison.Ordinal))continue;
            retained.Add(receipt);
            if(!ThreadManager.IsMainThread()||!Guid.TryParseExact(receipt.Substring(8),"N",out var id))continue;
            try
            {
                string save=GameIO.GetSaveGameDir();var store=new RebirthVehicleTransferPlanStore(save,GameIO.GetSaveGameDir,()=>ThreadManager.IsMainThread()&&save==GameIO.GetSaveGameDir());
                var plan=store.Read(id);
                if(plan?.Training!=null&&store.HasTrainingCompletion(plan))retained.Remove(receipt);
            }
            catch{/* Unknown original retains its character receipt. */}
        }
        return retained;
    }
}
