using System;
using System.Collections.Generic;

// Detached native banks keep their real ItemValues. Only paid native charge
// operations run here; no graph parenting, generator debit or battery feedback.
internal sealed class RebirthCamperBatteryCharging
{
    private double periodRemainder;
    private bool engineWasRunning;

    internal void Reset() { periodRemainder = 0; engineWasRunning = false; }

    internal static bool EngineRunning(EntityVehicle vehicle)
        => vehicle != null && vehicle.IsEngineRunning && vehicle.GetAttached(0) is EntityPlayer
            && (vehicle.vehicle.GetFuelLevel() > 0 || EntityVehicle.VehicleFuelUsageModifier == 0);

    internal void Apply(IList<RebirthCamperEnergyState.Source> sources, Dictionary<Guid, long> solarBudget,
        double elapsed, bool engineRunning, int alternatorWatts)
    {
        if (double.IsNaN(elapsed) || double.IsInfinity(elapsed) || elapsed <= 0)
            throw new ArgumentException("Finite positive charging interval required.");
        // Never accrue an unloaded/held catch-up debt. A very long live frame
        // also cannot accelerate charging beyond 64 native periods (10.24s).
        double periods = Math.Min(64, elapsed / RebirthCamperEnergyRuntime.NativePowerPeriodSeconds + periodRemainder);
        int wholePeriods = (int)Math.Floor(periods);
        periodRemainder = periods - wholePeriods;
        // A newly started engine has not run during the preceding interval.
        // Stopping immediately removes alternator funding; no energy is carried.
        long alternator = engineRunning && engineWasRunning ? (long)alternatorWatts * wholePeriods : 0;
        engineWasRunning = engineRunning;
        for (int tick = 0; tick < wholePeriods; tick++)
        foreach (var source in sources)
        {
            if (!(source.Bank is PowerBatteryBank bank) || !bank.isOn || bank.InputPerTick == 0) continue;
            // The installed native bank definitions use ChargePerInput=1.
            // Native AddPowerToBatteries can overdraw its argument for larger
            // custom conversions; refuse that unsupported conversion safely.
            if (bank.ChargePerInput != 1) continue;
            bool accepts = false;
            foreach (var item in bank.Stacks)
                if (!item.IsEmpty() && item.itemValue.UseTimes >= 1) { accepts = true; break; }
            if (!accepts) continue;
            long solar = AvailableSolar(sources, solarBudget);
            int input = bank.InputPerTick;
            if (solar + alternator < input) continue;
            // Exactly the same publicized native conversion used by
            // HandlePowerReceived, with its native per-tick acceptance ceiling.
            bank.AddPowerToBatteries(input);
            int accepted = bank.LastInputAmount;
            if (accepted < 0 || accepted > input)
                throw new InvalidOperationException("Native battery accepted more input than offered.");
            long fromSolar = Math.Min(solar, accepted);
            DebitSolar(sources, solarBudget, fromSolar);
            alternator -= accepted - fromSolar;
        }
    }

    private static long AvailableSolar(IList<RebirthCamperEnergyState.Source> sources, Dictionary<Guid, long> budget)
    {
        long available = 0;
        foreach (var source in sources)
            if (source.Bank is PowerSolarPanel solar && solar.isOn && solar.HasLight && budget.TryGetValue(source.Id, out long amount))
                available = checked(available + Math.Max(0, amount));
        return available;
    }

    private static void DebitSolar(IList<RebirthCamperEnergyState.Source> sources, Dictionary<Guid, long> budget, long amount)
    {
        foreach (var source in sources)
        {
            if (!(source.Bank is PowerSolarPanel solar) || !solar.isOn || !solar.HasLight || !budget.TryGetValue(source.Id, out long available)) continue;
            long taken = Math.Min(amount, Math.Max(0, available));
            budget[source.Id] = available - taken; amount -= taken;
            if (amount == 0) return;
        }
        if (amount != 0) throw new InvalidOperationException("Paid solar charging budget changed.");
    }
}
