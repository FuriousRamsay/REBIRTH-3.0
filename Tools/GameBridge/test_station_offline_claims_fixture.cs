// CODEC
public class RebirthStationGridAdmission{
 public string JobId,CreationId;public int X;
 public bool SharesStation(RebirthStationGridAdmission o){return X==o.X;}
 public XElement Write(){return new XElement("claim",new XAttribute("job",JobId),new XAttribute("creation",CreationId),new XAttribute("x",X));}
 public static bool TryNormalizeCreation(string s,out string n){n=s;return !string.IsNullOrEmpty(s);}
}
public static class RebirthStationPreparationPersistence{
 public static bool MatchesOwner(IDictionary<string,RebirthStationGridAdmission> r,string c){return r.Values.All(v=>v.CreationId==c);}
 public static bool TryRead(XElement p,out Dictionary<string,RebirthStationGridAdmission> r,out string error){r=new Dictionary<string,RebirthStationGridAdmission>();error="";foreach(var n in p.Elements("claim")){var a=new RebirthStationGridAdmission{JobId=(string)n.Attribute("job"),CreationId=(string)n.Attribute("creation"),X=(int)n.Attribute("x")};r.Add(a.JobId,a);}return true;}
}
public static class RebirthAtomicXmlFile{public static bool TryLoad(string p,out XDocument d,out string error){error="";try{d=XDocument.Load(p);return true;}catch{d=null;return false;}}}
class Check{
 static void A(bool ok,string why){if(!ok)throw new Exception(why);}
 static XDocument Doc(string owner,params RebirthStationGridAdmission[] claims){return new XDocument(new XElement("rebirthWorldCharacter",new XAttribute("stablePlayerKey",owner),new XElement("origin",new XAttribute("creationId","c")),new XElement("progression",claims.Select(v=>v.Write()))));}
 static void Main(){var a=new RebirthStationGridAdmission{JobId="a",CreationId="c",X=4};var b=new RebirthStationGridAdmission{JobId="b",CreationId="c",X=4};
 A(RebirthStationOfflineClaims.CheckDocument(Doc("owner",a),"owner","owner",a),"exact own retry");A(!RebirthStationOfflineClaims.CheckDocument(Doc("offline",b),"offline","owner",a),"offline conflicting station");A(!RebirthStationOfflineClaims.CheckDocument(Doc("offline",a),"offline","owner",a),"same IDs cannot impersonate other owner");b.X=5;A(RebirthStationOfflineClaims.CheckDocument(Doc("offline",b),"offline","owner",a),"independent station");b.JobId="a";A(!RebirthStationOfflineClaims.CheckDocument(Doc("offline",b),"offline","owner",a),"same job ID elsewhere");A(!RebirthStationOfflineClaims.CheckDocument(Doc("other"),"offline","owner",a),"filename owner mismatch");
 string dir=Path.Combine(Path.GetTempPath(),"rebirth-claims-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
 try{Doc("owner",a).Save(Path.Combine(dir,"owner.xml"));A(RebirthStationOfflineClaims.CheckDirectory(dir,"owner",a),"directory own retry");File.WriteAllText(Path.Combine(dir,"offline.xml.bak"),"backup");A(!RebirthStationOfflineClaims.CheckDirectory(dir,"owner",a),"orphan backup refuses");File.Delete(Path.Combine(dir,"offline.xml.bak"));File.WriteAllText(Path.Combine(dir,"offline.xml"),"malformed");A(!RebirthStationOfflineClaims.CheckDirectory(dir,"owner",a),"malformed final refuses");}
 finally{foreach(var file in Directory.GetFiles(dir))File.Delete(file);Directory.Delete(dir);}
 Console.WriteLine("PASS actual offline claim gate: exact retry, offline collision, identity/filename refusal, independent station, orphan backup and malformed final (admission codec doubled)");
 }
}