// NOT_RUN. Deferred standalone fixture: replace PRODUCTION_PUBLICATION with the
// actual RebirthWorkstationCompletionPublication class (without its using list).
// Uses real disposable files/locks and production publisher/reader, not IO mocks.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Reflection;
class World {internal string Guid="11111111-1111-1111-1111-111111111111";}
class GameManager {internal static GameManager Instance=new GameManager();internal World World=new World();}
static class ThreadManager {internal static bool IsMainThread()=>true;}
static class GameIO {internal static string Save;internal static string GetSaveGameDir()=>Save;}
static class RebirthStationCompletionOriginalScope {internal const StringComparison PathComparison=StringComparison.OrdinalIgnoreCase;}
static class RebirthWorkstationCompletionOutbox {internal static string CurrentGuid(World world)=>world?.Guid;}
static class RebirthWorkstationCompletionOriginal
{internal static string Hash(string text){using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-","").ToLowerInvariant();}}

// PRODUCTION_PUBLICATION

static class RebirthWorkstationCompletionPublicationFixture
{
    static void Need(bool value,string message){if(!value)throw new Exception(message);}
    static XElement Original(string operation,int stage)=>new XElement("workstationCompletionIntent",new XAttribute("operation",operation),
        new XAttribute("world",GameManager.Instance.World.Guid),new XAttribute("stage",stage),new XElement("before",new XElement("nativeOwningBefore",new XAttribute("operation",operation))));
    static string PathFor(string operation)=>Path.Combine(GameIO.Save,"RebirthData","Survivor","WorkstationCompletions","server","intents",operation+".xml");
    static void RefusesRead(string path)
    {bool refused=false;try{RebirthWorkstationCompletionPublication.ReadPrimary(path);}catch{refused=true;}Need(refused,"strict primary must refuse uncertain source");}
    public static int Main()
    {
        GameIO.Save=Environment.GetEnvironmentVariable("REBIRTH_MP015_PUBLICATION_SAVE")??Path.Combine(Path.GetTempPath(),"REBIRTH_MP015_Publication_"+Guid.NewGuid().ToString("N"));
        string temporaryRoot=Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
        string actualSave=Path.GetFullPath(GameIO.Save);
        Need(actualSave.StartsWith(temporaryRoot,StringComparison.OrdinalIgnoreCase)&&
            (actualSave.Substring(temporaryRoot.Length).StartsWith("rebirth-mp015-publication-",StringComparison.OrdinalIgnoreCase)||
             actualSave.Substring(temporaryRoot.Length).StartsWith("REBIRTH_MP015_Publication_",StringComparison.OrdinalIgnoreCase)),"disposable publication fixture directory required");
        Directory.CreateDirectory(Path.GetDirectoryName(PathFor(Guid.NewGuid().ToString("N"))));
        string operation=Guid.NewGuid().ToString("N"),path=PathFor(operation);var before=Original(operation,0);var after=Original(operation,1);
        Need(RebirthWorkstationCompletionPublication.Write(path,before),"initial actual publication");
        byte[] primary=File.ReadAllBytes(path),foreign=Encoding.UTF8.GetBytes("foreign original staged bytes");File.WriteAllBytes(path+".tmp",foreign);
        Need(!RebirthWorkstationCompletionPublication.Write(path,after),"foreign staged original must refuse");
        Need(primary.SequenceEqual(File.ReadAllBytes(path))&&foreign.SequenceEqual(File.ReadAllBytes(path+".tmp")),"unknown primary/staging changed");RefusesRead(path);
        // Fresh path with identical valid staged bytes models cold staging:
        // equality does not manufacture same-process CreateNew ownership.
        string coldOperation=Guid.NewGuid().ToString("N"),cold=PathFor(coldOperation);var coldImage=Original(coldOperation,0);
        byte[] exact=(byte[])typeof(RebirthWorkstationCompletionPublication).GetMethod("Bytes",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{coldImage});
        File.WriteAllBytes(cold+".tmp",exact);Need(!RebirthWorkstationCompletionPublication.Write(cold,coldImage),"cold equal staging must refuse");
        Need(!File.Exists(cold)&&exact.SequenceEqual(File.ReadAllBytes(cold+".tmp")),"cold staged evidence changed");
        // Genuine Windows primary sharing refusal leaves this process's exact
        // fsynced candidate. Releasing the real lock permits that exact retry.
        string warmOperation=Guid.NewGuid().ToString("N"),warm=PathFor(warmOperation);var warmBefore=Original(warmOperation,0);var warmAfter=Original(warmOperation,1);
        Need(RebirthWorkstationCompletionPublication.Write(warm,warmBefore),"warm initial publication");
        using(var held=new FileStream(warm,FileMode.Open,FileAccess.Read,FileShare.Read))
            Need(!RebirthWorkstationCompletionPublication.Write(warm,warmAfter)&&File.Exists(warm+".tmp"),"actual locked replacement must retain candidate");
        Need(RebirthWorkstationCompletionPublication.Write(warm,warmAfter)&&!File.Exists(warm+".tmp")&&XNode.DeepEquals(warmAfter,RebirthWorkstationCompletionPublication.ReadPrimary(warm)),"owned exact candidate retry");
        string oversized=PathFor(Guid.NewGuid().ToString("N"));using(var file=new FileStream(oversized,FileMode.CreateNew))file.SetLength(24L*1024*1024+1);RefusesRead(oversized);
        string dtd=PathFor(Guid.NewGuid().ToString("N"));File.WriteAllText(dtd,"<!DOCTYPE x [<!ENTITY e 'expanded'>]><x>&e;</x>");RefusesRead(dtd);
        string reparse=Environment.GetEnvironmentVariable("REBIRTH_MP015_REPARSE_PATH"),outside=Environment.GetEnvironmentVariable("REBIRTH_MP015_OUTSIDE_PATH");
        Need(!string.IsNullOrEmpty(reparse)&&!string.IsNullOrEmpty(outside),"actual junction setup required; use deferred Node runner");
        Need(Path.GetFullPath(outside).StartsWith(temporaryRoot,StringComparison.OrdinalIgnoreCase),"outside-scope sentinel must still be disposable temporary evidence");
        var linked=Original(Path.GetFileNameWithoutExtension(reparse),0);byte[] linkedBytes=(byte[])typeof(RebirthWorkstationCompletionPublication).GetMethod("Bytes",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{linked});
        File.WriteAllBytes(outside,linkedBytes);RefusesRead(reparse);
        Need(!RebirthWorkstationCompletionPublication.Write(reparse,linked)&&linkedBytes.SequenceEqual(File.ReadAllBytes(outside))&&!File.Exists(outside+".tmp"),"reparse read/write must refuse without touching outside primary/staging");
        Console.WriteLine("PASS actual completion publication fixture; evidence retained at "+GameIO.Save);return 0;
    }
}
