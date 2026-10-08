// Offline harness: compile with the production generator and legacy targets.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
public class XmlFile { public XDocument XmlDoc; }
public static class TEFeatureStorage { public const string PropLootList="LootList"; }
public static class TEFeatureRebirthContainerName { public const string FeatureName="TEFeatureRebirthContainerName"; }
public static class RebirthContainerSizeService {
 public const string PropertyContainerSize="RebirthContainerSize", PropertyLegacyContainerSize="ContainerSize", PropertyLootContainerSize="LootContainerSize";
}
public static class RebirthBlockPickupClassifier { public const string PropertyVanillaCanPickup="RebirthVanillaCanPickup"; }
public static class RebirthBlockPickupTargetResolver { public const string PropertyVanillaPickupTarget="RebirthVanillaPickupTarget"; }
public static class RebirthLogSettings { public static bool BlockPickupLoggingEnabled=false; }
public static class Log { public static void Out(string s) {} public static void Warning(string s) {throw new Exception(s);} }
public static class Localization { public static Dictionary<string,string[]> Dictionary=new Dictionary<string,string[]>(); public static bool Exists(string s) {return false;} }
public static class CloneInheritanceHarness {
 static string Prop(XElement e,string n) {var p=e.Elements("property").LastOrDefault(x=>(string)x.Attribute("name")==n);return p==null?null:(string)p.Attribute("value");}
 static string Effective(Dictionary<string,XElement> all,string id,string prop) {
  var seen=new HashSet<string>();
  while(id!=null && seen.Add(id) && all.ContainsKey(id)) {
   var e=all[id];var value=Prop(e,prop);if(value!=null)return value;
   var ext=e.Elements("property").LastOrDefault(x=>(string)x.Attribute("name")=="Extends");
   if(ext==null || ((string)ext.Attribute("param1")??"").Split(',').Any(x=>x.Trim()==prop))return null;
   id=(string)ext.Attribute("value");
  }return null;
 }
 static void Check(bool ok,string message) {if(!ok)throw new Exception(message);}
 public static void Main(string[] args) {
  var file=new XmlFile{XmlDoc=XDocument.Load(args[0])};
  var before=file.XmlDoc.Root.Elements("block").ToDictionary(x=>(string)x.Attribute("name"));
  RebirthBlockPickupEmptyVariantGenerator.Prepare(file);
  var all=file.XmlDoc.Root.Elements("block").ToDictionary(x=>(string)x.Attribute("name"));
  int count=0,inherited=0,fallback=0;
  foreach(var pair in all.Where(x=>!before.ContainsKey(x.Key))) {
   count++;var parent=Prop(pair.Value,"Extends");var expected=Effective(all,parent,"CustomIcon");
   if(!string.IsNullOrEmpty(expected)) {Check(Prop(pair.Value,"CustomIcon")==null,pair.Key+" shadows inherited icon");inherited++;}
   else {expected=parent;fallback++;}
   Check(Effective(all,pair.Key,"CustomIcon")==expected,pair.Key+" wrong icon");
   Check(Effective(all,pair.Key,"CustomIconTint")==Effective(all,parent,"CustomIconTint"),pair.Key+" wrong tint");
   Check(Prop(pair.Value,"RebirthPickup")=="Allow",pair.Key+" lost pickup marker");
   Check(pair.Value.Elements("property").Any(x=>(string)x.Attribute("class")=="CompositeFeatures"),pair.Key+" lost storage features");
  }
  Check(count>0,"No generated definitions tested");
  file.XmlDoc.Save(args[1]);
  Console.WriteLine("PASS generated="+count+" inherited="+inherited+" nameFallback="+fallback);
 }
}
