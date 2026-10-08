using System;
using System.Text;

#nullable disable

public static class RebirthSkillOutcomeValueVectorHarness
{
    public static string Run()
    {
        StringBuilder b=new StringBuilder();
        bool ok=true;
        Check(b,ref ok,"neutral attribute adds zero",Near(RebirthSkillOutcomeValueService.ApplyAttributeContribution("skill.archery",25f,50f),25f));
        Check(b,ref ok,"max attribute adds bounded ten",Near(RebirthSkillOutcomeValueService.ApplyAttributeContribution("skill.archery",25f,100f),35f));
        Check(b,ref ok,"min attribute subtracts bounded ten",Near(RebirthSkillOutcomeValueService.ApplyAttributeContribution("skill.archery",25f,0f),15f));
        Check(b,ref ok,"skill ceiling remains 100",Near(RebirthSkillOutcomeValueService.ApplyAttributeContribution("skill.archery",98f,100f),100f));
        Check(b,ref ok,"skill floor remains -50",Near(RebirthSkillOutcomeValueService.ApplyAttributeContribution("skill.archery",-48f,0f),-50f));
        Check(b,ref ok,"Tailoring maps Dexterity",string.Equals(RebirthAttributeProgressionService.GetPrimaryAttributeForSkill("skill.tailoring"),"dexterity",StringComparison.OrdinalIgnoreCase));
        Check(b,ref ok,"Cooking maps Intelligence",string.Equals(RebirthAttributeProgressionService.GetPrimaryAttributeForSkill("skill.cooking"),"intelligence",StringComparison.OrdinalIgnoreCase));
        Check(b,ref ok,"Mining maps Strength",string.Equals(RebirthAttributeProgressionService.GetPrimaryAttributeForSkill("skill.mining"),"strength",StringComparison.OrdinalIgnoreCase));
        b.Insert(0,ok?"PASS\n":"FAIL\n");
        return b.ToString().TrimEnd();
    }

    private static bool Near(float a,float b){return Math.Abs(a-b)<0.001f;}
    private static void Check(StringBuilder b,ref bool ok,string name,bool pass){if(!pass)ok=false;b.Append(pass?"PASS ":"FAIL ").AppendLine(name);}
}
