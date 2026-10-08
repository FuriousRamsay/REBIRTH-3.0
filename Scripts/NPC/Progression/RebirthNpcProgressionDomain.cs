using System;
using System.Collections.Generic;
using System.Globalization;

#nullable disable

public enum RebirthNpcProgressionTrackKind : byte { Generic=0, Attribute=1, WeaponSpecialty=2, Profession=3, AnimalSkill=4 }
public enum RebirthNpcProgressionAwardStatus : byte { Accepted=0, Duplicate=1, Rejected=2, AtMaximum=3 }

public sealed class RebirthNpcProgressionDefinition
{
    public string Id; public RebirthNpcProgressionTrackKind Kind; public int MaximumLevel;
    public long QuadraticA; public long LinearB; public int CurveVersion; public int MaximumAward;
    public RebirthNpcProgressionDefinition(string id, RebirthNpcProgressionTrackKind kind, int maxLevel=20, long a=125, long b=375, int curveVersion=1, int maximumAward=10000)
    {
        if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Progression definition id is required.", nameof(id));
        if (maxLevel < 1 || maxLevel > 1000) throw new ArgumentOutOfRangeException(nameof(maxLevel));
        if (a < 0 || b < 0 || (a == 0 && b == 0)) throw new ArgumentOutOfRangeException(nameof(a));
        if (curveVersion < 1) throw new ArgumentOutOfRangeException(nameof(curveVersion));
        if (maximumAward < 1) throw new ArgumentOutOfRangeException(nameof(maximumAward));
        Id=id.Trim(); Kind=kind; MaximumLevel=maxLevel; QuadraticA=a; LinearB=b; CurveVersion=curveVersion; MaximumAward=maximumAward;
    }
    public long XpFloor(int level)
    {
        int clamped=Math.Max(1,Math.Min(MaximumLevel,level)); long n=clamped-1L;
        checked { return checked(QuadraticA*n*n + LinearB*n); }
    }
    public int ResolveLevel(long cumulativeXp)
    {
        if (cumulativeXp <= 0) return 1;
        int lo=1, hi=MaximumLevel;
        while(lo<hi){int mid=lo+(hi-lo+1)/2;if(XpFloor(mid)<=cumulativeXp)lo=mid;else hi=mid-1;}
        return lo;
    }
}

public sealed class RebirthNpcProgressionEntryRecord
{
    public string ProgressionId; public long CumulativeXp; public int CachedLevel; public int AppliedCurveVersion;
    public long EntryRevision; public long LastAwardUtcTicks; public Guid LastAwardId;
    public RebirthNpcProgressionEntryRecord Clone(){return (RebirthNpcProgressionEntryRecord)MemberwiseClone();}
}
public sealed class RebirthNpcProgressionRecord
{
    public RebirthNpcStableId NpcId; public int SchemaVersion=1; public long ComponentRevision;
    public readonly Dictionary<string,RebirthNpcProgressionEntryRecord> Entries=new Dictionary<string,RebirthNpcProgressionEntryRecord>(StringComparer.OrdinalIgnoreCase);
    public RebirthNpcProgressionRecord Clone(){var r=new RebirthNpcProgressionRecord{NpcId=NpcId,SchemaVersion=SchemaVersion,ComponentRevision=ComponentRevision};foreach(var p in Entries)r.Entries[p.Key]=p.Value.Clone();return r;}
}
public sealed class RebirthNpcProgressionAwardRequest
{
    public Guid AwardId; public RebirthNpcStableId NpcId; public string ProgressionId; public long RequestedXp;
    public string SourceType; public string SourceId; public long CreatedUtcTicks; public bool IsAdministrative;
}
public sealed class RebirthNpcProgressionAwardResult
{
    public RebirthNpcProgressionAwardStatus Status; public string Code; public string Message; public long RequestedXp; public long AcceptedXp;
    public long OldXp; public long NewXp; public int OldLevel; public int NewLevel; public int CrossedLevels; public long ComponentRevision; public long EntryRevision;
    public bool Accepted { get { return Status==RebirthNpcProgressionAwardStatus.Accepted; } }
    public static RebirthNpcProgressionAwardResult Reject(RebirthNpcProgressionAwardStatus status,string code,string message,long requested){return new RebirthNpcProgressionAwardResult{Status=status,Code=code,Message=message,RequestedXp=requested};}
}
public sealed class RebirthNpcProgressionView
{
    public RebirthNpcStableId NpcId; public string ProgressionId; public long CumulativeXp; public int Level; public int MaximumLevel;
    public long CurrentFloor; public long NextFloor; public long XpToNext; public long ProgressNumerator; public long ProgressDenominator; public long ComponentRevision; public long EntryRevision;
    public string ToLine(){return NpcId+" "+ProgressionId+" level="+Level+"/"+MaximumLevel+" xp="+CumulativeXp+" next="+XpToNext+" revision="+ComponentRevision+":"+EntryRevision;}
}
