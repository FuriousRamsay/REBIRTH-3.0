using System;
using System.Globalization;
using System.Text;

#nullable disable

/// <summary>
/// Phase 12 read-only release-candidate diagnostics. This does not manufacture runtime evidence;
/// it exposes the exact balance bands, authority state and known release blockers so live host/P2P/
/// dedicated acceptance can be recorded against one deterministic contract.
/// </summary>
public static class RebirthSurvivorReleaseAcceptance
{
    public static readonly float[] Bands = new float[] { -50f, -25f, 0f, 25f, 50f, 75f, 100f };

    public static string BuildSummary(EntityPlayer player)
    {
        StringBuilder b = new StringBuilder();
        RebirthSurvivorDefinitionBundle bundle = RebirthSurvivorDefinitionRegistry.Bundle;
        int skills = bundle != null && bundle.Progression != null ? bundle.Progression.Skills.Count : 0;
        ConnectionManager c = SingletonMonoBehaviour<ConnectionManager>.Instance;
        bool server = c != null && c.IsServer;
        b.AppendLine("[REBIRTH Survivor Release Acceptance]");
        b.AppendLine("stage=12/12 status=STATIC_OFFLINE_COMPLETE_RUNTIME_ACCEPTANCE_REQUIRED releaseApproved=False");
        int expectedSkills = RebirthSurvivorSkillMigrationPolicy.GetCurrentSkillIds().Length;
        b.AppendLine("definitionsReady=" + RebirthSurvivorDefinitionRegistry.IsReady + " skills=" + skills + " expected=" + expectedSkills + " theoryAreas=" + skills + " expectedTheoryAreas=" + expectedSkills + " reachability=" + RebirthSkillReachabilityValidator.Count + " theoryReveal=" + RebirthTheoryRevealService.SubjectCount);
        b.AppendLine("authority server=" + server + " repositoryServer=" + RebirthWorldCharacterRepository.IsServerAuthority + " debugMutationGate=" + RebirthSurvivorDebug.Enabled);
        b.AppendLine("requiredRoles=host,p2p_client,dedicated_client bands=-50,-25,0,+25,+50,+75,+100");
        b.AppendLine("runtimeEvidenceRequired=True compile/runtime evidence cannot be synthesized by static diagnostics");
        b.AppendLine("persistenceSchema="+RebirthWorldCharacterRecord.CurrentSchemaVersion+" persistedSkillCooldowns=True persistedCommerceAntiLoop=True cooldownClock=character_active_play_seconds");
        b.AppendLine("gates construction=" + RebirthComplexSkillSystemService.ConstructionWorkActionEnabled
            + " electrical=" + RebirthComplexSkillSystemService.ElectricalWorkmanshipEnabled
            + " chemistryPayload=" + RebirthComplexSkillSystemService.ChemistryPayloadPotencyEnabled
            + " turretPerformance=" + RebirthComplexSkillSystemService.DeployableTurretPerformanceScalingEnabled
            + " turretFeedReliability=" + RebirthComplexSkillSystemService.DeployableTurretFeedReliabilityEnabled
            + " metalHeatGrade=" + RebirthServiceCraftSkillService.MetalworkingPersistentHeatTreatmentGradeEnabled);
        b.AppendLine("releaseBlockers=live_compile,host_runtime,p2p_client_runtime,dedicated_client_runtime,save_reload_reconnect,ui_screenshot_parity,performance_observation; intentionallyBlocked=chemistry_payload_metadata,turret_feed_reliability");
        if (player != null)
        {
            b.AppendLine("player=" + player.entityId);
            string[] ids = new string[] { "skill.lockpicking", "skill.bartering", "skill.trading", "skill.teaching", "skill.medicine", "skill.cooking", "skill.drink_preparation", "skill.scythes", "skill.animal_handling", "skill.athletics", "skill.stealth", "skill.armor_proficiency", "skill.explosives", "skill.deployable_turrets", "skill.drone_operations", "skill.construction", "skill.electrical" };
            for (int i = 0; i < ids.Length; i++)
            {
                float v; bool ok = RebirthServiceCraftSkillService.TryGetSkillValue(player, ids[i], out v);
                b.AppendLine("  " + ids[i] + "=" + (ok ? v.ToString("0.###", CultureInfo.InvariantCulture) : "<unavailable>"));
            }
        }
        return b.ToString().TrimEnd();
    }

