using System;
using System.Text;

#nullable disable

public static class RebirthClinicalMobilityTraitVectorHarness
{
    public static string Run()
    {
        StringBuilder b=new StringBuilder();
        bool ok=true;

        Check(b,ref ok,"creation Skill bias supported",
            RebirthSurvivorCreationValidator.SupportsCreationTraitTarget("skill.start_bias"));
        Check(b,ref ok,"illness recovery target supported",
            RebirthConditionTraitModifierService.SupportsHealthCapacityTraitTarget("health.capacity.illness_recovery"));
        Check(b,ref ok,"sprint/jump Energy target supported",
            RebirthConditionTraitModifierService.SupportsConditionTraitTarget("energy.use.sprint_jump"));
        Check(b,ref ok,"Axes are a live weapon-family Skill",
            RebirthWeaponFamilySkillService.IsChunk7Skill("skill.axes"));

        b.Insert(0,ok?"PASS\n":"FAIL\n");
        return b.ToString().TrimEnd();
    }

    private static void Check(StringBuilder b,ref bool ok,string name,bool pass)
    {
        if(!pass)ok=false;
        b.Append(pass?"PASS ":"FAIL ").AppendLine(name);
    }
}
