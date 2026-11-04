param([switch]$Baseline)
$ErrorActionPreference='Stop'
$root=(Resolve-Path "$PSScriptRoot/../..").Path
$path=Join-Path $root 'Scripts/Purge/Evidence/RebirthPoiNativeEvidence.cs'
if($Baseline){$path=Join-Path $root '_Documentation/Purge/FullRegression20261102/b19-before/RebirthPoiNativeEvidence.cs'}
$source=Get-Content -LiteralPath $path -Raw
function Slice([string]$start,[string]$end,[int]$offset=0){
 $a=$source.IndexOf($start,$offset,[StringComparison]::Ordinal);if($a -lt 0){throw "Missing $start"}
 $b=$source.IndexOf($end,$a+$start.Length,[StringComparison]::Ordinal);if($b -lt 0){throw "Missing $end"}
 $source.Substring($a,$b-$a)
}
$process=$source.IndexOf('private void ProcessCandidate(Candidate c,bool journalOnly)',[StringComparison]::Ordinal)
$branch=Slice 'if(record.State==RebirthPoiClearanceState.Cleared)' '// Persist native enrollment' $process
$same=Slice 'private static bool SameActor(' 'private static bool Advance('
$ack=Slice 'private static bool AcknowledgedLatest(' 'private void ConfirmDeaths('
"using System;using System.Linq;partial class Probe { $same`n$ack`nstatic void Process(Candidate c,RebirthPoiClearanceRecord record,Store store,int live) { object snapshot=new(); $branch } }" | Set-Content "$PSScriptRoot/Source.g.cs"
& dotnet run --project "$PSScriptRoot/Fixture.csproj" --no-restore -- $(if($Baseline){'baseline'}else{'candidate'})
if($LASTEXITCODE){throw 'Cold clear journal fixture failed'}
