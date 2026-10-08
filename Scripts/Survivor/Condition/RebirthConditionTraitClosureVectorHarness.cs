using System;
using System.Text;

#nullable disable

public static class RebirthConditionTraitClosureVectorHarness
{
    public static string Run()
    {
        StringBuilder b=new StringBuilder();
        bool ok=true;

        Check(b,ref ok,"injury Health Capacity target",
            RebirthConditionTraitModifierService.SupportsHealthCapacityTraitTarget("health.capacity.injury_loss"));
        Check(b,ref ok,"illness Health Capacity target",
            RebirthConditionTraitModifierService.SupportsHealthCapacityTraitTarget("health.capacity.illness_loss"));
        Check(b,ref ok,"negative Mood recovery target",
            RebirthConditionTraitModifierService.SupportsConditionTraitTarget("mood.recovery.negative"));
        Check(b,ref ok,"injury Mood target",
            RebirthConditionTraitModifierService.SupportsConditionTraitTarget("mood.injury"));
        Check(b,ref ok,"injured Energy target",
            RebirthConditionTraitModifierService.SupportsConditionTraitTarget("energy.use.injured"));
        Check(b,ref ok,"sprint/jump Energy target",
            RebirthConditionTraitModifierService.SupportsConditionTraitTarget("energy.use.sprint_jump"));

        b.Insert(0,ok?"PASS\n":"FAIL\n");
        return b.ToString().TrimEnd();
    }

    private static void Check(StringBuilder b,ref bool ok,string name,bool pass)
    {
        if(!pass)ok=false;
        b.Append(pass?"PASS ":"FAIL ").AppendLine(name);
    }
}
