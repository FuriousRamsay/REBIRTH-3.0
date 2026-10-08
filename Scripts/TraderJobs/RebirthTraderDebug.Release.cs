#if !DEBUG
using System.Diagnostics;

#nullable disable

/// <summary>
/// Release build facade. Trader diagnostic implementation and command are excluded.
/// Trace calls are removed by the compiler because DEBUG is not defined.
/// </summary>
public static class RebirthTraderDebug
{
    public const bool Enabled = false;

    [Conditional("DEBUG")]
    public static void Trace(string message) { }
}
#endif
