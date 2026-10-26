using System;
using System.Collections.Generic;
using System.Globalization;

public static partial class RebirthNpcWorkReservationService
{
    private static RebirthNpcAncillaryOriginalScope reservationScope;
    private static bool UseReservationScope(){if(!RebirthNpcWorkReleaseGate.Enabled)return false;lock(Sync)return RebirthNpcAncillaryOriginalScope.Use(ref reservationScope,ById.Count==0&&ByAssignment.Count==0&&ByTarget.Count==0);}
    private static bool UseLiteratureReservationScope(ulong assignmentId)
    {
        if(!RebirthNpcWorkAssignmentService.TryGet(assignmentId,out var assignment)||!RebirthNpcQualifiedWorkGate.Allows(assignment))return false;
        lock(Sync)return RebirthNpcAncillaryOriginalScope.Use(ref reservationScope,ById.Count==0&&ByAssignment.Count==0&&ByTarget.Count==0);
    }
    internal static bool CanRetireEmptyReservations{get{lock(Sync)return ThreadManager.IsMainThread()&&ById.Count==0&&ByAssignment.Count==0&&ByTarget.Count==0&&RebirthNpcWorkCallbackObservation.IsIdle;}}
    internal static RebirthNpcSaveQuarantineDomain PrepareReservationQuarantine(RebirthNpcSaveQuarantineScope scope)
    {
        lock(Sync)
        {
            RebirthNpcAncillaryOriginalScope.RequireScope(scope,reservationScope);if(!CanRetireEmptyReservations)return CapturePackageCustody(scope).Domain;
            var byId=ById;var byAssignment=ByAssignment;var byTarget=ByTarget;var original=reservationScope;ulong nextId=nextReservationId;long a=granted,b=denied,c=renewed,d=released,e=expired;var doc=RebirthNpcAncillaryOriginalScope.Image("workReservations",original,new[]{a,b,c,d,e});doc.DocumentElement.SetAttribute("emptyOnly","1");doc.DocumentElement.SetAttribute("nextId",nextId.ToString(CultureInfo.InvariantCulture));var nextById=new Dictionary<ulong,RebirthNpcWorkReservation>();var nextByAssignment=new Dictionary<ulong,ulong>();var nextByTarget=new Dictionary<string,List<ulong>>(StringComparer.OrdinalIgnoreCase);
            return new RebirthNpcSaveQuarantineDomain("work-reservations-empty",doc,
                ()=>{lock(Sync)return CanRetireEmptyReservations&&ReferenceEquals(ById,byId)&&ReferenceEquals(ByAssignment,byAssignment)&&ReferenceEquals(ByTarget,byTarget)&&ReferenceEquals(reservationScope,original)&&nextReservationId==nextId&&granted==a&&denied==b&&renewed==c&&released==d&&expired==e;},
                ()=>{lock(Sync){ById=nextById;ByAssignment=nextByAssignment;ByTarget=nextByTarget;reservationScope=null;nextReservationId=1;granted=denied=renewed=released=expired=0;}},
                ()=>{lock(Sync){ById=byId;ByAssignment=byAssignment;ByTarget=byTarget;reservationScope=original;nextReservationId=nextId;granted=a;denied=b;renewed=c;released=d;expired=e;}});
        }
    }
}
public static partial class RebirthNpcWorkAssignmentCoordinator
{
    private static RebirthNpcAncillaryOriginalScope assignmentCoordinatorScope;
    private static bool UseAssignmentCoordinatorScope(){if(!RebirthNpcWorkReleaseGate.Enabled)return false;lock(Sync)return RebirthNpcAncillaryOriginalScope.Use(ref assignmentCoordinatorScope,Sessions.Count==0&&SessionSchedule.Count==0);}
    private static bool UseLiteratureCoordinatorScope(ulong assignmentId)
    {
        if(!RebirthNpcWorkAssignmentService.TryGet(assignmentId,out var assignment)||!RebirthNpcQualifiedWorkGate.Allows(assignment))return false;
        lock(Sync)return RebirthNpcAncillaryOriginalScope.Use(ref assignmentCoordinatorScope,Sessions.Count==0&&SessionSchedule.Count==0);
    }
    private static bool UseLiteratureCoordinatorTickScope()
    {
        ulong[] ids;lock(Sync){ids=new ulong[Sessions.Count];Sessions.Keys.CopyTo(ids,0);}
        for(int i=0;i<ids.Length;i++)
            if(!RebirthNpcWorkAssignmentService.TryGet(ids[i],out var assignment)||!RebirthNpcQualifiedWorkGate.Allows(assignment))return false;
        lock(Sync)return RebirthNpcAncillaryOriginalScope.Use(ref assignmentCoordinatorScope,Sessions.Count==0&&SessionSchedule.Count==0);
    }
    internal static bool CanRetireEmptyAssignmentCoordinator{get{lock(Sync)return ThreadManager.IsMainThread()&&Sessions.Count==0&&SessionSchedule.Count==0&&RebirthNpcWorkCallbackObservation.IsIdle;}}
    internal static RebirthNpcSaveQuarantineDomain PrepareAssignmentCoordinatorQuarantine(RebirthNpcSaveQuarantineScope scope)
    {
        lock(Sync)
        {
            RebirthNpcAncillaryOriginalScope.RequireScope(scope,assignmentCoordinatorScope);if(!CanRetireEmptyAssignmentCoordinator)return CapturePackageCustody(scope).Domain;
            var records=Sessions;var order=SessionSchedule;var original=assignmentCoordinatorScope;long a=started,b=completed,c=failed,d=suspended,e=noExecutor,f=executorFaults;var doc=RebirthNpcAncillaryOriginalScope.Image("workAssignmentCoordinator",original,new[]{a,b,c,d,e,f});doc.DocumentElement.SetAttribute("emptyOnly","1");var next=new Dictionary<ulong,Session>();var nextOrder=new RebirthNpcFairRoundRobin<ulong>();
            return new RebirthNpcSaveQuarantineDomain("work-assignment-controller-empty",doc,
                ()=>{lock(Sync)return CanRetireEmptyAssignmentCoordinator&&ReferenceEquals(Sessions,records)&&ReferenceEquals(SessionSchedule,order)&&ReferenceEquals(assignmentCoordinatorScope,original)&&started==a&&completed==b&&failed==c&&suspended==d&&noExecutor==e&&executorFaults==f;},
                ()=>{lock(Sync){Sessions=next;SessionSchedule=nextOrder;assignmentCoordinatorScope=null;started=completed=failed=suspended=noExecutor=executorFaults=0;}},
                ()=>{lock(Sync){Sessions=records;SessionSchedule=order;assignmentCoordinatorScope=original;started=a;completed=b;failed=c;suspended=d;noExecutor=e;executorFaults=f;}});
        }
    }
}
public static partial class RebirthNpcWorkstationExecutionService
{
    private static RebirthNpcAncillaryOriginalScope workstationScope;
    private static bool UseWorkstationScope(){if(!RebirthNpcWorkReleaseGate.Enabled)return false;lock(Sync)return RebirthNpcAncillaryOriginalScope.Use(ref workstationScope,Jobs.Count==0&&TerminalHistory.Count==0);}
    internal static bool CanRetireEmptyWorkstations{get{lock(Sync)return ThreadManager.IsMainThread()&&Jobs.Count==0&&TerminalHistory.Count==0&&RebirthNpcWorkCallbackObservation.IsIdle;}}
    private static long[] JobCounts()=>new[]{submitted,rejected,supplyPackages,queueAttempts,queued,started,collections,completed,suspended,failed,cancelled,missingStations,busyStations,reconciliations};
    private static void SetJobCounts(long[] v){submitted=v[0];rejected=v[1];supplyPackages=v[2];queueAttempts=v[3];queued=v[4];started=v[5];collections=v[6];completed=v[7];suspended=v[8];failed=v[9];cancelled=v[10];missingStations=v[11];busyStations=v[12];reconciliations=v[13];}
    internal static RebirthNpcSaveQuarantineDomain PrepareWorkstationQuarantine(RebirthNpcSaveQuarantineScope scope)
    {
        lock(Sync)
        {
            RebirthNpcAncillaryOriginalScope.RequireScope(scope,workstationScope);if(!CanRetireEmptyWorkstations)return CapturePackageCustody(scope).Domain;var records=Jobs;var history=TerminalHistory;var original=workstationScope;var counts=JobCounts();var doc=RebirthNpcAncillaryOriginalScope.Image("workstationJobs",original,counts);doc.DocumentElement.SetAttribute("emptyOnly","1");var next=new Dictionary<Guid,Job>();var nextHistory=new Queue<Guid>();
            return new RebirthNpcSaveQuarantineDomain("workstation-jobs-empty",doc,
                ()=>{lock(Sync){if(!CanRetireEmptyWorkstations||!ReferenceEquals(Jobs,records)||!ReferenceEquals(TerminalHistory,history)||!ReferenceEquals(workstationScope,original))return false;var current=JobCounts();for(int i=0;i<current.Length;i++)if(current[i]!=counts[i])return false;return true;}},
                ()=>{lock(Sync){Jobs=next;TerminalHistory=nextHistory;workstationScope=null;SetJobCounts(new long[14]);}},
                ()=>{lock(Sync){Jobs=records;TerminalHistory=history;workstationScope=original;SetJobCounts(counts);}});
        }
    }
}
