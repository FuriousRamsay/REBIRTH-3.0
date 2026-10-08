$ErrorActionPreference = 'Stop'
Add-Type -Path (Join-Path $PSScriptRoot '../../Scripts/Crafting/Cooking/RebirthCookingPreparation.cs')
function Assert-Cooking($condition, $message) { if (-not $condition) { throw $message } }
$prep = New-Object RebirthCookingPreparation
$prep.Begin('stew', 0)
Assert-Cooking (-not $prep.Tick($true, 'stew', 9)) 'Preparation completed early'
$prep.Cancel()
Assert-Cooking (-not $prep.Tick($true, 'stew', 11)) 'Closed preparation finished later'
Assert-Cooking ($prep.Remaining('stew', 11) -eq 0) 'Closing granted a benefit'
$prep.Begin('stew', 20)
Assert-Cooking (-not $prep.Tick($false, 'stew', 30)) 'Closing at completion granted a benefit'
$prep.Begin('stew', 40)
Assert-Cooking (-not $prep.Tick($true, 'pie', 50)) 'Changing recipe completed preparation'
$prep.Begin('stew', 60)
Assert-Cooking ($prep.Tick($true, 'stew', 70)) 'Valid preparation did not complete'
Assert-Cooking ($prep.Remaining('stew', 70) -eq 300) 'Incorrect benefit duration'
$prep.Begin('stew', 80)
$prep.Cancel()
Assert-Cooking ($prep.Remaining('stew', 90) -eq 280) 'Aborted refresh changed existing benefit'
Assert-Cooking ($prep.Remaining('pie', 90) -eq 0) 'Benefit leaked to another recipe'
Assert-Cooking ($prep.Remaining('stew', 371) -eq 0) 'Benefit did not expire'
$prep.Begin('stew', 400)
Assert-Cooking ($prep.Progress(405) -eq 0.5) 'Reopening did not start fresh'
$prep.Reset()
Assert-Cooking (-not $prep.IsPreparing) 'World reset retained pending work'
Write-Output 'Preparation checks passed: cancel, close at deadline, change recipe, refresh, expiry, reset.'
