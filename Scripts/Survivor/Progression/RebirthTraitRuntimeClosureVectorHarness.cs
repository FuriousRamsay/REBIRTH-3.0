using System;
using System.Text;

#nullable disable

public static class RebirthTraitRuntimeClosureVectorHarness
{
    public static string Run()
    {
        StringBuilder b=new StringBuilder();
        bool ok=true;

        Check(b,ref ok,"Efficient Prep target applies below 50",
            RebirthTraitGameplayModifierService.SupportsSkillGainTarget("skill.gain.cooking.below50","skill.cooking",25f));
        Check(b,ref ok,"Efficient Prep target stops at 50",
            !RebirthTraitGameplayModifierService.SupportsSkillGainTarget("skill.gain.cooking.below50","skill.cooking",50f));

        Check(b,ref ok,"Soil Sense target applies to Farming",
            RebirthTraitGameplayModifierService.SupportsSkillGainTarget("skill.gain.farming.below50","skill.farming",30f));
        Check(b,ref ok,"Cooking target does not leak to Farming",
            !RebirthTraitGameplayModifierService.SupportsSkillGainTarget("skill.gain.cooking.below50","skill.farming",20f));

        Check(b,ref ok,"Meat positive Mood target supported",
            RebirthConditionTraitModifierService.SupportsFoodMoodTarget("food.mood.meat_positive"));
        Check(b,ref ok,"Plant positive Mood target supported",
            RebirthConditionTraitModifierService.SupportsFoodMoodTarget("food.mood.plant_positive"));

        b.Insert(0,ok?"PASS\n":"FAIL\n");
        return b.ToString().TrimEnd();
    }

    private static void Check(StringBuilder b,ref bool ok,string name,bool pass)
    {
        if(!pass)ok=false;
        b.Append(pass?"PASS ":"FAIL ").AppendLine(name);
    }
}
