using System;
using System.Linq;
using System.Xml.Linq;

#nullable disable

// One stable completion time per physical storage move / deposit (C2C-065). The executor re-offers its outcome on every
// retry after the saved physical credit; a fresh DateTime.UtcNow per retry would make each retry a different outcome.
// The first mint is saved in the NPC aggregate record (keyed by the credit operation id) and every later call returns
// that saved value, including after restart. Rows are a bounded ring (32 per NPC): a pruned row is only ever needed for
// an operation whose outcome was committed long before 32 later moves, and AllowsOutcome accepts an absent row.
internal static class RebirthNpcB09CompletionStamp
{
    private const string Element = "storageMoveStamp";
    private const int Max = 32;

    // Accessor for P6's SameCompletionWitness / current-primary readback: the saved completion ticks of a credit operation.
    internal static bool TryRead(RebirthNpcStableId npc, Guid operation, out long ticks)
    {
        ticks = 0;
        try
        {
            if (npc.IsEmpty || operation == Guid.Empty || !RebirthNpcAggregatePersistenceStore.TryReadSavedPerson(npc, out var person)) return false;
            var rows = (person.UnknownOptionalElements ?? Array.Empty<string>()).Select(XElement.Parse)
                .Where(x => x.Name == Element && (string)x.Attribute("operation") == operation.ToString("N")).ToArray();
            return rows.Length == 1 && long.TryParse((string)rows[0].Attribute("ticks"), out ticks) && ticks > 0;
        }
        catch { ticks = 0; return false; }
    }

    // Main thread, server only. Returns the saved value, minting and saving it first when absent.
    internal static bool GetOrMint(RebirthNpcStableId npc, Guid operation, long minTicks, out long ticks)
    {
        ticks = 0;
        try
        {
            var world = GameManager.Instance?.World;
            if (npc.IsEmpty || operation == Guid.Empty || world == null || world.IsRemote() || !ThreadManager.IsMainThread()) return false;
            if (TryRead(npc, operation, out ticks)) return true;
            long minted = Math.Max(DateTime.UtcNow.Ticks, minTicks);
            string raw = new XElement(Element, new XAttribute("operation", operation.ToString("N")),
                new XAttribute("ticks", minted.ToString(System.Globalization.CultureInfo.InvariantCulture))).ToString(SaveOptions.DisableFormatting);
            if (!RebirthNpcAggregatePersistenceStore.Mutate(npc, record =>
            {
                var rows = (record.UnknownOptionalElements ?? Array.Empty<string>()).ToList();
                var mine = rows.Where(x => XElement.Parse(x).Name == Element).ToList();
                while (mine.Count >= Max) { rows.Remove(mine[0]); mine.RemoveAt(0); }
                rows.Add(raw);
                record.UnknownOptionalElements = rows.ToArray();
            })) return false;
            RebirthNpcAggregatePersistenceStore.Save();
            return TryRead(npc, operation, out ticks);
        }
        catch { ticks = 0; return false; }
    }
}
