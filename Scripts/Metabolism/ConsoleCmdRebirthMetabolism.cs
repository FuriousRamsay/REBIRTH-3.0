using System;
using System.Collections.Generic;
using System.Globalization;

#nullable disable

public sealed class ConsoleCmdRebirthMetabolism : ConsoleCmdAbstract
{
    public override string[] getCommands() { return new[] { "rbmet" }; }
    public override string getDescription() { return "Inspect and test the REBIRTH metabolism, hydration and digestion system."; }
    public override string getHelp()
    {
        return "rbmet status|rates|genetics|stomach|slot|validate|debug on|off\n"
             + "rbmet trace [seconds]   (default 120, range 10-600; detailed flow log, auto-stops)\n"
             + "rbmet trace stop\n"
             + "rbmet set hydration <0-100>\nrbmet set nutrition <0-100>\nrbmet set food <0-100> (legacy alias)\nrbmet set digestivehealth <0-100>\nrbmet set energy <0-100>\n"
             + "rbmet energytest <hydration> <nutrition> [energy=50] [traceSeconds=120]  (cap = Nutrition x Hydration efficiency; minimum sustainable cap 10)\n"
             + "rbmet clearstomach\nrbmet ingest water <ml>\nrbmet ingest food <nutrition> <volumeMl> [waterMl]\n"
             + "rbmet autosip on|off";
    }

