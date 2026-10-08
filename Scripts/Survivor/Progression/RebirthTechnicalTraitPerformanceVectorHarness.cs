using System;
using System.Text;

#nullable disable

public static class RebirthTechnicalTraitPerformanceVectorHarness
{
    public static string Run()
    {
        StringBuilder b=new StringBuilder();
        bool ok=true;

        Check(b,ref ok,"Chemistry craft-time target",
            RebirthTraitGameplayModifierService.SupportsGenericTraitTarget("craft.time.chemistry"));
        Check(b,ref ok,"Metalworking craft-time target",
            RebirthTraitGameplayModifierService.SupportsGenericTraitTarget("craft.time.metalworking"));
        Check(b,ref ok,"vehicle repair-health target",
            RebirthTraitGameplayModifierService.SupportsGenericTraitTarget("vehicle.repair.health"));
        Check(b,ref ok,"heat environment target",
            RebirthConditionTraitModifierService.SupportsConditionTraitTarget("environment.heat"));
        Check(b,ref ok,"cold environment target",
            RebirthConditionTraitModifierService.SupportsConditionTraitTarget("environment.cold"));

        b.Insert(0,ok?"PASS\n":"FAIL\n");
        return b.ToString().TrimEnd();
    }

    private static void Check(StringBuilder b,ref bool ok,string name,bool pass)
    {
        if(!pass)ok=false;
        b.Append(pass?"PASS ":"FAIL ").AppendLine(name);
    }
}
