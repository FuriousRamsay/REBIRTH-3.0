using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;

// AUTHORED_NOT_RUN. Actual bounded head readers; fixture-local files only.
// No native World, transition seal, generation, domain transfer or reentry proof.
static class OriginalHeadsFixture
{
// ACTUAL_HEAD_READERS
 static void Need(bool value,string message){if(!value)throw new Exception(message);}
 static void Refuse(Action action,string message){try{action();}catch(InvalidDataException){return;}catch(DirectoryNotFoundException){return;}throw new Exception(message);}
 public static void Main()
 {
  string directory=Path.Combine(Path.GetTempPath(),"rebirth-npc-heads-"+Guid.NewGuid().ToString("N"));
  Directory.CreateDirectory(directory);
  try
  {
   Refuse(()=>ReadHeads("relative"),"relative original scope accepted");
   if(Path.DirectorySeparatorChar=='\\')
   {
    Refuse(()=>ReadHeads("C:relative"),"drive-relative original scope accepted");
    Refuse(()=>ReadHeads("C:"),"bare drive original scope accepted");
    Refuse(()=>ReadHeads("\\Windows"),"current-drive-rooted original scope accepted");
    Refuse(()=>ReadHeads("/Windows"),"alternate current-drive-rooted original scope accepted");
   }
   Refuse(()=>ReadHeads(Path.Combine(directory,"missing")),"missing original scope accepted");
   string economy=Path.Combine(directory,"RebirthNpcEconomyOffline.xml"),inventory=Path.Combine(directory,"RebirthNpcInventory.xml");
   File.WriteAllText(economy,"economy-one");File.WriteAllText(inventory,"inventory");
   var first=ReadHeads(directory);Need(first.Length==2&&first[0].StartsWith("RebirthNpcEconomyOffline.xml|",StringComparison.Ordinal),"original heads missing or unsorted");
   File.WriteAllText(Path.Combine(directory,"unrelated.txt"),"unrelated");
   File.WriteAllText(Path.Combine(directory,"RebirthNpcQuarantineBundle.test.xml"),"forensic only");
   Need(first.SequenceEqual(ReadHeads(directory)),"unrelated/forensic records changed original backing membership");
   File.WriteAllText(economy,"economy-two");Need(!first.SequenceEqual(ReadHeads(directory)),"same-length changed original head not detected");
   string candidate=Path.Combine(directory,"RebirthNpcInventory.xml.tmp");File.WriteAllText(candidate,"retained candidate");
   Need(ReadHeads(directory).Length==3,"original side evidence silently excluded");
   string pending=Path.Combine(directory,".rebirth-npc-presave-unknown");Directory.CreateDirectory(pending);
   Refuse(()=>ReadHeads(directory),"unresolved coordinated pre-save evidence accepted");Directory.Delete(pending);
   Need(ReadHeads(directory).Length==3,"refusal mutated existing original heads");
   Console.WriteLine("PASS original head reader source; native transition/reentry and redirected-path native cases NOT certified");
  }
  finally
  {
   if(Path.GetFileName(directory).StartsWith("rebirth-npc-heads-",StringComparison.Ordinal)&&Path.GetDirectoryName(Path.GetFullPath(directory))==Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar))Directory.Delete(directory,true);
   else throw new InvalidDataException("Unsafe fixture directory");
  }
 }
}
