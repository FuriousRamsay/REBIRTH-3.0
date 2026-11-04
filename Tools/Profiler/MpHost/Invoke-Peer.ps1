param([ValidateSet('server','client')][string]$Role,[string]$Label,[string]$ArgumentsJson,[ValidateSet('help','purge')][string]$Fixture='help')
$ErrorActionPreference='Stop'
$mod=(Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
$stage=Join-Path $mod ("Tools/Profiler/out/"+$Fixture+'-remote-20261102')
$control=Get-Content -LiteralPath (Join-Path $stage ($Role+'.private.json')) -Raw|ConvertFrom-Json
$env:REBIRTH_BRIDGE_RUN_TOKEN=$control.runToken
$argList=@(ConvertFrom-Json $ArgumentsJson)
$result=& powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $mod 'Tools/GameBridge/gamebridge.ps1') @argList -SessionPath $control.session
$exitCode=$LASTEXITCODE
$result|Set-Content -LiteralPath (Join-Path $stage ($Label+'.json'))
if($exitCode){throw "Bridge returned exit $exitCode; inspect saved $Label result."}
try {$parsed=($result -join "`n")|ConvertFrom-Json}catch {$parsed=$null}
if($parsed -and $parsed.PSObject.Properties['ok'] -and -not $parsed.ok){throw "Bridge rejected request; inspect saved $Label result."}
$result
