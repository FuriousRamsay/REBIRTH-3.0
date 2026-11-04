param([switch]$Baseline)
$ErrorActionPreference='Stop'
$root=(Resolve-Path "$PSScriptRoot/../..").Path
$source=Join-Path $root 'Scripts/NPC/Foundation/RebirthNpcLifecycle.cs'
if($Baseline){$source=Join-Path $root '_Documentation/Purge/FullRegression20261102/b16-before/RebirthNpcLifecycle.cs'}
$text=Get-Content -LiteralPath $source -Raw
function Extract([string]$start,[string]$end){
  $a=$text.IndexOf($start,[StringComparison]::Ordinal)
  if($a -lt 0){throw "Missing start $start"}
  $b=$text.IndexOf($end,$a+$start.Length,[StringComparison]::Ordinal)
  if($b -lt 0){throw "Missing end $end"}
  return $text.Substring($a,$b-$a)
}
$methods=@(
  (Extract 'private static void QualifyWholeBundleAdmission()' 'public static void OnWorldAvailable()'),
  (Extract 'private static void ShutdownWorld(bool gameShutdown)' '/// <summary>' )
) -join "`n"
# Exact production callers and shutdown scope predicates, no rewritten method bodies.
"using System;using System.IO;partial class Probe {`n$methods`n}" | Set-Content -LiteralPath "$PSScriptRoot/Source.g.cs"
& dotnet run --project "$PSScriptRoot/Fixture.csproj" --no-launch-profile --no-restore -- $(if($Baseline){'baseline'}else{'candidate'})
if($LASTEXITCODE -ne 0){throw "Caller fixture failed ($LASTEXITCODE)"}
