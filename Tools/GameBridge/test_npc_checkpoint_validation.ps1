$ErrorActionPreference='Stop'
$source=Get-Content -Raw (Join-Path $PSScriptRoot '../../Scripts/NPC/Persistence/RebirthNpcWorkCheckpointPersistence.cs')
$start=$source.IndexOf('    private static bool Validate(XmlDocument document)')
$end=$source.IndexOf('    private static string GetPath()',$start)
if($start -lt 0 -or $end -le $start){throw 'Production checkpoint validator missing'}
$method=$source.Substring($start,$end-$start).Replace('private static bool Validate','public static bool Validate')
$fixture=@'
using System;using System.Xml;using System.Globalization;using System.Collections.Generic;
enum RebirthNpcWorkExecutorPhase:byte{ResolvingTarget,ValidatingTarget,AcquiringInputs,MovingToTarget,Mutating,CollectingOutput,DeliveringOutput,Finalizing,Suspended,Terminal}
public static class CheckpointValidationChecks{
const int FormatVersion=1;
static int ParseInt(string value,int fallback){int parsed;return int.TryParse(value,NumberStyles.Integer,CultureInfo.InvariantCulture,out parsed)?parsed:fallback;}
// METHOD
static bool Read(string rows){var doc=new XmlDocument();doc.LoadXml("<rebirthNpcWorkCheckpoints format='1'>"+rows+"</rebirthNpcWorkCheckpoints>");return Validate(doc);}
public static void Run(){
 const string row="<checkpoint assignment='1' phase='4' progress='0.5' retries='0' updated='1' executor='work' target='source'/>";
 if(!Read(row)||Read(row+row))throw new Exception("valid/duplicate assignment");
 foreach(var bad in new[]{row.Replace("0.5","NaN"),row.Replace("phase='4'","phase='255'"),row.Replace("retries='0'","retries='-1'"),row.Replace("updated='1'","updated='invalid'"),row.Replace("assignment='1'","assignment='0'")})
 if(Read(bad))throw new Exception("invalid checkpoint accepted");
 Console.WriteLine("PASS actual checkpoint validator: valid, duplicate, NaN, enum, retry, timestamp, identity. Runtime store NOT covered.");}
}
'@
Add-Type -TypeDefinition $fixture.Replace('// METHOD',$method)
[CheckpointValidationChecks]::Run()
