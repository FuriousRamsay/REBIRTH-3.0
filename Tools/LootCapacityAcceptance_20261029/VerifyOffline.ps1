$ErrorActionPreference='Stop';$python='C:/Users/Etienne/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe'
& $python "$PSScriptRoot/test_acceptance.py";if($LASTEXITCODE){throw 'Offline acceptance guards failed'}
'PASS offline only; no game/session request performed'
