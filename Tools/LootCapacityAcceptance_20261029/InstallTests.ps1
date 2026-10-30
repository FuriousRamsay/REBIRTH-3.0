param([Parameter(Mandatory=$true)][string]$TargetRoot)
$ErrorActionPreference='Stop';$root=(Resolve-Path "$PSScriptRoot/../..").Path;$target=(Resolve-Path -LiteralPath $TargetRoot).Path
foreach($entry in Get-Content "$PSScriptRoot/source-contract.json" -Raw|ConvertFrom-Json){if((Get-FileHash -LiteralPath (Join-Path $target $entry.path)).Hash-ne$entry.sha256){throw "Reviewed UI contract drift: $($entry.path)"}}
$manifest=Get-Content "$PSScriptRoot/test-files.json" -Raw|ConvertFrom-Json
foreach($entry in $manifest){
 if(($entry.path -notlike 'Tools/LootCapacityAcceptance_20261029/*'-and$entry.path -notlike '_Documentation/LootCapacityAcceptance_20261029/*')-or@($entry.path.Split('/')|Where-Object {$_-eq'..'}).Count){throw 'Non-test destination forbidden'}
 if((Get-FileHash -LiteralPath (Join-Path $root $entry.path)).Hash-ne$entry.proposal){throw "Package changed: $($entry.path)"}
 $destination=Join-Path $target $entry.path
 if((Test-Path -LiteralPath $destination)-and(Get-FileHash -LiteralPath $destination).Hash-ne$entry.proposal){throw "Different existing test file: $($entry.path)"}
}
$manifestDestination=Join-Path $target 'Tools/LootCapacityAcceptance_20261029/test-files.json'
if((Test-Path -LiteralPath $manifestDestination)-and(Get-FileHash -LiteralPath $manifestDestination).Hash-ne(Get-FileHash -LiteralPath "$PSScriptRoot/test-files.json").Hash){throw 'Existing test manifest differs'}
foreach($entry in $manifest){$destination=Join-Path $target $entry.path;if(!(Test-Path -LiteralPath $destination)){New-Item -ItemType Directory -Force (Split-Path $destination)|Out-Null;Copy-Item -LiteralPath (Join-Path $root $entry.path) -Destination $destination}}
if(!(Test-Path -LiteralPath $manifestDestination)){Copy-Item -LiteralPath "$PSScriptRoot/test-files.json" -Destination $manifestDestination}
'Test/research files installed only; no runtime C#/XML/save/build/game action.'
