using System;using System.IO;using System.Linq;using System.Collections.Generic;using System.Text;using System.Xml;using System.Xml.Linq;
// Only protocol XML loading, original digest and native version are adapters.
// No native owner effect, saved source payment or full lifecycle is certified.
static class RebirthCookingRemoteAdmission
{
 internal static XElement ReadProtocolXml(string value,int maximum)
 {using(var reader=XmlReader.Create(new StringReader(value),new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=maximum}))return XElement.Load(reader);}
}
static class RebirthRemoteResourceDurableJournal
{internal static string Digest(string value){using(var hash=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-","").ToLowerInvariant();}}
class PlayerDataFile{internal const byte cFileVersion=47;}
// ACTUAL_SOURCE
internal static partial class RebirthConsumptionDurableDelivery
{internal static Dictionary<string,XElement> ReadActual(string path,Guid operation,out string original)=>ReadInventoryHistory(path,operation,out original);internal static byte[] ReadActualNative(Dictionary<string,XElement> history,string phase)=>ReadInventoryNativeStage(history,phase);}
static class Program
{
 static int assertions;static Guid operation=Guid.NewGuid();static string directory;
 static void Require(bool value,string message){assertions++;if(!value)throw new Exception(message);}
 static void Fresh()
 {directory=Path.Combine(Path.GetTempPath(),"rb-player-original-history-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);Write(0,"prepared","original");}
 static string Head(int index)=>Path.Combine(directory,"head-"+index.ToString("D5")+".xml");
 static void Write(int index,string name,string original,XElement detail=null)
 {
  string body=new XElement("playerConsumptionStage",new XAttribute("version",1),new XAttribute("stage",name),new XElement("original",original),detail).ToString(SaveOptions.DisableFormatting);
  File.WriteAllText(Path.Combine(directory,name+".xml"),body,new UTF8Encoding(false));
  File.WriteAllText(Head(index),new XElement("playerConsumptionHead",new XAttribute("version",1),new XAttribute("sequence",index),new XAttribute("operation",operation.ToString("N")),new XAttribute("original",RebirthRemoteResourceDurableJournal.Digest(original)),new XAttribute("stage",name),new XAttribute("body",RebirthRemoteResourceDurableJournal.Digest(body))).ToString(SaveOptions.DisableFormatting),new UTF8Encoding(false));
 }
 static bool Rejected(){try{RebirthConsumptionDurableDelivery.ReadActual(directory,operation,out _);return false;}catch{return true;}}
 static void Main()
 {
  Fresh();var first=RebirthConsumptionDurableDelivery.ReadActual(directory,operation,out string original);Require(original=="original"&&first.Count==1,"Actual prepared original is retained");
  Write(1,"owner-before-intent","original");Require(RebirthConsumptionDurableDelivery.ReadActual(directory,operation,out _).Count==2,"Actual contiguous full-body history is accepted");
  File.AppendAllText(Path.Combine(directory,"owner-before-intent.xml")," ");Require(Rejected(),"Changed full stage bytes reject unchanged digest head");
  Fresh();Write(2,"source-intent","original");Require(Rejected(),"Missing positive control predecessor is rejected");
  Fresh();Write(1,"source-intent","different");Require(Rejected(),"Mixed original lineage is rejected");
  Fresh();var head=XElement.Load(Head(0));head.SetAttributeValue("operation",Guid.NewGuid().ToString("N"));head.Save(Head(0));Require(Rejected(),"Foreign operation control is rejected");
  Fresh();head=XElement.Load(Head(0));head.SetAttributeValue("stage","../prepared");head.Save(Head(0));Require(Rejected(),"Stage path traversal is rejected before body read");
  Fresh();Write(1,"prepared","original");Require(Rejected(),"Duplicate stage controls are rejected");
  Fresh();File.Move(Head(0),Path.Combine(directory,"head-00000-extra.xml"));Require(Rejected(),"Noncanonical control filename is rejected");
  Fresh();head=XElement.Load(Head(0));head.Add(new XElement("nativeReply","paid"));head.Save(Head(0));Require(Rejected(),"Control cannot introduce financial reply evidence");
  Fresh();var native=new byte[12*1024*1024];native[0]=(byte)'t';native[1]=(byte)'t';native[2]=(byte)'p';native[4]=PlayerDataFile.cFileVersion;
  string encoded=Convert.ToBase64String(native);Write(1,"owner-before-saved","original",new XElement("native",encoded));Write(2,"owner-source-saved","original",new XElement("native",encoded));Write(3,"owner-after-saved","original",new XElement("native",encoded));
  var large=RebirthConsumptionDurableDelivery.ReadActual(directory,operation,out _);Require(large.Count==4&&large["owner-before-saved"].Element("native").Value==""&&(int)large["owner-before-saved"].Element("native").Attribute("encodedLength")==encoded.Length,"Supported complete native history exceeding32MiB retains lazy descriptors rather than an arbitrary aggregate refusal");
  Require(RebirthConsumptionDurableDelivery.ReadActualNative(large,"source").SequenceEqual(native),"Lazy native qualifier rereads complete body-bound native bytes");
  Console.WriteLine("Actual player original history: "+assertions+" assertions passed; native lifecycle remains separate evidence.");
 }
}
