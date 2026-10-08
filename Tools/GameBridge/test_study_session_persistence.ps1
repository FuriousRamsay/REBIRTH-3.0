$ErrorActionPreference='Stop'
# Uses actual current bool-returning reading/audio persistence methods.
# This is an isolated adapter fixture; it never launches or controls the game.
$taskRunner=Join-Path $PSScriptRoot 'test_study_saved_progress.mjs'
& node $taskRunner
if($LASTEXITCODE -ne 0){throw "Study persisted-progress fixture failed with exit code $LASTEXITCODE"}