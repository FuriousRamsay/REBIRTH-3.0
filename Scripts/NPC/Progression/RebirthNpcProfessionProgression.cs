using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

#nullable disable

public enum RebirthNpcProfession : byte
{
    Farming=0, Looting=1, Gathering=2, Mining=3, Salvaging=4, Cooking=5, BaseRepairs=6, Medicine=7
}

public enum RebirthNpcProfessionOutcomeStatus : byte
{
    Accepted=0, Failed=1, Cancelled=2, RolledBack=3, Compensated=4
}

public sealed class RebirthNpcProfessionOutcome
{
    public Guid OutcomeId;
    public RebirthNpcStableId NpcId;
    public RebirthNpcProfession Profession;
    public RebirthNpcProfessionOutcomeStatus Status;
    public string OperationId;
    public string TargetId;
    public int Quantity;
    public int Difficulty;
    public int ResourceCost;
    public int RestoredDurability;
    public long CompletedUtcTicks;
}

public sealed class RebirthNpcProfessionModifierSnapshot
{
    public RebirthNpcStableId NpcId;
    public RebirthNpcProfession Profession;
    public int Level;
    public long ProgressionRevision;
    public float SpeedMultiplier;
    public float YieldMultiplier;
    public float QualityMultiplier;
    public float ResourceEfficiencyMultiplier;
    public float SuccessMultiplier;
    public float TreatmentMultiplier;
}

public static class RebirthNpcProfessionDefinitions
{
    public static string TrackId(RebirthNpcProfession profession)
    {
        switch(profession)
        {
            case RebirthNpcProfession.Farming:return "profession.farming";
            case RebirthNpcProfession.Looting:return "profession.looting";
            case RebirthNpcProfession.Gathering:return "profession.gathering";
            case RebirthNpcProfession.Mining:return "profession.mining";
            case RebirthNpcProfession.Salvaging:return "profession.salvaging";
            case RebirthNpcProfession.Cooking:return "profession.cooking";
            case RebirthNpcProfession.BaseRepairs:return "profession.base_repairs";
            case RebirthNpcProfession.Medicine:return "profession.medicine";
            default:throw new ArgumentOutOfRangeException(nameof(profession));
        }
    }

    public static bool TryResolve(string value,out RebirthNpcProfession profession)
    {
        string s=(value??string.Empty).Trim().ToLowerInvariant().Replace("-","_").Replace(" ","_");
        if(s.StartsWith("profession."))s=s.Substring(11);
        switch(s)
        {
            case "farm":case "farming":case "harvest":profession=RebirthNpcProfession.Farming;return true;
            case "loot":case "looting":case "scavenge":case "scavenging":profession=RebirthNpcProfession.Looting;return true;
            case "gather":case "gathering":case "forage":case "foraging":profession=RebirthNpcProfession.Gathering;return true;
            case "mine":case "mining":profession=RebirthNpcProfession.Mining;return true;
            case "salvage":case "salvaging":case "dismantle":profession=RebirthNpcProfession.Salvaging;return true;
            case "cook":case "cooking":case "workstation_cooking":profession=RebirthNpcProfession.Cooking;return true;
            case "repair":case "repairs":case "base_repair":case "base_repairs":case "construction_repair":profession=RebirthNpcProfession.BaseRepairs;return true;
            case "medicine":case "medical":case "heal":case "healing":case "treatment":profession=RebirthNpcProfession.Medicine;return true;
            default:profession=default(RebirthNpcProfession);return false;
        }
    }

    public static void RegisterAll()
    {
        foreach(RebirthNpcProfession p in Enum.GetValues(typeof(RebirthNpcProfession)))
            RebirthNpcProgressionService.RegisterDefinition(new RebirthNpcProgressionDefinition(TrackId(p),RebirthNpcProgressionTrackKind.Profession,20,125,375,1,10000));
    }
}

