using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml.Linq;

static class Program
{
    static int checks;
    static void Check(bool value, string name) { if (!value) throw new Exception(name); checks++; }
    static void Main()
    {
        string dir = Path.Combine(Path.GetTempPath(), "RebirthRetirement-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            RebirthWorldCharacterRepository.Seed(dir);
            string file = Path.Combine(dir, "player.xml");
            File.WriteAllText(file, "<record id='EOS_test' key='player' value='1.000' />");
            Check(!XNode.DeepEquals(RebirthWorldCharacterRepository.Expected().Root, XDocument.Load(file).Root), "Baseline raw comparison reproduces mismatch");
            Check(RebirthWorldCharacterRepository.CanResetAfterFlush(), "Clean equivalent old scope retires");
            RebirthWorldCharacterRepository.Record.Value = 2;
            Check(!RebirthWorldCharacterRepository.CanResetAfterFlush(), "Untracked real mutation retained");
            RebirthWorldCharacterRepository.Record.Value = 1;
            RebirthWorldCharacterRepository.Record.Dirty = true;
            Check(!RebirthWorldCharacterRepository.CanResetAfterFlush(), "Dirty state retained");
            RebirthWorldCharacterRepository.Record.Dirty = false;
            RebirthWorldCharacterRepository.SetGate(true, null);
            Check(!RebirthWorldCharacterRepository.CanResetAfterFlush(), "Active write retained");
            RebirthWorldCharacterRepository.SetGate(false, new object());
            Check(!RebirthWorldCharacterRepository.CanResetAfterFlush(), "Pending journal retained");
            RebirthWorldCharacterRepository.SetGate(false, null);
            RebirthWorldCharacterRepository.SetRetry(true);
            Check(!RebirthWorldCharacterRepository.CanResetAfterFlush(), "Retry retained");
            RebirthWorldCharacterRepository.SetRetry(false);
            File.WriteAllText(file, "<record id='EOS_other' key='player' value='1' />");
            Check(!RebirthWorldCharacterRepository.CanResetAfterFlush(), "Other owner retained");
            File.WriteAllText(file, "<record id='EOS_test' key='player' value='2' />");
            Check(!RebirthWorldCharacterRepository.CanResetAfterFlush(), "Changed disk state retained");
            File.WriteAllText(file, "bad xml");
            Check(!RebirthWorldCharacterRepository.CanResetAfterFlush(), "Malformed file retained");
            File.Delete(file);
            Check(!RebirthWorldCharacterRepository.CanResetAfterFlush(), "Missing file retained");
            File.WriteAllText(file, "<record id='EOS_test' key='player' value='1.000' />");
            Check(RebirthWorldCharacterRepository.TryResetAfterFlush(true), "Old scope reset succeeds after verified disk match");
            Check(RebirthWorldCharacterRepository.Count == 0, "Old cache not carried into next world");
            Console.WriteLine(checks + " actual lifecycle guard checks PASS; native scope/model adapters controlled.");
        }
        finally { Directory.Delete(dir, true); }
    }
}
public class World { }
public class GameManager { public static GameManager Instance = new GameManager(); public World World = new World(); }
public class RebirthNativeSaveDirectoryCache { public static string DirectoryPath; public string Get() => DirectoryPath; }
public class RebirthWorldCharacterRecord { public bool Dirty; public bool IsComplete = true; public string StablePlayerKey = "player", StablePlayerId = "EOS_test"; public double Value = 1; }
public static partial class RebirthWorldCharacterRepository
{
    private static readonly object Sync = new object(); private static bool serverAuthority = true;
    private static readonly Dictionary<string,RebirthWorldCharacterRecord> Cache = new();
    private static readonly Dictionary<string,long> RetryAfterUtcTicks = new();
    private static readonly Dictionary<string,object> KeyWriteLocks = new();
    private const StringComparison RepositoryPathComparison = StringComparison.Ordinal;
    private class RepositoryWriteGate { public bool Active; public object PendingJournal; }
    public static RebirthWorldCharacterRecord Record;
    public static int Count => Cache.Count;
    public static void Seed(string path) { scopeWorld = new World(); scopePath = path; scopeSaveDirectory = path; RebirthNativeSaveDirectoryCache.DirectoryPath = path; Cache["player"] = Record = new(); }
    public static void SetGate(bool active, object pending) { KeyWriteLocks["player"] = new RepositoryWriteGate { Active = active, PendingJournal = pending }; }
    public static void SetRetry(bool value) { RetryAfterUtcTicks.Clear(); if (value) RetryAfterUtcTicks["player"] = 1; }
    public static XDocument Expected() => Serialize(Record);
    private static XDocument Serialize(RebirthWorldCharacterRecord record) => new(new XElement("record", new XAttribute("id",record.StablePlayerId),new XAttribute("key",record.StablePlayerKey),new XAttribute("value",record.Value.ToString("R",CultureInfo.InvariantCulture))));
    private static bool TryDeserialize(XDocument doc, out RebirthWorldCharacterRecord record, out string error)
    { error = null; record = null; if(doc.Root?.Name != "record" || !double.TryParse((string)doc.Root.Attribute("value"),NumberStyles.Float,CultureInfo.InvariantCulture,out var value)) return false; record = new() { StablePlayerId=(string)doc.Root.Attribute("id"), StablePlayerKey=(string)doc.Root.Attribute("key"),Value=value };return true; }
    public static void Reset(bool authority) { if(!CanRetireLocked())return;Cache.Clear();scopeWorld=null;scopePath=null;scopeSaveDirectory=null;serverAuthority=authority; }
    private static void SaveAllDirty(string reason) { }
    private static string GetPath(string key) => Path.Combine(scopePath,key+".xml");
}
