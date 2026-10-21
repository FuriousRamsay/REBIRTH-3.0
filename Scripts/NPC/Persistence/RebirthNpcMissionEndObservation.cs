using System;

public static partial class RebirthNpcActivityRegistry
{
    // Primitive cleanup only: does not certify executor output/payment or permit
    // replay. Missing history is UNKNOWN, never a successful terminal.
    internal static bool HasTerminalMissionOriginal(World world,int entity,ulong activity,ulong lease,uint revision)
    {
        if(!ThreadManager.IsMainThread()||world==null||!ReferenceEquals(world,GameManager.Instance?.World)||RebirthNpcPersistenceCoordinator.IsRecoveryInstalling)return false;
        lock(Sync)return ReferenceEquals(activityWorld,world)&&ByActivityId.TryGetValue(activity,out var record)&&record!=null&&record.TargetEntityId==entity&&record.LeaseId==lease&&record.AcceptedRevision==revision&&record.ControllerId=="rebirth.execution.mission"&&Enum.IsDefined(typeof(RebirthNpcActivityStatus),record.Status)&&record.Status!=RebirthNpcActivityStatus.Running;
    }
}
public static partial class RebirthNpcMissionRuntime
{
    private static void ReconcileCompletedEnds()
    {
        RebirthNpcMissionSession[] originals;
        lock(Sync){originals=new RebirthNpcMissionSession[UncertainEnds.Count];UncertainEnds.Values.CopyTo(originals,0);}
        foreach(var session in originals)
        {
            if(session?.Original==null||!session.EndCompleted||!session.Original.Current())continue;
            try
            {
                bool terminal=RebirthNpcActivityRegistry.HasTerminalMissionOriginal(session.Original.World,session.EntityId,session.ActivityId,session.LeaseId,session.AcceptedRevision);
                if(!terminal&&session.HasEndOutcome)
                    terminal=RebirthNpcActivityRegistry.TryReportResult(session.EntityId,session.ActivityId,session.LeaseId,session.AcceptedRevision,session.EndOutcome,session.EndDetail);
                if(!terminal)continue;
                lock(Sync)if(UncertainEnds.TryGetValue(session.ActivityId,out var retained)&&ReferenceEquals(retained,session))UncertainEnds.Remove(session.ActivityId);
            }
            catch{ /* Original remains retained; executor.End is never replayed. */ }
        }
    }
}
