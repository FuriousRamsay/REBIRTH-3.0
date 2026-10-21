# Recover only the exact batch owner; never steal another run's lease.
[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$ControlFile,[switch]$Quit)
$ErrorActionPreference='Stop'
$control=Get-Content -LiteralPath $ControlFile -Raw | ConvertFrom-Json
$oldSession=$env:REBIRTH_BRIDGE_SESSION; $oldToken=$env:REBIRTH_BRIDGE_RUN_TOKEN
try {
    $env:REBIRTH_BRIDGE_SESSION=$control.session
    $env:REBIRTH_BRIDGE_RUN_TOKEN=$control.runToken
    $bridge=Join-Path $PSScriptRoot 'gamebridge.ps1'
    & powershell -NoProfile -ExecutionPolicy Bypass -File $bridge stop
    if ($LASTEXITCODE) { throw 'Owner input cleanup failed; inspect pinned session' }
    if ($Quit) {
        & powershell -NoProfile -ExecutionPolicy Bypass -File $bridge quit
    } else {
        & powershell -NoProfile -ExecutionPolicy Bypass -File $bridge call POST /batch action=release
    }
    if ($LASTEXITCODE) { throw 'Lease remains held (possibly uncertain timed-out action); inspect before an owned quit' }
    Remove-Item -LiteralPath $ControlFile
} finally {
    $env:REBIRTH_BRIDGE_SESSION=$oldSession; $env:REBIRTH_BRIDGE_RUN_TOKEN=$oldToken
}
