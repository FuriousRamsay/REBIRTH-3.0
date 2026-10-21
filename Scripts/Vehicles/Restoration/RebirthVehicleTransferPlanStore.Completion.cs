using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

public sealed partial class RebirthVehicleTransferPlanStore
{
    private RebirthCamperTransferJournal Completion()=>new RebirthCamperTransferJournal(Path.Combine(saveDirectory,"RebirthVehicleTransferCompletion"));
    private static byte[] Image(RebirthVehicleTransferPlan plan)=>Encoding.UTF8.GetBytes(plan.ToXml().ToString(SaveOptions.DisableFormatting));
    private static bool Same(RebirthCamperTransferJournal.Record record,RebirthVehicleTransferPlan plan,bool rejected=false)
    {
        byte[] image=Image(plan);
        return record!=null&&record.Operation==plan.TransactionId&&record.Vehicle==plan.ReadBefore().AssemblyId&&record.OwnerCreation==plan.OwnerScope&&
            record.Revision==plan.ReadBefore().Revision&&record.Before.SequenceEqual(image)&&record.After.SequenceEqual(rejected?image.Concat(new byte[]{0}).ToArray():image);
    }
    internal bool HasCompletion(RebirthVehicleTransferPlan plan)
    {AssertScope();var record=RebirthCamperTransferJournal.ReadExisting(Path.Combine(saveDirectory,"RebirthVehicleTransferCompletion"),plan.TransactionId);return (Same(record,plan)||Same(record,plan,true))&&record.State==RebirthCamperTransferJournal.Phase.Applied;}
    internal bool HasRejectedCompletion(RebirthVehicleTransferPlan plan)
    {AssertScope();var record=RebirthCamperTransferJournal.ReadExisting(Path.Combine(saveDirectory,"RebirthVehicleTransferCompletion"),plan.TransactionId);return Same(record,plan,true)&&record.State==RebirthCamperTransferJournal.Phase.Applied;}
    internal bool TryComplete(RebirthVehicleTransferPlan plan,Func<bool> jointSaved,bool rejected=false)
    {
        lock(Sync)
        {
            try
            {
                AssertScope();if(plan==null||jointSaved==null||!jointSaved())return false;
                var stored=Read(plan.TransactionId);if(stored==null||stored.ToXml().ToString()!=plan.ToXml().ToString())return false;
                var journal=Completion();var image=Image(plan);
                var record=journal.Prepare(plan.TransactionId,plan.ReadBefore().AssemblyId,plan.OwnerScope,plan.ReadBefore().Revision,image,rejected?image.Concat(new byte[]{0}).ToArray():image);
                if(!Same(record,plan,rejected)||!jointSaved())return false;
                if(record.State==RebirthCamperTransferJournal.Phase.Prepared)record=journal.Advance(plan.TransactionId,RebirthCamperTransferJournal.Phase.Prepared,RebirthCamperTransferJournal.Phase.Uncertain);
                AssertScope();if(!jointSaved())return false;
                if(record.State==RebirthCamperTransferJournal.Phase.Uncertain)record=journal.Advance(plan.TransactionId,RebirthCamperTransferJournal.Phase.Uncertain,RebirthCamperTransferJournal.Phase.Applied);
                return Same(record,plan,rejected)&&record.State==RebirthCamperTransferJournal.Phase.Applied&&jointSaved()&&HasCompletion(plan);
            }
            catch{return false;}
        }
    }
}
