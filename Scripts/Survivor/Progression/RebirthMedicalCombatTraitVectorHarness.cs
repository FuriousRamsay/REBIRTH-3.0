using System;
using System.Text;

#nullable disable

public static class RebirthMedicalCombatTraitVectorHarness
{
    public static string Run()
    {
        StringBuilder b=new StringBuilder();
        bool ok=true;

        Check(b,ref ok,"Clinical Routine Medicine gain target",
            RebirthTraitGameplayModifierService.SupportsSkillGainTarget("skill.gain.medicine.below50","skill.medicine",25f));
        Check(b,ref ok,"Clinical Routine threshold closes at 50",
            !RebirthTraitGameplayModifierService.SupportsSkillGainTarget("skill.gain.medicine.below50","skill.medicine",50f));
        Check(b,ref ok,"Field Discipline Maintenance gain target",
            RebirthTraitGameplayModifierService.SupportsSkillGainTarget("skill.gain.maintenance.below50","skill.maintenance",25f));
        Check(b,ref ok,"Field Discipline injury-Mood target",
            RebirthConditionTraitModifierService.SupportsConditionTraitTarget("mood.injury"));
        Check(b,ref ok,"Field Triage reserve target",
            RebirthTraitGameplayModifierService.SupportsGenericTraitTarget("medicine.firstaid.reserve"));
        Check(b,ref ok,"bandage classified first aid",
            RebirthServiceCraftSkillService.IsFirstAidReserveTreatment("medicalBandage"));
        Check(b,ref ok,"first aid kit classified first aid",
            RebirthServiceCraftSkillService.IsFirstAidReserveTreatment("medicalFirstAidKit"));
        Check(b,ref ok,"antibiotic not first aid reserve",
            !RebirthServiceCraftSkillService.IsFirstAidReserveTreatment("medicalAntibiotics"));

        b.Insert(0,ok?"PASS\n":"FAIL\n");
        return b.ToString().TrimEnd();
    }

    private static void Check(StringBuilder b,ref bool ok,string name,bool pass)
    {
        if(!pass)ok=false;
        b.Append(pass?"PASS ":"FAIL ").AppendLine(name);
    }
}
