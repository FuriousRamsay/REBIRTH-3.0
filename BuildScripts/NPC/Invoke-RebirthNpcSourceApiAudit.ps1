[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$ProjectRoot,
    [Parameter(Mandatory=$true)][string]$OutputRoot
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$npcRoot = Join-Path $ProjectRoot 'Scripts\Rebirth\NPC'
if (-not (Test-Path $npcRoot)) { throw "NPC source root not found: $npcRoot" }
New-Item -ItemType Directory -Force -Path $OutputRoot | Out-Null

$patterns = [ordered]@{
    ReflectionMethod = 'GetMethod\s*\('
    ReflectionProperty = 'GetProperty\s*\('
    ReflectionField = 'GetField\s*\('
    Activator = 'Activator\.CreateInstance'
    NotImplemented = 'NotImplementedException|TODO|FIXME'
    NativeWorld = 'GameManager\.Instance\.World'
    NativeNavigator = 'getNavigator\s*\('
    NativeInventory = '\.inventory\b'
}

$rows = New-Object System.Collections.Generic.List[object]
Get-ChildItem -Path $npcRoot -Filter '*.cs' -Recurse | ForEach-Object {
    $file = $_
    $relative = $file.FullName.Substring($ProjectRoot.Length + 1)
    $lineNumber = 0
    Get-Content $file.FullName | ForEach-Object {
        $lineNumber++
        $line = $_
        foreach ($entry in $patterns.GetEnumerator()) {
            if ($line -match $entry.Value) {
                $rows.Add([pscustomobject]@{
                    Category = $entry.Key
                    File = $relative
                    Line = $lineNumber
                    Text = $line.Trim()
                })
            }
        }
    }
}

$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$csv = Join-Path $OutputRoot "REBIRTH_NPC_SOURCE_API_AUDIT_$stamp.csv"
$json = Join-Path $OutputRoot "REBIRTH_NPC_SOURCE_API_AUDIT_$stamp.json"
$summary = Join-Path $OutputRoot "REBIRTH_NPC_SOURCE_API_AUDIT_SUMMARY_$stamp.txt"
$rows | Export-Csv -NoTypeInformation -Encoding UTF8 -Path $csv
$rows | ConvertTo-Json -Depth 4 | Set-Content -Encoding UTF8 $json
@(
    'REBIRTH 3.0 NPC Framework Source/API Audit'
    "Generated UTC: $([DateTime]::UtcNow.ToString('o'))"
    "Project root: $ProjectRoot"
    "NPC source files: $((Get-ChildItem -Path $npcRoot -Filter '*.cs' -Recurse).Count)"
    "Total findings: $($rows.Count)"
    ''
    'Findings by category:'
) + ($rows | Group-Object Category | Sort-Object Name | ForEach-Object { "  $($_.Name): $($_.Count)" }) |
    Set-Content -Encoding UTF8 $summary
Write-Host "NPC source/API audit written to $OutputRoot"
