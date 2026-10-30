param([Parameter(Mandatory=$true)][string]$Python)
$ErrorActionPreference='Stop'
& $Python "$PSScriptRoot/prepare.py"
if($LASTEXITCODE){throw 'Preparation failed'}
Add-Type -TypeDefinition (Get-Content "$PSScriptRoot/out/Actual.cs" -Raw)
[Checks]::Run("$PSScriptRoot/out")
