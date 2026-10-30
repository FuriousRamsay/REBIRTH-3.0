using System;
using System.Collections.Generic;

// Authored in code only. Use the complete EOS_ identifier from rebirth playerid.
internal static class RebirthPurgePlayerWrapAssignments
{
    internal sealed class Assignment
    {
        internal readonly string EosId, WrapItem;
        internal Assignment(string eosId, string wrapItem) { EosId = eosId; WrapItem = wrapItem; }
    }

    private static readonly List<Assignment> Rows = new List<Assignment>
    {
        new Assignment("EOS_00024e82683f45e09594ae9d6f6893cc", "modVehicleWrapFuriousRamsay"),
        new Assignment("EOS_00029b75a39747cb85a3278a0fb31ac3", "modVehicleWrapCaptStress"),
        new Assignment("EOS_00025e46dead4c0f90abddffa76bc8d1", "modVehicleWrapPurpleLlama"),
        new Assignment("EOS_0002be0688df4e849ff8dd9ac2025cca", "modVehicleWrapUndeadChill"),
        new Assignment("EOS_0002be0688df4e849ff8dd9ac2025cca", "modVehicleWrapUndeadChillAfterHours"),
        new Assignment("EOS_00026266d8b842f981e60ea4640fdc8f", "modVehicleWrapGenmaSaotome"),
        new Assignment("EOS_0002d44f5ff04025b6661810fd559abe", "modVehicleWrapMissBanjo"),
        new Assignment("EOS_0002bb72bdeb454295e656063b9d8838", "modVehicleWrapHeyWeps"),
        // Replace the sample EOS with the complete ID
        // printed by rebirth playerid, then uncomment one row per assigned wrap.
        // Repeating the same EOS with different items gives that player both:
        // new Assignment("EOS_REPLACE_WITH_REAL_ID", "modVehicleWrapUndeadChill"),
        // new Assignment("EOS_REPLACE_WITH_REAL_ID", "modVehicleWrapUndeadChillAfterHours"),
    };

    internal static string[] ForPlayer(string eosId) => Resolve(eosId, Rows);

    internal static string[] Resolve(string eosId, IEnumerable<Assignment> rows)
    {
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(eosId) || !eosId.StartsWith("EOS_", StringComparison.OrdinalIgnoreCase))
            return result.ToArray();
        foreach (var row in rows)
            if (row != null && string.Equals(eosId, row.EosId, StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(row.WrapItem) && seen.Add(row.WrapItem)) result.Add(row.WrapItem);
        return result.ToArray();
    }

    // Selection goes first; every other valid distinct assignment is a backpack item.
    internal static string[] Select(IReadOnlyList<string> items, Func<string, bool> valid, System.Random random)
    {
        var eligible = new List<string>();
        foreach (string item in items) if (valid(item)) eligible.Add(item);
        if (eligible.Count == 0) return eligible.ToArray();
        int selected = random.Next(eligible.Count);
        string first = eligible[0]; eligible[0] = eligible[selected]; eligible[selected] = first;
        return eligible.ToArray();
    }
}
