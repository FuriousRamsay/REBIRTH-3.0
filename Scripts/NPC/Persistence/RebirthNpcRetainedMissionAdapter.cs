using System;
using System.Collections.Generic;

public static partial class RebirthNpcMissionRuntime
{
    private static string RetainedMissionFields(RebirthNpcMissionSession s)=>RebirthNpcRetainedControllerCustody.Fields(s.EntityId,s.LeaseId,s.ActivityId,
        s.AcceptedRevision,s.IsRecovery,s.EndCompleted,s.HasEndOutcome,s.EndOutcome,s.EndDetail,s.Executor?.ExecutorId,
        RebirthNpcExecutionLeaseRegistry.RetainedControllerLeaseImage(s.CapturedLease));
    internal static RebirthNpcRetainedControllerCustody.Member CaptureControllerCustody(RebirthNpcSaveQuarantineScope scope)
    {
        lock(Sync)
        {
            var records=Sessions;var world=controllerWorld;ulong generation=controllerGeneration;long timer=nextTickUtcTicks;
            var ends=UncertainEnds;var observed=new Dictionary<ulong,RebirthNpcMissionSession>(ends);
            var images=new Dictionary<ulong,string>();var executors=new Dictionary<ulong,IRebirthNpcMissionExecutor>();
            var originals=new Dictionary<ulong,RebirthNpcControllerOriginalScope>();var leases=new Dictionary<ulong,RebirthNpcExecutionLease>();
            var endingOwners=new List<RebirthNpcRetainedControllerCustody.Owner>();
            foreach(var pair in ends)
            {
                if(pair.Value==null||pair.Value.ActivityId!=pair.Key||pair.Value.Original==null)throw new InvalidOperationException("Mission ending original missing");
                images.Add(pair.Key,RetainedMissionFields(pair.Value));executors.Add(pair.Key,pair.Value.Executor);originals.Add(pair.Key,pair.Value.Original);leases.Add(pair.Key,pair.Value.CapturedLease);
                endingOwners.Add(new RebirthNpcRetainedControllerCustody.Owner{Original=pair.Value.Original,Activity=pair.Value.ActivityId,Lease=pair.Value.LeaseId,
                    NativeNavigator=pair.Value.Original.Actor.getNavigator()});
            }
            var next=new Dictionary<int,RebirthNpcMissionSession>();var nextEnds=new Dictionary<ulong,RebirthNpcMissionSession>();
            var keys=new List<ulong>(images.Keys);keys.Sort();var endingImage=new System.Text.StringBuilder();
            foreach(var key in keys)endingImage.Append(RebirthNpcRetainedControllerCustody.Fields(key,images[key]));
            bool Current()
            {
                lock(Sync)
                {
                    if(!ReferenceEquals(Sessions,records)||!ReferenceEquals(controllerWorld,world)||controllerGeneration!=generation||nextTickUtcTicks!=timer||
                        !ReferenceEquals(UncertainEnds,ends)||ends.Count!=observed.Count)return false;
                    foreach(var pair in observed)if(!ends.TryGetValue(pair.Key,out var value)||!ReferenceEquals(value,pair.Value)||
                        !ReferenceEquals(value.Executor,executors[pair.Key])||!ReferenceEquals(value.Original,originals[pair.Key])||
                        !ReferenceEquals(value.CapturedLease,leases[pair.Key])||RetainedMissionFields(value)!=images[pair.Key])return false;
                    return true;
                }
            }
            return RebirthNpcRetainedControllerCustody.Capture("mission-controller-empty",scope,world,generation,timer,records,
                s=>s.Original,s=>s.LeaseId,s=>s.ActivityId,RetainedMissionFields,s=>new object[]{s.Original,s.Executor,s.CapturedLease},Current,
                ()=>{lock(Sync)return !ReferenceEquals(Sessions,records)&&!ReferenceEquals(UncertainEnds,ends)&&(world==null||!ReferenceEquals(controllerWorld,world));},
                ()=>{lock(Sync){Sessions=next;UncertainEnds=nextEnds;controllerWorld=null;controllerGeneration=0;nextTickUtcTicks=0;}},
                ()=>{lock(Sync){Sessions=records;UncertainEnds=ends;controllerWorld=world;controllerGeneration=generation;nextTickUtcTicks=timer;}},endingOwners.ToArray(),endingImage.ToString());
        }
    }
}
