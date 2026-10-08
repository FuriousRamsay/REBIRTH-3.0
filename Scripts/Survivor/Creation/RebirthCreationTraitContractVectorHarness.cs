using System;
using System.Text;

#nullable disable

public static class RebirthCreationTraitContractVectorHarness
{
    public static string Run()
    {
        StringBuilder b=new StringBuilder();
        bool ok=true;

        Check(b,ref ok,"Knowledge grant target supported",
            RebirthSurvivorCreationValidator.SupportsCreationTraitTarget("knowledge.grant"));
        Check(b,ref ok,"Skill start bias target supported",
            RebirthSurvivorCreationValidator.SupportsCreationTraitTarget("skill.start_bias"));
        Check(b,ref ok,"unencumbered slot creation target supported",
            RebirthSurvivorCreationValidator.SupportsCreationTraitTarget("inventory.unencumbered_slots"));
        Check(b,ref ok,"unknown creation target rejected",
            !RebirthSurvivorCreationValidator.SupportsCreationTraitTarget("unsupported.fake.target"));

        Check(b,ref ok,"Assault Rifles live weapon-family Skill",
            RebirthWeaponFamilySkillService.IsChunk7Skill("skill.assault_rifles"));
        Check(b,ref ok,"Pistols live weapon-family Skill",
            RebirthWeaponFamilySkillService.IsChunk7Skill("skill.pistols"));
        Check(b,ref ok,"Batons live weapon-family Skill",
            RebirthWeaponFamilySkillService.IsChunk7Skill("skill.batons"));

        b.Insert(0,ok?"PASS\n":"FAIL\n");
        return b.ToString().TrimEnd();
    }

    private static void Check(StringBuilder b,ref bool ok,string name,bool pass)
    {
        if(!pass)ok=false;
        b.Append(pass?"PASS ":"FAIL ").AppendLine(name);
    }
}
