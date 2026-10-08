[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$teamRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$projectRoot = [IO.Path]::GetFullPath((Join-Path $teamRoot '../..'))
# Parses only. NEVER imports or invokes gamebridge.ps1.
& (Join-Path $projectRoot 'Tools/DemoReleaseQualificationTeam/Validate-ScenarioStructure.ps1') -ScenarioRoot $teamRoot
foreach ($file in Get-ChildItem -LiteralPath $teamRoot -Filter *.ps1 -File -Recurse) {
    $tokens = $null; $parseErrors = $null
    [void][System.Management.Automation.Language.Parser]::ParseFile($file.FullName, [ref]$tokens, [ref]$parseErrors)
    if ($parseErrors.Count) { throw ($parseErrors | Out-String) }
}
Write-Output 'PASS: scenario structure and PowerShell syntax only; nativeExecuted=false'
