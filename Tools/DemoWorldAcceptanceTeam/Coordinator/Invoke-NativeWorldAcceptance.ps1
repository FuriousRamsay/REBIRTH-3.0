# Candidate coordinator only. Default mode is offline and never invokes the bridge.
[CmdletBinding()]
param(
 [Parameter(Mandatory=$true)][string]$Scenario,
 [switch]$NativeAuthorized,
 [string]$Preconditions,
 [string]$ExpectedScenarioSha256
)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
$toolsRoot=[IO.Path]::GetFullPath((Join-Path $root 'Tools'))+[IO.Path]::DirectorySeparatorChar
$scenarioPath=[IO.Path]::GetFullPath((Join-Path $root $Scenario))
if(-not $scenarioPath.StartsWith($toolsRoot,[StringComparison]::OrdinalIgnoreCase) -or [IO.Path]::GetExtension($scenarioPath) -ine '.json'){throw 'Scenario must be a Tools JSON file'}
$doc=Get-Content -LiteralPath $scenarioPath -Raw|ConvertFrom-Json
if([string]::IsNullOrWhiteSpace($doc.name) -or @($doc.steps).Count -eq 0){throw 'Not a runnable scenario'}
$sha=(Get-FileHash -LiteralPath $scenarioPath -Algorithm SHA256).Hash
$plan=[ordered]@{scenario=$scenarioPath;sha256=$sha;name=$doc.name;stepCount=@($doc.steps).Count;nativeExecuted=$false;acceptance='NOT_RUN';note='Runner PASS proves only asserted subset. Manual visual/custody/recovery/remote oracles still apply.'}
if(-not $NativeAuthorized){$plan|ConvertTo-Json -Depth 6;return}
# This switch records fresh human authorization supplied by the invoking lead.
# It does not grant authorization to an agent with a standing gameplay prohibition.
if([string]::IsNullOrWhiteSpace($ExpectedScenarioSha256) -or $sha -cne $ExpectedScenarioSha256){throw 'Explicit reviewed scenario SHA256 required and must still match'}
if([string]::IsNullOrWhiteSpace($Preconditions)){throw 'Operator precondition record required'}
$pre=Get-Content -LiteralPath $Preconditions -Raw|ConvertFrom-Json
foreach($field in @('humanAuthorized','disposableSaveVerified','guardOn','cursorEmpty','fixtureCustodyVerified','scenarioNotesFulfilled')){
 if($pre.$field -isnot [bool] -or $pre.$field -ne $true){throw "Missing verified precondition: $field"}
}
if($pre.gameName -cne 'CodexTest' -or [string]::IsNullOrWhiteSpace($pre.evidence) -or [string]::IsNullOrWhiteSpace($pre.operator)){throw 'CodexTest operator/evidence identity required'}
$cfg=Get-Content -LiteralPath (Join-Path $root 'Tools/GameBridge/gamebridge.config.json') -Raw|ConvertFrom-Json
if($cfg.save -cne 'CodexTest'){throw 'Configured save is not disposable CodexTest'}
$bridge=Join-Path $root 'Tools/GameBridge/gamebridge.ps1'
function ReadBridge([string[]]$arguments){
 $raw=& powershell -NoProfile -ExecutionPolicy Bypass -File $bridge @arguments
 if($LASTEXITCODE -ne 0){throw 'Bridge preflight failed'}
 $reply=($raw -join "`n")|ConvertFrom-Json
 if($reply.ok -isnot [bool] -or -not $reply.ok){throw 'Bridge did not return explicit successful JSON'}
 return $reply
}
$ping=ReadBridge @('status')
if($ping.state -cne 'ingame'){throw 'Existing authorized game must already be ingame; wrapper never launches or rebuilds'}
$before=ReadBridge @('state','-Sections','world,player,inventory,buffs,cvars,stats')
if($before.world.gameName -cne 'CodexTest' -or $before.world.gameWorld -cne $cfg.world -or $before.player.dead -ne $false){throw 'Active session differs from reviewed live disposable fixture'}
$runDir=Join-Path $PSScriptRoot ('runs/'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $runDir|Out-Null
$pre|ConvertTo-Json -Depth 20|Set-Content (Join-Path $runDir 'preconditions.json') -Encoding utf8
$before|ConvertTo-Json -Depth 30|Set-Content (Join-Path $runDir 'before.json') -Encoding utf8
& powershell -NoProfile -ExecutionPolicy Bypass -File $bridge test $scenarioPath 2>&1|Tee-Object -FilePath (Join-Path $runDir 'runner.txt')
$runnerExit=$LASTEXITCODE
$after=ReadBridge @('state','-Sections','world,player,inventory,buffs,cvars,stats')
$after|ConvertTo-Json -Depth 30|Set-Content (Join-Path $runDir 'after.json') -Encoding utf8
$plan.nativeExecuted=$true
$plan.acceptance='MANUAL_ORACLES_PENDING'
$plan['runnerExit']=$runnerExit
$plan['runnerSha256']=(Get-FileHash -LiteralPath $bridge -Algorithm SHA256).Hash
$plan['evidenceDirectory']=$runDir
$plan|ConvertTo-Json -Depth 6|Set-Content (Join-Path $runDir 'result.json') -Encoding utf8
if($runnerExit -ne 0){throw "Native runner failed ($runnerExit); retain fixture and inspect evidence at $runDir"}
$plan|ConvertTo-Json -Depth 6

