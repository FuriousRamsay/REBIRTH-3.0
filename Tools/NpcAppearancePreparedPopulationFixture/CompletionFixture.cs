using System;using System.Collections.Generic;using System.IO;using System.Linq;using System.Xml;using System.Xml.Linq;
static class CompletionFixture
{
 internal static int Run(RebirthNpcPendingSpawn request)
 {
  int n=0;void Check(bool value,string name){if(!value)throw new Exception(name);n++;}
  Check(RebirthNpcSpawnCompletion.TryCreate(request,7,DateTime.UtcNow.Ticks,out var value),"completion uses supplied aggregate generation");
  Check(value.Matches(value.Stable,request.Profile,request.EntityClass,request.NativeEntityId,7),"completion exact original binding");
  Check(!value.Matches(value.Stable,request.Profile,request.EntityClass,request.NativeEntityId,8),"different embodiment refused");
  Check(!value.Matches(new RebirthNpcStableId(99,88),request.Profile,request.EntityClass,request.NativeEntityId,7),"different original person refused");
  Check(!value.Matches(value.Stable,"other",request.EntityClass,request.NativeEntityId,7),"different profile refused");
  Check(!value.Matches(value.Stable,request.Profile,"other",request.NativeEntityId,7),"different physical class refused");
  Check(!value.Matches(value.Stable,request.Profile,request.EntityClass,request.NativeEntityId+1,7),"different native embodiment refused");
  Check(RebirthNpcSpawnCompletion.TryRead(value.Write(),out var copy)&&XNode.DeepEquals(copy.Write(),value.Write()),"canonical completion roundtrip");
  var detached=value.Write();detached.SetAttributeValue("nativeId",90);Check(value.NativeId==request.NativeEntityId,"immutable detached image");
  foreach(string field in new[]{"version","key","stable","profile","entity","nativeId","generation","completed"})
  {var node=value.Write();node.Attribute(field).Remove();Check(!RebirthNpcSpawnCompletion.TryRead(node,out _),"required completion field "+field);}
  foreach(var pair in new[]{new[]{"version","2"},new[]{"nativeId","09"},new[]{"generation","0"},new[]{"generation","07"},new[]{"completed","0"},new[]{"key","promote:other"},new[]{"profile"," padded "}})
  {var node=value.Write();node.SetAttributeValue(pair[0],pair[1]);Check(!RebirthNpcSpawnCompletion.TryRead(node,out _),"malformed completion "+pair[0]+pair[1]);}
  var extra=value.Write();extra.SetAttributeValue("unknown","true");Check(!RebirthNpcSpawnCompletion.TryRead(extra,out _),"unknown completion field refused");
  var text=value.Write();text.Add(" ");Check(!RebirthNpcSpawnCompletion.TryRead(text,out _),"leaf content refused");
  var namespaced=value.Write();namespaced.Name=XName.Get("completedSpawn","other");Check(!RebirthNpcSpawnCompletion.TryRead(namespaced,out _),"namespace refused");
  var root=new XElement("root");Check(RebirthNpcSpawnCompletion.TryReadSection(root,false,out var empty)&&empty.Count==0,"legacy absence unresolved");Check(!RebirthNpcSpawnCompletion.TryReadSection(root,true,out _),"new schema requires provenance component");
  var dictionary=new Dictionary<string,RebirthNpcSpawnCompletion>{{value.ReplayKey,value}};
  using(var writer=root.CreateWriter())RebirthNpcSpawnCompletion.WriteSection(writer,dictionary);
  Check(RebirthNpcSpawnCompletion.TryReadSection(root,true,out var values)&&values[value.ReplayKey].Matches(value.Stable,request.Profile,request.EntityClass,request.NativeEntityId,7),"required section roundtrip");
  var duplicate=new XElement(root);duplicate.Add(new XElement(root.Element("completedSpawns")));Check(!RebirthNpcSpawnCompletion.TryReadSection(duplicate,true,out _),"duplicate section refused");
  duplicate=new XElement(root);duplicate.Element("completedSpawns").Add(value.Write());Check(!RebirthNpcSpawnCompletion.TryReadSection(duplicate,true,out _),"duplicate replay provenance refused");
  var invalid=new XElement(root);invalid.Element("completedSpawns").SetAttributeValue("required","0");Check(!RebirthNpcSpawnCompletion.TryReadSection(invalid,true,out _),"forward required marker enforced");
  invalid=new XElement(root);invalid.Element("completedSpawns").Name="CompletedSpawns";Check(!RebirthNpcSpawnCompletion.TryReadSection(invalid,true,out _),"case altered section refused");
  XmlDocument Snapshot(string version)
  {
    var doc=new XmlDocument();doc.LoadXml("<rebirthNpcWorldIntegration version='"+version+"'><identity stableId='"+value.Stable+"' ambientEntityId='"+value.NativeId+"' profileId='"+value.Profile+"' displayName='Original' promotedUtcTicks='"+DateTime.UtcNow.Ticks+"'/><replays version='1'><replay key='"+value.ReplayKey+"'/></replays><pendingSpawns version='1'/></rebirthNpcWorldIntegration>");return doc;
  }
  var legacy=Snapshot("1");Check(RebirthNpcWorldIdentityLoad.TryRead(legacy.DocumentElement,out _,out _,out _,out var legacyBinding)&&legacyBinding.Count==0,"actual legacy loader keeps completed keys unbound");
  var next=Snapshot("2");Check(!RebirthNpcWorldIdentityLoad.TryRead(next.DocumentElement,out _,out _,out _,out _),"actual schema2 requires binding component");
  next.DocumentElement.AppendChild(next.ImportNode(LoadNode(root.Element("completedSpawns")),true));
  Check(RebirthNpcWorldIdentityLoad.TryRead(next.DocumentElement,out _,out _,out _,out var newBinding)&&newBinding[value.ReplayKey].Matches(value.Stable,value.Profile,value.EntityClass,value.NativeId,7),"actual schema2 loader qualifies original binding");
  next.DocumentElement.SetAttribute("version","1");Check(!RebirthNpcWorldIdentityLoad.TryRead(next.DocumentElement,out _,out _,out _,out _),"new required component cannot masquerade as old schema");next.DocumentElement.SetAttribute("version","2");
  var changed=new XmlDocument();changed.LoadXml(next.OuterXml);changed.DocumentElement.SelectSingleNode("replays/replay").Attributes["key"].Value="spawn:other";Check(!RebirthNpcWorldIdentityLoad.TryRead(changed.DocumentElement,out _,out _,out _,out _),"orphan binding replay refused");
  changed.LoadXml(next.OuterXml);changed.DocumentElement.SelectSingleNode("identity").Attributes["profileId"].Value="other";Check(!RebirthNpcWorldIdentityLoad.TryRead(changed.DocumentElement,out _,out _,out _,out _),"binding and identity profile conflict refused");
  changed.LoadXml(next.OuterXml);changed.DocumentElement.SelectSingleNode("identity").ParentNode.RemoveChild(changed.DocumentElement.SelectSingleNode("identity"));Check(!RebirthNpcWorldIdentityLoad.TryRead(changed.DocumentElement,out _,out _,out _,out _),"orphan original person refused");
  changed.LoadXml(next.OuterXml);changed.DocumentElement.SetAttribute("version","3");Check(!RebirthNpcWorldIdentityLoad.TryRead(changed.DocumentElement,out _,out _,out _,out _),"unknown future journal refused");
  XmlNode LoadNode(XElement element){var doc=new XmlDocument();doc.LoadXml(element.ToString());return doc.DocumentElement;}  string directory=Path.Combine(Path.GetTempPath(),"RebirthCompletionFixture-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
  try
  {
    string path=Path.Combine(directory,"snapshot.xml");next.Save(path);
    Check(RebirthNpcCompletionSnapshot.TryLoad(path,value.ReplayKey,value.Stable,value.Profile,value.EntityClass,value.NativeId,7,out var snapshot)&&snapshot.Completions[value.ReplayKey].Generation==7,"actual final-file snapshot qualifies original owner generation");
    Check(!RebirthNpcCompletionSnapshot.TryLoad(path,value.ReplayKey,value.Stable,value.Profile,value.EntityClass,value.NativeId,8,out _),"recovery generation mismatch refused");
    Check(!RebirthNpcCompletionSnapshot.TryLoad(path,value.ReplayKey,new RebirthNpcStableId(5,6),value.Profile,value.EntityClass,value.NativeId,7,out _),"recovery original-person mismatch refused");
    Check(!RebirthNpcCompletionSnapshot.TryLoad(path,"spawn:other",value.Stable,value.Profile,value.EntityClass,value.NativeId,7,out _),"recovery unrelated completed key refused");
    Check(!RebirthNpcCompletionSnapshot.TryLoad(path,value.ReplayKey,value.Stable,"other",value.EntityClass,value.NativeId,7,out _),"recovery changed authored profile refused");
    Check(!RebirthNpcCompletionSnapshot.TryLoad(path,value.ReplayKey,value.Stable,value.Profile,"other",value.NativeId,7,out _),"recovery physical class mismatch refused");
    Check(!RebirthNpcCompletionSnapshot.TryLoad(path,value.ReplayKey,value.Stable,value.Profile,value.EntityClass,value.NativeId+1,7,out _),"recovery reused native ID refused");
    legacy.Save(path);Check(!RebirthNpcCompletionSnapshot.TryLoad(path,value.ReplayKey,value.Stable,value.Profile,value.EntityClass,value.NativeId,7,out _),"legacy unbound final file cannot prove original person");
    File.WriteAllText(path,"<!DOCTYPE x [<!ENTITY external SYSTEM 'file:///not-read'>]><rebirthNpcWorldIntegration version='2'>&external;</rebirthNpcWorldIntegration>");Check(!RebirthNpcCompletionSnapshot.TryLoad(path,value.ReplayKey,value.Stable,value.Profile,value.EntityClass,value.NativeId,7,out _),"snapshot DTD/external entities refused");
    File.WriteAllText(path,"<broken>");Check(!RebirthNpcCompletionSnapshot.TryLoad(path,value.ReplayKey,value.Stable,value.Profile,value.EntityClass,value.NativeId,7,out _),"malformed final file refused");
  }
  finally{Directory.Delete(directory,true);}  return n;
 }
}