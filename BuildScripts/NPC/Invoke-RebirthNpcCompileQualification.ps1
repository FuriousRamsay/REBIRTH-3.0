[CmdletBinding()]
param(
    [string]$GameRoot = $env:REBIRTH_7DTD_ROOT,
    [ValidateSet('Debug','Release')][string]$Configuration = 'Release',
    [string]$OutputRoot = '',
    [switch]$NoBuild,
    [switch]$AcceptReferenceBaseline
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = (Resolve-Path (Join-Path $scriptDir '..\..')).Path
if ([string]::IsNullOrWhiteSpace($OutputRoot)) { $OutputRoot = Join-Path $projectRoot 'BuildArtifacts\NPC-b259' }
New-Item -ItemType Directory -Force -Path $OutputRoot | Out-Null

$baselineScript = Join-Path $scriptDir 'Invoke-RebirthNpcB259Baseline.ps1'
& $baselineScript -GameRoot $GameRoot -Configuration $Configuration -OutputRoot $OutputRoot -NoBuild:$NoBuild
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$latestManifest = Get-ChildItem $OutputRoot -Filter 'REBIRTH_NPC_B259_API_BASELINE_*.json' |
    Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 1
if ($null -eq $latestManifest) { throw 'Reference manifest was not generated.' }

$lockPath = Join-Path $projectRoot 'BuildScripts\NPC\REBIRTH_NPC_B259_REFERENCE_LOCK.json'
$current = Get-Content $latestManifest.FullName -Raw | ConvertFrom-Json
$normalized = $current | Sort-Object Name | Select-Object Name,Length,FileVersion,ProductVersion,Sha256

if ($AcceptReferenceBaseline -or -not (Test-Path $lockPath)) {
    $normalized | ConvertTo-Json -Depth 4 | Set-Content -Encoding UTF8 $lockPath
    Write-Host "Accepted reference baseline: $lockPath"
} else {
    $locked = Get-Content $lockPath -Raw | ConvertFrom-Json
    $currentJson = $normalized | ConvertTo-Json -Depth 4 -Compress
    $lockedJson = ($locked | Sort-Object Name | Select-Object Name,Length,FileVersion,ProductVersion,Sha256) |
        ConvertTo-Json -Depth 4 -Compress
    if ($currentJson -ne $lockedJson) {
        $drift = Join-Path $OutputRoot 'REBIRTH_NPC_B259_REFERENCE_DRIFT.txt'
        @(
            'REBIRTH NPC b259 managed-reference drift detected.'
            "Locked baseline: $lockPath"
            "Observed manifest: $($latestManifest.FullName)"
            'Review and rerun with -AcceptReferenceBaseline only when the exact intended game build is confirmed.'
        ) | Set-Content -Encoding UTF8 $drift
        throw "Managed-reference drift detected. Report: $drift"
    }
    Write-Host 'Managed-reference lock matched.'
}

$summary = [pscustomobject]@{
    Status = if ($NoBuild) { 'ReferencesQualified_NoBuildRequested' } else { 'BuildDelegatedToBaselineScript' }
    Configuration = $Configuration
    ReferenceLock = $lockPath
    ObservedManifest = $latestManifest.FullName
    CompletedUtc = [DateTime]::UtcNow.ToString('o')
}
$summary | ConvertTo-Json -Depth 4 | Set-Content -Encoding UTF8 (Join-Path $OutputRoot 'REBIRTH_NPC_WP21_QUALIFICATION_RESULT.json')
