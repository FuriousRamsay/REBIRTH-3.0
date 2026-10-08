using System;
using System.Text;

#nullable disable

/// <summary>
/// Phase 12 read-only aggregation of the deterministic release vectors. It deliberately does not
/// claim compilation or live host/P2P/dedicated acceptance; those remain explicit runtime gates.
/// </summary>
public static class RebirthSurvivorReleaseAcceptanceVectorHarness
{
    public static string RunAll()
    {
        int pass = 0, fail = 0;
        StringBuilder b = new StringBuilder();
        Check(b, ref pass, ref fail, "schema 18 persisted anti-repeat", RebirthWorldCharacterRecord.CurrentSchemaVersion == 18);
        Check(b, ref pass, ref fail, "seven representative bands", RebirthSurvivorReleaseAcceptance.Bands.Length == 7);
        Check(b, ref pass, ref fail, "48 locked training profiles", RebirthSkillTrainingProfileRegistry.ProfileCount == 48);
        Check(b, ref pass, ref fail, "48 reachability declarations", RebirthSkillReachabilityValidator.Count == 48);
        Check(b, ref pass, ref fail, "48 Theory reveal subjects", RebirthTheoryRevealService.SubjectCount == 48);
        Check(b, ref pass, ref fail, "neutral explosive zero", Near(RebirthSurvivorReleaseAcceptance.S(0f, RebirthProgressionRuntimeConfig.ExplosivesDamageNegative, RebirthProgressionRuntimeConfig.ExplosivesDamagePositive), 0f));
        Check(b, ref pass, ref fail, "neutral resource zero", Near(RebirthSurvivorReleaseAcceptance.S(0f, RebirthProgressionRuntimeConfig.ResourceHarvestNegative, RebirthProgressionRuntimeConfig.ResourceHarvestPositive), 0f));
        Check(b, ref pass, ref fail, "neutral craft zero", Near(RebirthSurvivorReleaseAcceptance.S(0f, RebirthProgressionRuntimeConfig.ServiceCraftTimeNegative, RebirthProgressionRuntimeConfig.ServiceCraftTimePositive), 0f));
        Check(b, ref pass, ref fail, "explosive endpoints bounded", Math.Abs(RebirthProgressionRuntimeConfig.ExplosivesDamageNegative) <= .35f && Math.Abs(RebirthProgressionRuntimeConfig.ExplosivesDamagePositive) <= .35f);
        Check(b, ref pass, ref fail, "resource endpoints bounded", Math.Abs(RebirthProgressionRuntimeConfig.ResourceHarvestNegative) <= .35f && Math.Abs(RebirthProgressionRuntimeConfig.ResourceHarvestPositive) <= .35f);
        Check(b, ref pass, ref fail, "single awards bounded", RebirthProgressionRuntimeConfig.MechanicsHotwire <= .60f && RebirthProgressionRuntimeConfig.GunsmithingCraftMax <= .60f);
        Check(b, ref pass, ref fail, "debug defaults fail closed", !RebirthSurvivorDebug.Enabled);
        Check(b, ref pass, ref fail, "construction runtime enabled", RebirthComplexSkillSystemService.ConstructionWorkActionEnabled);
        Check(b, ref pass, ref fail, "electrical runtime enabled", RebirthComplexSkillSystemService.ElectricalWorkmanshipEnabled);
        Check(b, ref pass, ref fail, "chemistry payload gate remains explicit", !RebirthComplexSkillSystemService.ChemistryPayloadPotencyEnabled);
        Check(b, ref pass, ref fail, "turret calibration performance enabled", RebirthComplexSkillSystemService.DeployableTurretPerformanceScalingEnabled);
        Check(b, ref pass, ref fail, "turret feed reliability remains fail closed", !RebirthComplexSkillSystemService.DeployableTurretFeedReliabilityEnabled);
        Check(b, ref pass, ref fail, "metal heat-treatment persistent grade enabled", RebirthServiceCraftSkillService.MetalworkingPersistentHeatTreatmentGradeEnabled);

        // Persisted anti-repeat state must survive normal record cloning, which is the repository's
        // snapshot/rollback boundary before XML serialization.
        RebirthWorldProgressionState persisted = new RebirthWorldProgressionState();
        persisted.SkillAntiRepeat.AwardReadyAtActiveSeconds["skill.drone_operations|drone-stock-recovery:17"] = 123.5d;
        persisted.CommerceTraining.LastGlobalAwardActiveSeconds = 44d;
        persisted.CommerceTraining.Items["123"] = new RebirthCommerceItemTrainingRuntimeState
        { LastBuyActiveSeconds=40d, LastSellActiveSeconds=20d, LastAwardActiveSeconds=40d, RepeatChain=2 };
        RebirthWorldProgressionState cloned = persisted.Clone();
        double cooldown;
        RebirthCommerceItemTrainingRuntimeState commerceItem;
        Check(b, ref pass, ref fail, "Skill cooldown survives snapshot clone",
            cloned.SkillAntiRepeat.AwardReadyAtActiveSeconds.TryGetValue("skill.drone_operations|drone-stock-recovery:17", out cooldown) && Math.Abs(cooldown-123.5d)<0.000001d);
        Check(b, ref pass, ref fail, "commerce anti-loop survives snapshot clone",
            Math.Abs(cloned.CommerceTraining.LastGlobalAwardActiveSeconds-44d)<0.000001d && cloned.CommerceTraining.Items.TryGetValue("123",out commerceItem) && commerceItem!=null && commerceItem.RepeatChain==2 && Math.Abs(commerceItem.LastBuyActiveSeconds-40d)<0.000001d);

        CheckSuite(b, ref pass, ref fail, "migration vectors", RebirthSurvivorMigrationVectorHarness.RunAll(), "summary=PASS");
        CheckSuite(b, ref pass, ref fail, "48-Skill reachability vectors", RebirthSkillReachabilityValidator.RunVectors(), "RESULT 3/3");
        CheckSuite(b, ref pass, ref fail, "unified training vectors", RebirthSkillTrainingVectorHarness.RunAll(), "summary=PASS");
        CheckSuite(b, ref pass, ref fail, "Theory/Insight vectors", RebirthTheoryProgressionService.RunVectors(), "summary=PASS");
        CheckSuite(b, ref pass, ref fail, "Theory reveal vectors", RebirthTheoryRevealService.RunVectors(), "RESULT 5/5");
        CheckSuite(b, ref pass, ref fail, "combat normalization vectors", RebirthCombatProgressionVectorHarness.RunAll(), "summary=PASS");
        CheckSuite(b, ref pass, ref fail, "weapon-family vectors", RebirthWeaponFamilySkillVectorHarness.RunAll(), "vectors: PASS");
        CheckSuite(b, ref pass, ref fail, "resource/field vectors", RebirthResourceFieldSkillVectorHarness.RunAll(), "PASS");
        CheckSuite(b, ref pass, ref fail, "Phase 8 world/output vectors", RebirthPhase8WorldOutputTrainingService.RunVectors(), "Phase8 Vectors] PASS");
        CheckSuite(b, ref pass, ref fail, "Phase 9 technical/process vectors", RebirthPhase9TechnicalTrainingService.RunVectors(), "Phase9 Vectors] PASS");
        CheckSuite(b, ref pass, ref fail, "social/exertion vectors", RebirthSkillWaveAVectorHarness.RunAll(), "vectors: PASS");
        CheckSuite(b, ref pass, ref fail, "advanced/device vectors", RebirthComplexSkillSystemVectorHarness.RunAll(), "result=PASS");
        Check(b, ref pass, ref fail, "suite parser accepts passing fail-closed checks", !HasFailedRow("PASS turret reliability fail closed\nresult=PASS"));
        Check(b, ref pass, ref fail, "suite parser rejects failed checks", HasFailedRow("  FAIL owner attribution\nresult=PASS"));

        b.Insert(0, "REBIRTH Survivor Phase 12 release-acceptance vectors: " + (fail == 0 ? "PASS" : "FAIL") + "\npass=" + pass + " fail=" + fail + " total=" + (pass + fail) + "\n");
        b.AppendLine("releaseApproved=False runtimeEvidenceRequired=True compileValidationClaimed=False");
        return b.ToString().TrimEnd();
    }

