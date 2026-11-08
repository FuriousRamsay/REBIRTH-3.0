#nullable disable
/// <summary>Camper energy step-by-step traces ([REBIRTH EnergyFlow] / [REBIRTH EnergyTrace]). They log only when the REBIRTH
/// logging config turns automatic logging on; warnings and errors are logged directly and are never gated.</summary>
internal static class RebirthCamperEnergyLog
{
    internal static bool Enabled => RebirthLogSettings.AutomaticLoggingDefault;
    internal static void Out(string message) { if (Enabled) Log.Out(message); }
}
