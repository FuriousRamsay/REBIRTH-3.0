using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Only durable acknowledgement is retried. No inventory/cursor write or debit.
internal static class RebirthCamperOwnerSaveRetry
{
    private static readonly HashSet<Guid> running=new HashSet<Guid>();
    internal static void Start(RebirthCamperTransferJournal journal,Guid operation,EntityVehicle vehicle,
        EntityPlayerLocal player,Func<bool> current)
    {
        if(!Eligible(journal,operation,vehicle,player,current))
            throw new InvalidOperationException("Complete local publication required for owner-save recovery.");
        if(!running.Add(operation))return;
        try{GameManager.Instance.StartCoroutine(Observe(journal,operation,vehicle,player,current));}
        catch{running.Remove(operation);throw;}
    }
    internal static bool Eligible(RebirthCamperTransferJournal journal,Guid operation,EntityVehicle vehicle,
        EntityPlayerLocal player,Func<bool> current)
    {
        if(!ThreadManager.IsMainThread() || journal==null || operation==Guid.Empty || current==null || !current()
            || vehicle?.world==null || vehicle.world.IsRemote() || player==null || player.IsDead()
            || !ReferenceEquals(vehicle.world.GetPrimaryPlayer(),player)
            || !ReferenceEquals(vehicle.world.GetEntity(vehicle.entityId),vehicle)
            || player.Buffs.GetCustomVar("rbCamper_"+operation.ToString("N"))!=1f)return false;
        var receipt=journal.Read(operation);
        var runtime=RebirthCamperModuleRuntime.For(vehicle);
        if(receipt==null || receipt.State!=RebirthCamperTransferJournal.Phase.Uncertain || receipt.Vehicle!=runtime.Identity)return false;
        var after=RebirthCamperTransferSnapshot.Restore(receipt.Vehicle,receipt.After);
        var live=RebirthCamperTransferSnapshot.Capture(runtime.Storage,runtime.Stations,vehicle.bag.ItemGrid.items,after.Cursor,after.BackpackSlot);
        if(live.Length!=receipt.After.Length)return false;
        for(int i=0;i<live.Length;i++)if(live[i]!=receipt.After[i])return false;
        return current();
    }
    private static IEnumerator Observe(RebirthCamperTransferJournal journal,Guid operation,EntityVehicle vehicle,
        EntityPlayerLocal player,Func<bool> current)
    {
        float deadline=Time.realtimeSinceStartup+30f;
        try
        {
            while(Time.realtimeSinceStartup<deadline)
            {
                bool stop=false;
                try
                {
                    if(!Eligible(journal,operation,vehicle,player,current))stop=true;
                    else if(RebirthCamperSavedCursor.SaveAndVerifyPublishedLocal(player,operation,current))
                    {
                        RebirthCamperTrace.Write("owner-retry.saved",vehicle,operation.ToString("N"));
                        RebirthCamperSaveAcknowledgement.Start(journal,operation,vehicle,player,current,true);
                        stop=true;
                    }
                }
                catch(Exception error)
                {RebirthCamperTrace.Write("owner-retry.refused",vehicle,error.Message);stop=true;}
                if(stop)yield break;
                yield return new WaitForSecondsRealtime(2f);
            }
            RebirthCamperTrace.Write("owner-retry.timeout",vehicle,operation.ToString("N"));
        }
        finally{running.Remove(operation);}
    }
}
