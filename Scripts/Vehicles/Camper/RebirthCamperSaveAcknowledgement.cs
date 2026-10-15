using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class RebirthCamperSaveAcknowledgement
{
    private static readonly HashSet<Guid> pending=new HashSet<Guid>();
    public static void Start(RebirthCamperTransferJournal journal,Guid operation,EntityVehicle vehicle,
        EntityPlayerLocal player,Func<bool> currentCharacter,bool resumePublished=false)
    {
        if(!ThreadManager.IsMainThread() || journal==null || vehicle==null || player==null || currentCharacter==null
            || !currentCharacter())throw new InvalidOperationException("Current authoritative save acknowledgement required.");
        var receipt=journal.Read(operation);
        if(receipt==null || receipt.State!=RebirthCamperTransferJournal.Phase.Uncertain)
            throw new InvalidOperationException("Pending publication receipt required.");
        if(pending.Contains(operation))return;
        var after=RebirthCamperTransferSnapshot.Restore(receipt.Vehicle,receipt.After);
        var runtime=RebirthCamperModuleRuntime.For(vehicle);
        byte[] live=RebirthCamperTransferSnapshot.Capture(runtime.Storage,runtime.Stations,vehicle.bag.ItemGrid.items,after.Cursor,after.BackpackSlot);
        byte[] wanted=receipt.After;
        if(live.Length!=wanted.Length)throw new InvalidOperationException("Published vehicle differs from the receipt candidate.");
        for(int i=0;i<live.Length;i++)if(live[i]!=wanted[i])
            throw new InvalidOperationException("Published vehicle differs from the receipt candidate.");
        var owner=GameManager.Instance.getPersistentPlayerID(null);
        // Recovery only acknowledges an already published exact vehicle candidate.
        // A legitimate native cursor return after closing does not undo the
        // durable publication marker. No owner inventory is replayed here.
        bool ownerSaved=owner!=null && (resumePublished || after.BackpackSlot>=0
            ? RebirthCamperSavedCursor.VerifyPublishedReceipt(owner.CombinedString,operation,currentCharacter)
            : RebirthCamperSavedCursor.Verify(owner.CombinedString,operation,after.Cursor,currentCharacter));
        if(!ownerSaved)
        {
            RebirthCamperOwnerSaveRetry.Start(journal,operation,vehicle,player,currentCharacter);
            return;
        }
        var expected=RebirthCamperSavedVehicle.RequestSave(vehicle);
        pending.Add(operation);
        RebirthCamperTrace.Write("acknowledgement.start",vehicle,operation.ToString("N"));
        try{GameManager.Instance.StartCoroutine(Observe(journal,operation,vehicle,player,expected,currentCharacter));}
        catch{pending.Remove(operation);throw;}
    }
    private static IEnumerator Observe(RebirthCamperTransferJournal journal,Guid operation,EntityVehicle vehicle,EntityPlayerLocal player,
        RebirthCamperSavedVehicle.Expected expected,Func<bool> current)
    {
        float deadline=Time.realtimeSinceStartup+30f,next=0f;
        try
        {
            while(Time.realtimeSinceStartup<deadline && current())
            {
                if(Time.realtimeSinceStartup>=next)
                {
                    bool finished=false;
                    try
                    {
                        var receipt=journal.Read(operation);
                        if(receipt==null || receipt.State!=RebirthCamperTransferJournal.Phase.Uncertain)yield break;
                        if(RebirthCamperSavedVehicle.Verify(vehicle,expected,current))
                        {
                            var confirmed=RebirthCamperTransferSnapshot.Restore(receipt.Vehicle,receipt.After);
                            var prior=RebirthCamperTransferSnapshot.Restore(receipt.Vehicle,receipt.Before);
                            RebirthCamperStoragePackedTransfer.Applied(prior,confirmed);
                            journal.Advance(operation,RebirthCamperTransferJournal.Phase.Uncertain,RebirthCamperTransferJournal.Phase.Applied);
                            RebirthCamperModuleRuntime.For(vehicle).SetTransferPending(false);
                            RebirthCamperTrace.Write("acknowledgement.applied",vehicle,operation.ToString("N"));
                            RebirthCamperPreferredRemoval.Confirm(player,vehicle,prior,confirmed);
                            finished=true;
                        }
                    }
                    catch(Exception error){Log.Error("[CamperTransfer] Save acknowledgement remains uncertain "+operation.ToString("N")+": "+error);}
                    if(finished)yield break;
                    next=Time.realtimeSinceStartup+.5f;
                }
                yield return null;
            }
            Log.Warning("[CamperTransfer] Save acknowledgement requires recovery "+operation.ToString("N"));
            RebirthCamperTrace.Write("acknowledgement.timeout",vehicle,operation.ToString("N"));
        }
        finally{pending.Remove(operation);}
    }
}
