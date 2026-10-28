using System;
using System.IO;
using System.Xml.Linq;

class Program
{
    static int checks;
    static void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; }
    static XDocument Doc(string value) => new XDocument(new XElement("record", value));
    static int Main()
    {
        try { Run(); return 0; }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
    static void Run()
    {
        // Isolated throwaway directory, never a game save.
        string root = Path.Combine(Path.GetTempPath(), "RebirthAtomicXmlFixture-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string path = Path.Combine(root, "record.xml");
        try
        {
            Check(RebirthAtomicXmlFile.TryWrite(path, Doc("first"), out var error), error);
            Check(RebirthAtomicXmlFile.TryWrite(path, Doc("second"), out error), error);
            Check(XDocument.Load(path).Root.Value == "second", "new primary");
            Check(XDocument.Load(path + ".bak").Root.Value == "first", "previous primary backup");
            byte[] primary = File.ReadAllBytes(path), backup = File.ReadAllBytes(path + ".bak");
            using (var held = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                Check(!RebirthAtomicXmlFile.TryWrite(path, Doc("blocked"), out error), "locked primary must fail");
            Check(Convert.ToBase64String(primary) == Convert.ToBase64String(File.ReadAllBytes(path)), "failed replace preserves primary");
            Check(Convert.ToBase64String(backup) == Convert.ToBase64String(File.ReadAllBytes(path + ".bak")), "failed replace preserves backup");
            Check(!File.Exists(path + ".tmp"), "failed candidate cleanup");
            Check(RebirthAtomicXmlFile.TryWrite(path, Doc("retry"), out error), error);
            Check(XDocument.Load(path).Root.Value == "retry", "retry publishes");
            Check(!RebirthAtomicXmlFile.TryWrite(path, new XDocument(), out error), "invalid document rejected");
            Check(XDocument.Load(path).Root.Value == "retry", "invalid candidate preserves primary");
            File.WriteAllText(path, "<broken");
            Check(RebirthAtomicXmlFile.TryLoadFinalThenBackup(path, out var recovered, out bool usedBackup, out error), error);
            Check(usedBackup && recovered.Root.Value == "second", "backup read recovery");
            Console.WriteLine($"PASS {checks} publication/recovery checks (Windows/.NET; game Mono still requires validation).");
        }
        finally { Directory.Delete(root, true); }
    }
}
