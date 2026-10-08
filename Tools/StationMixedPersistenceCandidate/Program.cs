using System.Xml.Linq;
using System.Text;
class Program {
 static int count;static void A(bool ok,string text){if(!ok)throw new Exception(text);count++;}
 static string B(string s)=>Convert.ToBase64String(Encoding.UTF8.GetBytes(s));
 static XElement Grid(string name,int q)=>new(name,new XElement("cell",new XAttribute("index",0),new XAttribute("count",q),new XAttribute("image","AQ==")));
 static void Main(){
 var a=new XElement("stationAdmission",new XAttribute("version",1),new XAttribute("job","job"),new XAttribute("creation","creation"),new XAttribute("definition",new string('A',64)),new XAttribute("block","campfire"),new XAttribute("x",1),new XAttribute("y",2),new XAttribute("z",3));
 var receipt=new XElement("nativeReceipt",new XAttribute("actor",1),new XAttribute("recipe","water"),new XAttribute("scrap",""),new XAttribute("xp",0),new XAttribute("used",1),new XAttribute("count",2),new XAttribute("image","AQ=="));
 XElement R(string name)=>new(name,new XElement("row",new XAttribute("image",B(receipt.ToString(SaveOptions.DisableFormatting)))));
 var c=new XElement("nativePaidOrdinaryChain",new XAttribute("version",1),new XAttribute("job","job"),new XAttribute("creation","creation"),new XAttribute("definition",new string('A',64)),new XAttribute("owner","owner"),new XAttribute("world","world"),new XAttribute("root","/save"),new XAttribute("block","campfire"),new XAttribute("x",1),new XAttribute("y",2),new XAttribute("z",3),new XAttribute("paidQuantity",2),new XAttribute("state","pending"),new XAttribute("reason",""),new XAttribute("unresolvedHeld",0),new XElement("admission",B(a.ToString(SaveOptions.DisableFormatting))),R("paidReceipt"),Grid("paidBefore",0),Grid("paidAfter",2),Grid("tail",2),R("receiptTail"),new XElement("transitions"));
 A(NativePaidOrdinaryChainCodec.TryRead(c.ToString(SaveOptions.DisableFormatting),out var chain),"actual strict chain codec accepts baseline DATA");
 XElement O(string name)=>new(name,new XAttribute("job","job"),new XAttribute("creation","creation"));
 A(MixedCompletionRecord.TryCreateData(a,O("stationTerminalIntent"),O("stationPublication"),O("stationCompletionPublication"),chain,new[]{new byte[]{1},new byte[]{2},new byte[]{3},new byte[]{4}},out var r),"detached record");
 var legacy=new XElement("progression",new XElement("legacy",new XAttribute("value","unchanged")));
 A(MixedCompletionRecord.TryAppendData(legacy,new[]{r},out var saved),"atomic append");A(XNode.DeepEquals(legacy.Element("legacy"),saved.Element("legacy")),"legacy preserved");A(!legacy.Elements("stationMixedCompletionRecords").Any(),"original not mutated");
 A(MixedCompletionRecord.MatchesFinalData(saved,r),"exact DATA final comparison");A(!MixedCompletionRecord.MatchesFinalData(legacy,r),"predecessor no witness");
 var clone=r.Clone();var exposed=clone.Write();exposed.SetAttributeValue("job","other");A(MixedCompletionRecord.MatchesFinalData(saved,clone),"clone detached");
 foreach(var key in new[]{"job","creation","definition","owner","world","root","block","x","y","z"}){var n=r.Write();n.SetAttributeValue(key,"other");A(!MixedCompletionRecord.TryReadData(n,out var denied)&&denied==null,"binding "+key);}
 foreach(var key in new[]{"input","queue","output","completion"}){var n=r.Write();n.Element(key).Value="AQI=";A(!MixedCompletionRecord.TryReadData(n,out _),"span digest "+key);}
 var duplicate=new XElement(saved);duplicate.Element("stationMixedCompletionRecords").Add(r.Write());A(!MixedCompletionRecord.TryReadAllData(duplicate,out var bad)&&bad==null,"duplicate atomic refusal");
 var malformed=new XElement(saved);malformed.Element("stationMixedCompletionRecords").Add(new XElement("bad"));A(!MixedCompletionRecord.TryReadAllData(malformed,out bad)&&bad==null,"later bad record no partial");
 var unknown=r.Write();unknown.Add(new XAttribute("extra",1));A(!MixedCompletionRecord.TryReadData(unknown,out _),"unknown schema refused");
 var large=new XElement(saved);large.Add(new XElement("stationCompletionExpectationProjections",new string('a',MixedCompletionRecord.MaximumBytes)));A(!MixedCompletionRecord.TryReadAllData(large,out bad)&&bad==null,"combined ordinary+mixed budget");
 A(MixedCompletionRecord.TryReadAllData(legacy,out var empty)&&empty.Count==0,"legacy absence supported");
 A(XNode.DeepEquals(r.Write(),clone.Write()),"uncertain save retains original object image");
  var model=new MixedProgressionCandidate();model.StationMixedCompletionRecords.Add(r.Job,r);
 var modelClone=model.Clone();A(!ReferenceEquals(model,modelClone)&&!ReferenceEquals(r,modelClone.StationMixedCompletionRecords[r.Job]),"actual candidate detached model clone");
 var seamSaved=ActualMixedRepositorySeams.SerializeSeam(legacy,model);A(XNode.DeepEquals(seamSaved,saved),"actual candidate save seam exact section");
 A(ActualMixedRepositorySeams.DeserializeSeam(seamSaved,out var loaded,out var error)&&error==""&&loaded.StationMixedCompletionRecords.Count==1,"actual candidate load seam");
 A(!ReferenceEquals(r,loaded.StationMixedCompletionRecords[r.Job])&&XNode.DeepEquals(r.Write(),loaded.StationMixedCompletionRecords[r.Job].Write()),"cold DATA detached no live proof remint");
 A(ActualMixedRepositorySeams.DeserializeSeam(legacy,out loaded,out error)&&loaded.StationMixedCompletionRecords.Count==0,"actual old save absence");
 A(!ActualMixedRepositorySeams.DeserializeSeam(duplicate,out loaded,out error)&&loaded.StationMixedCompletionRecords.Count==0&&error!="","actual duplicate load refuses before publication");
 A(!ActualMixedRepositorySeams.DeserializeSeam(malformed,out loaded,out error)&&loaded.StationMixedCompletionRecords.Count==0,"actual late malformed load atomic");
 A(!ActualMixedRepositorySeams.DeserializeSeam(large,out loaded,out error)&&loaded.StationMixedCompletionRecords.Count==0,"actual combined budget load refusal");
 var originalRef=model.StationMixedCompletionRecords[r.Job];try{ActualMixedRepositorySeams.SerializeSeam(new XElement("progression",large.Elements().Where(x=>x.Name!="stationMixedCompletionRecords").Select(x=>new XElement(x)).Prepend(new XElement("irrelevant"))),model);}catch{}
 A(ReferenceEquals(originalRef,model.StationMixedCompletionRecords[r.Job]),"failed save retains exact original reservation object");
 A(MixedCompletionRecord.MatchesFinalData(seamSaved,originalRef)&&!MixedCompletionRecord.MatchesFinalData(legacy,originalRef),"retry final DATA identifies exact original not predecessor");
Console.WriteLine("PASS "+count+"; DATA-only, no native publisher/semantic authority");
 }
}