    private static void CheckSuite(StringBuilder b, ref int pass, ref int fail, string name, string output, string successToken)
    {
        bool ok=!string.IsNullOrEmpty(output) && output.IndexOf(successToken,StringComparison.OrdinalIgnoreCase)>=0 && !HasFailedRow(output);
        Check(b,ref pass,ref fail,name,ok);
        if(!ok && !string.IsNullOrEmpty(output)) b.AppendLine("  detail="+Compact(output));
    }

    private static bool HasFailedRow(string output)
    {
        foreach (string line in (output ?? string.Empty).Split('\n'))
        {
            string row = line.Trim();
            if (row.Equals("FAIL", StringComparison.OrdinalIgnoreCase)
                || row.StartsWith("FAIL ", StringComparison.OrdinalIgnoreCase)
                || row.StartsWith("FAIL:", StringComparison.OrdinalIgnoreCase)
                || row.IndexOf("result=FAIL", StringComparison.OrdinalIgnoreCase) >= 0
                || row.IndexOf("summary=FAIL", StringComparison.OrdinalIgnoreCase) >= 0) return true;
        }
        return false;
    }

    private static string Compact(string value)
    {
        string s=(value??string.Empty).Replace('\r',' ').Replace('\n',' ');
        return s.Length<=240?s:s.Substring(0,240)+"...";
    }
    private static bool Near(float a, float b) { return Math.Abs(a - b) < 0.0001f; }
    private static void Check(StringBuilder b, ref int pass, ref int fail, string name, bool ok) { if (ok) pass++; else fail++; b.AppendLine((ok ? "PASS " : "FAIL ") + name); }
}
