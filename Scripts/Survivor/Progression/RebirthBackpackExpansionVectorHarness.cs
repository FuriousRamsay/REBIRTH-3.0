using System;
using System.Text;

#nullable disable

public static class RebirthBackpackExpansionVectorHarness
{
    public static string Run()
    {
        StringBuilder b=new StringBuilder();
        bool ok=true;
        Check(b,ref ok,"base physical bag slots are 52",RebirthSurvivorGearService.BasePhysicalBagSlots==52);
        Check(b,ref ok,"base unencumbered slots are 26",RebirthSurvivorGearService.BaseUnencumberedBagSlots==26);
        Check(b,ref ok,"backend ceiling includes Scavenger at 169",RebirthSurvivorGearService.MaxPhysicalBagSlots==169);
        string[] packs={"Daypack","ExpandedDaypack","FieldPack","ExpandedFieldPack","HikingPack","ExpandedHikingPack","ExpeditionPack","ExpandedExpeditionPack"};
        for(int i=0;i<packs.Length;i++)
        {
            RebirthTraitSupportProfileDefinition profile;
            bool found=RebirthSurvivorDefinitionRegistry.TryGetSupportByGearItem("rebirthGear"+packs[i],out profile);
            Check(b,ref ok,packs[i]+" adds "+(13*(i+1))+" slots",found && profile!=null && profile.GearBagSlotBonus==13*(i+1));
        }
        Check(b,ref ok,"Scavenger adds one 13-slot row",RebirthBackgroundStorageService.Bonus("background.scavenger","backpack_slot_bonus")==13);
        b.Insert(0,ok?"PASS\n":"FAIL\n");
        return b.ToString().TrimEnd();
    }

    private static void Check(StringBuilder b,ref bool ok,string name,bool pass)
    {
        if(!pass)ok=false;
        b.Append(pass?"PASS ":"FAIL ").AppendLine(name);
    }
}
