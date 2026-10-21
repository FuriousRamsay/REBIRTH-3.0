#if DEBUG
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

public static partial class RebirthNpcWorkReservationService
{
    internal static void VerifyActualHeldReservations(string confirmation)
    {
        if(!RebirthNpcQualificationMutationGuard.Allows(confirmation)||RebirthNpcWorkReleaseGate.Enabled)throw new InvalidOperationException("Actual held disposable fixture required");
        lock(Sync)
        {
            var byId=ById;var byAssignment=ByAssignment;var byTarget=ByTarget;string before=HeldReservationImage();
            GetReport();if(TryReserve(null,null,out _,out _)!=RebirthNpcWorkAssignmentResult.InvalidRequest)throw new InvalidOperationException("Held reservation admission changed");
            foreach(var pair in ById){var row=pair.Value;if(Renew(pair.Key,TimeSpan.FromSeconds(1),out _)||Release(pair.Key,"fixture")||ReleaseAssignment(row.AssignmentId,"fixture")||TryGetForAssignment(row.AssignmentId,out _))throw new InvalidOperationException("Held original reservation mutated");break;}
            if(ById.Count!=0||ByAssignment.Count!=0||ByTarget.Count!=0){bool refused=false;try{Reset();}catch(InvalidOperationException){refused=true;}if(!refused)throw new InvalidOperationException("Opaque original reservation reset admitted");}
            if(!ReferenceEquals(ById,byId)||!ReferenceEquals(ByAssignment,byAssignment)||!ReferenceEquals(ByTarget,byTarget)||HeldReservationImage()!=before)throw new InvalidOperationException("Actual held reservation image changed");
        }
    }
    private static string HeldReservationImage()
    {
        if(ById.Count>8192||ByAssignment.Count>8192||ByTarget.Count>8192)throw new InvalidOperationException("Actual reservation fixture observation bound exceeded");var b=new StringBuilder();long text=0;var ids=new List<ulong>(ById.Keys);ids.Sort();foreach(ulong id in ids){var row=ById[id];if(row==null||row.TargetKey==null||row.TargetKey.Length>65536||(text+=row.TargetKey.Length)>4L*1024*1024)throw new InvalidOperationException("Actual reservation row unknown");b.Append(id).Append('/').Append(row.ReservationId).Append('/').Append(row.AssignmentId).Append('/').Append(row.NpcId).Append('/').Append(row.TargetKey.Length).Append(':').Append(row.TargetKey).Append('/').Append(row.Slot).Append('/').Append(row.ExpiresUtcTicks).Append('/').Append(row.AssignmentRevision).Append(';');}ids=new List<ulong>(ByAssignment.Keys);ids.Sort();foreach(ulong id in ids)b.Append('a').Append(id).Append('/').Append(ByAssignment[id]).Append(';');var keys=new List<string>(ByTarget.Keys);keys.Sort(StringComparer.OrdinalIgnoreCase);long count=0;foreach(string key in keys){if(key.Length>65536||(text+=key.Length)>4L*1024*1024||ByTarget[key]==null||(count+=ByTarget[key].Count)>8192)throw new InvalidOperationException("Actual reservation target graph unknown");b.Append('t').Append(key.Length).Append(':').Append(key).Append('/');foreach(ulong id in ByTarget[key])b.Append(id).Append(',');b.Append(';');}b.Append('|').Append(nextReservationId).Append('/').Append(granted).Append('/').Append(denied).Append('/').Append(renewed).Append('/').Append(released).Append('/').Append(expired);return b.ToString();
    }
}
public static partial class RebirthNpcWorkstationExecutionService
{
    internal static void VerifyActualHeldWorkstations(string confirmation)
    {
        if(!RebirthNpcQualificationMutationGuard.Allows(confirmation)||RebirthNpcWorkReleaseGate.Enabled)throw new InvalidOperationException("Actual held disposable fixture required");
        lock(Sync){var records=Jobs;var history=TerminalHistory;var counts=JobCounts();int rows=Jobs.Count,terminal=TerminalHistory.Count;if(TrySubmit(null,out _,out _)||Cancel(Guid.Empty,out _))throw new InvalidOperationException("Held native job admission changed");Tick(1);if(rows!=0||terminal!=0){bool refused=false;try{ResetForWorldChange();}catch(InvalidOperationException){refused=true;}if(!refused)throw new InvalidOperationException("Opaque native job reset admitted");}var after=JobCounts();for(int i=0;i<counts.Length;i++)if(counts[i]!=after[i])throw new InvalidOperationException("Held native job diagnostic changed");if(!ReferenceEquals(records,Jobs)||!ReferenceEquals(history,TerminalHistory)||Jobs.Count!=rows||TerminalHistory.Count!=terminal)throw new InvalidOperationException("Held native jobs changed");}
    }
}
#endif
