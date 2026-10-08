using System;using System.Collections.Generic;using System.IO;using System.Linq;using System.Text;using System.Xml;using System.Xml.Linq;
// Optional frozen payment-target storage. No queue phase, payment or discovery authority is inferred.
internal static class RebirthStationDiscoveryAdmissionPersistence
{
 internal const int MaximumRecords=64,MaximumBytes=8*1024*1024;
 internal static XElement Write(IDictionary<string,RebirthStationDiscoveryAdmissionBinding> records,string owner,string creation)
 {
  if(!Scope(owner,creation)||records==null||records.Count>MaximumRecords)throw new InvalidDataException("Invalid discovery admission scope or collection.");
  var section=new XElement("stationDiscoveryAdmissions",new XAttribute("version",1));long used=Measure(new XElement("stationDiscoveryAdmissions",new XAttribute("version",1),new XText("")),MaximumBytes);
  foreach(var pair in records.OrderBy(p=>p.Key,StringComparer.Ordinal))
  {
   if(pair.Value==null||pair.Key!=pair.Value.JobId)throw new InvalidDataException("Invalid discovery admission key.");
   var image=pair.Value.Write();used+=Measure(image,MaximumBytes-used);
   if(!RebirthStationDiscoveryAdmissionBinding.TryReadStored(image,out var validated)||!Matches(validated,owner,creation))throw new InvalidDataException("Invalid discovery admission record or scope.");
   section.Add(validated.Write());
  }
  Measure(section,MaximumBytes);return section;
 }
 internal static bool TryRead(XElement progression,string owner,string creation,out Dictionary<string,RebirthStationDiscoveryAdmissionBinding> records,out string error)
 {
  records=null;error=null;
  try
  {
   if(progression==null||progression.Name.NamespaceName.Length!=0||!Scope(owner,creation)){error="Invalid discovery admission parent scope.";return false;}
   var sections=progression.Elements().Where(n=>n.Name.LocalName=="stationDiscoveryAdmissions").ToArray();
   if(sections.Length>1){error="Duplicate discovery admission section.";return false;}
   var result=new Dictionary<string,RebirthStationDiscoveryAdmissionBinding>(StringComparer.Ordinal);
   if(sections.Length==0){records=result;return true;}
   var section=sections[0];if(section.Name!="stationDiscoveryAdmissions"||section.Attributes().Count()!=1||section.Attribute("version")?.Value!="1"||
    section.Attributes().Any(a=>a.IsNamespaceDeclaration||a.Name.NamespaceName.Length!=0)||section.Nodes().Any(n=>!(n is XElement)&&(!(n is XText t)||!string.IsNullOrWhiteSpace(t.Value)))||section.Elements().Count()>MaximumRecords)
    {error="Invalid discovery admission section.";return false;}
   Measure(section,MaximumBytes);
   foreach(var node in section.Elements())
   {
    if(!RebirthStationDiscoveryAdmissionBinding.TryReadStored(node,out var binding)||!Matches(binding,owner,creation)||result.ContainsKey(binding.JobId))
     {error="Invalid or duplicate discovery admission record.";return false;}
    result.Add(binding.JobId,binding);
   }
   records=result;return true;
  }catch(Exception){error="Invalid discovery admission encoding or section budget.";return false;}
 }
 private static bool Matches(RebirthStationDiscoveryAdmissionBinding binding,string owner,string creation)
 {
  var node=binding.Write();var witness=node.Element("stationDiscoveryWitness");var admission=node.Element("stationAdmission");
  return (string)witness?.Attribute("owner")==owner&&(string)witness?.Attribute("creation")==creation&&(string)admission?.Attribute("creation")==creation&&
   (string)witness?.Attribute("job")==binding.JobId&&(string)admission?.Attribute("job")==binding.JobId;
 }
 private static bool Scope(string owner,string creation)=>owner!=null&&owner.Length==64&&owner.All(c=>c>='0'&&c<='9'||c>='a'&&c<='f')&&RebirthSurvivorRequestScope.TryNormalize(creation,out var normalized)&&normalized==creation;
 private static long Measure(XElement node,long budget)
 {
  if(budget<0)throw new InvalidDataException("Discovery admission section exceeds budget.");
  var stack=new Stack<Tuple<XElement,int>>();stack.Push(Tuple.Create(node,0));int count=0;
  while(stack.Count>0){var entry=stack.Pop();if(entry.Item2>66||++count>16385)throw new InvalidDataException("Discovery admission tree exceeds budget.");foreach(var child in entry.Item1.Elements())stack.Push(Tuple.Create(child,entry.Item2+1));}
  using(var stream=new BudgetStream(budget))using(var writer=XmlWriter.Create(stream,new XmlWriterSettings{Encoding=new UTF8Encoding(false,true),OmitXmlDeclaration=true,Indent=false,CloseOutput=false})){node.WriteTo(writer);writer.Flush();return stream.Bytes;}
 }
 private sealed class BudgetStream:Stream
 {
  private readonly long limit;internal long Bytes;internal BudgetStream(long budget){limit=budget;}
  public override void Write(byte[] buffer,int offset,int count){if(count<0||Bytes>limit-count)throw new InvalidDataException("Discovery admission section exceeds budget.");Bytes+=count;}
  public override bool CanRead=>false;public override bool CanSeek=>false;public override bool CanWrite=>true;public override long Length=>Bytes;public override long Position{get=>Bytes;set=>throw new NotSupportedException();}public override void Flush(){}public override int Read(byte[] b,int o,int c)=>throw new NotSupportedException();public override long Seek(long o,SeekOrigin s)=>throw new NotSupportedException();public override void SetLength(long v)=>throw new NotSupportedException();
 }
}
