#if !DEBUG
#nullable disable

/// <summary>
/// Release compatibility facade. Protect-crate trace collection and commands are omitted.
/// </summary>
public static class RebirthProtectCrateTrace
{
    public static bool Enabled { get { return false; } }
    public static void SetEnabled(bool value) { }
    public static void Emit(string message) { }
    public static void EmitThrottled(string key, string message, float minIntervalSeconds) { }
    public static string DescribeRole() { return ConnectionManager.Instance != null && ConnectionManager.Instance.IsServer ? "server" : "client"; }
}
#endif