public static class RebirthNpcProfessionProgressionService
{
    private static readonly object Sync=new object();
    private static readonly HashSet<Guid> Committed=new HashSet<Guid>();
    private static readonly Queue<Guid> CommitOrder=new Queue<Guid>();
    private const int MaxReplay=8192;
    private static bool initialized;
    private static long accepted,rejected,duplicates,evaluations,migrations;

    public static void EnsureInitialized()
    {
        lock(Sync){if(initialized)return;RebirthNpcProfessionDefinitions.RegisterAll();initialized=true;}
    }

    public static RebirthNpcProgressionAwardResult Commit(RebirthNpcProfessionOutcome outcome)
    {
        EnsureInitialized();
        if(outcome==null||outcome.OutcomeId==Guid.Empty||outcome.NpcId.IsEmpty)
        {Interlocked.Increment(ref rejected);return RebirthNpcProgressionAwardResult.Reject(RebirthNpcProgressionAwardStatus.Rejected,"invalid-profession-outcome","Outcome id and NPC id are required.",0);}
        if(outcome.Status!=RebirthNpcProfessionOutcomeStatus.Accepted)
        {Interlocked.Increment(ref rejected);return RebirthNpcProgressionAwardResult.Reject(RebirthNpcProgressionAwardStatus.Rejected,"non-committed-outcome","Failed, cancelled, rolled-back or compensated work cannot award profession XP.",0);}
        if(string.IsNullOrWhiteSpace(outcome.OperationId)||string.IsNullOrWhiteSpace(outcome.TargetId)||outcome.Quantity<=0)
        {Interlocked.Increment(ref rejected);return RebirthNpcProgressionAwardResult.Reject(RebirthNpcProgressionAwardStatus.Rejected,"invalid-profession-source","Committed operation, target and positive quantity are required.",0);}
        lock(Sync)
        {
            if(Committed.Contains(outcome.OutcomeId)){Interlocked.Increment(ref duplicates);return RebirthNpcProgressionAwardResult.Reject(RebirthNpcProgressionAwardStatus.Duplicate,"duplicate-profession-outcome","Profession outcome was already committed.",0);}
        }
        long xp=CalculateXp(outcome);
        float mentorshipMultiplier=RebirthNpcAdvancedProgressionService.PrepareMentorshipMultiplier(outcome.OutcomeId,outcome.NpcId,outcome.Profession,outcome.CompletedUtcTicks);
        xp=Math.Max(1,Math.Min(10000,(long)Math.Floor(xp*mentorshipMultiplier)));
        RebirthNpcProgressionAwardResult result=RebirthNpcProgressionService.Award(new RebirthNpcProgressionAwardRequest
        {
            AwardId=outcome.OutcomeId,NpcId=outcome.NpcId,ProgressionId=RebirthNpcProfessionDefinitions.TrackId(outcome.Profession),RequestedXp=xp,
            SourceType="profession."+outcome.Profession.ToString().ToLowerInvariant(),SourceId=outcome.OperationId+":"+outcome.TargetId,
            CreatedUtcTicks=outcome.CompletedUtcTicks>0?outcome.CompletedUtcTicks:DateTime.UtcNow.Ticks
        });
        if(result.Accepted)
        {
            lock(Sync)
            {
                if(Committed.Add(outcome.OutcomeId))
                {
                    CommitOrder.Enqueue(outcome.OutcomeId);
                    while(CommitOrder.Count>MaxReplay)Committed.Remove(CommitOrder.Dequeue());
                }
            }
            Interlocked.Increment(ref accepted);
            RebirthNpcAdvancedProgressionService.CommitMentorshipCredit(outcome.OutcomeId,outcome.NpcId,outcome.Profession,outcome.CompletedUtcTicks);
            RebirthNpcAdvancedProgressionService.OnProfessionAwardAccepted(outcome.NpcId,outcome.Profession,outcome.OutcomeId);
        }
        else Interlocked.Increment(ref rejected);
        return result;
    }

