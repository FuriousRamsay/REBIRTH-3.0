using System;
using System.Text;

#nullable disable

public static class RebirthUnsafeProgressionGuardrailVectorHarness
{
    public static string Run()
    {
        StringBuilder b=new StringBuilder();
        bool ok=true;
        Check(b,ref ok,"turret calibration damage remains live",
            RebirthComplexSkillSystemService.DeployableTurretPerformanceScalingEnabled);
        Check(b,ref ok,"turret feed reliability is fail closed",
            !RebirthComplexSkillSystemService.DeployableTurretFeedReliabilityEnabled);
        Check(b,ref ok,"metal persistent heat-treatment grade is enabled by Chunk G",
            RebirthServiceCraftSkillService.MetalworkingPersistentHeatTreatmentGradeEnabled);
        Check(b,ref ok,"chemistry payload metadata is fail closed",
            !RebirthComplexSkillSystemService.ChemistryPayloadPotencyEnabled);
        b.Insert(0,ok?"PASS\n":"FAIL\n");
        return b.ToString().TrimEnd();
    }

    private static void Check(StringBuilder b,ref bool ok,string name,bool pass)
    {
        if(!pass)ok=false;
        b.Append(pass?"PASS ":"FAIL ").AppendLine(name);
    }
}
