# One launch for a sequential batch. Without -Run this only validates scripts.
[CmdletBinding()]
param(
    [switch]$Run, [switch]$Launch, [switch]$Reset, [switch]$QuitAfterBatch,
    [string]$Plan,
    [string[]]$Case, [string[]]$Group, [string[]]$Ack,
    [string]$Session,
    [string]$Python = "$env:USERPROFILE/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe"
)
$ErrorActionPreference='Stop'
if ($QuitAfterBatch -and (-not $Run -or -not $Launch -or $Session)) {
    throw '-QuitAfterBatch requires -Run -Launch and the launcher session'
}
if (-not $Plan) { $Plan=Join-Path $PSScriptRoot 'batches/current.json' }
if (-not $Session) { $Session=Join-Path $PSScriptRoot 'out/session.json' }
if (-not (Test-Path -LiteralPath $Python)) { throw 'Set -Python to a real Python 3 executable' }
$argsList=@((Join-Path $PSScriptRoot 'run_gamebridge_batch.py'),'--plan',$Plan)
foreach ($value in $Case) { foreach ($part in $value.Split(',')) { if ($part.Trim()) { $argsList+=@('--case',$part.Trim()) } } }
foreach ($value in $Group) { foreach ($part in $value.Split(',')) { if ($part.Trim()) { $argsList+=@('--group',$part.Trim()) } } }
foreach ($value in $Ack) { foreach ($part in $value.Split(',')) { if ($part.Trim()) { $argsList+=@('--ack',$part.Trim()) } } }
& $Python @argsList --session $Session --preflight
if ($LASTEXITCODE) { throw 'Batch preflight failed; game was not launched' }
if (-not $Run) { if ($Launch) { throw '-Launch requires -Run' }; return }
$bridge = Join-Path $PSScriptRoot 'gamebridge.ps1'
if ($Launch) {
    $launchArgs=@('-NoProfile','-ExecutionPolicy','Bypass','-File',$bridge,'launch')
    if ($Reset) { $launchArgs+='-Reset' }
    & powershell @launchArgs
    if ($LASTEXITCODE) { throw 'Single batch launch failed' }
} elseif ($Reset) { throw '-Reset requires -Launch' }
$sessionPin = $null
if ($QuitAfterBatch) {
    $launchedSession = Get-Content -LiteralPath $Session -Raw | ConvertFrom-Json
    $sessionPin = Join-Path $launchedSession.outputDir 'session.json'
    $Session = $sessionPin
}
$batchExit = 1
try {
    & $Python @argsList --session $Session
    $batchExit = $LASTEXITCODE
} finally {
    if ($sessionPin) {
        $shutdown = [Diagnostics.Stopwatch]::StartNew()
        & powershell -NoProfile -ExecutionPolicy Bypass -File $bridge quit -SessionPath $sessionPin
        $quitExit = $LASTEXITCODE
        [Console]::Out.WriteLine((@{ event = 'batch-shutdown'; milliseconds = $shutdown.ElapsedMilliseconds; exitCode = $quitExit } | ConvertTo-Json -Compress))
        if ($quitExit -and -not $batchExit) { $batchExit = $quitExit }
    }
}
exit $batchExit
