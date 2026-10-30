param([Parameter(Mandatory=$true)][string]$Python)
$ErrorActionPreference='Stop'
& $Python "$PSScriptRoot/prepare.py"
if($LASTEXITCODE){throw 'Source guard failed'}
Add-Type -TypeDefinition (Get-Content "$PSScriptRoot/out/Actual.cs" -Raw)
[Checks]::Run()
