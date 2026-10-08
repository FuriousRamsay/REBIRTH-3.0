using System;
using System.Text;

#nullable disable

public static class RebirthNpcProgressionAcip03Qualification
{
    public static string Run()
    {
        RebirthNpcProfessionProgressionService.EnsureInitialized();
        RebirthNpcStableId id=RebirthNpcStableId.NewId();int pass=0,total=0;StringBuilder b=new StringBuilder("[REBIRTH NPC ACIP-03 Qualification]");
        foreach(RebirthNpcProfession p in Enum.GetValues(typeof(RebirthNpcProfession)))
        {
            total++;Guid eventId=Guid.NewGuid();
            RebirthNpcProgressionAwardResult r=RebirthNpcProfessionProgressionService.Commit(new RebirthNpcProfessionOutcome{OutcomeId=eventId,NpcId=id,Profession=p,Status=RebirthNpcProfessionOutcomeStatus.Accepted,OperationId="qualification."+p,TargetId="target",Quantity=10,Difficulty=5,CompletedUtcTicks=DateTime.UtcNow.Ticks});
            RebirthNpcProgressionView v;bool ok=r.Accepted&&RebirthNpcProgressionService.TryGetView(id,RebirthNpcProfessionDefinitions.TrackId(p),out v)&&v.CumulativeXp>0;
            if(ok)pass++;b.AppendLine().Append("  ").Append(p).Append('=').Append(ok?"PASS":"FAIL");
            total++;RebirthNpcProgressionAwardResult duplicate=RebirthNpcProfessionProgressionService.Commit(new RebirthNpcProfessionOutcome{OutcomeId=eventId,NpcId=id,Profession=p,Status=RebirthNpcProfessionOutcomeStatus.Accepted,OperationId="qualification."+p,TargetId="target",Quantity=10});
            bool dup=duplicate.Status==RebirthNpcProgressionAwardStatus.Duplicate;if(dup)pass++;b.AppendLine().Append("  ").Append(p).Append(" duplicate=").Append(dup?"PASS":"FAIL");
            total++;RebirthNpcProgressionAwardResult failed=RebirthNpcProfessionProgressionService.Commit(new RebirthNpcProfessionOutcome{OutcomeId=Guid.NewGuid(),NpcId=id,Profession=p,Status=RebirthNpcProfessionOutcomeStatus.RolledBack,OperationId="qualification."+p,TargetId="target",Quantity=10});
            bool rejected=!failed.Accepted;if(rejected)pass++;b.AppendLine().Append("  ").Append(p).Append(" rollback=").Append(rejected?"PASS":"FAIL");
            total++;RebirthNpcProfessionModifierSnapshot m=RebirthNpcProfessionProgressionService.Evaluate(id,p);bool consumer=m.SpeedMultiplier>=1f&&m.Level>=1;if(consumer)pass++;b.AppendLine().Append("  ").Append(p).Append(" consumer=").Append(consumer?"PASS":"FAIL");
        }
        total++;int baseDuration=100,adjusted=RebirthNpcProfessionWorkExecutor.ApplyDurationSeconds(id,"farming",baseDuration);bool direct=adjusted<=baseDuration;if(direct)pass++;b.AppendLine().Append("  direct-executor=").Append(direct?"PASS":"FAIL");
        b.AppendLine().Append("Result: ").Append(pass==total?"PASS":"FAIL").Append(" (").Append(pass).Append('/').Append(total).Append(')');return b.ToString();
    }
}
