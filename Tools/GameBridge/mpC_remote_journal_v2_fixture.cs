using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.Reflection;
using System.Globalization;

// Deferred in-native-context fixture. The runner must supply an ACTUAL
// native-produced phase-zero original after registry/raw-codec initialization.
// Writes only a new disposable temp directory, never the save's real journal.
// This fixture cannot prove source publication or owner payment authority.
internal static class RemoteJournalV2Checks
{
    private static void Require(bool value,string label){if(!value)throw new Exception(label);}
    private static void Refuses(Action action,string label){try{action();}catch(InvalidDataException){return;}throw new Exception(label);}
    internal static string Run(XElement actualNativeOriginal)
    {
        var root=new XElement(actualNativeOriginal);RebirthRemoteResourceDurableJournal.ValidateImage(root);
        Require((int)root.Attribute("phase")==0,"fixture requires original before any effects");
        Guid operation=Guid.ParseExact((string)root.Attribute("operation"),"N");string directory=Path.Combine(Path.GetTempPath(),"REBIRTH_mpC_v2_"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
        var original=new RebirthRemoteResourceDurableJournal.Record(operation,0,root);
        byte[] first=RebirthRemoteResourceJournalFormat.Encode(original,directory,()=>true);
        Require(first.Length<=RebirthRemoteResourceJournalFormat.MaximumStage+128,"stage bound independent of full native plan");
        string firstPath=Path.Combine(directory,operation.ToString("N")+"."+0L.ToString("D20",CultureInfo.InvariantCulture)+".rrj");File.WriteAllBytes(firstPath,first);
        var restored=RebirthRemoteResourceJournalFormat.Read(firstPath,operation,0);
        Require(restored.Binding==original.Binding&&XNode.DeepEquals(restored.Copy(),root),"full native payload and exact original survive decomposition");
        string plan=Path.Combine(directory,operation.ToString("N")+".plan.rrp");byte[] originalPlan=File.ReadAllBytes(plan);
        byte[] second=RebirthRemoteResourceJournalFormat.Encode(new RebirthRemoteResourceDurableJournal.Record(operation,1,root),directory,()=>true);
        Require(File.ReadAllBytes(plan).SequenceEqual(originalPlan)&&Directory.GetFiles(directory,"*.rrp").Length==1,"immutable plan stored once across generations");
        Require(second.Length==first.Length,"same stage metadata does not replicate plan bytes");
        string duplicate=Path.Combine(directory,"duplicate.fixture");File.WriteAllBytes(duplicate,first);
        Require(RebirthRemoteResourceJournalFormat.SameFiles(firstPath,duplicate)&&RebirthRemoteResourceJournalFormat.MatchesFile(duplicate,first),"bounded duplicate comparison accepts only same bytes");
        byte[] changed=(byte[])first.Clone();changed[changed.Length-1]^=1;File.WriteAllBytes(duplicate,changed);
        Require(!RebirthRemoteResourceJournalFormat.SameFiles(firstPath,duplicate),"duplicate comparison refuses changed retained bytes");
        using(var oversized=new FileStream(duplicate,FileMode.Create,FileAccess.Write))oversized.SetLength(RebirthRemoteResourceJournalFormat.MaximumPlan+129L);
        Refuses(()=>RebirthRemoteResourceJournalFormat.SameFiles(firstPath,duplicate),"oversized duplicate refuses before full read");
        // The installed reader retains the old full-image format, not a rewrite
        // or an inferred new snapshot. Invoke ACTUAL legacy writer/reader here.
        var flags=BindingFlags.NonPublic|BindingFlags.Static;
        byte[] legacy=(byte[])typeof(RebirthRemoteResourceDurableJournal).GetMethod("Encode",flags).Invoke(null,new object[]{original});
        string legacyPath=Path.Combine(directory,"legacy.fixture");File.WriteAllBytes(legacyPath,legacy);
        var old=(RebirthRemoteResourceDurableJournal.Record)typeof(RebirthRemoteResourceDurableJournal).GetMethod("ReadFile",flags).Invoke(null,new object[]{legacyPath,operation,0L});
        Require(old.Binding==original.Binding&&XNode.DeepEquals(old.Copy(),root),"exact v1 native original still readable");
        string archive=Path.Combine(directory,"completed",operation.ToString("N"));Directory.CreateDirectory(archive);File.Move(plan,Path.Combine(archive,Path.GetFileName(plan)));
        string secondPath=Path.Combine(directory,operation.ToString("N")+"."+1L.ToString("D20",CultureInfo.InvariantCulture)+".rrj");File.WriteAllBytes(secondPath,second);
        string archivedFirst=Path.Combine(archive,Path.GetFileName(firstPath));File.Move(firstPath,archivedFirst);
        Require(RebirthRemoteResourceDurableJournal.OrderCommitted(new[]{secondPath,archivedFirst}).SequenceEqual(new[]{archivedFirst,secondPath}),"interrupted archive orders by generation rather than directory");
        firstPath=archivedFirst;
        Require(RebirthRemoteResourceJournalFormat.Read(firstPath,operation,0).Binding==original.Binding,"interrupted terminal move resolves same plan in archive");
        string archivedPlan=Path.Combine(archive,Path.GetFileName(plan));byte[] corrupt=File.ReadAllBytes(archivedPlan);corrupt[corrupt.Length-1]^=1;File.WriteAllBytes(archivedPlan,corrupt);
        Refuses(()=>RebirthRemoteResourceJournalFormat.Read(firstPath,operation,0),"bad immutable plan never falls back to another generation");
        return directory;
    }
}
