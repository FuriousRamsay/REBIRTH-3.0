using System;
using System.Text;

#nullable disable

public static class RebirthAuthoringTruthTraitClosureVectorHarness
{
    public static string Run()
    {
        StringBuilder b=new StringBuilder();
        bool ok=true;

        Check(b,ref ok,"Knowledge grant target",
            RebirthSurvivorCreationValidator.SupportsCreationTraitTarget("knowledge.grant"));
        Check(b,ref ok,"Skill starting bias target",
            RebirthSurvivorCreationValidator.SupportsCreationTraitTarget("skill.start_bias"));
        Check(b,ref ok,"current Dexterity target",
            RebirthSurvivorCreationValidator.SupportsCreationTraitTarget("attribute.dexterity.current"));
        Check(b,ref ok,"Dexterity Potential target",
            RebirthSurvivorCreationValidator.SupportsCreationTraitTarget("attribute.dexterity.potential"));

        b.Insert(0,ok?"PASS\n":"FAIL\n");
        return b.ToString().TrimEnd();
    }

    private static void Check(StringBuilder b,ref bool ok,string name,bool pass)
    {
        if(!pass)ok=false;
        b.Append(pass?"PASS ":"FAIL ").AppendLine(name);
    }
}
