// AUTHORED_NOT_RUN. Actual metadata consumer, with I/O and network doubles.
using System;using System.IO;using System.Linq;using System.Text;using System.Xml.Linq;using System.Security.Cryptography;
class World{}
class Peer{internal string Owner="owner",Account="account";internal string Reply;internal void SendPackage(NetPackageRebirthRecyclerPlacedReplyV1 packet){Reply=packet.Text;}}
class NetPackageRebirthRecyclerPlacedRequestV1{internal Peer Sender=new Peer();}
class NetPackageRebirthRecyclerPlacedReplyV1{internal string Text;internal NetPackageRebirthRecyclerPlacedReplyV1 Setup(string text){Text=text;return this;}}
static class NetPackageManager{internal static T GetPackage<T>()where T:new()=>new T();}
class Identity{internal string StorageKey,CanonicalId;}
static class RebirthStablePlayerIdentity{internal static bool TryFromClientInfo(Peer peer,out Identity identity){identity=new Identity{StorageKey=peer.Owner,CanonicalId=peer.Account};return true;}}
static class RebirthWorkstationCompletionOutbox{internal static string Directory;internal static string Root(string purpose)=>Directory;internal static string CurrentGuid(World world)=>"world";}
static class RebirthWorkstationCompletionPublication
{
 internal static long PublishedLength(string path)=>File.Exists(path)?new FileInfo(path).Length:-1;
 internal static XElement ReadPrimary(string path)=>File.Exists(path)?XElement.Load(path):null;
 internal static bool Write(string path,XElement value){File.WriteAllText(path,value.ToString(SaveOptions.DisableFormatting));return true;}
}
static class RebirthStationPublicationRecord{internal static string Digest(byte[] value){using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(value)).Replace("-","");}}
static class RebirthProductiveEarnedObservationBudget
{class Lease:IDisposable{public void Dispose(){}}internal static bool Available=true;internal static bool TryRetainDetached(long value,out IDisposable memory){memory=Available?new Lease():null;return Available;}}
static class Subject
{
 static bool ArtifactNodeExists(string path)=>File.Exists(path);
 static string RecyclerFileDigest(string path)=>File.Exists(path)?RebirthStationPublicationRecord.Digest(File.ReadAllBytes(path)):null;
 // PRODUCTION_RETIRED
 static void Check(bool value,string text){if(!value)throw new Exception(text);Console.WriteLine("PASS "+text);}
 static Guid operation=Guid.NewGuid(),nonce=Guid.NewGuid();static XElement original;static string completedPath;
 static XElement Request(string command)=>new XElement("recyclerRemoteCommand",new XAttribute("command",command),new XAttribute("nonce",nonce.ToString("N")),
  new XAttribute("original",RebirthStationPublicationRecord.Digest(Encoding.UTF8.GetBytes(original.ToString(SaveOptions.DisableFormatting)))));
 static void WriteCompletion(bool loaded=false)
 {
  var receipt=new XElement("recyclerRemoteCompleted",new XAttribute("completed",1),new XAttribute("terminal",RecyclerFileDigest(Path.Combine(RebirthWorkstationCompletionOutbox.Directory,operation.ToString("N")+".terminal.xml"))),
   new XAttribute("terminalLength",4),new XElement("recyclerCleanupSaved"));
  if(loaded)receipt.Add(new XAttribute("actualLoadedCompletion",true));else receipt.Add(new XAttribute("nonce",nonce.ToString("N")));
  RebirthWorkstationCompletionPublication.Write(completedPath,new XElement("workstationCompletionIntent",new XAttribute("operation",operation.ToString("N")),new XElement("before",new XElement(original)),receipt));
 }
 public static void Main()
 {
  string root=Path.Combine(Path.GetTempPath(),"recycler-retired-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);RebirthWorkstationCompletionOutbox.Directory=root;
  try
  {
   completedPath=Path.Combine(root,operation.ToString("N")+".remote-completed.xml");
   original=new XElement("placedRecyclerOriginal",new XAttribute("operation",operation.ToString("N")),new XAttribute("owner","owner"),new XAttribute("account","account"),
    new XAttribute("world","world"),new XAttribute("creation","birth"),new XAttribute("deaths",2),new XAttribute("bagLocks","0"),new XAttribute("beltLocks","0"));
   File.WriteAllBytes(Path.Combine(root,operation.ToString("N")+".terminal.xml"),new byte[]{1,2,3,4});WriteCompletion();var packet=new NetPackageRebirthRecyclerPlacedRequestV1();var world=new World();
   Check(ReplyPlacedRetired(packet,world,Request("query"),operation)&&(int)XElement.Parse(packet.Sender.Reply).Attribute("stage")==5,"saved full completion query is metadata stage5 without received ACK");
   Check(!File.Exists(Path.Combine(root,operation.ToString("N")+".remote-received.xml")),"query alone cannot manufacture client received publication");
   var request=Request("query");request.SetAttributeValue("nonce",Guid.NewGuid().ToString("N"));Check(!ReplyPlacedRetired(packet,world,request,operation),"wrong retained session cannot use live-generation completion");
   packet.Sender.Account="foreign";Check(!ReplyPlacedRetired(packet,world,Request("ack"),operation),"foreign authenticated account cannot retire original");packet.Sender.Account="account";
   request=Request("owner");Check(!ReplyPlacedRetired(packet,world,request,operation),"completed metadata cannot admit owner financial command");
   RebirthProductiveEarnedObservationBudget.Available=false;Check(!ReplyPlacedRetired(packet,world,Request("ack"),operation),"budget refusal creates no receipt");RebirthProductiveEarnedObservationBudget.Available=true;
   Check(ReplyPlacedRetired(packet,world,Request("ack"),operation)&&(int)XElement.Parse(packet.Sender.Reply).Attribute("stage")==6,"authenticated ACK fsyncs received before stage6 reply");
   Check(ReplyPlacedRetired(packet,world,Request("query"),operation)&&(int)XElement.Parse(packet.Sender.Reply).Attribute("stage")==6,"dropped retired reply converges from durable received file");
   File.Delete(Path.Combine(root,operation.ToString("N")+".remote-received.xml"));WriteCompletion(true);
   Check(ReplyPlacedRetired(packet,world,Request("ack"),operation)&&(int)XElement.Parse(packet.Sender.Reply).Attribute("stage")==6,"actual loaded full completion metadata uses authenticated historical terminal nonce without financial source");
   File.Delete(Path.Combine(root,operation.ToString("N")+".remote-received.xml"));WriteCompletion();File.AppendAllText(Path.Combine(root,operation.ToString("N")+".terminal.xml"),"changed");
   Check(!ReplyPlacedRetired(packet,world,Request("ack"),operation),"changed full terminal prevents metadata release");
  }
  finally{Directory.Delete(root,true);}
 }
}
