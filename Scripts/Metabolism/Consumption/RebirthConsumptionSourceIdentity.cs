internal static class RebirthConsumptionSourceIdentity
{
    internal static byte[] Fingerprint(ItemValue value)
    {
        if (value == null) throw new System.ArgumentNullException(nameof(value));
        using (var sha = System.Security.Cryptography.SHA256.Create())
            return sha.ComputeHash(System.Convert.FromBase64String(RebirthNativeItemCodec.Encode(value)));
    }
    internal static bool MatchesFingerprint(ItemValue value, byte[] expected)
    {
        if (value == null || expected == null || expected.Length != 32) return false;
        var actual = Fingerprint(value);
        for (int i = 0; i < actual.Length; i++) if (actual[i] != expected[i]) return false;
        return true;
    }
    internal static byte[] ScopeFingerprint(string world, string creation, int deaths)
    {
        using (var stream = new System.IO.MemoryStream())
        using (var writer = new System.IO.BinaryWriter(stream, System.Text.Encoding.UTF8))
        using (var sha = System.Security.Cryptography.SHA256.Create())
        {
            writer.Write(world ?? string.Empty);writer.Write(creation ?? string.Empty);writer.Write(deaths);writer.Flush();
            return sha.ComputeHash(stream.ToArray());
        }
    }
    internal static bool MatchesScope(string world, string creation, int deaths, byte[] expected)
    {
        if (expected == null || expected.Length != 32) return false;
        var actual = ScopeFingerprint(world, creation, deaths);
        for (int i = 0; i < actual.Length; i++) if (actual[i] != expected[i]) return false;
        return true;
    }
    // Type/seed narrows candidates; current remote requests additionally bind
    // the complete native item fingerprint. Stack size is not item identity.
    internal static bool Matches(ItemStack stack, int itemType, ushort seed)
        => stack != null && !stack.IsEmpty() && stack.itemValue != null
            && stack.itemValue.type == itemType && stack.itemValue.Seed == seed;
}