    private static long CalculateXp(RebirthNpcProfessionOutcome o)
    {
        long q=Math.Max(1,Math.Min(1000,o.Quantity)); long d=Math.Max(0,Math.Min(20,o.Difficulty));
        switch(o.Profession)
        {
            case RebirthNpcProfession.Farming:return Math.Min(10000,20+q*4+d*5);
            case RebirthNpcProfession.Looting:return Math.Min(10000,25+q*3+d*8);
            case RebirthNpcProfession.Gathering:return Math.Min(10000,10+q*3+d*4);
            case RebirthNpcProfession.Mining:return Math.Min(10000,15+q*4+d*6);
            case RebirthNpcProfession.Salvaging:return Math.Min(10000,20+q*5+d*6);
            case RebirthNpcProfession.Cooking:return Math.Min(10000,30+q*4+d*7);
            case RebirthNpcProfession.BaseRepairs:return Math.Min(10000,15+Math.Max(1,Math.Min(5000,o.RestoredDurability))/10+d*5);
            case RebirthNpcProfession.Medicine:return Math.Min(10000,35+q*5+d*10);
            default:return 1;
        }
    }

    public static RebirthNpcProfessionModifierSnapshot Evaluate(RebirthNpcStableId npcId,RebirthNpcProfession profession)
    {
        EnsureInitialized();Interlocked.Increment(ref evaluations);
        RebirthNpcProgressionView view;int level=1;long revision=0;
        if(RebirthNpcProgressionService.TryGetView(npcId,RebirthNpcProfessionDefinitions.TrackId(profession),out view)){level=view.Level;revision=view.ComponentRevision;}
        float t=(level-1f)/19f;
        var m=new RebirthNpcProfessionModifierSnapshot{NpcId=npcId,Profession=profession,Level=level,ProgressionRevision=revision,SpeedMultiplier=1f, YieldMultiplier=1f,QualityMultiplier=1f,ResourceEfficiencyMultiplier=1f,SuccessMultiplier=1f,TreatmentMultiplier=1f};
        switch(profession)
        {
            case RebirthNpcProfession.Farming:m.SpeedMultiplier=1f+0.30f*t;m.YieldMultiplier=1f+0.25f*t;m.ResourceEfficiencyMultiplier=1f+0.20f*t;break;
            case RebirthNpcProfession.Looting:m.SpeedMultiplier=1f+0.35f*t;m.QualityMultiplier=1f+0.25f*t;m.SuccessMultiplier=1f+0.15f*t;break;
            case RebirthNpcProfession.Gathering:m.SpeedMultiplier=1f+0.30f*t;m.YieldMultiplier=1f+0.30f*t;m.ResourceEfficiencyMultiplier=1f+0.15f*t;break;
            case RebirthNpcProfession.Mining:m.SpeedMultiplier=1f+0.25f*t;m.YieldMultiplier=1f+0.35f*t;m.ResourceEfficiencyMultiplier=1f+0.20f*t;break;
            case RebirthNpcProfession.Salvaging:m.SpeedMultiplier=1f+0.25f*t;m.YieldMultiplier=1f+0.30f*t;m.QualityMultiplier=1f+0.20f*t;break;
            case RebirthNpcProfession.Cooking:m.SpeedMultiplier=1f+0.30f*t;m.QualityMultiplier=1f+0.30f*t;m.ResourceEfficiencyMultiplier=1f+0.20f*t;break;
            case RebirthNpcProfession.BaseRepairs:m.SpeedMultiplier=1f+0.35f*t;m.QualityMultiplier=1f+0.20f*t;m.ResourceEfficiencyMultiplier=1f+0.25f*t;break;
            case RebirthNpcProfession.Medicine:m.SpeedMultiplier=1f+0.25f*t;m.ResourceEfficiencyMultiplier=1f+0.20f*t;m.SuccessMultiplier=1f+0.25f*t;m.TreatmentMultiplier=1f+0.35f*t;break;
        }
        RebirthNpcSpecializationDefinition specialization=RebirthNpcAdvancedProgressionService.GetSpecialization(npcId,profession);
        if(specialization!=null)
        {
            m.SpeedMultiplier*=specialization.SpeedMultiplier;m.YieldMultiplier*=specialization.YieldMultiplier;m.QualityMultiplier*=specialization.QualityMultiplier;
            m.ResourceEfficiencyMultiplier*=specialization.ResourceEfficiencyMultiplier;m.SuccessMultiplier*=specialization.SuccessMultiplier;m.TreatmentMultiplier*=specialization.TreatmentMultiplier;
        }
        return m;
    }

