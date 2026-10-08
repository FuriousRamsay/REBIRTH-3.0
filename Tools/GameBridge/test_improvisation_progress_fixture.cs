using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml.Linq;
// PRODUCTION_CLASS
public static class Check {
 static void A(bool ok,string name){if(!ok)throw new Exception(name);}
 static bool Read(string xml){Dictionary<string,float> v;string e;return RebirthImprovisationProgressPersistence.TryRead(XElement.Parse(xml),out v,out e);}
 public static void Main(){
 Dictionary<string,float> values;string error;
 A(RebirthImprovisationProgressPersistence.TryRead(new XElement("progression"),out values,out error)&&values.Count==0,"old save starts untrained");
 var input=new Dictionary<string,float>{{"electrical",1.234567f},{"building",87.5f}};
 CultureInfo.CurrentCulture=new CultureInfo("fr-CA");
 var written=RebirthImprovisationProgressPersistence.Write(input);
 A(RebirthImprovisationProgressPersistence.TryRead(new XElement("progression",written),out values,out error),"roundtrip");
 A(values["building"]==87.5f&&values["electrical"]==1.234567f,"independent exact values invariant culture");values["building"]=0;A(input["building"]==87.5f,"detached read");
 A(!Read("<progression><improvisation version='1'/><improvisation version='1'/></progression>"),"duplicate section");
 A(!Read("<progression><improvisation version='2'/></progression>"),"unknown version");
 foreach(string value in new[]{"NaN","Infinity","-1","101","1,5"}) A(!Read("<progression><improvisation version='1'><category id='building' value='"+value+"'/></improvisation></progression>"),"bad numeric "+value);
 A(!Read("<progression><improvisation version='1'><category id='Building' value='1'/></improvisation></progression>"),"noncanonical ID");
 A(!Read("<progression><improvisation version='1'><category id='building' value='1'/><category id='building' value='2'/></improvisation></progression>"),"duplicate category");
 var oversized=new XElement("improvisation",new XAttribute("version","1"));for(int i=0;i<65;i++)oversized.Add(new XElement("category",new XAttribute("id","c"+i),new XAttribute("value",i)));
 A(!RebirthImprovisationProgressPersistence.TryRead(new XElement("progression",oversized),out values,out error)&&values==null,"bounded no partial publication");
 input["building"]=float.NaN;bool threw=false;try{RebirthImprovisationProgressPersistence.Write(input);}catch(InvalidDataException){threw=true;}A(threw,"refuse corrupt save");
 Console.WriteLine("PASS old-save default, exact category roundtrip, invariant culture, detached data, duplicate/version/numeric/ID/bound refusal");
 }
}
