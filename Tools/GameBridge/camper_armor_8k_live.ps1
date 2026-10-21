param([int]$Entity=192)
$ErrorActionPreference='Stop'
$bridge=Join-Path $PSScriptRoot 'gamebridge.ps1'
$records=New-Object System.Collections.Generic.List[object]
$plan=Import-Csv (Join-Path $PSScriptRoot '../../_Documentation/CamperTurrets/ARMOR_THRESHOLDS_8K_20261019.csv')
function ObserveArmor([string]$Mode,[int]$Expected=-1) {
 $raw=& powershell -ExecutionPolicy Bypass -File $bridge console "rbarmorphoto $Entity $Mode"
 if($LASTEXITCODE -ne 0){throw $raw}
 $reply=$raw|ConvertFrom-Json
 $output=($reply.results|ForEach-Object {$_.output}) -join "`n"
 if($reply.hadError -or $output -match 'ERROR rbarmorphoto'){throw $output}
 $match=[regex]::Match($output,'armorPieces=(\d+) visiblePieces=(\d+) visibilityValid=(True|False)')
 if(!$match.Success -or $match.Groups[1].Value -ne '54' -or $match.Groups[3].Value -ne 'True'){throw "Invalid native mesh observation: $output"}
 $actual=[int]$match.Groups[2].Value
 $records.Add([pscustomobject]@{mode=$Mode;expected=$Expected;visible=$actual;output=$output})
 if($Expected -ge 0 -and $actual -ne $Expected){throw "Expected $Expected pieces, observed $actual at $Mode"}
 Write-Output "PASS $Mode visible=$actual"
}
try {
 ObserveArmor plain
 foreach($hp in @(24000,23158,23157,22822,22821,16000,8001,8000,7999)) {
  $expected=@($plan|Where-Object {$hp -gt [int]$_.HideAtHP}).Count
  ObserveArmor "health=$hp" $expected
 }
 ObserveArmor 'health=24000' 54
} finally {
 $records|ConvertTo-Json -Depth 8|Set-Content (Join-Path $PSScriptRoot '../../_Documentation/CamperTurrets/ARMOR_8K_NATIVE_RESULTS_20261019.json')
}
