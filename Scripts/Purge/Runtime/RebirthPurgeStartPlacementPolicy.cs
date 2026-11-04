using System;
internal static class RebirthPurgeStartPlacementPolicy
{
    internal const int MaximumSiteAttempts = 6;
    internal const float MaximumSiteSeconds = 30f;
    internal const float MinimumNewSiteDistance = 256f;
    internal static bool Relocate(int attempts, double seconds) => attempts >= MaximumSiteAttempts || seconds >= MaximumSiteSeconds;
    internal static bool FarEnough(double dx, double dz) => dx * dx + dz * dz >= MinimumNewSiteDistance * MinimumNewSiteDistance;
    internal static bool Removable(string name, bool decoration, string shape)
    {
        if (!decoration || string.IsNullOrEmpty(name)) return false;
        if (name.StartsWith("rock", StringComparison.OrdinalIgnoreCase) && name.Length > 4)
        {
            bool digits = true;
            for (int i = 4; i < name.Length; ++i) digits &= char.IsDigit(name[i]);
            if (digits) return true;
        }
        return name.StartsWith("goreBlock", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("goreHuman", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(shape, "BillboardPlant", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("tree", StringComparison.OrdinalIgnoreCase) &&
            (name.IndexOf("Leaf", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("Bush", StringComparison.OrdinalIgnoreCase) >= 0);
    }
}
