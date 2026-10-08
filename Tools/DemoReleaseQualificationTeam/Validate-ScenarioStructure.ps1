# Offline structural validator. NEVER imports or invokes gamebridge.ps1.
[CmdletBinding()]
param([string]$ScenarioRoot)
if ([string]::IsNullOrWhiteSpace($ScenarioRoot)) { $ScenarioRoot = $PSScriptRoot }
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$runner = Get-Content -LiteralPath (Join-Path $projectRoot 'Tools/GameBridge/gamebridge.ps1') -Raw
$steps = @('action','markItem','expectItemDelta','markSkill','expectSkillGain','expectChallenge','console','wait','look','expectCvar','waitFor','expectStat','expectLivingEntities','activateNearestEntity','expectThreats','expectBuff','expectNoBuff','expectItem','expectNoItem','ui','expectWindowOpen','screenshot','expectLog','expectNoErrors','expectUi','waitForUi','expectNoUi','expectTarget','expectSkill')
$map = [regex]::Match($runner, '(?s)\$KvCommands\s*=\s*@\{(.*?)\r?\n\}').Groups[1].Value
if (!$map) { throw 'Unable to retrieve bridge command map' }
$commands = @([regex]::Matches($map, '(\w+)\s*=\s*@\(') | ForEach-Object { $_.Groups[1].Value })
$results = @()
foreach ($file in Get-ChildItem -LiteralPath $ScenarioRoot -Filter *.json -File -Recurse) {
  if ($file.FullName -match '[\\/]offline-fixtures[\\/]') { continue }
  $doc = Get-Content -LiteralPath $file.FullName -Raw | ConvertFrom-Json
  # Evidence contracts/manifests are intentionally not runner scenarios.
  if (!$doc.PSObject.Properties['steps']) { continue }
  if ([string]::IsNullOrWhiteSpace($doc.name) -or @($doc.steps).Count -eq 0) { throw "Invalid scenario: $($file.FullName)" }
  $assertions = 0; $actions = 0; $i = 0
  foreach ($step in $doc.steps) {
    $i++; $keys = @($step.PSObject.Properties)
    if ($keys.Count -ne 1) { throw "Invalid step $i in $($file.Name)" }
    $kind = $keys[0].Name; $arg = $keys[0].Value
    if ($kind.StartsWith('_')) { continue }
    if ($kind -notin $steps -and $kind -notin $commands) { throw "Unknown step $kind in $($file.Name)" }
    if ($kind -eq 'action' -and $arg.command -notin $commands) { throw "Unknown command $($arg.command) in $($file.Name)" }
    if ($kind -match '^expect|^waitFor') { $assertions++ }
    if ($kind -eq 'action' -or $kind -in $commands) { $actions++ }
  }
  $results += [pscustomobject]@{file=$file.FullName;steps=$i;assertions=$assertions;actions=$actions;status='STRUCTURE_ONLY';nativeExecuted=$false}
}
$results | ConvertTo-Json -Depth 4


