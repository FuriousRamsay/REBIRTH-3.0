using System;
using System.Text;

#nullable disable

public static class RebirthNpcProgressionQualificationService
{
    public static string RunSelfTest()
    {
        RebirthNpcProgressionService.EnsureInitialized();var id=RebirthNpcStableId.NewId();Guid award=Guid.NewGuid();
        var first=RebirthNpcProgressionService.Award(new RebirthNpcProgressionAwardRequest{AwardId=award,NpcId=id,ProgressionId="profession.general",RequestedXp=4000,SourceType="qualification",SourceId="multi-level",CreatedUtcTicks=DateTime.UtcNow.Ticks,IsAdministrative=true});
        var duplicate=RebirthNpcProgressionService.Award(new RebirthNpcProgressionAwardRequest{AwardId=award,NpcId=id,ProgressionId="profession.general",RequestedXp=4000,SourceType="qualification",SourceId="duplicate",CreatedUtcTicks=DateTime.UtcNow.Ticks,IsAdministrative=true});
        RebirthNpcProgressionView view;bool has=RebirthNpcProgressionService.TryGetView(id,"profession.general",out view);
        bool p1=first.Accepted&&first.CrossedLevels>=2;bool p2=duplicate.Status==RebirthNpcProgressionAwardStatus.Duplicate;bool p3=has&&view.CumulativeXp==first.NewXp&&view.Level==first.NewLevel;
        return "[REBIRTH NPC ACIP-01 Qualification] multiLevel="+(p1?"PASS":"FAIL")+" duplicateSuppression="+(p2?"PASS":"FAIL")+" durableQuery="+(p3?"PASS":"FAIL")+" result="+(p1&&p2&&p3?"PASS":"FAIL");
    }
}
