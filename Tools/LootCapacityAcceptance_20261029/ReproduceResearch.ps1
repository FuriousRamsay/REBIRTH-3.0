$ErrorActionPreference='Stop';$python='C:/Users/Etienne/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe'
& $python "$PSScriptRoot/research_legacy.py";if($LASTEXITCODE){throw 'Legacy reconstruction failed'}
& $python "$PSScriptRoot/research_current_inputs.py";if($LASTEXITCODE){throw 'Current input reconstruction failed'}
& pwsh -NoProfile -File "$PSScriptRoot/GenerateCurrent.ps1";if($LASTEXITCODE){throw 'Actual source generation replay failed'}
& $python "$PSScriptRoot/classify_inherited.py";if($LASTEXITCODE){throw 'Full250 classification failed'}
'Completed offline research against NOW-installed source; newer lead fixes may legitimately change the frozen snapshot counts'