    public static string BuildBalanceBands()
    {
        StringBuilder b = new StringBuilder();
        b.AppendLine("[REBIRTH Survivor Balance Bands]");
        b.AppendLine("band,explosiveDamage,resourceHarvest,farmingHarvest,animalHarvest,craftTime,repairAmount,meleeStamina,rangedReload,rangedSpread,droneDamage,droneStunCycle,medicineReserve,medicineFractureHealing,medicineInfectionAcceleration");
        for (int i = 0; i < Bands.Length; i++)
        {
            float v = Bands[i];
            b.AppendLine(F(v) + "," +
                F(S(v, RebirthProgressionRuntimeConfig.ExplosivesDamageNegative, RebirthProgressionRuntimeConfig.ExplosivesDamagePositive)) + "," +
                F(S(v, RebirthProgressionRuntimeConfig.ResourceHarvestNegative, RebirthProgressionRuntimeConfig.ResourceHarvestPositive)) + "," +
                F(S(v, RebirthProgressionRuntimeConfig.FarmingHarvestNegative, RebirthProgressionRuntimeConfig.FarmingHarvestPositive)) + "," +
                F(S(v, RebirthProgressionRuntimeConfig.AnimalHarvestNegative, RebirthProgressionRuntimeConfig.AnimalHarvestPositive)) + "," +
                F(S(v, RebirthProgressionRuntimeConfig.ServiceCraftTimeNegative, RebirthProgressionRuntimeConfig.ServiceCraftTimePositive)) + "," +
                F(S(v, RebirthProgressionRuntimeConfig.ServiceRepairAmountNegative, RebirthProgressionRuntimeConfig.ServiceRepairAmountPositive)) + "," +
                F(S(v, RebirthProgressionRuntimeConfig.MeleeStaminaNegative, RebirthProgressionRuntimeConfig.MeleeStaminaPositive)) + "," +
                F(S(v, RebirthProgressionRuntimeConfig.RangedReloadNegative, RebirthProgressionRuntimeConfig.RangedReloadPositive)) + "," +
                F(S(v, RebirthProgressionRuntimeConfig.RangedSpreadNegative, RebirthProgressionRuntimeConfig.RangedSpreadPositive)) + "," +
                F(S(v, RebirthProgressionRuntimeConfig.DroneDamageNegative, RebirthProgressionRuntimeConfig.DroneDamagePositive)) + "," +
                F(S(v, RebirthProgressionRuntimeConfig.DroneStunCycleNegative, RebirthProgressionRuntimeConfig.DroneStunCyclePositive)) + "," +
                F(S(v, RebirthProgressionRuntimeConfig.MedicineReserveNegative, RebirthProgressionRuntimeConfig.MedicineReservePositive)) + "," +
                F(S(v, RebirthProgressionRuntimeConfig.MedicineFractureHealingNegative, RebirthProgressionRuntimeConfig.MedicineFractureHealingPositive)) + "," +
                F(S(v, 0f, RebirthProgressionRuntimeConfig.MedicineInfectionAccelerationPositive)));
        }
        b.AppendLine("NOTE: values are percent-add deltas; 0 is native-neutral. Tracking uses distance/acquisition interpolation and is covered by Chunk 6 vectors.");
        return b.ToString().TrimEnd();
    }

    public static string BuildAuthorityReport()
    {
        ConnectionManager c = SingletonMonoBehaviour<ConnectionManager>.Instance;
        StringBuilder b = new StringBuilder();
        b.AppendLine("[REBIRTH Survivor Authority Acceptance]");
        b.AppendLine("connectionPresent=" + (c != null) + " isServer=" + (c != null && c.IsServer) + " repositoryServer=" + RebirthWorldCharacterRepository.IsServerAuthority);
        b.AppendLine("skillAwardAuthority=server RebirthSkillAwardService");
        b.AppendLine("theoryAuthority=server RebirthSkillKnowledgeService(internal compatibility name) persistenceSchema=" + RebirthWorldCharacterRecord.CurrentSchemaVersion + " networkProtocol=" + RebirthSurvivorNetworkProtocol.Version);
        b.AppendLine("debugMutationGate=" + RebirthSurvivorDebug.Enabled + " expectedReleaseDefault=False");
        b.AppendLine("clientTrustedSkillRewardPacket=False");
        b.AppendLine("persistedAntiRepeat=SkillAwardCooldowns+CommerceTraining clock=character_active_play_seconds schema="+RebirthWorldCharacterRecord.CurrentSchemaVersion);
        b.AppendLine("runtimeRolesRequired=host,p2p_client,dedicated_client");
        return b.ToString().TrimEnd();
    }

