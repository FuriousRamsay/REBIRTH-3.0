using System;
using System.IO;
using System.Linq;

static class Program
{
    static int checks;
    static void Check(bool pass, string reason) { checks++; if (!pass) throw new Exception(reason); }
    static void Main()
    {
        var world = new object(); var other = new object();
        string root = Path.Combine(Path.GetTempPath(), "rebirth-empty-recovery-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string intents = Path.Combine(root, "intents"), pending = Path.Combine(root, "pending");
            int diskScans = 0;
            string[] Scan(string folder)
            {
                if (!RebirthWorkstationCompletionEmptyRecovery.ShouldScan(world, "save-guid-A", folder)) return Array.Empty<string>();
                diskScans++;
                var files = Directory.Exists(folder) ? Directory.GetFiles(folder, "*.xml") : Array.Empty<string>();
                if (files.Length == 0) RebirthWorkstationCompletionEmptyRecovery.ObservedEmpty(world, "save-guid-A", folder);
                return files;
            }
            Check(Scan(intents).Length == 0, "Missing folder starts empty");
            Directory.CreateDirectory(pending);
            Check(Scan(pending).Length == 0, "Existing empty folder starts empty");
            for (int i = 0; i < 10000; i++) { Scan(intents); Scan(pending); }
            Check(diskScans == 2, "Idle recovery repeated filesystem IO");
            string intent = Path.Combine(intents, "original.xml");
            RebirthWorkstationCompletionEmptyRecovery.BeforePublication(world, intent);
            Directory.CreateDirectory(intents); File.WriteAllText(intent, "<original/>");
            Check(Scan(intents).Single() == intent, "New owned intent must be discovered");
            Scan(intents); Check(diskScans == 4, "Nonempty/uncertain originals must keep retrying");
            Check(!RebirthWorkstationCompletionEmptyRecovery.ShouldScan(world, "save-guid-A", pending), "Intent invalidation must preserve empty pending result");
            string receipt = Path.Combine(pending, "receipt.xml");
            RebirthWorkstationCompletionEmptyRecovery.BeforePublication(world, receipt);
            Check(RebirthWorkstationCompletionEmptyRecovery.ShouldScan(world, "save-guid-A", pending), "Failed publication must also invalidate");
            File.WriteAllText(receipt, "<receipt/>");
            Check(Scan(pending).Single() == receipt, "New pending original must be discovered");
            File.Delete(intent); Check(Scan(intents).Length == 0, "Archived intent permits empty result again");
            int before = diskScans; Scan(intents); Check(before == diskScans, "Empty-after-archive must avoid repeated IO");
            Check(RebirthWorkstationCompletionEmptyRecovery.ShouldScan(other, "save-guid-A", intents), "New native world must scan reused path");
            Check(RebirthWorkstationCompletionEmptyRecovery.ShouldScan(world, "save-guid-B", intents), "New save GUID must invalidate");
            Check(RebirthWorkstationCompletionEmptyRecovery.ShouldScan(world, "other-save-guid-B", intents), "Changed save scope must invalidate");
            Check(RebirthWorkstationCompletionEmptyRecovery.ShouldScan(null, "save-guid-A", intents), "Missing native identity cannot cache emptiness");
            Check(RebirthWorkstationCompletionEmptyRecovery.ShouldScan(world, "", intents), "Missing save identity cannot cache emptiness");
            Console.WriteLine($"PASS {checks} checks; 20000 idle probes perform zero additional disk scans");
        }
        finally
        {
            // This exact GUID-named fixture directory was created above.
            Directory.Delete(root, true);
        }
    }
}
