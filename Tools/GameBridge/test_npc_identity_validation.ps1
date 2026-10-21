$ErrorActionPreference='Stop'
$source=Get-Content -Raw (Join-Path $PSScriptRoot '../../Scripts/NPC/Persistence/RebirthNpcStableIdentityStore.cs')
$start=$source.IndexOf('    private static bool ValidateDocument(XmlDocument document)');$end=$source.IndexOf('    private static string FormatDistance(',$start)
if($start -lt 0 -or $end -le $start){throw 'Production identity validator missing'}
$method=$source.Substring($start,$end-$start).Replace('private static bool ValidateDocument','public static bool ValidateDocument')
$fixture=@'
using System;using System.Xml;using System.Globalization;using System.Collections.Generic;
struct RebirthNpcStableId{Guid value;public bool IsEmpty=>value==Guid.Empty;public static bool TryParse(string text,out RebirthNpcStableId id){Guid value;bool ok=Guid.TryParse(text,out value);id=new RebirthNpcStableId{value=value};return ok;}}
public static class IdentityValidationChecks{
const int FormatVersion=3;
// METHOD
static bool Read(string rows,int version=3){var doc=new XmlDocument();doc.LoadXml("<rebirthNpcStableIdentities format='"+version+"'>"+rows+"</rebirthNpcStableIdentities>");return ValidateDocument(doc);}
public static void Run(){
 const string row="<npc stableId='11111111111111111111111111111111' entity='1' class='2' x='1.5' y='0' z='-2'/>";
 if(!Read(row)||!Read(row,1)||Read(row+row))throw new Exception("valid/legacy/duplicate identity");
 foreach(var bad in new[]{row.Replace("x='1.5'","x='NaN'"),row.Replace("y='0'","y='Infinity'"),row.Replace("class='2'","class='invalid'"),row.Replace("/>"," attempts='-1'/>"),row.Replace("<npc ","<unknown ")})
 if(Read(bad))throw new Exception("invalid identity accepted");
 Console.WriteLine("PASS actual identity validator with stable-ID double: legacy/finite/duplicates/optional fields. Save-scope/cache NOT covered.");}
}
'@
Add-Type -TypeDefinition $fixture.Replace('// METHOD',$method)
[IdentityValidationChecks]::Run()
