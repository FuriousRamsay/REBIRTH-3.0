param([Parameter(Mandatory=$true)][string]$ItemSlotPath,[string]$Label='weapon-ui')
$ErrorActionPreference='Stop'
# Run ONLY after the lead stages the chosen weapon and opens its inventory window
# in an authorized disposable game session. Path comes from a fresh uitree.
$bridge=Join-Path $PSScriptRoot '../GameBridge/gamebridge.ps1'
& $bridge hover "path=$ItemSlotPath";if($LASTEXITCODE){throw 'Hover failed'}
& $bridge uitree;if($LASTEXITCODE){throw 'UI tree failed'}
& $bridge screenshot $Label;if($LASTEXITCODE){throw 'Capture failed'}
& $bridge errors;if($LASTEXITCODE){throw 'Error query failed'}
'CAPTURE ONLY: inspect screenshot and UI values against NATIVE_VALIDATION.md. No visual/value PASS is inferred.'
