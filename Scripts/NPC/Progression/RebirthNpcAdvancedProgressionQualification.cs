using System;
using System.Text;

#nullable disable

public static class RebirthNpcAdvancedProgressionQualificationService
{
    public static string Run()
    {
        RebirthNpcAdvancedProgressionService.EnsureInitialized();
        var b=new StringBuilder("[REBIRTH NPC ACIP-04 Qualification]");
        RebirthNpcCertificationGraphResult graph=RebirthNpcAdvancedProgressionService.ValidateCertificationGraph();
        Append(b,"certification-graph",graph.Valid,graph.ToReport());
        var mentor=new RebirthNpcStableId(0xA401,0xB401);var trainee=new RebirthNpcStableId(0xA402,0xB402);
        Seed(mentor,RebirthNpcProfession.Farming,12);Seed(trainee,RebirthNpcProfession.Farming,4);
        Guid session=Guid.NewGuid();string reason;
        bool started=RebirthNpcAdvancedProgressionService.StartMentorship(session,mentor,trainee,RebirthNpcProfession.Farming,TimeSpan.TicksPerHour,20f,out reason);
        Append(b,"mentorship-start",started,reason);
        Guid outcome=Guid.NewGuid();float mult=RebirthNpcAdvancedProgressionService.PrepareMentorshipMultiplier(outcome,trainee,RebirthNpcProfession.Farming,DateTime.UtcNow.Ticks);
        Append(b,"mentorship-contribution",mult>1f,"multiplier="+mult);
        RebirthNpcAdvancedProgressionService.CommitMentorshipCredit(outcome,trainee,RebirthNpcProfession.Farming,DateTime.UtcNow.Ticks);
        float duplicate=RebirthNpcAdvancedProgressionService.PrepareMentorshipMultiplier(outcome,trainee,RebirthNpcProfession.Farming,DateTime.UtcNow.Ticks);
        Append(b,"mentorship-replay",duplicate==1f,"secondMultiplier="+duplicate);
        bool active=RebirthNpcAdvancedProgressionService.UpdateMentorshipPresence(session,true,true,100f,DateTime.UtcNow.Ticks);
        Append(b,"mentorship-range-termination",!active,"active="+active);
        var worker=new RebirthNpcStableId(0xA403,0xB403);Seed(worker,RebirthNpcProfession.Farming,12);
        for(int i=0;i<25;i++)RebirthNpcAdvancedProgressionService.OnProfessionAwardAccepted(worker,RebirthNpcProfession.Farming,Guid.NewGuid());
        string cert="cert.farming.journeyman";Append(b,"certification-unlock",RebirthNpcAdvancedProgressionService.HasCertification(worker,cert),cert);
        var spec=RebirthNpcAdvancedProgressionService.GetSpecialization(worker,RebirthNpcProfession.Farming);Append(b,"specialization-resolution",spec!=null,spec==null?"none":spec.Id);
        float cap=RebirthNpcAdvancedProgressionService.CapabilityMultiplier(worker,RebirthNpcProfession.Farming);Append(b,"specialization-consumer",cap>1f,"capability="+cap);
        Append(b,"work-eligibility",RebirthNpcAdvancedProgressionService.IsWorkEligible(worker,RebirthNpcProfession.Farming,10,cert),"level+certification");
        return b.ToString();
    }
    private static void Seed(RebirthNpcStableId id,RebirthNpcProfession p,int level){var d=new RebirthNpcProgressionDefinition(RebirthNpcProfessionDefinitions.TrackId(p),RebirthNpcProgressionTrackKind.Profession);long xp=d.XpFloor(level);if(xp<=0)return;RebirthNpcProgressionService.Award(new RebirthNpcProgressionAwardRequest{AwardId=Guid.NewGuid(),NpcId=id,ProgressionId=d.Id,RequestedXp=Math.Min(10000,xp),SourceType="qualification.acip04",SourceId="seed",CreatedUtcTicks=DateTime.UtcNow.Ticks,IsAdministrative=true});if(xp>10000)RebirthNpcProgressionService.Award(new RebirthNpcProgressionAwardRequest{AwardId=Guid.NewGuid(),NpcId=id,ProgressionId=d.Id,RequestedXp=xp-10000,SourceType="qualification.acip04",SourceId="seed2",CreatedUtcTicks=DateTime.UtcNow.Ticks,IsAdministrative=true});}
    private static void Append(StringBuilder b,string name,bool pass,string detail){b.Append("\n  ").Append(name).Append('=').Append(pass?"PASS":"FAIL").Append(" ").Append(detail);}
}