    public override void Execute(List<string> p, CommandSenderInfo senderInfo)
    {
        string cmd = p != null && p.Count > 0 ? p[0].ToLowerInvariant() : "status";
        if (cmd == "validate") { Log.Out(RebirthMetabolismAuthoringValidator.BuildReport()); return; }
        if (cmd == "debug")
        {
            bool on = p != null && p.Count > 1 && p[1].Equals("on", StringComparison.OrdinalIgnoreCase);
            RebirthMetabolismService.DebugEnabled = on;
            Log.Out("[REBIRTH Metabolism] debug=" + on);
            return;
        }

        EntityPlayer player = ResolvePlayer(senderInfo);
        if (player == null) { Log.Warning("[REBIRTH Metabolism] no player available for command."); return; }
        RebirthMetabolismService.EnsurePlayerReady(player);
        RebirthMetabolismState state = RebirthMetabolismStateRepository.GetOrCreate(player);
        if (state == null) { Log.Warning("[REBIRTH Metabolism] player state unavailable."); return; }

        if (cmd == "trace")
        {
            if (p.Count > 1 && p[1].Equals("stop", StringComparison.OrdinalIgnoreCase))
            {
                RebirthMetabolismService.StopDebugTrace(player);
                return;
            }
            float seconds = 120f;
            if (p.Count > 1 && !float.TryParse(p[1], NumberStyles.Float, CultureInfo.InvariantCulture, out seconds))
            {
                Log.Out(getHelp());
                return;
            }
            RebirthMetabolismService.StartDebugTrace(player, seconds);
            Log.Out("[REBIRTH Metabolism] Trace captures movement/Energy plus stomach -> intestine -> Hydration/Nutrition flow once per metabolism tick.");
            return;
        }

        if (cmd == "energytest")
        {
            if (p.Count < 3) { Log.Out(getHelp()); return; }

            float hydration;
            float nutrition;
            float energy = 50f;
            float seconds = 120f;
            if (!float.TryParse(p[1], NumberStyles.Float, CultureInfo.InvariantCulture, out hydration)
                || !float.TryParse(p[2], NumberStyles.Float, CultureInfo.InvariantCulture, out nutrition)
                || (p.Count > 3 && !float.TryParse(p[3], NumberStyles.Float, CultureInfo.InvariantCulture, out energy))
                || (p.Count > 4 && !float.TryParse(p[4], NumberStyles.Float, CultureInfo.InvariantCulture, out seconds)))
            {
                Log.Out(getHelp());
                return;
            }

            hydration = UnityEngine.Mathf.Clamp(hydration, 0f, 100f);
            nutrition = UnityEngine.Mathf.Clamp(nutrition, 0f, 100f);
            energy = UnityEngine.Mathf.Clamp(energy, 0f, RebirthMetabolismConfig.EnergyMax);
            seconds = UnityEngine.Mathf.Clamp(seconds, 10f, 600f);

            // Make this a controlled Energy test: no stomach/intestine payload may silently
            // replenish Hydration or Nutrition while the requested reserve levels are observed.
            state.IngestionEntries.Clear();
            player.Stats.Water.Value = hydration;
            player.Stats.Food.Value = nutrition;
            state.Energy = energy;
            RebirthMetabolismService.ClampEnergyToReserveCeiling(player, state);
            state.AutoSipEnabled = false;
            state.AutoSipCooldownRemainingRealSeconds = 0f;
            state.SmoothedActivity = 0f;
            state.LastStamina = player.Stats.Stamina.Value;
            state.Touch();
            RebirthMetabolismService.SendSnapshotToOwner(player, true);
            RebirthMetabolismService.StartDebugTrace(player, seconds);

            Log.Out("[REBIRTH Metabolism] Energy test prepared: hydration="
                + hydration.ToString("0.0", CultureInfo.InvariantCulture)
                + " nutrition=" + nutrition.ToString("0.0", CultureInfo.InvariantCulture)
                + " requestedEnergy=" + energy.ToString("0.0", CultureInfo.InvariantCulture)
                + " actualEnergy=" + state.Energy.ToString("0.0", CultureInfo.InvariantCulture)
                + " ceiling=" + RebirthMetabolismService.GetEnergyReserveCeiling(player).ToString("0.0", CultureInfo.InvariantCulture)
                + " trace=" + seconds.ToString("0", CultureInfo.InvariantCulture)
                + "s; digestion cleared; autosip disabled (rbmet autosip on to restore it).");
            return;
        }

        if (cmd == "status" || cmd == "rates" || cmd == "genetics")
        {
            RebirthMetabolismSnapshot s = RebirthMetabolismService.BuildSnapshot(player);
            Log.Out("[REBIRTH Metabolism] " + RebirthMetabolismService.FormatSnapshot(s));
            Log.Out("  modifiers hydrationNeed=" + P(s.HydrationRequirementMultiplier) + " nutritionNeed=" + P(s.FoodRequirementMultiplier)
                + " fluidAbsorb=" + P(s.FluidAbsorptionMultiplier) + " fluidUse=" + P(s.FluidUtilizationMultiplier)
                + " nutrientUse=" + P(s.NutrientUtilizationMultiplier) + " digestion=" + P(s.DigestionSpeedMultiplier)
                + " gut=" + P(s.GutResilienceMultiplier));
            return;
        }
        if (cmd == "stomach")
        {
            Log.Out("[REBIRTH Metabolism] stomach fluid=" + RebirthMetabolismService.GetFluidMl(state).ToString("0.0", CultureInfo.InvariantCulture)
                + "ml solid=" + RebirthMetabolismService.GetSolidMl(state).ToString("0.0", CultureInfo.InvariantCulture)
                + "ml intestineFluid=" + RebirthMetabolismService.GetIntestinalFluidMl(state).ToString("0.0", CultureInfo.InvariantCulture)
                + "ml intestineChyme=" + RebirthMetabolismService.GetIntestinalSolidMl(state).ToString("0.0", CultureInfo.InvariantCulture)
                + "ml intestineNutrition=" + RebirthMetabolismService.GetIntestinalNutritionUnits(state).ToString("0.0", CultureInfo.InvariantCulture)
                + " intestineEnergy=" + RebirthMetabolismService.GetIntestinalEnergyUnits(state).ToString("0.0", CultureInfo.InvariantCulture)
                + " hold=" + RebirthMetabolismService.GetGastricHoldSecondsRemaining(state).ToString("0.0", CultureInfo.InvariantCulture)
                + "s entries=" + state.IngestionEntries.Count + " digestiveHealth=" + state.DigestiveHealth.ToString("0.0", CultureInfo.InvariantCulture));
            for (int i = 0; i < state.IngestionEntries.Count; i++)
            {
                RebirthIngestionEntry e = state.IngestionEntries[i];
                if (e == null) continue;
                Log.Out("  #" + e.EntryId + " " + e.Kind + " source=" + e.SourceItemName + " stomachFluid=" + e.RemainingFluidVolumeMl.ToString("0.0")
                    + " stomachSolid=" + e.RemainingSolidVolumeMl.ToString("0.0") + " stomachNutrition=" + e.RemainingNutritionUnits.ToString("0.0")
                    + " stomachEnergy=" + e.RemainingEnergyUnits.ToString("0.0")
                    + " intestineFluid=" + e.IntestinalFluidVolumeMl.ToString("0.0") + " intestineChyme=" + e.IntestinalSolidVolumeMl.ToString("0.0")
                    + " intestineNutrition=" + e.IntestinalNutritionUnits.ToString("0.0")
                    + " intestineEnergy=" + e.IntestinalEnergyUnits.ToString("0.0")
                    + " stomachHolds=" + e.FluidHoldSecondsRemaining.ToString("0") + "/" + e.SolidHoldSecondsRemaining.ToString("0") + "s"
                    + " intestineHolds=" + e.IntestinalFluidResidenceSecondsRemaining.ToString("0") + "/" + e.IntestinalNutritionResidenceSecondsRemaining.ToString("0") + "s");
            }
            return;
        }
        if (cmd == "slot")
        {
            RebirthMetabolismSnapshot s = RebirthMetabolismService.BuildSnapshot(player);
            Log.Out("[REBIRTH Metabolism] slot='" + (s.HydrationSlotItemName ?? string.Empty) + "' " + s.HydrationSlotVolumeMl.ToString("0")
                + "/" + s.HydrationSlotCapacityMl.ToString("0") + "ml liquid=" + (s.HydrationSlotLiquidProfile ?? string.Empty) + " autosip=" + s.AutoSipEnabled);
            return;
        }
        if (cmd == "clearstomach")
        {
            state.IngestionEntries.Clear(); state.Touch(); RebirthMetabolismService.SendSnapshotToOwner(player, true); Log.Out("[REBIRTH Metabolism] stomach cleared."); return;
        }
        if (cmd == "autosip" && p.Count > 1)
        {
            bool wanted = p[1].Equals("on", StringComparison.OrdinalIgnoreCase);
            if (state.AutoSipEnabled != wanted) RebirthMetabolismService.ToggleAutoSip(player);
            Log.Out("[REBIRTH Metabolism] autosip=" + wanted); return;
        }
        if (cmd == "set" && p.Count > 2)
        {
            float v; if (!float.TryParse(p[2], NumberStyles.Float, CultureInfo.InvariantCulture, out v)) { Log.Out(getHelp()); return; }
            string stat = p[1].ToLowerInvariant();
            if (stat == "hydration") player.Stats.Water.Value = UnityEngine.Mathf.Clamp(v, 0f, 100f);
            else if (stat == "nutrition" || stat == "food") player.Stats.Food.Value = UnityEngine.Mathf.Clamp(v, 0f, 100f);
            else if (stat == "digestivehealth") state.DigestiveHealth = UnityEngine.Mathf.Clamp(v, 0f, 100f);
            else if (stat == "energy") state.Energy = UnityEngine.Mathf.Clamp(v, 0f, RebirthMetabolismConfig.EnergyMax);
            else { Log.Out(getHelp()); return; }
            // Nutrition and Hydration define the sustainable Energy curve, so debug stat
            // edits enforce the same invariant as normal gameplay immediately.
            RebirthMetabolismService.ClampEnergyToReserveCeiling(player, state);
            state.Touch();
            RebirthMetabolismService.SendSnapshotToOwner(player, true);
            float displayed = stat == "hydration" ? player.Stats.Water.Value
                : (stat == "nutrition" || stat == "food") ? player.Stats.Food.Value
                : stat == "energy" ? state.Energy : state.DigestiveHealth;
            Log.Out("[REBIRTH Metabolism] set " + stat + "=" + displayed.ToString("0.0", CultureInfo.InvariantCulture));
            return;
        }
        if (cmd == "ingest" && p.Count > 2)
        {
            string kind = p[1].ToLowerInvariant();
            float a; if (!float.TryParse(p[2], NumberStyles.Float, CultureInfo.InvariantCulture, out a)) { Log.Out(getHelp()); return; }
            if (kind == "water")
            {
                state.IngestionEntries.Add(new RebirthIngestionEntry { EntryId=state.AllocateEntryId(), Kind=RebirthIngestionKind.Fluid, SourceItemName="rbmet-debug-water", LiquidProfileId="cleanWater", RemainingFluidVolumeMl=Math.Max(0f,a), FluidYieldMultiplierSnapshot=1f, NutrientYieldMultiplierSnapshot=1f, DigestionRateMultiplierSnapshot=1f, FluidIsMealBound=false, FluidHoldSecondsRemaining=RebirthMetabolismConfig.ClearLiquidHoldRealSeconds, FluidGastricHalfTimeSecondsSnapshot=RebirthMetabolismConfig.ClearLiquidGastricHalfTimeRealSeconds, IngestedWorldTime=player.world.GetWorldTime() });
            }
            else if (kind == "food" && p.Count > 3)
            {
                float volume; if (!float.TryParse(p[3], NumberStyles.Float, CultureInfo.InvariantCulture, out volume)) { Log.Out(getHelp()); return; }
                float water=0f; if (p.Count>4) float.TryParse(p[4], NumberStyles.Float, CultureInfo.InvariantCulture, out water);
                state.IngestionEntries.Add(new RebirthIngestionEntry { EntryId=state.AllocateEntryId(), Kind=RebirthIngestionKind.Food, SourceItemName="rbmet-debug-food", DigestionProfileId="normal", RemainingNutritionUnits=Math.Max(0f,a), RemainingSolidVolumeMl=Math.Max(0f,volume), RemainingFluidVolumeMl=Math.Max(0f,water), FluidYieldMultiplierSnapshot=1f, NutrientYieldMultiplierSnapshot=1f, DigestionRateMultiplierSnapshot=1f, FluidIsMealBound=water>0f, FluidHoldSecondsRemaining=water>0f?RebirthMetabolismConfig.MealFluidHoldRealSeconds:0f, SolidHoldSecondsRemaining=RebirthMetabolismConfig.NormalFoodGastricLagRealSeconds, FluidGastricHalfTimeSecondsSnapshot=water>0f?RebirthMetabolismConfig.MealFluidGastricHalfTimeRealSeconds:0f, IngestedWorldTime=player.world.GetWorldTime() });
            }
            else { Log.Out(getHelp()); return; }
            state.Touch(); RebirthMetabolismService.SendSnapshotToOwner(player, true); return;
        }
        Log.Out(getHelp());
    }

    private static string P(float v) { return (v * 100f).ToString("0", CultureInfo.InvariantCulture) + "%"; }

    private static EntityPlayer ResolvePlayer(CommandSenderInfo senderInfo)
    {
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (world == null) return null;
        if (senderInfo.RemoteClientInfo != null)
        {
            EntityPlayer remote = world.GetEntity(senderInfo.RemoteClientInfo.entityId) as EntityPlayer;
            if (remote != null) return remote;
        }
        return world.GetPrimaryPlayer() as EntityPlayer;
    }
}