    public static string BuildRuntimeMatrix()
    {
        StringBuilder b=new StringBuilder();
        b.AppendLine("[REBIRTH Survivor Phase 12 Runtime Acceptance Matrix]");
        b.AppendLine("releaseApproved=False until every REQUIRED row is executed against the exact built revision.");
        b.AppendLine("01 REQUIRED compile/startup: rebuild C#; no compile errors; Harmony/install startup clean.");
        b.AppendLine("02 REQUIRED host UI: Character/Progression + Explorer show 5 Attributes, Skill/Theory/status/source/reveal and authoritative contribution.");
        b.AppendLine("03 REQUIRED combat host: normal/power melee, bow/crossbow, revolver, semiauto, full-auto, shotgun/reload-heavy; projection vs actual; overkill/mitigation.");
        b.AppendLine("04 REQUIRED devices: stock drone operation/recovery, idle/deploy zero, shock/optional combat attribution, turret owner attribution.");
        b.AppendLine("05 REQUIRED world/output: primitive/powered Mining+Logging, Salvage output/no-output, mature/immature Farming + yield bonus, carcass output/kill-shot zero.");
        b.AppendLine("06 REQUIRED technical/process: recipe batches/blocked output, Mechanics, Medicine self/other parity, HP reserve scaling, infection cure-budget conservation/acceleration, fracture treated-base no-repeat-compounding, bleeding unchanged, equipment repair, Electrical repeat-state, lockpick success/failure.");
        b.AppendLine("07 REQUIRED social/exertion: buy->Bartering, sell->Trading, buy/sell loop guards, Athletics idle/jump zero, armor burden, NPC Teaching, Black Magic/Rage.");
        b.AppendLine("08 REQUIRED save/reload: Attributes/Skills/Theory/study markers/Insights/lesson history/durable receipts persist; schema17->18 migrates once.");
        b.AppendLine("09 REQUIRED anti-repeat restart: drone/Tracking/Black-Magic minimum intervals and commerce global/repeat/opposite/variety windows survive reload; offline time does not consume them.");
        b.AppendLine("10 REQUIRED reconnect: personal crafting/deferred cooking/Teaching/repair receipts do not duplicate and legitimate deferred credit is not lost.");
        b.AppendLine("11 REQUIRED P2P client: representative combat, process, commerce, Teaching and UI projections match host authority; no client XP amount trusted.");
        b.AppendLine("12 REQUIRED dedicated client: same representative routes and five-Attribute snapshots match dedicated authority.");
        b.AppendLine("13 REQUIRED exploit sweep: zero-effect, cancel, output blocked, duplicate packet, reconnect, install/remove, buy/sell, deploy/pickup, re-dose, yield bonus, idle time all award zero when ineligible.");
        b.AppendLine("14 REQUIRED visual/performance: screenshots show readable parity; Progression/Explorer and projection polling introduce no unacceptable FPS or log spam regression.");
        b.AppendLine("15 REQUIRED acquisition: fresh non-specialist characters can obtain Campfire Cooking Basics and Electrical service manuals through normal loot/trader routes; learned items grant exact authored Knowledge; non-Farmer Rusty Sickle acquisition succeeds through tool loot/trader sources.");
        b.AppendLine("intentionallyOutOfScope=chemistry_payload_metadata,turret_feed_reliability");
        return b.ToString().TrimEnd();
    }

    public static float S(float value, float negativeAtMinus50, float positiveAt100)
    {
        value = Math.Max(-50f, Math.Min(100f, value));
        if (value < 0f) return negativeAtMinus50 * (-value / 50f);
        if (value > 0f) return positiveAt100 * (value / 100f);
        return 0f;
    }

    private static string F(float v) { return v.ToString("0.###", CultureInfo.InvariantCulture); }
}
