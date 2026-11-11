using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

public sealed partial class RebirthVehicleTransferPlanStore
{
    private RebirthCamperTransferJournal Completion()=>new RebirthCamperTransferJournal(Path.Combine(saveDirectory,"RebirthVehicleTransferCompletion"));
    private static byte[] Image(RebirthVehicleTransferPlan plan)=>Encoding.UTF8.GetBytes(plan.ToXml().ToString(SaveOptions.DisableFormatting));
    // With no requested outcome, an exact applied or rejected record can release custody.
    private static bool Same(RebirthCamperTransferJournal.Record record, RebirthVehicleTransferPlan plan, bool? rejected = null)
        =>Same(record,plan,rejected,out _);
    private static bool Same(RebirthCamperTransferJournal.Record record, RebirthVehicleTransferPlan plan, bool? rejected, out bool applied)
    {
        applied=false;
        if (record == null || record.Operation != plan.TransactionId || record.OwnerCreation != plan.OwnerScope)
            return false;
        var before = plan.ReadBefore();
        if (record.Vehicle != before.AssemblyId || record.Revision != before.Revision)
            return false;

        byte[] image = Image(plan);
        if (!record.Before.SequenceEqual(image))
            return false;

        byte[] after = record.After;
        if (rejected != true && after.SequenceEqual(image))
        {
            applied=true;
            return true;
        }
        return rejected != false && after.Length == image.Length + 1 && after[image.Length] == 0 &&
            after.Take(image.Length).SequenceEqual(image);
    }
    internal bool HasCompletion(RebirthVehicleTransferPlan plan)=>TryReadCompletion(plan,out _);
    internal bool TryReadCompletion(RebirthVehicleTransferPlan plan,out bool applied)
    {
        applied=false;
        AssertScope();
        var record=RebirthCamperTransferJournal.ReadExisting(Path.Combine(saveDirectory,"RebirthVehicleTransferCompletion"),plan.TransactionId);
        return record?.State==RebirthCamperTransferJournal.Phase.Applied&&Same(record,plan,null,out applied);
    }
    internal bool HasAppliedCompletion(RebirthVehicleTransferPlan plan)
    {AssertScope();var record=RebirthCamperTransferJournal.ReadExisting(Path.Combine(saveDirectory,"RebirthVehicleTransferCompletion"),plan.TransactionId);return record?.State==RebirthCamperTransferJournal.Phase.Applied&&Same(record,plan,false);}
    internal bool HasRejectedCompletion(RebirthVehicleTransferPlan plan)
    {AssertScope();var record=RebirthCamperTransferJournal.ReadExisting(Path.Combine(saveDirectory,"RebirthVehicleTransferCompletion"),plan.TransactionId);return record?.State==RebirthCamperTransferJournal.Phase.Applied&&Same(record,plan,true);}
    internal bool TryComplete(RebirthVehicleTransferPlan plan,Func<bool> jointSaved,bool rejected=false)
    {
        lock(Sync)
        {
            try
            {
                AssertScope();if(plan==null||jointSaved==null||!jointSaved())return false;
                var stored=Read(plan.TransactionId);if(stored==null||stored.ToXml().ToString()!=plan.ToXml().ToString())return false;
                var journal=Completion();var image=Image(plan);
                var before=plan.ReadBefore();
                var record=journal.Prepare(plan.TransactionId,before.AssemblyId,plan.OwnerScope,before.Revision,image,rejected?image.Concat(new byte[]{0}).ToArray():image);
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
