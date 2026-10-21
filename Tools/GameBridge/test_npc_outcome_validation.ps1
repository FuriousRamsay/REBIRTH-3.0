$ErrorActionPreference='Stop'
$root=Join-Path $PSScriptRoot '../..'
$source=Get-Content -Raw (Join-Path $root 'Scripts/NPC/Persistence/RebirthNpcWorkOutcomePersistence.cs')
$start=$source.IndexOf('    private static bool Validate(XmlDocument document)');$end=$source.IndexOf('    private static string GetPath()',$start)
if($start -lt 0 -or $end -le $start){throw 'Production outcome validator missing'}
$method=$source.Substring($start,$end-$start).Replace('private static bool Validate','public static bool Validate')
$enumSource=Get-Content -Raw (Join-Path $root 'Scripts/NPC/Work/RebirthNpcConcreteWorkInfrastructure.cs')
$enumStart=$enumSource.IndexOf('public enum RebirthNpcWorkFailureCategory');$enumEnd=$enumSource.IndexOf('}',$enumStart)
if($enumStart -lt 0 -or $enumEnd -le $enumStart){throw 'Production failure enum missing'}
$failure=$enumSource.Substring($enumStart,$enumEnd-$enumStart+1)
$fixture=@'
using System;using System.Xml;using System.Globalization;using System.Collections.Generic;
// FAILURE_ENUM
enum RebirthNpcWorkOutcomeState:byte{Completed,Failed,Cancelled,Suspended,Superseded}
struct RebirthNpcStableId{Guid value;public bool IsEmpty=>value==Guid.Empty;public static bool TryParse(string text,out RebirthNpcStableId id){Guid value;bool ok=Guid.TryParse(text,out value);id=new RebirthNpcStableId{value=value};return ok;}}
public static class OutcomeValidationChecks{
const int FormatVersion=1;
static int ParseInt(string value,int fallback){int parsed;return int.TryParse(value,NumberStyles.Integer,CultureInfo.InvariantCulture,out parsed)?parsed:fallback;}
// METHOD
static bool Read(string rows){var doc=new XmlDocument();doc.LoadXml("<rebirthNpcWorkOutcomes format='1'>"+rows+"</rebirthNpcWorkOutcomes>");return Validate(doc);}
public static void Run(){
 const string row="<outcome id='11111111111111111111111111111111' assignment='1' npc='22222222222222222222222222222222' state='0' failure='0' retries='0' started='1' completed='2'><produced><item key='wood' count='2'/></produced></outcome>";
 if(!Read(row)||Read(row+row))throw new Exception("valid/duplicate outcome");
 foreach(var bad in new[]{row.Replace("state='0'","state='255'"),row.Replace("completed='2'","completed='bad'"),row.Replace("retries='0'","retries='-1'"),row.Replace("count='2'","count='0'"),row.Replace("</produced>","<item key='WOOD' count='1'/></produced>")})
 if(Read(bad))throw new Exception("malformed outcome accepted");
 Console.WriteLine("PASS actual outcome validator with native identity double: enums/timestamps/retries/resources/duplicates. Runtime store NOT covered.");}
}
'@
Add-Type -TypeDefinition $fixture.Replace('// FAILURE_ENUM',$failure).Replace('// METHOD',$method)
[OutcomeValidationChecks]::Run()
