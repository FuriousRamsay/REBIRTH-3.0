using System;
using System.Text;

#nullable disable

public static class RebirthGenericTraitContractVectorHarness
{
    public static string Run()
    {
        StringBuilder b=new StringBuilder();
        bool ok=true;

        Check(b,ref ok,"technical LBD",RebirthTraitGameplayModifierService.SupportsSkillGainTarget("skill.gain.technical.below50","skill.mechanics",25f));
        Check(b,ref ok,"ranged LBD",RebirthTraitGameplayModifierService.SupportsSkillGainTarget("skill.gain.ranged.below50","skill.pistols",25f));
        Check(b,ref ok,"combat slowdown",RebirthTraitGameplayModifierService.SupportsSkillGainTarget("skill.gain.combat.below50","skill.clubs",25f));
        Check(b,ref ok,"Quick Study taper",RebirthTraitGameplayModifierService.SupportsSkillGainTarget("skill.gain.all.taper40_60","skill.cooking",50f));

        Check(b,ref ok,"barter bridge",RebirthTraitGameplayModifierService.SupportsGenericTraitTarget("barter.buying"));
        Check(b,ref ok,"harvest bridge",RebirthTraitGameplayModifierService.SupportsGenericTraitTarget("harvest.count"));
        Check(b,ref ok,"vehicle fuel bridge",RebirthTraitGameplayModifierService.SupportsGenericTraitTarget("vehicle.fuel_use"));
        Check(b,ref ok,"unencumbered slots",RebirthTraitGameplayModifierService.SupportsGenericTraitTarget("inventory.unencumbered_slots"));

        Check(b,ref ok,"strenuous Energy",RebirthConditionTraitModifierService.SupportsConditionTraitTarget("energy.use.strenuous"));
        Check(b,ref ok,"heat condition",RebirthConditionTraitModifierService.SupportsConditionTraitTarget("environment.heat"));
        Check(b,ref ok,"companion Mood",RebirthConditionTraitModifierService.SupportsConditionTraitTarget("mood.companion.nearby"));
        Check(b,ref ok,"plant food Mood",RebirthConditionTraitModifierService.SupportsConditionTraitTarget("food.mood.plant_positive"));

        b.Insert(0,ok?"PASS\n":"FAIL\n");
        return b.ToString().TrimEnd();
    }

    private static void Check(StringBuilder b,ref bool ok,string name,bool pass)
    {
        if(!pass)ok=false;
        b.Append(pass?"PASS ":"FAIL ").AppendLine(name);
    }
}
