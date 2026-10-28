using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;

// Only native environment and checkpoint catalog are doubled. Actual restore/journal/
// durable commit bodies are inserted unchanged by mpC_npc_timeline_restore.ps1.
static class GameIO { public static string Root; public static string GetSaveGameDir()=>Root; }
static class ThreadManager { public static bool IsMainThread()=>true; }
enum EnumGamePrefs { GameName }
static class GamePrefs { public static string GetString(EnumGamePrefs key)=>"CodexTest"; }
public static partial class RebirthNpcPersistenceTimelineService
{
    internal static readonly string[] SidecarNames={"a.xml","b.xml","c.xml"};
    static string ResolveCheckpointDirectory(string root,ulong time,bool required)=>Path.Combine(root,"checkpoint");
    static bool ValidateCheckpoint(string directory,out string detail)
    {
        detail="explicit fixture catalog";
        return SidecarNames.All(name=>File.Exists(Path.Combine(directory,name))&&File.ReadAllText(Path.Combine(directory,name))=="target:"+name);
    }
    internal static void Restore(string root)=>RestoreCheckpoint(root,root,42);
    internal static void Recover(string root)=>RecoverInterruptedTimelineRestore(root);
    // ACTUAL_RESTORE_METHODS
}
static class Program
{
    static readonly string[] Names={"a.xml","b.xml","c.xml"};
    static void Need(bool value,string label){if(!value)throw new Exception(label);Console.WriteLine("PASS "+label);}
    static string Fresh(string root,string name)
    {
        string dir=Path.Combine(root,name);Directory.CreateDirectory(dir);Directory.CreateDirectory(Path.Combine(dir,"checkpoint"));
        foreach(string member in Names)File.WriteAllText(Path.Combine(dir,"checkpoint",member),"target:"+member);
        File.WriteAllText(Path.Combine(dir,"checkpoint","_complete.txt"),"complete");
        File.WriteAllText(Path.Combine(dir,"a.xml"),"prior:a");File.WriteAllText(Path.Combine(dir,"a.xml.bak"),"prior:backup:a");
        File.WriteAllText(Path.Combine(dir,"b.xml.bak"),"prior:backup:b");File.WriteAllText(Path.Combine(dir,"c.xml"),"prior:c");
        return dir;
    }
    static Dictionary<string,string> Image(string dir)=>Names.SelectMany(n=>new[]{n,n+".bak"}).ToDictionary(n=>n,n=>File.Exists(Path.Combine(dir,n))?Convert.ToBase64String(File.ReadAllBytes(Path.Combine(dir,n))):null);
    static void Same(Dictionary<string,string> image,string dir,string label)
    {var current=Image(dir);Need(image.All(pair=>current[pair.Key]==pair.Value),label);}
    static int Child(string mode,string dir,string stage="",int nth=0,string action="exit")
    {
        var info=new ProcessStartInfo("dotnet"){UseShellExecute=false};
        foreach(string arg in new[]{Assembly.GetExecutingAssembly().Location,mode,dir,stage,nth.ToString(),action})info.ArgumentList.Add(arg);
        using(var process=Process.Start(info)){process.WaitForExit();return process.ExitCode;}
    }
    static void Arm(string stage,int nth,string action)
    {
        var field=typeof(RebirthNpcPersistenceTimelineService).GetField("RestoreScenarioBoundary",BindingFlags.Static|BindingFlags.NonPublic);
        Need(field!=null,"actual DEBUG restore fault boundary exists");int count=0;
        field.SetValue(null,(Action<string,string>)((observed,path)=>
        {
            if(observed!=stage)return;
            if(stage=="copy-before"&&!path.Contains(".rebirth-npc-restore-staging-"))return;
            if(++count!=nth)return;
            if(action=="throw")throw new IOException("actual boundary failure at "+path);
            Environment.Exit(73); // A true process stop bypasses catch/finally rollback.
        }));
    }
    static void Suite(string root)
    {
        Directory.CreateDirectory(root);
        string normal=Fresh(root,"normal");Need(Child("restore",normal)==0,"complete actual full-set restore");
        Need(Names.All(n=>File.ReadAllText(Path.Combine(normal,n))=="target:"+n&&File.ReadAllText(Path.Combine(normal,n+".bak"))=="target:"+n),"all primaries and backups advance together");
        string staged=Fresh(root,"staging-failure");var before=Image(staged);
        Need(Child("restore",staged,"copy-before",2,"throw")!=0,"actual second staging copy fails");Same(before,staged,"staging failure preserves all later live files and absent membership");
        for(int i=1;i<=6;i++)
        {
            string dir=Fresh(root,"interrupted-publication-"+i);var original=Image(dir);
            Need(Child("restore",dir,"publish-after",i)==73,"actual publication interruption "+i);
            Need(Child("recover",dir)==0,"actual startup journal recovery "+i);
            Same(original,dir,"exact complete prior membership recovered "+i);
        }
        string multiple=Fresh(root,"multiple-unfinished");
        Need(Child("restore",multiple,"publish-after",1)==73,"first genuine unfinished journal");
        Need(Child("restore",multiple,"publish-after",1)==73,"second genuine unfinished journal");
        var mixed=Image(multiple);Need(Child("recover",multiple)!=0,"multiple unfinished originals refuse ambiguous recovery order");
        Same(mixed,multiple,"multiple unfinished originals freeze complete live set before any recovery write");
        string corrupt=Fresh(root,"corrupt-prior");Need(Child("restore",corrupt,"publish-after",1)==73,"capture corrupt-prior interrupted generation");
        string rollback=Directory.GetDirectories(corrupt,".rebirth-npc-restore-rollback-*").Single();File.WriteAllText(Path.Combine(rollback,"c.xml"),"corrupt captured prior");
        var frozen=Image(corrupt);Need(Child("recover",corrupt)!=0,"corrupt last prior member refuses startup recovery");Same(frozen,corrupt,"corrupt prior freezes every live member before any write");
        foreach(string boundary in new[]{"terminal-after","retired-after","cleanup-before"})
        {
            string dir=Fresh(root,"terminal-"+boundary);Need(Child("restore",dir,boundary,1)==73,"interrupt completed journal boundary "+boundary);
            File.WriteAllText(Path.Combine(dir,"a.xml"),"later live generation");
            Need(Child("recover",dir)==0,"completed generation cleanup resumes "+boundary);
            Need(File.ReadAllText(Path.Combine(dir,"a.xml"))=="later live generation","terminal cleanup never rolls back later live "+boundary);
        }
        string unknown=Fresh(root,"unknown-members");Need(Child("restore",unknown,"publish-after",1)==73,"unknown candidate setup");
        string staging=Directory.GetDirectories(unknown,".rebirth-npc-restore-staging-*").Single();string candidate=Path.Combine(staging,"unknown.payload");File.WriteAllText(candidate,"retain me");
        string temp=Path.Combine(unknown,"a.xml.restore.unknown.tmp");File.WriteAllText(temp,"retain independent candidate");
        Need(Child("recover",unknown)!=0,"unknown staged member prevents cleanup");Need(File.Exists(candidate)&&File.Exists(temp),"unknown staging and temporary files preserved");
        File.WriteAllText(Path.Combine(unknown,"a.xml"),"later-after-retirement");Child("recover",unknown);
        Need(File.ReadAllText(Path.Combine(unknown,"a.xml"))=="later-after-retirement","retired unknown cleanup never reacquires rollback authority");
        string reparse=Fresh(root,"reparse");string outside=Path.Combine(root,"reparse-target");Directory.CreateDirectory(outside);
        string linked=Path.Combine(reparse,".rebirth-npc-restore-rollback-"+Guid.NewGuid().ToString("N"));
        try{Directory.CreateSymbolicLink(linked,outside);}catch(Exception ex){throw new Exception("Reparse scenario requires symlink-capable fixture account; scenario NOT_RUN, not PASS",ex);}
        var untouched=Image(reparse);Need(Child("recover",reparse)!=0,"actual reparse recovery path refuses");Same(untouched,reparse,"reparse refusal preserves all live files");
        foreach(bool checkpointLink in new[]{true,false})
        {
            string dir=Fresh(root,checkpointLink?"checkpoint-source-reparse":"live-prior-reparse");
            string linkedFile=checkpointLink?Path.Combine(dir,"checkpoint","a.xml"):Path.Combine(dir,"a.xml");
            string target=Path.Combine(root,checkpointLink?"external-checkpoint.xml":"external-prior.xml");
            File.WriteAllText(target,checkpointLink?"target:a.xml":"external-original");File.Delete(linkedFile);
            File.CreateSymbolicLink(linkedFile,target);
            var unchanged=Image(dir);string external=File.ReadAllText(target);
            Need(Child("restore",dir)!=0,"actual source/prior file reparse refuses "+checkpointLink);
            Same(unchanged,dir,"file reparse leaves complete live membership untouched "+checkpointLink);
            Need(File.ReadAllText(target)==external,"file reparse cannot write linked external target "+checkpointLink);
        }
        Console.WriteLine("MP045 actual journal/restore filesystem fixtures complete; native GameIO and checkpoint catalog are disclosed doubles.");
    }
    public static int Main(string[] args)
    {
        try
        {
            if(args[0]=="suite"){Suite(Path.GetFullPath(args[1]));return 0;}
            GameIO.Root=Path.GetFullPath(args[1]);
            if(args.Length>3&&!string.IsNullOrEmpty(args[2]))Arm(args[2],int.Parse(args[3]),args[4]);
            if(args[0]=="restore")RebirthNpcPersistenceTimelineService.Restore(GameIO.Root);else RebirthNpcPersistenceTimelineService.Recover(GameIO.Root);
            return 0;
        }
        catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
    }
}
