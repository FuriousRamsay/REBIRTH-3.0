#requires -Version 7.0
$ErrorActionPreference='Stop'
if(Get-Process 7DaysToDie* -ErrorAction SilentlyContinue){throw 'Game must be closed'}
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$source=[IO.File]::ReadAllText((Join-Path $root 'Scripts/Crafting/Cooking/RebirthCookingMillingPublication.MobileColdTerminal.cs'))
$start=$source.IndexOf('    private static IEnumerator<string> EnumerateMobileColdOriginals()')
$end=$source.IndexOf('    private static bool CanTickUnqualifiedMobileSource',$start)
if($start -lt 0 -or $end -le $start){throw 'Actual history helper missing'}
$fixture=@'
using System;
using System.IO;
using System.Collections.Generic;
static class RebirthWorkstationCompletionOutbox { public static string Path; public static string Root(string role){if(role!="cookingMilling")throw new Exception();return Path;} }
static class RebirthWorkstationCompletionPublication { public static void RequireSafeDirectory(string path){if(path!=RebirthWorkstationCompletionOutbox.Path)throw new Exception();} }
public static class EmptyHistoryFixture {
// ACTUAL_HELPER
public static void Run(string root){
RebirthWorkstationCompletionOutbox.Path=root;
using(var empty=EnumerateMobileColdOriginals()){if(empty.MoveNext())throw new Exception("Fresh directory must be empty");}
File.WriteAllText(System.IO.Path.Combine(root,"original.xml"),"original");
File.WriteAllText(System.IO.Path.Combine(root,"original.xml.saved"),"witness");
using(var existing=EnumerateMobileColdOriginals()){if(!existing.MoveNext()||System.IO.Path.GetFileName(existing.Current)!="original.xml"||existing.MoveNext())throw new Exception("Original enumeration changed");}
Console.WriteLine("PASS actual history helper: absent directory, empty history, existing original retained, witness excluded; native gameplay NOT RUN");
}
}
'@
Add-Type -TypeDefinition $fixture.Replace('// ACTUAL_HELPER',$source.Substring($start,$end-$start))
$testRoot=Join-Path ([IO.Path]::GetTempPath()) ('rebirth-empty-history-'+[Guid]::NewGuid().ToString('N'))
[EmptyHistoryFixture]::Run($testRoot)
