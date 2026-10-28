using System;
using System.Linq;
partial class Audit {
    private static void Update(ref ModEvents.SGameUpdateData data)
    {
        var world=GameManager.Instance?.World;if(!installed||world==null||world.IsRemote()||!ThreadManager.IsMainThread())return;
        foreach(var entity in world.Entities.list.ToArray())if(entity is EntityVehicle vehicle&&RebirthCamperRadial.IsCamper(vehicle))
        {
            try
            {
                Ready(vehicle);var run=runs[vehicle];
                if(run.Record!=null){Progress(run);continue;}
                if(!RebirthCamperEnergyRuntime.TryObserveMode(vehicle,out var mode,out _,out _)||mode!=RebirthCamperEnergyMode.Camp){run.Targets.Clear();run.Next=Time.realtimeSinceStartup+IntervalSeconds;continue;}
                // No repair can start before the scheduled scan when the queue is empty.
                // Pending receipts and queued targets still take their immediate paths.
                if(run.Targets.Count==0&&Time.realtimeSinceStartup<run.Next)continue;
                if(run.Targets.Count==0&&Time.realtimeSinceStartup<run.NextOwnerProbe)continue;
                var owner=Owner(vehicle,out var creation,out var key);
                if(owner==null){if(run.Targets.Count==0)run.NextOwnerProbe=Time.realtimeSinceStartup+1f;continue;}
                run.NextOwnerProbe=0;
                if(RebirthCamperRemoteAdmission.NativeInteractionOwner(vehicle)>=0||RebirthCamperEnergyRuntime.IsHeld(vehicle))continue;
                if(Time.realtimeSinceStartup>=run.Next)
                {
                    run.Next=Time.realtimeSinceStartup+IntervalSeconds;run.Targets.Clear();
                    if(PowerManager.HasInstance)foreach(var node in PowerManager.Instance.PowerItemDictionary.Values)
                        if(node?.TileEntity is TileEntityPowered te&&PoweredAdmission(vehicle,te,owner))run.Targets.Enqueue(node.Position);
                }
                while(run.Targets.Count>0)
                {var pos=run.Targets.Dequeue();if(Prepare(run,owner,creation,key,pos))break;}
            }
            catch(Exception ex){if(runs.TryGetValue(vehicle,out var run))Warn(run,"exception-"+ex.GetType().Name);}
        }
    }

}