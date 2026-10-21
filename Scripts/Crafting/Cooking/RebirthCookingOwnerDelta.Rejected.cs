using System;
using System.IO;
using System.Linq;

internal static partial class RebirthCookingOwnerDelta
{
    // Persist the server's positive observation of a saved owner no-effect
    // acknowledgement. Historical rejection alone never permits a new plan.
    internal static bool ObserveRejected(RebirthStablePlayerIdentity owner,RebirthCamperTransferJournal.Record rejected)
    {
        try
        {
            if(!ThreadManager.IsMainThread()||GameManager.Instance?.World?.IsRemote()!=false||owner==null||rejected==null||
                rejected.State!=RebirthCamperTransferJournal.Phase.Rejected||!rejected.Before.SequenceEqual(rejected.After))return false;
            var plan=Decode(System.Text.Encoding.UTF8.GetString(rejected.Before));
            if(plan.Operation!=rejected.Operation||plan.Creation!=rejected.OwnerCreation||plan.Owner!=owner.CanonicalId||plan.World!=GameManager.Instance.World.worldState.Guid)return false;
            string directory=Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH","CookingOwnerDeltaRejected");
            var record=RebirthCamperTransferJournal.ReadExisting(directory,plan.Operation);
            if(record!=null&&(record.Vehicle!=rejected.Vehicle||record.OwnerCreation!=rejected.OwnerCreation||record.Revision!=rejected.Revision||
                !record.Before.SequenceEqual(rejected.Before)||!record.After.SequenceEqual(rejected.After)))return false;
            if(record?.State==RebirthCamperTransferJournal.Phase.Applied)return true;
            if(!Saved(owner,plan,4))return false;
            var journal=new RebirthCamperTransferJournal(directory);
            if(record==null)record=journal.Prepare(rejected.Operation,rejected.Vehicle,rejected.OwnerCreation,rejected.Revision,rejected.Before,rejected.After);
            if(record.State==RebirthCamperTransferJournal.Phase.Prepared)record=journal.Advance(record.Operation,RebirthCamperTransferJournal.Phase.Prepared,RebirthCamperTransferJournal.Phase.Uncertain);
            if(record.State==RebirthCamperTransferJournal.Phase.Uncertain)record=journal.Advance(record.Operation,RebirthCamperTransferJournal.Phase.Uncertain,RebirthCamperTransferJournal.Phase.Applied);
            var saved=RebirthCamperTransferJournal.ReadExisting(directory,plan.Operation);
            return record.State==RebirthCamperTransferJournal.Phase.Applied&&saved?.State==RebirthCamperTransferJournal.Phase.Applied&&
                saved.Vehicle==rejected.Vehicle&&saved.OwnerCreation==rejected.OwnerCreation&&saved.Revision==rejected.Revision&&
                saved.Before.SequenceEqual(rejected.Before)&&saved.After.SequenceEqual(rejected.After);
        }
        catch(Exception error){Log.Warning("[Cooking] Rejected owner original retained: "+error.GetType().Name);return false;}
    }
}
