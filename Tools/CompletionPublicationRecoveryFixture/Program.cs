using System;
using System.IO;
using System.Xml.Linq;
// Actual publication and recovery classes are linked. Only native world/thread
// identity is doubled. This tests file custody, not gameplay award authority.
class World { internal string Guid=System.Guid.NewGuid().ToString("N"); }
class GameManager { internal static GameManager Instance=new GameManager();internal World World=new World(); }
static class GameIO { internal static string Save;internal static string GetSaveGameDir()=>Save; }
static class ThreadManager { internal static bool IsMainThread()=>true; }
static class RebirthStationCompletionOriginalScope { internal static StringComparison PathComparison=StringComparison.OrdinalIgnoreCase; }
static class RebirthWorkstationCompletionOutbox { internal static string CurrentGuid(World w)=>w?.Guid; }
// Digest binding only; no completion/award constructor or parser is provided.
static class RebirthWorkstationCompletionOriginal {
 internal static string Hash(string text){using(var sha=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(text))).Replace("-", "").ToLowerInvariant();}
}
class Program {
 static int checks;
 static void Check(bool pass,string why){checks++;if(!pass)throw new Exception(why);}
 static XElement Intent(string world,string operation)=>new XElement("workstationCompletionIntent",new XAttribute("world",world),new XAttribute("operation",operation),new XElement("before",new XElement("fixtureSnapshot")));
 static void Main(){try{Run();}catch(Exception e){Console.Error.WriteLine(e);Environment.ExitCode=1;}}
 static void Run(){
  string save=Path.Combine(Path.GetTempPath(),"REBIRTH-PublicationRecovery-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(save);GameIO.Save=save;
  try{
   var world=GameManager.Instance.World;
   string root=Path.Combine(save,"RebirthData","Survivor","WorkstationCompletions","server"),folder=Path.Combine(root,"intents"),identity=root+"|"+world.Guid;
   RebirthWorkstationCompletionEmptyRecovery.ObservedEmpty(world,identity,folder);
   Check(!RebirthWorkstationCompletionEmptyRecovery.ShouldScan(world,identity,folder),"Initial empty recovery memoized");
   string operation=Guid.NewGuid().ToString("N"),path=Path.Combine(folder,operation+".xml");var intent=Intent(world.Guid,operation);
   Check(RebirthWorkstationCompletionPublication.Write(path,intent),"Actual durable publication succeeds");
   Check(RebirthWorkstationCompletionEmptyRecovery.ShouldScan(world,identity,folder),"Actual publication invalidates recovery cache");
   Check(XNode.DeepEquals(RebirthWorkstationCompletionPublication.ReadPrimary(path),intent),"Published original readback unchanged");
   Check(!File.Exists(path+".tmp"),"Successful candidate retired");
   Check(RebirthWorkstationCompletionEmptyRecovery.Diagnostic(world).Contains("publicationInvalidations=1"),"Production invalidation counter incremented");
   File.Delete(path);RebirthWorkstationCompletionEmptyRecovery.ObservedEmpty(world,identity,folder);
   string next=Guid.NewGuid().ToString("N"),nextPath=Path.Combine(folder,next+".xml");File.WriteAllText(nextPath+".tmp","foreign staging");
   Check(!RebirthWorkstationCompletionPublication.Write(nextPath,Intent(world.Guid,next)),"Foreign staged candidate refused");
   Check(File.ReadAllText(nextPath+".tmp")=="foreign staging"&&!File.Exists(nextPath),"Foreign evidence retained without new primary");
   Check(RebirthWorkstationCompletionEmptyRecovery.ShouldScan(world,identity,folder),"Failed publication also invalidates negative result");
   RebirthWorkstationCompletionEmptyRecovery.ObservedEmpty(world,identity,folder);
   Check(!RebirthWorkstationCompletionPublication.Write(Path.Combine(save,"outside.xml"),intent),"Actual scope guard still enforced");
   Check(!RebirthWorkstationCompletionEmptyRecovery.ShouldScan(world,identity,folder),"Refused out-of-scope attempt cannot change admitted scope");
   var wrong=Intent(Guid.NewGuid().ToString("N"),Guid.NewGuid().ToString("N"));
   Check(!RebirthWorkstationCompletionPublication.Write(Path.Combine(folder,"wrong-world.xml"),wrong),"Native world identity guard retained");
   Check(!File.Exists(Path.Combine(save,"outside.xml"))&&!File.Exists(Path.Combine(folder,"wrong-world.xml")),"Refused writes leave no new files");
   if(Path.DirectorySeparatorChar=='\\'){
    string mixedOperation=Guid.NewGuid().ToString("N"),mixed=Path.Combine(folder.ToUpperInvariant(),mixedOperation+".xml");
    Check(RebirthWorkstationCompletionPublication.Write(mixed,Intent(world.Guid,mixedOperation)),"Valid Windows case-alias publication succeeds");
    Check(RebirthWorkstationCompletionEmptyRecovery.ShouldScan(world,identity,folder),"Case-alias write invalidates the same actual recovery folder");
   }
   Check(RebirthWorkstationCompletionEmptyRecovery.ShouldScan(new World{Guid=world.Guid},identity,folder),"New native instance scans reused scope");
   Console.WriteLine($"PASS {checks} linked publication/recovery checks; native world/thread doubled, no gameplay completion asserted");
  }finally{Directory.Delete(save,true);}
 }
}
