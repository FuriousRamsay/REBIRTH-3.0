using System;using System.Collections.Generic;using System.Linq;using System.Xml.Linq;using System.Security.Cryptography;using System.Text;
// Immutable DATA custody. A stored node NEVER proves a native award occurred or permits replay.
public sealed class OpenStationRewardCustodyRecord
{
 readonly XElement image;private OpenStationRewardCustodyRecord(XElement x){image=new XElement(x);}
 public string JobId=>(string)image.Attribute("job");public string Mode=>(string)image.Attribute("mode");public string Phase=>(string)image.Attribute("phase");
 public XElement Write()=>new XElement(image);public OpenStationRewardCustodyRecord Clone()=>new OpenStationRewardCustodyRecord(image);
 public bool ForbidsNativeReplay=>Mode=="early-native"||Phase!="pending";
 static string Binding(XElement x){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(x.ToString(SaveOptions.DisableFormatting)))).Replace("-","");}
 internal static bool TryCreateAttempt(RebirthStationCompletionOriginalScope original,int nativeInputXp,out OpenStationRewardCustodyRecord record)
 {
  record=null;try{if(original==null||!original.IsCurrent())return false;var x=new XElement("stationOpenRewardCustody",new XAttribute("version",1),new XAttribute("job",original.Admission.JobId),new XAttribute("creation",original.Admission.CreationId),new XAttribute("definition",original.Admission.DefinitionId),new XAttribute("owner",original.Identity.StorageKey),new XAttribute("world",original.WorldGuid),new XAttribute("root",original.SaveRoot),new XAttribute("admission",Binding(original.Admission.Write())),new XAttribute("queued",Binding(original.Queued.Write())),new XAttribute("actor",original.Actor),new XAttribute("mode","early-native"),new XAttribute("phase","attempted"),new XAttribute("inputXp",nativeInputXp),new XAttribute("result",0));if(!TryRead(x,original.Admission,original.Queued,original.Identity.StorageKey,out var value)||!original.IsCurrent())return false;record=value;return true;}catch{return false;}
 }
 internal OpenStationRewardCustodyRecord WithNativeReturn(int result){if(Mode!="early-native"||Phase!="attempted")throw new InvalidOperationException();var copy=Write();copy.SetAttributeValue("phase","returned");copy.SetAttributeValue("result",result);return new OpenStationRewardCustodyRecord(copy);}
 internal OpenStationRewardCustodyRecord WithUncertainty(){var copy=Write();copy.SetAttributeValue("phase","uncertain");return new OpenStationRewardCustodyRecord(copy);}
 public static bool TryRead(XElement node,RebirthStationGridAdmission admission,RebirthStationPublicationRecord queued,string owner,out OpenStationRewardCustodyRecord record)
 {
  record=null;try {
   string[] names={"version","job","creation","definition","owner","world","root","admission","queued","actor","mode","phase","inputXp","result"};
   if(node==null||admission==null||queued==null||node.Name!="stationOpenRewardCustody"||node.Attributes().Count()!=names.Length||node.Attributes().Any(a=>!names.Contains(a.Name.LocalName,StringComparer.Ordinal)||a.Name.Namespace!=XNamespace.None||a.Value.Length>4096)||node.Nodes().Any(n=>!(n is XText)||!string.IsNullOrWhiteSpace(((XText)n).Value))||(string)node.Attribute("version")!="1"||(string)node.Attribute("job")!=admission.JobId||(string)node.Attribute("creation")!=admission.CreationId||(string)node.Attribute("definition")!=admission.DefinitionId||(string)node.Attribute("owner")!=owner||string.IsNullOrWhiteSpace(owner)||(string)node.Attribute("admission")!=Binding(admission.Write())||(string)node.Attribute("queued")!=Binding(queued.Write())||string.IsNullOrWhiteSpace((string)node.Attribute("world"))||string.IsNullOrWhiteSpace((string)node.Attribute("root"))||(int)node.Attribute("actor")<=0||!int.TryParse((string)node.Attribute("inputXp"),out _)||!int.TryParse((string)node.Attribute("result"),out _))return false;
   var mode=(string)node.Attribute("mode");var phase=(string)node.Attribute("phase");if(mode!="early-native"&&mode!="delayed-native"||mode=="early-native"&&phase!="attempted"&&phase!="returned"&&phase!="uncertain"||mode=="delayed-native"&&phase!="pending"&&phase!="attempted"&&phase!="returned"&&phase!="uncertain")return false;
   if(phase!="returned"&&(int)node.Attribute("result")!=0)return false;
   record=new OpenStationRewardCustodyRecord(node);return true;
  }catch{return false;}
 }
 public static bool ReadAll(XElement parent,IDictionary<string,RebirthStationGridAdmission> admissions,IDictionary<string,RebirthStationPublicationRecord> queued,string owner,out Dictionary<string,OpenStationRewardCustodyRecord> result)
 {
  result=null;try{var local=new Dictionary<string,OpenStationRewardCustodyRecord>(StringComparer.Ordinal);var sections=parent.Elements("stationOpenRewardCustodies").ToArray();if(sections.Length==0){result=local;return true;}if(sections.Length!=1||sections[0].Attributes().Count()!=1||(string)sections[0].Attribute("version")!="1"||Encoding.UTF8.GetByteCount(sections[0].ToString(SaveOptions.DisableFormatting))>8*1024*1024||sections[0].Nodes().Any(n=>!(n is XElement)&&(!(n is XText)||!string.IsNullOrWhiteSpace(((XText)n).Value))))return false;foreach(var n in sections[0].Elements()){var job=(string)n.Attribute("job");if(local.Count>=64||job==null||local.ContainsKey(job)||!admissions.TryGetValue(job,out var a)||!queued.TryGetValue(job,out var q)||!TryRead(n,a,q,owner,out var record))return false;local.Add(job,record);}result=local;return true;}catch{return false;}
 }
 public static XElement WriteAll(IDictionary<string,OpenStationRewardCustodyRecord> records,IDictionary<string,RebirthStationGridAdmission> admissions,IDictionary<string,RebirthStationPublicationRecord> queued,string owner)
 {
  if(records==null||records.Count>64)throw new InvalidOperationException();var section=new XElement("stationOpenRewardCustodies",new XAttribute("version",1));foreach(var p in records.OrderBy(p=>p.Key,StringComparer.Ordinal)){if(p.Value==null||p.Key!=p.Value.JobId||!admissions.TryGetValue(p.Key,out var a)||!queued.TryGetValue(p.Key,out var q)||!TryRead(p.Value.Write(),a,q,owner,out var r))throw new InvalidOperationException();section.Add(r.Write());}if(Encoding.UTF8.GetByteCount(section.ToString(SaveOptions.DisableFormatting))>8*1024*1024)throw new InvalidOperationException();return section;
 }
}
