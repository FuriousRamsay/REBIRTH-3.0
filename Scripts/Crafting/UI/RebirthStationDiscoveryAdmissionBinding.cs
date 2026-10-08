using System;using System.Collections.Generic;using System.Linq;using System.Text;using System.Xml;using System.Xml.Linq;using System.IO;
// Detached frozen data. Canonical catalogue authority, payment and completion are external proofs.
public sealed class RebirthStationDiscoveryAdmissionBinding
{
 public const int MaximumBytes=8*1024*1024;private readonly XElement image;
 public string CanonicalRecipe=>(string)image.Attribute("recipe");public string JobId=>(string)image.Attribute("job");
 private RebirthStationDiscoveryAdmissionBinding(XElement node){image=new XElement(node);}
 public XElement Write()=>new XElement(image);public RebirthStationDiscoveryAdmissionBinding Clone()=>new RebirthStationDiscoveryAdmissionBinding(image);
 public static bool TryCreateDetached(string canonicalRecipe,RebirthStationDiscoveryWitness witness,RebirthStationGridAdmission admission,out RebirthStationDiscoveryAdmissionBinding binding)
 {
  binding=null;if(witness==null||admission==null||!Name(canonicalRecipe))return false;
  try{return TryReadStored(new XElement("stationDiscoveryAdmissionBinding",new XAttribute("version",1),new XAttribute("recipe",canonicalRecipe),new XAttribute("job",admission.JobId),witness.Write(),admission.Write()),out binding);}catch{return false;}
 }
 // Resolver must authenticate exact native catalogue names, including aliases and output-name ambiguity.
 // A caller-provided equality predicate or Recipe.GetName is not catalogue authentication.
 public bool TryValidateAuthority(string expectedCanonical,RebirthStationDiscoveryWitness authenticatedWitness,RebirthStationGridAdmission authenticatedAdmission,Func<string,string,bool> exactCanonicalDefinition)
 {
  if(authenticatedWitness==null||authenticatedAdmission==null||exactCanonicalDefinition==null||expectedCanonical!=CanonicalRecipe)return false;
  try{return XNode.DeepEquals(image.Element("stationDiscoveryWitness"),authenticatedWitness.Write())&&XNode.DeepEquals(image.Element("stationAdmission"),authenticatedAdmission.Write())&&exactCanonicalDefinition(CanonicalRecipe,authenticatedAdmission.DefinitionId);}catch{return false;}
 }
 public static bool TryReadStored(string xml,out RebirthStationDiscoveryAdmissionBinding binding)
 {
  binding=null;if(xml==null||xml.Length>MaximumBytes)return false;
  try{if(new UTF8Encoding(false,true).GetByteCount(xml)>MaximumBytes)return false;using(var sr=new StringReader(xml))using(var reader=XmlReader.Create(sr,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,IgnoreWhitespace=true,MaxCharactersInDocument=MaximumBytes,MaxCharactersFromEntities=0}))return TryReadStored(XElement.Load(reader,LoadOptions.None),out binding);}catch{return false;}
 }
 public static bool TryReadStored(XElement node,out RebirthStationDiscoveryAdmissionBinding binding)
 {
  binding=null;
  try
  {
   if(!Bounded(node)||node.Name!="stationDiscoveryAdmissionBinding"||node.Attributes().Count()!=3||
      (string)node.Attribute("version")!="1"||!Name((string)node.Attribute("recipe"))||node.Elements().Count()!=2||
      node.Elements().First().Name!="stationDiscoveryWitness"||node.Elements().Last().Name!="stationAdmission"||
      !RebirthStationGridAdmission.TryReadStored(node.Element("stationAdmission"),out var admission)||
      !RebirthStationDiscoveryWitness.TryReadStored(node.Element("stationDiscoveryWitness"),out var witness)||
      admission.JobId!=(string)node.Attribute("job")||witness.JobId!=admission.JobId)return false;
   var w=witness.Write();if(!RebirthStationDiscoveryWitness.TryRead(w,admission,(string)w.Attribute("owner"),(string)w.Attribute("save"),(string)w.Attribute("policy"),(string)w.Attribute("knowledge"),out _))return false;
   if(new UTF8Encoding(false,true).GetByteCount(node.ToString(SaveOptions.DisableFormatting))>MaximumBytes)return false;
   binding=new RebirthStationDiscoveryAdmissionBinding(node);return true;
  }catch{return false;}
 }
 private static bool Name(string value){if(string.IsNullOrEmpty(value)||value.Length>1024||value.Any(c=>char.IsControl(c)||char.IsWhiteSpace(c)))return false;try{XmlConvert.VerifyXmlChars(value);return new UTF8Encoding(false,true).GetByteCount(value)<=1024;}catch{return false;}}
 private static bool Bounded(XElement root)
 {
  if(root==null)return false;var pending=new Stack<Tuple<XElement,int>>();pending.Push(Tuple.Create(root,0));long bytes=0;int count=0;var utf8=new UTF8Encoding(false,true);
  while(pending.Count>0){var next=pending.Pop();if(next.Item2>64||++count>256||next.Item1.Name.NamespaceName.Length!=0)return false;foreach(var a in next.Item1.Attributes()){if(a.IsNamespaceDeclaration||a.Name.NamespaceName.Length!=0||a.Value.Length>MaximumBytes)return false;bytes+=utf8.GetByteCount(a.Value);if(bytes>MaximumBytes)return false;}foreach(var n in next.Item1.Nodes()){if(n is XElement child)pending.Push(Tuple.Create(child,next.Item2+1));else if(n is XText t&&string.IsNullOrWhiteSpace(t.Value)&&t.Value.Length<=8192){bytes+=utf8.GetByteCount(t.Value);if(bytes>MaximumBytes)return false;}else return false;}}
  return true;
 }
}
