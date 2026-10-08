using System;using System.Linq;using System.Xml.Linq;
class ActualDataIntegration {
 static int n; static void Check(bool ok,string text){if(!ok)throw new Exception(text);n++;Console.WriteLine("PASS "+text);}
 static void Main(string[] args){foreach(var mode in new[]{"Base","Rebirth"}){
 var full=XDocument.Load(System.IO.Path.Combine(args[0],mode+"-blocks-effective.xml"));
 var names=new[]{"cntPillCaseClosed","cntPillCaseEmpty","farmPlotBlock","farmPlotBlockRaised","farmPlotBlockCornerRound","farmPlotBlockPlayer","farmPlotBlockPlayerRaised","farmPlotBlockPlayerCornerRound","cntShippingCrateHero","cntShippingCrateShamway","cntShippingCrateConstructionSupplies"};
 var root=new XElement("blocks",full.Root.Elements("block").Where(b=>names.Contains((string)b.Attribute("name"))).Select(b=>new XElement(b)));
 var originals=root.Elements().ToDictionary(b=>(string)b.Attribute("name"),b=>new XElement(b));
 Localization.Dictionary.Clear();Localization.Dictionary["cntPillCaseClosed"]=new[]{"Medical Cabinet","Armoire médicale"};Localization.Dictionary["cntPillCaseEmpty"]=new[]{"Medical Cabinet Empty","Armoire médicale vide"};Localization.Dictionary["xuiStorage"]=new[]{"Storage","Stockage"};
 var file=new XmlFile{XmlDoc=new XDocument(root)};RebirthBlockPickupEmptyVariantGenerator.Prepare(file);
 foreach(var key in new[]{"cntPillCaseClosedRebirthPlayer","cntPillCaseEmptyRebirthPlayer"})Check(Localization.Dictionary.ContainsKey(key)&&Localization.Dictionary[key].SequenceEqual(Localization.Dictionary["cntPillCaseClosed"]),mode+" actual cabinet graph neutral "+key);
 foreach(var name in names.Where(x=>x.StartsWith("farmPlot"))){var after=root.Elements().Single(x=>(string)x.Attribute("name")==name);Check(XNode.DeepEquals(originals[name],after),mode+" farm source unchanged by generator "+name);}
 foreach(var name in names.Where(x=>x.StartsWith("cntShipping"))){var after=root.Elements().Single(x=>(string)x.Attribute("name")==name);Check(XNode.DeepEquals(originals[name],after),mode+" cover/conversion original unchanged "+name);}
 }Console.WriteLine(n+" PASS actual effective subgraph/current generator; localization/classifier/runtime adapters explicit");}
}
