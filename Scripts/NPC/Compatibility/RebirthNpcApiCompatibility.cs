#nullable disable

/// <summary>
/// Legacy compatibility telemetry retained for report compatibility. The 3.1
/// implementation no longer performs runtime member discovery: all gameplay
/// adapters are compiled directly against the supplied 3.1 API surface.
/// </summary>
public static class RebirthNpcApiCompatibility
{
    public static void Reset()
    {
    }

    public static RebirthNpcApiCompatibilitySnapshot GetSnapshot()
    {
        return new RebirthNpcApiCompatibilitySnapshot(0, 0);
    }
}

public struct RebirthNpcApiCompatibilitySnapshot
{
    public readonly int ResolvedMethodCount;
    public readonly int MissingMethodCount;

    public RebirthNpcApiCompatibilitySnapshot(int resolvedMethodCount, int missingMethodCount)
    {
        ResolvedMethodCount = resolvedMethodCount;
        MissingMethodCount = missingMethodCount;
    }
}
