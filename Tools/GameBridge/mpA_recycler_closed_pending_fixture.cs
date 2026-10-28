// AUTHORED_NOT_RUN. Exact native-marker/full-original link consumer.
using System;using System.IO;using System.Linq;using System.Text;using System.Xml.Linq;using System.Security.Cryptography;
static class RebirthWorkstationCompletionOutbox{internal static string Root(string value)=>"root";}
static class RebirthWorkstationCompletionPublication{internal static XElement Stored;internal static long PublishedLength(string path)=>Stored==null?-1:1000;internal static XElement ReadPrimary(string path)=>Stored;}
static class RebirthWorkstationCompletionOriginal{internal static string Hash(string value)=>RebirthStationPublicationRecord.Digest(Encoding.UTF8.GetBytes(value));}
static class RebirthStationPublicationRecord{internal static string Digest(byte[] value){using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(value)).Replace("-","");}}
static class RebirthProductiveEarnedObservationBudget{internal static bool Available=true;class Lease:IDisposable{public void Dispose(){}}internal static bool TryRetainDetached(long amount,out IDisposable lease){lease=Available?new Lease():null;return lease!=null;}}
class Subject
{
 // PRODUCTION_MATCH
 static void Check(bool value,string label){if(!value)throw new Exception(label);}
 static void Main()
 {
  Guid operation=Guid.NewGuid(),ticket=Guid.NewGuid(),group=Guid.NewGuid();
  var native=new XElement("recyclerClosedAuthorityOriginal",new XAttribute("version",1),new XAttribute("operation",operation.ToString("N")),new XAttribute("group",group.ToString("N")),
   new XAttribute("owner","owner"),new XAttribute("account","account"),new XAttribute("creation","life"),new XAttribute("deaths",2),new XAttribute("world","world"),
   new XAttribute("x",1),new XAttribute("y",2),new XAttribute("z",3),new XAttribute("block","recycler"),new XAttribute("beforeOffset",20),new XAttribute("afterOffset",20),new XAttribute("workSeconds",1),
   new XElement("beforeNative","before"),new XElement("afterNative","after"),new XElement("returns","rich-items"));
  var header=new XElement("recyclerClosedAuthorityPending",new XAttribute("ticket",ticket.ToString("N")),new XAttribute("original",RebirthWorkstationCompletionOriginal.Hash(native.ToString(SaveOptions.DisableFormatting))));
  Check(MatchesClosedPendingMarker(header,native),"exact native-only source hash");
  var changed=new XElement(native);changed.SetAttributeValue("owner","other");Check(!MatchesClosedPendingMarker(header,changed),"changed native source rejected");
  var full=new XElement(native);full.Name="placedRecyclerOriginal";full.Attribute("group").Remove();full.Add(new XElement("closedAuthority",new XAttribute("ticket",ticket.ToString("N")),
   new XAttribute("group",group.ToString("N")),new XAttribute("nativeOriginal",RebirthStationPublicationRecord.Digest(Encoding.UTF8.GetBytes(native.ToString(SaveOptions.DisableFormatting))))));
  RebirthWorkstationCompletionPublication.Stored=new XElement("workstationCompletionIntent",new XAttribute("operation",operation.ToString("N")),new XAttribute("world","world"),new XElement("recyclerClosedAuthoritySource",new XAttribute("ticket",ticket.ToString("N")),new XAttribute("actualRuntimeRow",true),new XElement(native)));
  Check(MatchesClosedPendingMarker(header,full),"full owner original links exact actual native original");
  foreach(string field in new[]{"operation","owner","account","creation","deaths","world","x","y","z","block","beforeOffset","afterOffset","workSeconds"})
  {changed=new XElement(full);changed.SetAttributeValue(field,"different");Check(!MatchesClosedPendingMarker(header,changed),"changed field rejected "+field);}
  foreach(string field in new[]{"beforeNative","afterNative","returns"}){changed=new XElement(full);changed.Element(field).Value="different";Check(!MatchesClosedPendingMarker(header,changed),"changed full bytes rejected "+field);}
  changed=new XElement(full);changed.Element("closedAuthority").SetAttributeValue("ticket",Guid.NewGuid().ToString("N"));Check(!MatchesClosedPendingMarker(header,changed),"different ticket rejected");
  RebirthProductiveEarnedObservationBudget.Available=false;Check(!MatchesClosedPendingMarker(header,full),"budget denial preserves unknown");
  RebirthProductiveEarnedObservationBudget.Available=true;RebirthWorkstationCompletionPublication.Stored.Element("recyclerClosedAuthoritySource").SetAttributeValue("actualRuntimeRow",false);
  Check(!MatchesClosedPendingMarker(header,full),"non-original saved event rejected");
  Console.WriteLine("PASS closed pending actual marker link scenarios");
 }
}