    public static float CapabilityScore(RebirthNpcStableId npcId,string professionOrWorkType)
    {
        RebirthNpcProfession p;if(!RebirthNpcProfessionDefinitions.TryResolve(professionOrWorkType,out p))return 0f;
        RebirthNpcProfessionModifierSnapshot m=Evaluate(npcId,p);
        return m.Level*100f + m.SpeedMultiplier*10f + m.QualityMultiplier*5f;
    }

    public static int MigrateLegacySkills(RebirthNpcStableId npcId,IDictionary<string,float> legacy)
    {
        EnsureInitialized();if(npcId.IsEmpty||legacy==null||legacy.Count==0)return 0;int count=0;
        foreach(KeyValuePair<string,float> pair in legacy)
        {
            RebirthNpcProfession p;if(!RebirthNpcProfessionDefinitions.TryResolve(pair.Key,out p)||pair.Value<=0f)continue;
            RebirthNpcProgressionView existing;if(RebirthNpcProgressionService.TryGetView(npcId,RebirthNpcProfessionDefinitions.TrackId(p),out existing))continue;
            int legacyLevel=Math.Max(1,Math.Min(20,(int)Math.Floor(pair.Value)+1));
            long floor=new RebirthNpcProgressionDefinition(RebirthNpcProfessionDefinitions.TrackId(p),RebirthNpcProgressionTrackKind.Profession).XpFloor(legacyLevel);
            if(floor<=0)continue;
            Guid id=DeterministicGuid(npcId,pair.Key,legacyLevel);
            RebirthNpcProgressionAwardResult r=RebirthNpcProgressionService.Award(new RebirthNpcProgressionAwardRequest{AwardId=id,NpcId=npcId,ProgressionId=RebirthNpcProfessionDefinitions.TrackId(p),RequestedXp=floor,SourceType="migration.legacy_settlement_skill",SourceId=pair.Key+":"+pair.Value,CreatedUtcTicks=DateTime.UtcNow.Ticks,IsAdministrative=true});
            if(r.Accepted){count++;Interlocked.Increment(ref migrations);}
        }
        return count;
    }

    private static Guid DeterministicGuid(RebirthNpcStableId id,string key,int level)
    {
        string s=id+"|"+(key??string.Empty).ToLowerInvariant()+"|"+level;byte[] b=System.Security.Cryptography.MD5.Create().ComputeHash(Encoding.UTF8.GetBytes(s));return new Guid(b);
    }

    public static string GetReport(){return "[REBIRTH NPC Profession Progression] accepted="+Interlocked.Read(ref accepted)+" rejected="+Interlocked.Read(ref rejected)+" duplicates="+Interlocked.Read(ref duplicates)+" evaluations="+Interlocked.Read(ref evaluations)+" migrations="+Interlocked.Read(ref migrations);}
    public static void ResetForWorldChange(){lock(Sync){Committed.Clear();CommitOrder.Clear();}}
}

