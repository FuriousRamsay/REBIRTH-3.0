using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
// TOOLS ONLY: immutable stored data. No native event, publication or grant authority.
internal sealed class MixedCompletionRecord
{
 internal const int MaximumBytes=8*1024*1024, MaximumRecords=64, MaximumSpanBytes=256*1024;
 static readonly string[] Originals={"admission","intent","queued","completed"};
 static readonly string[] Spans={"input","queue","output","completion"};
 readonly XElement image;
 MixedCompletionRecord(XElement node){image=new XElement(node);}
 internal XElement Write()=>new XElement(image);
 internal MixedCompletionRecord Clone()=>new MixedCompletionRecord(image);
 internal string Job=>(string)image.Attribute("job");
 static string Hash(byte[] bytes){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","");}
 internal static string Digest(XElement node)=>Hash(Encoding.UTF8.GetBytes(node.ToString(SaveOptions.DisableFormatting)));
 static bool Shape(XElement n,string name,string[] attrs,string[] children)=>n!=null&&n.Name==name&&n.Attributes().Count()==attrs.Length&&attrs.All(a=>n.Attribute(a)!=null)&&n.Attributes().All(a=>a.Name.Namespace==XNamespace.None&&!a.IsNamespaceDeclaration)&&n.Nodes().All(x=>x is XElement||x is XText t&&string.IsNullOrWhiteSpace(t.Value))&&n.Elements().Count()==children.Length&&children.All(c=>n.Elements(c).Count()==1);
 // All supplied objects are DATA. Caller authentication and native readback are separate prerequisites.
 internal static bool TryCreateData(XElement admission,XElement intent,XElement queued,XElement completed,NativePaidOrdinaryChainCodec.StoredData chain,byte[][] spans,out MixedCompletionRecord record)
 {
  record=null;try{
   if(admission==null||intent==null||queued==null||completed==null||chain==null||spans==null||spans.Length!=4||spans.Any(b=>b==null||b.Length<1||b.Length>MaximumSpanBytes))return false;
   var c=chain.Write();var n=new XElement("mixedCompletionRecord",new XAttribute("version",1));
   foreach(var key in new[]{"job","creation","definition","owner","world","root","block","x","y","z"})n.Add(new XAttribute(key,(string)c.Attribute(key)));
   var originals=new[]{admission,intent,queued,completed};
   for(int i=0;i<4;i++)n.Add(new XElement(Originals[i],new XAttribute("digest",Digest(originals[i])),new XElement(originals[i])));
   n.Add(new XElement("chain",new XAttribute("digest",Digest(c)),c));
   for(int i=0;i<4;i++)n.Add(new XElement(Spans[i],new XAttribute("digest",Hash(spans[i])),Convert.ToBase64String(spans[i])));
   return TryReadData(n,out record);
  }catch{return false;}
 }
 internal static bool TryReadData(XElement node,out MixedCompletionRecord record)
 {
  record=null;try{
   if(node==null||Encoding.UTF8.GetByteCount(node.ToString(SaveOptions.DisableFormatting))>MaximumBytes||!Shape(node,"mixedCompletionRecord",new[]{"version","job","creation","definition","owner","world","root","block","x","y","z"},Originals.Concat(new[]{"chain"}).Concat(Spans).ToArray())||(string)node.Attribute("version")!="1")return false;
   var chainNode=node.Element("chain");if(!Shape(chainNode,"chain",new[]{"digest"},new[]{"nativePaidOrdinaryChain"})||Digest(chainNode.Elements().Single())!=(string)chainNode.Attribute("digest")||!NativePaidOrdinaryChainCodec.TryRead(chainNode.Elements().Single().ToString(SaveOptions.DisableFormatting),out var chain))return false;
   var c=chain.Write();foreach(var key in new[]{"job","creation","definition","owner","world","root","block","x","y","z"})if((string)node.Attribute(key)!=(string)c.Attribute(key))return false;
   var names=new[]{"stationAdmission","stationTerminalIntent","stationPublication","stationCompletionPublication"};
   for(int i=0;i<4;i++){
    var wrapper=node.Element(Originals[i]);if(wrapper.Attributes().Count()!=1||wrapper.Attribute("digest")==null||wrapper.Elements().Count()!=1||wrapper.Nodes().Any(x=>!(x is XElement)))return false;
    var original=wrapper.Elements().Single();if(original.Name!=names[i]||Digest(original)!=(string)wrapper.Attribute("digest")||(string)original.Attribute("job")!=(string)node.Attribute("job")||(string)original.Attribute("creation")!=(string)node.Attribute("creation"))return false;
   }
   // Embedded original admission must be byte/semantic XML identical to the chain's retained original.
   var admitted=Encoding.UTF8.GetString(Convert.FromBase64String(c.Element("admission").Value));
   if(!XNode.DeepEquals(XElement.Parse(admitted),node.Element("admission").Elements().Single()))return false;
   foreach(var name in Spans){var span=node.Element(name);if(span.Attributes().Count()!=1||span.Attribute("digest")==null||span.HasElements||span.Nodes().Any(x=>!(x is XText))||span.Value.Length>4*((MaximumSpanBytes+2)/3))return false;var b=Convert.FromBase64String(span.Value);if(b.Length<1||b.Length>MaximumSpanBytes||Convert.ToBase64String(b)!=span.Value||Hash(b)!=(string)span.Attribute("digest"))return false;}
   record=new MixedCompletionRecord(node);return true;
  }catch{return false;}
 }
 // Additive candidate: ordinary sections remain untouched; publish no partial dictionary on failure.
 internal static bool TryReadAllData(XElement progression,out Dictionary<string,MixedCompletionRecord> records)
 {
  records=null;try{
   if(progression==null)return false;var sections=progression.Elements("stationMixedCompletionRecords").ToArray();
   long total=0;foreach(var section in progression.Elements().Where(n=>n.Name=="stationMixedCompletionRecords"||n.Name=="stationCompletionExpectationProjections"||n.Name=="stationCompletionPublications")){total+=Encoding.UTF8.GetByteCount(section.ToString(SaveOptions.DisableFormatting));if(total>MaximumBytes)return false;}
   var parsed=new Dictionary<string,MixedCompletionRecord>(StringComparer.Ordinal);if(sections.Length==0){records=parsed;return true;}
   var s=sections[0];if(sections.Length!=1||s.Attributes().Count()!=1||(string)s.Attribute("version")!="1"||s.Elements().Count()>MaximumRecords||s.Nodes().Any(x=>!(x is XElement)))return false;
   foreach(var n in s.Elements()){if(!TryReadData(n,out var r)||parsed.ContainsKey(r.Job))return false;parsed.Add(r.Job,r);}records=parsed;return true;
  }catch{return false;}
 }
 internal static bool TryAppendData(XElement progression,IEnumerable<MixedCompletionRecord> records,out XElement next)
 {
  next=null;try{if(progression==null||records==null||progression.Elements("stationMixedCompletionRecords").Any())return false;var n=new XElement(progression);n.Add(new XElement("stationMixedCompletionRecords",new XAttribute("version",1),records.Select(r=>r.Write())));if(!TryReadAllData(n,out _))return false;next=n;return true;}catch{return false;}
 }
 // Exact persisted DATA witness only. No backup, semantic authority or cold native event recreation.
 internal static bool MatchesFinalData(XElement finalProgression,MixedCompletionRecord retained)
 {
  return retained!=null&&TryReadAllData(finalProgression,out var saved)&&saved.TryGetValue(retained.Job,out var actual)&&XNode.DeepEquals(actual.image,retained.image);
 }
}
