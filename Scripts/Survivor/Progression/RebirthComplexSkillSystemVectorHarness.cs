using System;
using System.Text;

#nullable disable

public static class RebirthComplexSkillSystemVectorHarness
{
    private static int pass, fail;
    public static string RunAll()
    {
        pass = 0; fail = 0; StringBuilder b = new StringBuilder();
        b.AppendLine("[REBIRTH Survivor Chunk 10 Complex Skill vectors]");
        Check(b, "explosives zero neutral", Near(RebirthComplexSkillSystemService.SignedEndpoint(0f, -.20f, .25f), 0f));
        Check(b, "explosives weak lowers blast", RebirthComplexSkillSystemService.SignedEndpoint(-50f, -.20f, .25f) < 0f);
        Check(b, "explosives strong raises blast", RebirthComplexSkillSystemService.SignedEndpoint(100f, -.20f, .25f) > 0f);
        Check(b, "explosives clamp low", Near(RebirthComplexSkillSystemService.SignedEndpoint(-500f, -.20f, .25f), -.20f));
        Check(b, "explosives clamp high", Near(RebirthComplexSkillSystemService.SignedEndpoint(500f, -.20f, .25f), .25f));
        Check(b, "drone damage zero neutral", Near(RebirthComplexSkillSystemService.SignedEndpoint(0f, -.20f, .25f), 0f));
        Check(b, "drone damage weak lower", RebirthComplexSkillSystemService.SignedEndpoint(-50f, -.20f, .25f) < 0f);
        Check(b, "drone damage strong higher", RebirthComplexSkillSystemService.SignedEndpoint(100f, -.20f, .25f) > 0f);
        Check(b, "drone stun zero neutral", Near(RebirthComplexSkillSystemService.SignedEndpoint(0f, .20f, -.20f), 0f));
        Check(b, "drone stun weak slower", RebirthComplexSkillSystemService.SignedEndpoint(-50f, .20f, -.20f) > 0f);
        Check(b, "drone stun strong faster", RebirthComplexSkillSystemService.SignedEndpoint(100f, .20f, -.20f) < 0f);
        Check(b, "construction runtime enabled", RebirthComplexSkillSystemService.ConstructionWorkActionEnabled);
        Check(b, "electrical runtime enabled", RebirthComplexSkillSystemService.ElectricalWorkmanshipEnabled);
        Check(b, "chemistry payload gate closed", !RebirthComplexSkillSystemService.ChemistryPayloadPotencyEnabled);
        Check(b, "turret performance scaling enabled", RebirthComplexSkillSystemService.DeployableTurretPerformanceScalingEnabled);
        Check(b, "turret feed reliability fail closed", !RebirthComplexSkillSystemService.DeployableTurretFeedReliabilityEnabled);
        Check(b, "turret negative endpoint bounded", RebirthProgressionRuntimeConfig.TurretDamageNegative <= 0f && RebirthProgressionRuntimeConfig.TurretDamageNegative >= -.35f);
        Check(b, "turret positive endpoint bounded", RebirthProgressionRuntimeConfig.TurretDamagePositive >= 0f && RebirthProgressionRuntimeConfig.TurretDamagePositive <= .35f);
        Check(b, "explosive block award bounded", RebirthProgressionRuntimeConfig.ExplosivesBlockAward > 0f && RebirthProgressionRuntimeConfig.ExplosivesBlockAward <= .35f);
        Check(b, "drone shock award bounded", RebirthProgressionRuntimeConfig.DroneShockAward > 0f && RebirthProgressionRuntimeConfig.DroneShockAward <= .35f);
        Check(b, "drone repeat positive", RebirthProgressionRuntimeConfig.DroneShockRepeatSeconds >= 1f);
        Check(b, "stock drone recovery award locked", Near(RebirthProgressionRuntimeConfig.DroneStockRecoveryAward, .20f));
        Check(b, "stock drone recovery requires distance", RebirthProgressionRuntimeConfig.DroneStockRecoveryMinDistance >= 25f);
        Check(b, "stock drone recovery repeat guard active", RebirthProgressionRuntimeConfig.DroneStockRecoveryRepeatSeconds >= 300f);
        b.AppendLine("result=" + (fail == 0 ? "PASS" : "FAIL") + " pass=" + pass + " fail=" + fail + " total=" + (pass + fail));
        return b.ToString();
    }
    private static bool Near(float a, float b) { return Math.Abs(a - b) < .0001f; }
    private static void Check(StringBuilder b, string n, bool ok) { if (ok) { pass++; b.AppendLine("PASS " + n); } else { fail++; b.AppendLine("FAIL " + n); } }
}
