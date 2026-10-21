# All preparation/verification happens before any launch. This script never launches the game.
[CmdletBinding()]
param([switch]$Build,[string]$Python="$env:USERPROFILE/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe")
$ErrorActionPreference='Stop'
if (Get-Process -Name '7DaysToDie*' -ErrorAction SilentlyContinue) { throw 'Close game before offline preparation' }
& $Python (Join-Path $PSScriptRoot 'prepare_gamebridge_batches.py')
if ($LASTEXITCODE) { throw 'Inventory generation failed' }
& $Python (Join-Path $PSScriptRoot 'run_gamebridge_batch.py') --preflight
if ($LASTEXITCODE) { throw 'Preflight failed' }
& $Python -m unittest discover -s $PSScriptRoot -p test_gamebridge_batch.py -v
if ($LASTEXITCODE) { throw 'Batch orchestration tests failed' }
& $Python -m unittest discover -s $PSScriptRoot -p test_local_fixture_batch.py -v
if ($LASTEXITCODE) { throw 'Local fixture tests failed' }
foreach ($test in @('test_batch_lease.ps1','test_batch_ownership.ps1','test_assertion_guards.ps1','test_players_paging_reentry.ps1','test_mobile_source_classification.ps1','test_mobile_empty_history.ps1','test_mobile_save_clock.ps1','test_native_circuits.ps1','test_admission_progress.ps1')) {
    $testShell=if ($test -in @('test_players_paging_reentry.ps1','test_mobile_source_classification.ps1','test_mobile_empty_history.ps1','test_mobile_save_clock.ps1','test_native_circuits.ps1','test_admission_progress.ps1')) { 'pwsh' } else { 'powershell' }
    & $testShell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot $test)
    if ($LASTEXITCODE) { throw "Offline test failed: $test" }
}
if ($Build) {
    & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'gamebridge.ps1') build
    if ($LASTEXITCODE) { throw 'Build failed' }
}
[Console]::WriteLine('{"passed":true,"gameLaunched":false}')
