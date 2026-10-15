using System;

// Select once on the authority, using current membership rather than client seat claims.
internal static class RebirthVehicleSeatCycle
{
    internal static int Next(int count, int current, Func<int, bool> occupied, bool mayDrive)
    {
        if (count < 2 || current < 0 || current >= count || occupied == null) return -1;
        for (int offset = 1; offset < count; offset++)
        {
            int candidate = (current + offset) % count;
            if (candidate == 0 && !mayDrive) continue;
            if (!occupied(candidate)) return candidate;
        }
        return -1;
    }
}
