// AUTHORED_NOT_RUN. Actual historical identity reader; filesystem/publication
// and actual saved/native-loaded cache authority are explicit adapters.
using System;using System.IO;using System.Linq;using System.Xml.Linq;
static class File{internal static bool Exists(string path)=>path.EndsWith(".tmp")?Temporary:Present;internal static bool Temporary,Present;}
static class RebirthWorkstationCompletionPublication{
 internal static XElement Record;internal static bool Match=true,Throw;
 internal const int MaximumBytes=1048576;internal static long Length=100;internal static long PublishedLength(string path)=>Length;
 internal static XElement ReadPrimary(string path){if(Throw)throw new IOException("explicit unavailable artifact adapter");return new XElement(Record);}
 internal static byte[] CopyPublishedBytes(string path)=>new byte[]{8};
 internal static bool MatchesPublishedBytes(string path,byte[] bytes)=>Match;
}
internal static partial class RebirthWorkstationCompletionPipeline{
 class CachedCustody{internal bool Active=true,Native=true;internal string Path="original",WorldGuid="world";internal int Epoch=1;internal Guid Operation=Guid.NewGuid();internal byte[] Image={1};internal XElement Original=new XElement("workstationCompletionIntent",new XElement("before",new XElement("nativeOwningBefore")));internal bool SourceCurrent()=>Active;internal bool SavedCurrent()=>true;internal bool NativeCurrent()=>Native;}
 static string HistoricalHash(byte[] image)=>Convert.ToBase64String(image);
 static CachedCustody cache;static Guid ticket,action;static int checks;
 static void Fresh(){File.Temporary=false;File.Present=true;RebirthWorkstationCompletionPublication.Match=true;RebirthWorkstationCompletionPublication.Throw=false;RebirthWorkstationCompletionPublication.Length=100;cache=new CachedCustody();ticket=Guid.NewGuid();action=Guid.NewGuid();
  RebirthWorkstationCompletionPublication.Record=new XElement("workstationCompletionIntent",new XAttribute("operation",cache.Operation.ToString("N")),new XAttribute("world",cache.WorldGuid),new XElement(cache.Original.Element("before")),
   new XElement("cachedPhysicalAttempt",new XElement("cachedPhysicalPrepare",new XAttribute("ticket",ticket.ToString("N")),new XAttribute("action",action.ToString("N")),new XAttribute("group",Guid.NewGuid().ToString("N")),new XAttribute("session",1),new XAttribute("original",HistoricalHash(cache.Image))),new XElement(cache.Original)));}
 static void Need(bool value){if(!value)throw new Exception("Retained cached action "+checks);checks++;}
 public static void Main(){Fresh();Need(TryDescribeRetainedCachedAction(cache,out var foundTicket,out var foundAction)&&foundTicket==ticket&&foundAction==action);
  Fresh();File.Present=false;Need(TryDescribeRetainedCachedAction(cache,out foundTicket,out foundAction)&&foundTicket==Guid.Empty&&foundAction==Guid.Empty);
  Fresh();File.Temporary=true;Need(!TryDescribeRetainedCachedAction(cache,out _,out _));
  Fresh();RebirthWorkstationCompletionPublication.Record.SetAttributeValue("operation",Guid.NewGuid().ToString("N"));Need(!TryDescribeRetainedCachedAction(cache,out _,out _));
  Fresh();RebirthWorkstationCompletionPublication.Record.Element("cachedPhysicalAttempt").Element("cachedPhysicalPrepare").SetAttributeValue("ticket",Guid.Empty.ToString("N"));Need(!TryDescribeRetainedCachedAction(cache,out _,out _));
  Fresh();cache.Native=false;Need(!TryDescribeRetainedCachedAction(cache,out _,out _));
  Fresh();RebirthWorkstationCompletionPublication.Match=false;Need(!TryDescribeRetainedCachedAction(cache,out _,out _));
  Fresh();RebirthWorkstationCompletionPublication.Record.Element("cachedPhysicalAttempt").Element("cachedPhysicalPrepare").SetAttributeValue("group",Guid.Empty.ToString("N"));Need(!TryDescribeRetainedCachedAction(cache,out _,out _));
  Fresh();RebirthWorkstationCompletionPublication.Record.Element("cachedPhysicalAttempt").Element("cachedPhysicalPrepare").SetAttributeValue("session",0);Need(!TryDescribeRetainedCachedAction(cache,out _,out _));
  Fresh();RebirthWorkstationCompletionPublication.Length=RebirthWorkstationCompletionPublication.MaximumBytes+1;Need(!TryDescribeRetainedCachedAction(cache,out _,out _));
  Fresh();RebirthWorkstationCompletionPublication.Throw=true;Need(!TryDescribeRetainedCachedAction(cache,out foundTicket,out foundAction)&&foundTicket==Guid.Empty&&foundAction==Guid.Empty);
  Fresh();RebirthWorkstationCompletionPublication.Record.Element("cachedPhysicalAttempt").Element("cachedPhysicalPrepare").SetAttributeValue("challenge",new string('a',64));Need(TryDescribeRetainedCachedAction(cache,out foundTicket,out foundAction)&&foundTicket==ticket&&foundAction==action);
  Fresh();RebirthWorkstationCompletionPublication.Record.Element("cachedPhysicalAttempt").Element("cachedPhysicalPrepare").SetAttributeValue("foreign","not-a-transport-field");Need(!TryDescribeRetainedCachedAction(cache,out _,out _));
  Console.WriteLine("PASS "+checks+" actual retained cached identity assertions; native/file adapters excluded");}
}
// ACTUAL_RETAINED_ACTION
