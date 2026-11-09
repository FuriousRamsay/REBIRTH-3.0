#nullable disable

/// <summary>
/// Owner 2026-11-09: traces where the REBIRTH sandbox options come from and whether they are in effect.
/// Lines appear only with `rbdebug on survivor` (or `rbdebug on`), on clients and servers; Release builds remove them.
/// </summary>
internal static class RebirthSandboxTrace
{
    internal static string Describe()
    {
        var o = RebirthSandboxOptionManager.Current;
        return "code=" + o.CurrentCode + " revision=" + o.Revision + " progression=" + o.PlayerProgression +
            " theme=" + o.Theme + " purgeReleased=" + RebirthPurgeReleasePolicy.Enabled + " isPurge=" + o.IsPurge;
    }

    internal static void Client(string what)
    {
        if (!RebirthDebug.IsOn("survivor")) return;
        RebirthDebug.Log("survivor", "[REBIRTH OptionsTrace] " + what + " " + Describe());
    }

    internal static void Server(string what)
    {
        Client(what); // rbdebug only, on servers too (owner 2026-11-09: debug logging off by default)
    }
}
