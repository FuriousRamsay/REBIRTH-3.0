using System.Text;

#nullable disable

public static class RebirthRepairSignatureVectorHarness
{
    public static string Run()
    {
        StringBuilder b=new StringBuilder();int pass=0,fail=0;
        Check(b,ref pass,ref fail,"first repair free: no native loss means Maintenance adds nothing",RebirthRepairSignatureService.CalculateMaintenanceTarget(1000,1000,0.5f)==1000);
        Check(b,ref pass,ref fail,"10% native repair loss is halved",RebirthRepairSignatureService.CalculateMaintenanceTarget(1000,900,0.5f)==950);
        Check(b,ref pass,ref fail,"25% native repair loss is halved with integer ceiling",RebirthRepairSignatureService.CalculateMaintenanceTarget(1000,750,0.5f)==875);
        Check(b,ref pass,ref fail,"Maintenance never rewinds unrelated prior loss when this repair has no loss",RebirthRepairSignatureService.CalculateMaintenanceTarget(800,800,0.5f)==800);
        Check(b,ref pass,ref fail,"restoration recovers 25% of current deficit",RebirthRepairSignatureService.CalculateRestorationTarget(600,1000,0.25f)==700);
        Check(b,ref pass,ref fail,"restoration can recover death-created deficit even when repair degradation is off",RebirthRepairSignatureService.CalculateRestorationTarget(750,1000,0.25f)==813);
        Check(b,ref pass,ref fail,"restoration never exceeds original/crafted cap",RebirthRepairSignatureService.CalculateRestorationTarget(999,1000,0.25f)==1000);
        Check(b,ref pass,ref fail,"restoration is dormant with no deficit",RebirthRepairSignatureService.CalculateRestorationTarget(1000,1000,0.25f)==1000);
        Check(b,ref pass,ref fail,"zero restoration tuning remains dormant",RebirthRepairSignatureService.CalculateRestorationTarget(600,1000,0f)==600);
        b.AppendLine("SUMMARY: "+pass+" / "+(pass+fail)+" checks passed.");return b.ToString();
    }
    private static void Check(StringBuilder b,ref int pass,ref int fail,string name,bool ok){if(ok){pass++;b.AppendLine("PASS - "+name);}else{fail++;b.AppendLine("FAIL - "+name);}}
}
