// PRODUCTION_CLASS
class Check {
 static string path;
 static void A(bool ok,string reason){if(!ok)throw new Exception(reason);}
 static void Load(string xml){File.WriteAllText(path,xml);RebirthTheorySpecialistRegistry.Load(path,p=>string.Equals(p,"specialist",StringComparison.OrdinalIgnoreCase),s=>string.Equals(s,"skill",StringComparison.OrdinalIgnoreCase),20);}
 static void Refuse(string xml){bool failed=false;try{Load(xml);}catch(Exception){failed=true;}A(failed&&RebirthTheorySpecialistRegistry.ProfileCount==0,"bad reload must clear expertise");}
 static void Main(string[] args){path=Path.GetTempFileName();try{
 string row="<specialist profile_id='specialist'><subject skill_id='skill' theory='70'/></specialist>";
 string root="<survivor_theory_specialists schema_version='1'>",tail="</survivor_theory_specialists>";
 Load(root+row+tail);float value;A(RebirthTheorySpecialistRegistry.TryGetTheory("specialist","skill",out value)&&value==70,"authored lookup");A(RebirthTheorySpecialistRegistry.TryGetTheory("SPECIALIST","SKILL",out value)&&value==70,"case-insensitive native IDs");A(!RebirthTheorySpecialistRegistry.TryGetTheory("other","skill",out value),"unknown profile lookup");var subjects=RebirthTheorySpecialistRegistry.GetSubjects("SPECIALIST");A(subjects.Length==1&&subjects[0]=="skill","subject list");subjects[0]="changed";A(RebirthTheorySpecialistRegistry.GetSubjects("specialist")[0]=="skill","detached subjects");A(RebirthTheorySpecialistRegistry.GetSubjects(null).Length==0&&RebirthTheorySpecialistRegistry.GetSubjects("other").Length==0,"missing subjects");
 foreach(var bad in new[]{row.Replace("70","NaN"),row.Replace("70","Infinity"),row.Replace("70","19"),row.Replace("70","101"),row.Replace("skill_id='skill'","skill_id='other'"),row.Replace("profile_id='specialist'","profile_id='other'"),row.Replace("</specialist>","<subject skill_id='skill' theory='60'/></specialist>"),row+row,row+row.Replace("profile_id='specialist'","profile_id='SPECIALIST'"),row.Replace("<subject skill_id='skill' theory='70'/>",""),row+"unexpected"})Refuse(root+bad+tail);
 Refuse("<!DOCTYPE survivor_theory_specialists [<!ENTITY test 'secret'>]>"+root+row+tail);
 Load(root+tail);A(RebirthTheorySpecialistRegistry.ProfileCount==0,"empty catalogue");File.Delete(path);RebirthTheorySpecialistRegistry.Load(path,p=>true,s=>true,20);A(RebirthTheorySpecialistRegistry.ProfileCount==0,"missing optional catalogue");
 Console.WriteLine("PASS actual specialist registry/real temporary XML: lookup, unknown/duplicate profile/subject, numeric bounds/nonfinite, empty subject, stray text, DTD and stale reload refusal; validators doubled");
 A(args.Length==1,"live catalogue path required");
 RebirthTheorySpecialistRegistry.Load(args[0],p=>p.StartsWith("specialist.",StringComparison.Ordinal),s=>s.StartsWith("skill.",StringComparison.Ordinal),20);
 A(RebirthTheorySpecialistRegistry.ProfileCount==12,"twelve authored professions");
 var live= XDocument.Load(args[0]);var unique=new System.Collections.Generic.HashSet<string>();
 foreach(var profile in live.Root.Elements("specialist")) {
   var profileId=(string)profile.Attribute("profile_id");
   foreach(var subject in profile.Elements("subject")) {
     var skill=(string)subject.Attribute("skill_id");
     A(unique.Add(skill),"subject has exactly one primary profession");
     A(RebirthTheorySpecialistRegistry.TryGetTheory(profileId,skill,out value)&&value==100,"authored master expertise loaded");
   }
 }
 A(unique.Count==48,"actual catalogue covers 48 unique subjects");
 A(!RebirthTheorySpecialistRegistry.TryGetTheory("survivor.ambient","skill.medicine",out value),"ordinary survivor not silently expert");
 Console.WriteLine("PASS actual live catalogue: twelve professions, 48 unique subjects, authored expertise, generic survivor exclusion; profile/subject validators doubled");
 }finally{if(File.Exists(path))File.Delete(path);}}
}

