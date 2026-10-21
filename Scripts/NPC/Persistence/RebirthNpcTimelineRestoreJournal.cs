using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

public static partial class RebirthNpcPersistenceTimelineService
{
    private const string RestorePlanName="_restore-ready.xml",RestoreDoneName="_restore-done.xml";
#if REBIRTH_DEBUG
    internal static Action<string,string> RestoreScenarioBoundary;
#endif
    private static void ObserveRestoreScenario(string stage,string path)
    {
#if REBIRTH_DEBUG
        if(ThreadManager.IsMainThread()&&GamePrefs.GetString(EnumGamePrefs.GameName)=="CodexTest")RestoreScenarioBoundary?.Invoke(stage,path);
#endif
    }
    private static void RequireRestorePath(string path)
    {
        for(var item=new DirectoryInfo(Path.GetFullPath(path));item!=null;item=item.Parent)
            if(item.Exists&&(item.Attributes&FileAttributes.ReparsePoint)!=0)throw new IOException("NPC restore path is a reparse point");
    }
    private static void CopyRestoreDurable(string source,string destination)
    {
        ObserveRestoreScenario("copy-before",destination);
        RequireRestorePath(Path.GetDirectoryName(source));RequireRestorePath(Path.GetDirectoryName(destination));
        if((File.GetAttributes(source)&FileAttributes.ReparsePoint)!=0)throw new IOException("NPC restore source is a reparse point");
        using(var input=new FileStream(source,FileMode.Open,FileAccess.Read,FileShare.Read))
        using(var output=new FileStream(destination,FileMode.CreateNew,FileAccess.Write,FileShare.None))
        {input.CopyTo(output);output.Flush(true);}
        if(HashFile(source)!=HashFile(destination))throw new InvalidDataException("NPC restore copy mismatch");
    }
    private static void WriteRestoreRecord(string path,XElement record)
    {
        RequireRestorePath(Path.GetDirectoryName(path));byte[] bytes=new UTF8Encoding(false,true).GetBytes(record.ToString(SaveOptions.DisableFormatting));
        if(bytes.Length>65536)throw new InvalidDataException("NPC restore record too large");
        using(var stream=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None)){stream.Write(bytes,0,bytes.Length);stream.Flush(true);}
        if(!XNode.DeepEquals(ReadRestoreRecord(path),record))throw new InvalidDataException("NPC restore record readback failed");
    }
    private static XElement ReadRestoreRecord(string path)
    {
        RequireRestorePath(Path.GetDirectoryName(path));var info=new FileInfo(path);
        if(!info.Exists||info.Length<1||info.Length>65536||(info.Attributes&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("NPC restore record unavailable");
        using(var input=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))
        using(var reader=XmlReader.Create(input,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=65536}))return XElement.Load(reader);
    }
    private static void PrepareRestoreJournal(string save,string staging,string rollback,HashSet<string> primary,HashSet<string> backup)
    {
        RequireRestorePath(save);var plan=new XElement("npcTimelineRestore",new XAttribute("version",1),new XAttribute("save",Path.GetFullPath(save)),
            new XAttribute("operation",Path.GetFileName(rollback).Substring(".rebirth-npc-restore-rollback-".Length)));
        foreach(string name in SidecarNames)
        {
            plan.Add(new XElement("target",new XAttribute("name",name),new XAttribute("hash",HashFile(Path.Combine(staging,name)))));
            foreach(bool isBackup in new[]{false,true})
            {
                string file=name+(isBackup?".bak":"");bool exists=(isBackup?backup:primary).Contains(name);
                plan.Add(new XElement("prior",new XAttribute("name",file),new XAttribute("exists",exists),
                    new XAttribute("hash",exists?HashFile(Path.Combine(rollback,file)):string.Empty)));
            }
        }
        WriteRestoreRecord(Path.Combine(rollback,RestorePlanName),plan);
    }
    private static void CompleteRestoreJournal(string rollback,string outcome)
    {
        if(outcome!="published"&&outcome!="rolledBack")throw new InvalidDataException("Unknown restore outcome");
        string plan=Path.Combine(rollback,RestorePlanName),path=Path.Combine(rollback,RestoreDoneName);
        var done=new XElement("npcTimelineRestoreDone",new XAttribute("version",1),new XAttribute("plan",HashFile(plan)),new XAttribute("outcome",outcome));
        if(File.Exists(path)){if(!XNode.DeepEquals(ReadRestoreRecord(path),done))throw new InvalidDataException("NPC restore completion conflict");return;}
        WriteRestoreRecord(path,done);
        ObserveRestoreScenario("terminal-after",rollback);
    }
    private static bool HasPublishedRestoreTerminal(string rollback)
    {
        try
        {
            var done=ReadRestoreRecord(Path.Combine(rollback,RestoreDoneName));
            return done.Name=="npcTimelineRestoreDone"&&done.Attributes().Count()==3&&!done.HasElements&&
                (int?)done.Attribute("version")==1&&(string)done.Attribute("outcome")=="published"&&
                (string)done.Attribute("plan")==HashFile(Path.Combine(rollback,RestorePlanName));
        }
        catch{return false;}
    }
    // Called before any domain can load its sidecar generation. A complete
    // prior-set journal is the sole authority to undo an interrupted restore.
    private static void RecoverInterruptedTimelineRestore(string save)
    {
        RequireRestorePath(save);string root=Path.GetFullPath(save);
        foreach(string retired in Directory.EnumerateDirectories(root,".rebirth-npc-restore-retired-*").Take(33))CleanupRestoreDirectory(retired,true);
        var directories=Directory.EnumerateDirectories(root,".rebirth-npc-restore-rollback-*").Take(33).ToArray();
        if(directories.Length>32)throw new InvalidDataException("Too many pending NPC timeline restores");
        if(directories.Count(directory=>!File.Exists(Path.Combine(directory,RestoreDoneName)))>1)
            throw new InvalidDataException("Multiple unfinished NPC timeline restores retained for reconciliation");
        foreach(string rollback in directories)
        {
            RequireRestorePath(rollback);string suffix=Path.GetFileName(rollback).Substring(".rebirth-npc-restore-rollback-".Length);
            if(!Guid.TryParseExact(suffix,"N",out _))throw new InvalidDataException("Unknown NPC restore directory");
            string staging=Path.Combine(root,".rebirth-npc-restore-staging-"+suffix);
            string planPath=Path.Combine(rollback,RestorePlanName);
            // Pre-journal/legacy candidates are preserved, never classified as
            // a fresh empty store or guessed to be an unused publication.
            if(!File.Exists(planPath))throw new InvalidDataException("Incomplete NPC timeline restore capture retained");
            var plan=ReadRestoreRecord(planPath);
            if(plan.Name!="npcTimelineRestore"||plan.Attributes().Count()!=3||(int?)plan.Attribute("version")!=1||
                (string)plan.Attribute("save")!=root||(string)plan.Attribute("operation")!=suffix||
                plan.Elements().Any(node=>node.Name!="target"&&node.Name!="prior"))throw new InvalidDataException("NPC restore identity mismatch");
            var targets=plan.Elements("target").ToArray();var prior=plan.Elements("prior").ToArray();
            var expected=SidecarNames.SelectMany(name=>new[]{name,name+".bak"}).ToArray();
            if(targets.Length!=SidecarNames.Length||targets.Select(node=>(string)node.Attribute("name")).Distinct(StringComparer.Ordinal).Count()!=targets.Length||
                targets.Any(node=>node.Attributes().Count()!=2||node.HasElements||!SidecarNames.Contains((string)node.Attribute("name"))||((string)node.Attribute("hash"))?.Length!=64)||
                prior.Length!=expected.Length||prior.Select(node=>(string)node.Attribute("name")).Distinct(StringComparer.Ordinal).Count()!=prior.Length||
                prior.Any(node=>!expected.Contains((string)node.Attribute("name"))||node.Attributes().Count()!=3||node.HasElements))throw new InvalidDataException("NPC restore membership mismatch");
            // Verify EVERY captured member before replacing ANY live member.
            foreach(var node in prior)
            {
                bool exists=(bool)node.Attribute("exists");string hash=(string)node.Attribute("hash"),file=Path.Combine(rollback,(string)node.Attribute("name"));
                if(exists?hash?.Length!=64||!File.Exists(file)||HashFile(file)!=hash:hash!=string.Empty||File.Exists(file))throw new InvalidDataException("NPC restore prior member mismatch");
            }
            string donePath=Path.Combine(rollback,RestoreDoneName);
            if(File.Exists(donePath))
            {
                var done=ReadRestoreRecord(donePath);string outcome=(string)done.Attribute("outcome");
                if(done.Name!="npcTimelineRestoreDone"||done.Attributes().Count()!=3||done.HasElements||(int?)done.Attribute("version")!=1||
                    (string)done.Attribute("plan")!=HashFile(planPath)||(outcome!="published"&&outcome!="rolledBack"))throw new InvalidDataException("NPC restore completion mismatch");
            }
            else
            {
                var errors=new List<string>();
                foreach(var node in prior)
                {
                    string name=(string)node.Attribute("name");TryRestorePrior(Path.Combine(root,name),Path.Combine(rollback,name),(bool)node.Attribute("exists"),errors);
                }
                if(errors.Count!=0)throw new IOException("NPC timeline restore recovery incomplete: "+string.Join(" | ",errors));
                CompleteRestoreJournal(rollback,"rolledBack");
            }
            // Cleanup follows a complete durable terminal marker. Keep unknown
            // extra files rather than recursively deleting unrecognized data.
            RetireRestoreJournal(staging,rollback);
        }
    }
    private static void RetireRestoreJournal(string staging,string rollback)
    {
        RequireRestorePath(rollback);
        string save=Path.GetFullPath(GameIO.GetSaveGameDir()).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar);
        if(Path.GetDirectoryName(Path.GetFullPath(rollback))!=save)throw new IOException("NPC restore retirement outside save");
        string suffix=Path.GetFileName(rollback).Substring(".rebirth-npc-restore-rollback-".Length);
        string retired=Path.Combine(Path.GetDirectoryName(rollback),".rebirth-npc-restore-retired-"+suffix);
        // A completed journal leaves the recovery namespace atomically before
        // cleanup. Interrupted cleanup can never masquerade as partial capture.
        Directory.Move(rollback,retired);
        ObserveRestoreScenario("retired-after",retired);
        CleanupRestoreDirectory(staging,false);CleanupRestoreDirectory(retired,true);
    }
    private static void CleanupRestoreDirectory(string directory,bool rollback)
    {
        if(!Directory.Exists(directory))return;RequireRestorePath(directory);
        string save=Path.GetFullPath(GameIO.GetSaveGameDir()).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar);
        if(Path.GetDirectoryName(Path.GetFullPath(directory))!=save)throw new IOException("NPC restore cleanup outside save");
        string name=Path.GetFileName(directory);
        string prefix=rollback?(name.StartsWith(".rebirth-npc-restore-retired-",StringComparison.Ordinal)?".rebirth-npc-restore-retired-":".rebirth-npc-restore-rollback-"):".rebirth-npc-restore-staging-";
        if(!name.StartsWith(prefix,StringComparison.Ordinal)||!Guid.TryParseExact(name.Substring(prefix.Length),"N",out _))throw new IOException("Unknown restore cleanup target");
        var allowed=new HashSet<string>(SidecarNames,StringComparer.Ordinal);
        if(rollback){foreach(string member in SidecarNames)allowed.Add(member+".bak");allowed.Add(RestorePlanName);allowed.Add(RestoreDoneName);}
        var files=Directory.EnumerateFileSystemEntries(directory).ToArray();
        if(files.Any(file=>!File.Exists(file)||!allowed.Contains(Path.GetFileName(file))||(File.GetAttributes(file)&FileAttributes.ReparsePoint)!=0))throw new IOException("Unknown restore cleanup member retained");
        // Keep the terminal journal until every payload file has been removed.
        foreach(string file in files.Where(file=>Path.GetFileName(file)!=RestorePlanName&&Path.GetFileName(file)!=RestoreDoneName))
        {ObserveRestoreScenario("cleanup-before",file);File.Delete(file);}
        if(rollback){File.Delete(Path.Combine(directory,RestorePlanName));File.Delete(Path.Combine(directory,RestoreDoneName));}
        Directory.Delete(directory,false);
    }
}
