$ErrorActionPreference='Stop'
$root=Resolve-Path (Join-Path $PSScriptRoot '../..')
function Slice($path,$start,$end) {
 $s=[IO.File]::ReadAllText((Join-Path $root $path));$a=$s.IndexOf($start,[StringComparison]::Ordinal);if($a-lt0){throw $start}
 $b=$s.IndexOf($end,$a,[StringComparison]::Ordinal);if($b-lt0){throw $end};return $s.Substring($a,$b-$a)
}
$parse=Slice 'Scripts/Survivor/Definitions/RebirthSurvivorDefinitionLoader.cs' '            List<RebirthStartingAttributeDefinition> attributeStarts=' '            List<RebirthStartingSkillBiasDefinition> skills='
$apply=Slice 'Scripts/Survivor/Creation/RebirthSurvivorCreationValidator.cs' '            HashSet<string> startingAttributeIds=' '            foreach(RebirthStartingSkillBiasDefinition b in background.StartingSkills)'
$audit=Slice 'Scripts/Survivor/Definitions/RebirthSurvivorAuthoringValidator.cs' '            HashSet<string> backgroundAttributeIds=' '            float positiveSkillTotal='
$hash=Slice 'Scripts/Survivor/Domain/RebirthSurvivorDefinitionVersion.cs' '            // Preserve existing V5 hashes' '            AppendSorted(b,d.StartingKnowledgeIds);'
$model=Slice 'Scripts/Survivor/Domain/RebirthSurvivorDefinitionModels.cs' 'public sealed class RebirthStartingAttributeDefinition' 'public sealed class RebirthBackgroundDefinition'
$code=@'
using System;using System.Collections.Generic;using System.Globalization;using System.IO;using System.Text;using System.Xml;
__MODEL__
public class RebirthAttributeDefinition { public float Min=0,Max=100; }
public class Background { public string Id="fixture"; public List<RebirthStartingAttributeDefinition> StartingAttributes=new List<RebirthStartingAttributeDefinition>(); }
public class Report { public List<string> Errors=new List<string>(); }
public enum RebirthSurvivorCreationErrorCode {UnknownCreationModifier,AttributeOutOfRange}
public static class BackgroundAttributeFixture {
 public class MutableAttribute {public RebirthAttributeDefinition Def=new RebirthAttributeDefinition();public float Current=50,Potential=75;}
 static string E(RebirthSurvivorCreationErrorCode c,string a,string b){return c+":"+a+":"+b;}
 static string Req(XmlElement e,string k){string v=e.GetAttribute(k).Trim();if(v.Length==0)throw new InvalidDataException(k);return v;}
 public static List<RebirthStartingAttributeDefinition> Parse(string xml){var doc=new XmlDocument();doc.LoadXml("<background>"+xml+"</background>");var e=doc.DocumentElement;
__PARSE__
 return attributeStarts;}
 public static Dictionary<string,MutableAttribute> Apply(Background background,List<string> errors){
 var attributes=new Dictionary<string,MutableAttribute>(StringComparer.OrdinalIgnoreCase);
 foreach(string id in new[]{"strength","dexterity","constitution","intelligence","charisma"})attributes.Add(id,new MutableAttribute());
__APPLY__
 return attributes;}
 public static List<string> Audit(Background b){
 var at=new Dictionary<string,RebirthAttributeDefinition>(StringComparer.OrdinalIgnoreCase);
 foreach(string id in new[]{"strength","dexterity","constitution","intelligence","charisma"})at.Add(id,new RebirthAttributeDefinition());
 var r=new Report();
__AUDIT__
 return r.Errors;}
 static string F(float v){return v.ToString("R",CultureInfo.InvariantCulture);}
 static void AppendSorted(StringBuilder b,IEnumerable<string> values){var list=new List<string>(values);list.Sort(StringComparer.Ordinal);b.Append(string.Join(",",list)).Append('|');}
 public static string HashExtension(Background d){var b=new StringBuilder("prior-hash-input");
__HASH__
 return b.ToString();}
 static void Assert(bool v,string m){if(!v)throw new Exception(m);}
 static void RejectParse(string xml){try{Parse(xml);}catch(InvalidDataException){return;}throw new Exception("Parser accepted "+xml);}
 public static void Run(){
 var b=new Background();var errors=new List<string>();var attrs=Apply(b,errors);
 Assert(errors.Count==0&&attrs.Count==5,"absent defaults");foreach(var a in attrs.Values)Assert(a.Current==50&&a.Potential==75,"old defaults");
 Assert(HashExtension(b)=="prior-hash-input","legacy semantic input unchanged");
 b.StartingAttributes=Parse("<starting_attributes><attribute id='strength' current='30'/><attribute id='dexterity' current='31'/><attribute id='constitution' current='32'/><attribute id='intelligence' current='33'/><attribute id='charisma' current='34'/></starting_attributes>");
 attrs=Apply(b,errors);Assert(errors.Count==0&&Audit(b).Count==0,"all five accepted");int n=30;foreach(string id in new[]{"strength","dexterity","constitution","intelligence","charisma"})Assert(attrs[id].Current==n++&&attrs[id].Potential==75,"five starts preserved");
 string hash=HashExtension(b);b.StartingAttributes.Reverse();Assert(hash==HashExtension(b),"hash order invariant");b.StartingAttributes[0]=new RebirthStartingAttributeDefinition("charisma",35);Assert(hash!=HashExtension(b),"numeric hash mismatch");
 // Trait application follows this production block; Current override leaves Potential untouched.
 attrs=Apply(b,errors);attrs["strength"].Current+=8;attrs["strength"].Potential+=8;Assert(attrs["strength"].Current==38&&attrs["strength"].Potential==83,"trait composition");
 foreach(float invalid in new[]{float.NaN,float.PositiveInfinity,float.NegativeInfinity,-1f,101f}){
 b.StartingAttributes=new List<RebirthStartingAttributeDefinition>{new RebirthStartingAttributeDefinition("strength",invalid)};
 errors.Clear();attrs=Apply(b,errors);Assert(errors.Count==1&&attrs["strength"].Current==50&&Audit(b).Count==1,"finite bounds fail closed");}
 b.StartingAttributes=new List<RebirthStartingAttributeDefinition>{new RebirthStartingAttributeDefinition("strength",0),new RebirthStartingAttributeDefinition("dexterity",100)};
 errors.Clear();attrs=Apply(b,errors);Assert(errors.Count==0&&Audit(b).Count==0&&attrs["charisma"].Current==50,"inclusive bounds and partial defaults");
 b.StartingAttributes.Add(new RebirthStartingAttributeDefinition("STRENGTH",10));errors.Clear();Apply(b,errors);Assert(errors.Count==1&&Audit(b).Count==1,"case insensitive duplicate");
 b.StartingAttributes=new List<RebirthStartingAttributeDefinition>{new RebirthStartingAttributeDefinition("perception",30),null};errors.Clear();Apply(b,errors);Assert(errors.Count==2&&Audit(b).Count==2,"unknown and null rejected");
 RejectParse("<starting_attributes/><starting_attributes/>");
 RejectParse("<starting_attributes><attribute id='strength' current='30'/><attribute id='STRENGTH' current='31'/></starting_attributes>");
 foreach(string value in new[]{"NaN","Infinity","-Infinity","bad",""})RejectParse("<starting_attributes><attribute id='strength' current='"+value+"'/></starting_attributes>");
 RejectParse("<starting_attributes><attribute current='30'/></starting_attributes>");
 RejectParse("<starting_attributes><atribute id='strength' current='30'/></starting_attributes>");
 Console.WriteLine("PASS background starts: five attributes, absent defaults, partial defaults, finite bounds, duplicate/unknown/null guards, parser rejection, trait composition, hash compatibility/order/change.");
 }
}
'@
$code=$code.Replace('__MODEL__',$model).Replace('__PARSE__',$parse).Replace('__APPLY__',$apply).Replace('__AUDIT__',$audit).Replace('__HASH__',$hash)
Add-Type -TypeDefinition $code -Language CSharp
[BackgroundAttributeFixture]::Run()
$loader=Get-Content (Join-Path $root 'Scripts/Survivor/Definitions/RebirthSurvivorDefinitionLoader.cs') -Raw
if(!$loader.Contains('string.Empty,attributeStarts))')){throw 'Loader constructor connection missing'}
$ui=Get-Content (Join-Path $root 'Scripts/Survivor/UI/RebirthSurvivorCreatorViewModel.cs') -Raw
$server=Get-Content (Join-Path $root 'Scripts/Survivor/Creation/RebirthSurvivorCreationService.cs') -Raw
if(!$ui.Contains('RebirthSurvivorCreationValidator.Validate(BuildSelection()') -or !$server.Contains('RebirthSurvivorCreationValidator.ValidateForCommit(request.ToSelection(), true)')){throw 'Preview/server common validator changed'}
$backgrounds=[xml](Get-Content (Join-Path $root 'Config/_Survivor/backgrounds.xml') -Raw)
if($backgrounds.SelectNodes('//starting_attributes').Count-ne0){throw 'Numeric table unexpectedly activated'}
'PASS source wiring: loader, shared preview/server validator, no production numeric activation.'
