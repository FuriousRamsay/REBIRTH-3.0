// AUTHORED_NOT_RUN. Exercises the actual streaming hold classifier.
using System;using System.IO;using System.Linq;using System.Xml.Linq;
static class RebirthWorkstationCompletionOutbox{internal static string Directory;internal static string Root(string purpose)=>Directory;}
static class RebirthWorkstationCompletionPublication{internal static long PublishedLength(string path)=>File.Exists(path)?new FileInfo(path).Length:-1;}
static class Subject
{
 internal class RecyclerJoining{internal string Owner="owner",Account="account",Creation="creation",WorldKey="world";internal int Deaths=2;}
 // PRODUCTION_DISCOVERY
 static void Check(bool value,string text){if(!value)throw new Exception(text);Console.WriteLine("PASS "+text);}
 static XElement Original(Guid op)=>new XElement("placedRecyclerOriginal",new XAttribute("operation",op.ToString("N")),new XAttribute("world","world"),
  new XAttribute("owner","owner"),new XAttribute("account","account"),new XAttribute("creation","creation"),new XAttribute("deaths",2));
 static void Write(Guid op,XElement original)=>File.WriteAllText(Path.Combine(RebirthWorkstationCompletionOutbox.Directory,op.ToString("N")+".remote-client.xml"),
  new XElement("workstationCompletionIntent",new XAttribute("operation",op.ToString("N")),new XAttribute("world","world"),new XElement("before",original)).ToString());
 public static void Main()
 {
  string root=Path.Combine(Path.GetTempPath(),"recycler-join-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);RebirthWorkstationCompletionOutbox.Directory=root;
  try
  {
   Guid op=Guid.NewGuid();var source=new RecyclerJoining();Write(op,Original(op));Check(HasJoinedRecyclerOriginal(source,op),"exact original owner/life/world selects unresolved operation");
   foreach(string attribute in new[]{"owner","account","creation","world"}){var row=Original(op);row.SetAttributeValue(attribute,"other");Write(op,row);
    Check(!HasJoinedRecyclerOriginal(source,op),"different "+attribute+" cannot create joined financial hold");}
   var changed=Original(op);changed.SetAttributeValue("deaths",3);Write(op,changed);Check(!HasJoinedRecyclerOriginal(source,op),"old-life receipt cannot hold actual newly joined life");
   changed=Original(op);changed.SetAttributeValue("deaths","unknown");Write(op,changed);Check(!HasJoinedRecyclerOriginal(source,op),"unknown death stamp cannot match original life");
   Write(op,Original(Guid.NewGuid()));Check(!HasJoinedRecyclerOriginal(source,op),"foreign immutable operation cannot borrow filename");
   File.Delete(Path.Combine(root,op.ToString("N")+".remote-client.xml"));Check(!HasJoinedRecyclerOriginal(source,op),"missing primary grants no financial selector");
   Write(op,Original(op));source.Deaths=3;Check(!HasJoinedRecyclerOriginal(source,op),"same account after death does not reuse original");
  }
  finally{Directory.Delete(root,true);}
 }
}
