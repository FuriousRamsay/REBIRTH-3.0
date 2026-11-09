using System;
using System.Collections.Generic;
using System.Diagnostics;

#nullable disable

/// <summary>
/// The one switch for REBIRTH debug logging (owner 2026-11-08):
/// <list type="bullet">
/// <item>Release builds: every <see cref="Log"/> call is removed by the compiler (with its arguments), and
/// <see cref="On"/> is the constant false, so guarded code is dropped too. Debug logging costs nothing.</item>
/// <item>Debug builds (REBIRTH_DEBUG): nothing is logged until it is turned on with the console command
/// "rbdebug on" (everything) or "rbdebug on &lt;area&gt;" (one area, e.g. survivor, vehicles, npc). Off at every start.</item>
/// </list>
/// Areas are the top folder under Scripts, lower case. Warnings, errors and console command replies do not go
/// through here and are never hidden.
/// </summary>
public static class RebirthDebug
{
#if REBIRTH_DEBUG
    private static bool on;
    private static readonly HashSet<string> areas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>True once debug logging was turned on with "rbdebug on" (any area).</summary>
    public static bool On => on;

    /// <summary>True when this area logs: debug logging is on and either every area or this one was chosen.</summary>
    public static bool IsOn(string area) => on && (areas.Count == 0 || (area != null && areas.Contains(area)));

    internal static void TurnOn(string area)
    {
        on = true;
        if (!string.IsNullOrEmpty(area)) areas.Add(area);
    }

    internal static void TurnOff(string area)
    {
        if (string.IsNullOrEmpty(area)) { on = false; areas.Clear(); return; }
        areas.Remove(area);
        if (areas.Count == 0) on = false;
    }

    internal static string Describe() => !on ? "off" : areas.Count == 0 ? "on (all areas)" : "on: " + string.Join(", ", areas);
#else
    /// <summary>Release build: always false, so code behind it is removed.</summary>
    public const bool On = false;

    public static bool IsOn(string area) => false;
#endif

    /// <summary>Writes a debug line for an area. Removed from Release builds, silent in Debug builds until "rbdebug on".</summary>
    [Conditional("REBIRTH_DEBUG")]
    public static void Log(string area, string message)
    {
#if REBIRTH_DEBUG
        if (IsOn(area)) global::Log.Out(message);
#endif
    }
}
