#requires -Version 7.0
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$m=Get-Content (Join-Path $repo '_Documentation/Purge/SUB7_BOSS_SIGNED_MANIFEST_20261029.json') -Raw|ConvertFrom-Json
foreach($e in $m.payloads){if((Get-FileHash -LiteralPath (Join-Path $repo $e.relative)).Hash-ne$e.sha256){throw ('Payload changed '+$e.relative)}}
foreach($e in $m.runtime){if((Get-FileHash -LiteralPath (Join-Path $m.productionRoot $e.relative)).Hash-ne$e.baseline){throw ('Runtime baseline changed '+$e.relative)}}
foreach($e in $m.dependencies){if((Get-FileHash -LiteralPath $e.path).Hash-ne$e.sha256){throw ('Dependency changed '+$e.path)}}
'PASS signed-class payload/runtime/dependency guards; no installation/build/game action.'
