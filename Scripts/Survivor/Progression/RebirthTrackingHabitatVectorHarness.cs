using System;
using System.Text;

#nullable disable

public static class RebirthTrackingHabitatVectorHarness
{
    public static string Run()
    {
        StringBuilder b=new StringBuilder();
        bool ok=true;

        Check(b,ref ok,"no Habitat keeps distance unchanged",
            Near(RebirthResourceFieldSkillService.ApplyHabitatDistanceKnowledge(100f,false),100f));
        Check(b,ref ok,"Habitat gives configured distance bonus",
            Near(RebirthResourceFieldSkillService.ApplyHabitatDistanceKnowledge(100f,true),
                100f*Math.Max(1f,Math.Min(1.50f,RebirthProgressionRuntimeConfig.TrackingHabitatDistanceMultiplier))));

        Check(b,ref ok,"no Habitat keeps acquisition unchanged",
            Near(RebirthResourceFieldSkillService.ApplyHabitatAcquireKnowledge(3f,false),3f));
        Check(b,ref ok,"Habitat gives configured acquisition reduction",
            Near(RebirthResourceFieldSkillService.ApplyHabitatAcquireKnowledge(3f,true),
                3f*Math.Max(0.50f,Math.Min(1f,RebirthProgressionRuntimeConfig.TrackingHabitatAcquireMultiplier))));

        Check(b,ref ok,"distance multiplier bounded",
            RebirthProgressionRuntimeConfig.TrackingHabitatDistanceMultiplier>=1f &&
            RebirthProgressionRuntimeConfig.TrackingHabitatDistanceMultiplier<=1.50f);
        Check(b,ref ok,"acquire multiplier bounded",
            RebirthProgressionRuntimeConfig.TrackingHabitatAcquireMultiplier>=0.50f &&
            RebirthProgressionRuntimeConfig.TrackingHabitatAcquireMultiplier<=1f);

        b.Insert(0,ok?"PASS\n":"FAIL\n");
        return b.ToString().TrimEnd();
    }

    private static bool Near(float a,float b){return Math.Abs(a-b)<0.001f;}
    private static void Check(StringBuilder b,ref bool ok,string name,bool pass)
    {
        if(!pass)ok=false;
        b.Append(pass?"PASS ":"FAIL ").AppendLine(name);
    }
}
