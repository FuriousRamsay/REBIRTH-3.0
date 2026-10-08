using System;
using System.Text;

#nullable disable

public static class RebirthTailoringLeatherRepairVectorHarness
{
    public static string Run()
    {
        StringBuilder b=new StringBuilder();
        bool ok=true;

        Check(b,ref ok,"Biker family is leather-repair routed",
            RebirthServiceCraftSkillService.IsLeatherTailoringWearableName("armorBikerBoots"));
        Check(b,ref ok,"Biker outfit is leather-repair routed",
            RebirthServiceCraftSkillService.IsLeatherTailoringWearableName("armorBikerOutfit"));
        Check(b,ref ok,"Ranger leather duster is leather-repair routed",
            RebirthServiceCraftSkillService.IsLeatherTailoringWearableName("armorRangerOutfit"));
        Check(b,ref ok,"Ranger boots are not guessed as leather",
            !RebirthServiceCraftSkillService.IsLeatherTailoringWearableName("armorRangerBoots"));
        Check(b,ref ok,"Primitive armor remains Basic Garment Repair",
            !RebirthServiceCraftSkillService.IsLeatherTailoringWearableName("armorPrimitiveOutfit"));
        Check(b,ref ok,"explicit custom leather wearable remains compatible",
            RebirthServiceCraftSkillService.IsLeatherTailoringWearableName("armorCustomLeatherJacket"));

        b.Insert(0,ok?"PASS\n":"FAIL\n");
        return b.ToString().TrimEnd();
    }

    private static void Check(StringBuilder b,ref bool ok,string name,bool pass)
    {
        if(!pass)ok=false;
        b.Append(pass?"PASS ":"FAIL ").AppendLine(name);
    }
}
