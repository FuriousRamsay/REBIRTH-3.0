# One launch for a sequential batch. Without -Run this only validates scripts.
[CmdletBinding()]
param(
    [switch]$Run, [switch]$Launch, [switch]$Reset,
    [string]$Plan,
    [string[]]$Case, [string[]]$Group, [string[]]$Ack,
    [string]$Session,
    [string]$Python = "$env:USERPROFILE/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe"
)
$ErrorActionPreference='Stop'
if (-not $Plan) { $Plan=Join-Path $PSScriptRoot 'batches/current.json' }
if (-not $Session) { $Session=Join-Path $PSScriptRoot 'out/session.json' }
if (-not (Test-Path -LiteralPath $Python)) { throw 'Set -Python to a real Python 3 executable' }
$argsList=@((Join-Path $PSScriptRoot 'run_gamebridge_batch.py'),'--plan',$Plan,'--session',$Session)
foreach ($value in $Case) { foreach ($part in $value.Split(',')) { if ($part.Trim()) { $argsList+=@('--case',$part.Trim()) } } }
foreach ($value in $Group) { foreach ($part in $value.Split(',')) { if ($part.Trim()) { $argsList+=@('--group',$part.Trim()) } } }
foreach ($value in $Ack) { foreach ($part in $value.Split(',')) { if ($part.Trim()) { $argsList+=@('--ack',$part.Trim()) } } }
& $Python @argsList --preflight
if ($LASTEXITCODE) { throw 'Batch preflight failed; game was not launched' }
if (-not $Run) { if ($Launch) { throw '-Launch requires -Run' }; return }
if ($Launch) {
    $launchArgs=@('-NoProfile','-ExecutionPolicy','Bypass','-File',(Join-Path $PSScriptRoot 'gamebridge.ps1'),'launch')
    if ($Reset) { $launchArgs+='-Reset' }
    & powershell @launchArgs
    if ($LASTEXITCODE) { throw 'Single batch launch failed' }
} elseif ($Reset) { throw '-Reset requires -Launch' }
& $Python @argsList
exit $LASTEXITCODE
