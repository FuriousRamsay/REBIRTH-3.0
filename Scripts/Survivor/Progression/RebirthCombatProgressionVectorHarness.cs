using System;
using System.Text;

#nullable disable

/// <summary>Pure/static Phase-7 combat progression invariants. Runtime owner/network cases remain live acceptance.</summary>
public static class RebirthCombatProgressionVectorHarness
{
    private static int pass, fail;

    public static string RunAll()
    {
        pass=fail=0; StringBuilder b=new StringBuilder();
        b.AppendLine("[REBIRTH Survivor Phase 7 combat normalization vectors]");

        float rate=RebirthProgressionRuntimeConfig.CombatProgressPerSustainedSecond;
        float one=RebirthWeaponSustainedDpsService.CalculateCombatProgress(100f,100f,0f);
        float halfA=RebirthWeaponSustainedDpsService.CalculateCombatProgress(50f,100f,0f);
        float halfB=RebirthWeaponSustainedDpsService.CalculateCombatProgress(50f,100f,0f);
        Check(b,"continuous anchor 0.005/s",Near(rate,.005f));
        Check(b,"split damage preserves gain below cap",Near(one,halfA+halfB));
        float capped=RebirthWeaponSustainedDpsService.CalculateCombatProgress(10000f,100f,0f);
        Check(b,"six-second event ceiling applied",Near(capped,Math.Min(100f,RebirthProgressionRuntimeConfig.CombatMaxEquivalentSecondsPerHit)*rate));
        Check(b,"zero damage zero gain",Near(RebirthWeaponSustainedDpsService.CalculateCombatProgress(0f,100f,0f),0f));
        Check(b,"zero work rate zero gain",Near(RebirthWeaponSustainedDpsService.CalculateCombatProgress(100f,0f,0f),0f));
        Check(b,"skill bands decay",RebirthWeaponSustainedDpsService.CombatSkillMultiplier(0f)>RebirthWeaponSustainedDpsService.CombatSkillMultiplier(50f) && RebirthWeaponSustainedDpsService.CombatSkillMultiplier(50f)>RebirthWeaponSustainedDpsService.CombatSkillMultiplier(95f));
        Check(b,"stealth movement training disabled",!RebirthSkillWaveAService.StealthMovementTrainingEnabled);
        Check(b,"stock drone award locked",Near(RebirthProgressionRuntimeConfig.DroneStockRecoveryAward,.20f));
        Check(b,"stock drone distance guard",RebirthProgressionRuntimeConfig.DroneStockRecoveryMinDistance>=25f);
        Check(b,"stock drone repeat guard",RebirthProgressionRuntimeConfig.DroneStockRecoveryRepeatSeconds>=300f);
        Check(b,"drone shock remains optional additive route",Near(RebirthProgressionRuntimeConfig.DroneShockAward,.20f)&&RebirthProgressionRuntimeConfig.DroneShockRepeatSeconds>=5f);
        Check(b,"explosive block event retained",Near(RebirthProgressionRuntimeConfig.ExplosivesBlockAward,.12f));

        b.AppendLine("summary="+(fail==0?"PASS":"FAIL")+" pass="+pass+" fail="+fail+" total="+(pass+fail));
        return b.ToString().TrimEnd();
    }

    private static bool Near(float a,float b){return Math.Abs(a-b)<=.0001f;}
    private static void Check(StringBuilder b,string name,bool ok){if(ok){pass++;b.AppendLine("PASS "+name);}else{fail++;b.AppendLine("FAIL "+name);}}
}
