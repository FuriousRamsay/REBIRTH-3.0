#requires -Version 7.0
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$manifest=Get-Content (Join-Path $repo '_Documentation/Purge/SUB7_BOSS_POOL_MANIFEST_20261029.json') -Raw|ConvertFrom-Json
foreach($entry in $manifest.payloads){
    $path=Join-Path $repo $entry.relative
    if(!(Test-Path -LiteralPath $path)-or(Get-FileHash -LiteralPath $path).Hash-ne$entry.sha256){throw ('Payload mismatch: '+$entry.relative)}
}
foreach($entry in $manifest.runtime){
    $path=Join-Path $manifest.productionRoot $entry.relative
    if($null-eq$entry.baseline){if(Test-Path -LiteralPath $path){throw ('New runtime target exists: '+$entry.relative)}}
    elseif(!(Test-Path -LiteralPath $path)-or(Get-FileHash -LiteralPath $path).Hash-ne$entry.baseline){throw ('Runtime baseline changed: '+$entry.relative)}
}
foreach($entry in $manifest.dependencies){
    if(!(Test-Path -LiteralPath $entry.path)-or(Get-FileHash -LiteralPath $entry.path).Hash-ne$entry.sha256){throw ('Dependency changed: '+$entry.path)}
}
'PASS payloads, production baselines and read-only dependencies. No files installed; no game/build action.'
