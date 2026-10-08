using System;using System.Xml;using System.IO;using System.Collections.Generic;using System.Globalization;
struct RebirthNpcStableId{public string Id;}
class Identity{public RebirthNpcStableId StableNpcId;}
class RebirthNpcPersistentRecord{public Identity Identity;public string AggregateChecksum;public string Body;}
public class StagedAggregateFixture{
 const int CurrentFormat=1;
 static RebirthNpcPersistentRecord ReadRecord(XmlElement e){return new RebirthNpcPersistentRecord{Identity=new Identity{StableNpcId=new RebirthNpcStableId{Id=e.GetAttribute("id")}},AggregateChecksum=e.GetAttribute("checksum"),Body=e.GetAttribute("body")};}
 static bool ValidatePublication(RebirthNpcPersistentRecord r,RebirthNpcStableId id,out string reason){reason="";return !string.IsNullOrEmpty(id.Id);}
 static string ComputeRecordChecksum(RebirthNpcPersistentRecord r){return r.Body;}
 // ACTUAL_METHOD
 public static string Run(){int count=0;string path=Path.GetTempFileName();try{
 Action<string,bool> check=(xml,expected)=>{File.WriteAllText(path,xml);bool ok=true;try{ValidateStagedAggregate(path,"scope",1);}catch(InvalidDataException){ok=false;}if(ok!=expected)throw new Exception("Unexpected staged validation");count++;};
 string row="<record id='one' body='ABC' checksum='abc'/>";
 string head="<rebirthNpcPersistentRecords format='1' saveScope='scope'>",tail="</rebirthNpcPersistentRecords>";
 check(head+row+tail,true);
 check(head+row.Replace("checksum='abc'","checksum='wrong'")+tail,false);
 check(head+row.Replace("body='ABC'","body='changed'")+tail,false);
 check(head+row.Replace("checksum='abc'","checksum=''")+tail,false);
 check(head+row+row+tail,false);
 check(head+tail,false);
 check(head.Replace("scope","other")+row+tail,false);
 return "PASS "+count+" actual staged aggregate validator checks with real temporary XML; record codec/checksum computation doubled";
 }finally{File.Delete(path);}}
}