using System;
using System.Text;

#nullable disable

public static class RebirthDroneFieldServiceVectorHarness
{
    public static string Run()
    {
        StringBuilder b=new StringBuilder();
        bool ok=true;
        Check(b,ref ok,"stock drone platform is Drone Operations repair ownership",
            RebirthServiceCraftSkillService.IsDroneServiceItemName("gunBotT3JunkDrone"));
        Check(b,ref ok,"case-insensitive drone platform classification",
            RebirthServiceCraftSkillService.IsDroneServiceItemName("GUNBOTT3JUNKDRONE"));
        Check(b,ref ok,"drone mod name is not platform repair ownership",
            !RebirthServiceCraftSkillService.IsDroneServiceItemName("modRoboticDroneArmor"));
        Check(b,ref ok,"unrelated item is not drone repair ownership",
            !RebirthServiceCraftSkillService.IsDroneServiceItemName("gunPistolT1Pistol"));
        b.Insert(0,ok?"PASS\n":"FAIL\n");
        return b.ToString().TrimEnd();
    }

    private static void Check(StringBuilder b,ref bool ok,string name,bool pass)
    {
        if(!pass)ok=false;
        b.Append(pass?"PASS ":"FAIL ").AppendLine(name);
    }
}