public static class RebirthNpcProfessionOutcomePublisher
{
    private static RebirthNpcProgressionAwardResult Publish(Guid outcomeId,RebirthNpcStableId npc,RebirthNpcProfession profession,string operation,string target,int quantity,int difficulty,int cost,int restored)
    {return RebirthNpcProfessionProgressionService.Commit(new RebirthNpcProfessionOutcome{OutcomeId=outcomeId,NpcId=npc,Profession=profession,Status=RebirthNpcProfessionOutcomeStatus.Accepted,OperationId=operation,TargetId=target,Quantity=quantity,Difficulty=difficulty,ResourceCost=cost,RestoredDurability=restored,CompletedUtcTicks=DateTime.UtcNow.Ticks});}
    public static RebirthNpcProgressionAwardResult Farming(Guid id,RebirthNpcStableId npc,string crop,int quantity,int difficulty){return Publish(id,npc,RebirthNpcProfession.Farming,"farming.completed",crop,quantity,difficulty,0,0);}
    public static RebirthNpcProgressionAwardResult Looting(Guid id,RebirthNpcStableId npc,string container,int quantity,int difficulty){return Publish(id,npc,RebirthNpcProfession.Looting,"looting.transaction_committed",container,quantity,difficulty,0,0);}
    public static RebirthNpcProgressionAwardResult Gathering(Guid id,RebirthNpcStableId npc,string resource,int quantity,int difficulty){return Publish(id,npc,RebirthNpcProfession.Gathering,"gathering.transaction_committed",resource,quantity,difficulty,0,0);}
    public static RebirthNpcProgressionAwardResult Mining(Guid id,RebirthNpcStableId npc,string material,int quantity,int difficulty){return Publish(id,npc,RebirthNpcProfession.Mining,"mining.transaction_committed",material,quantity,difficulty,0,0);}
    public static RebirthNpcProgressionAwardResult Salvaging(Guid id,RebirthNpcStableId npc,string target,int quantity,int difficulty){return Publish(id,npc,RebirthNpcProfession.Salvaging,"salvaging.transaction_committed",target,quantity,difficulty,0,0);}
    public static RebirthNpcProgressionAwardResult Cooking(Guid id,RebirthNpcStableId npc,string recipe,int quantity,int difficulty,int ingredients){return Publish(id,npc,RebirthNpcProfession.Cooking,"cooking.output_committed",recipe,quantity,difficulty,ingredients,0);}
    public static RebirthNpcProgressionAwardResult BaseRepairs(Guid id,RebirthNpcStableId npc,string block,int durability,int difficulty,int materials){return Publish(id,npc,RebirthNpcProfession.BaseRepairs,"repair.transaction_committed",block,1,difficulty,materials,durability);}
    public static RebirthNpcProgressionAwardResult Medicine(Guid id,RebirthNpcStableId npc,string patient,int treatmentUnits,int difficulty,int consumables){return Publish(id,npc,RebirthNpcProfession.Medicine,"medicine.treatment_committed",patient,treatmentUnits,difficulty,consumables,0);}
}

public static class RebirthNpcProfessionWorkExecutor
{
    public static int ApplyDurationSeconds(RebirthNpcStableId npc,string professionOrWorkType,int baseSeconds){RebirthNpcProfession p;if(!RebirthNpcProfessionDefinitions.TryResolve(professionOrWorkType,out p))return Math.Max(1,baseSeconds);return Math.Max(1,(int)Math.Ceiling(baseSeconds/RebirthNpcProfessionProgressionService.Evaluate(npc,p).SpeedMultiplier));}
    public static int ApplyYield(RebirthNpcStableId npc,RebirthNpcProfession profession,int baseYield){return Math.Max(0,(int)Math.Floor(baseYield*RebirthNpcProfessionProgressionService.Evaluate(npc,profession).YieldMultiplier));}
    public static int ApplyResourceCost(RebirthNpcStableId npc,RebirthNpcProfession profession,int baseCost){float e=RebirthNpcProfessionProgressionService.Evaluate(npc,profession).ResourceEfficiencyMultiplier;return Math.Max(0,(int)Math.Ceiling(baseCost/e));}
    public static float ApplyQuality(RebirthNpcStableId npc,RebirthNpcProfession profession,float baseQuality){return baseQuality*RebirthNpcProfessionProgressionService.Evaluate(npc,profession).QualityMultiplier;}
    public static float ApplyTreatment(RebirthNpcStableId npc,float baseTreatment){return baseTreatment*RebirthNpcProfessionProgressionService.Evaluate(npc,RebirthNpcProfession.Medicine).TreatmentMultiplier;}
}
