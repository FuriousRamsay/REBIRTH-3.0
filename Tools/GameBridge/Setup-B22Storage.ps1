param([Parameter(Mandatory=$true)][string]$Stage)
$ErrorActionPreference='Stop';$h=Join-Path $PSScriptRoot 'Invoke-PurgeRegression.ps1'
function B($label,$argsList){$r=& $h -Stage $Stage -Label $label -ArgumentsJson (ConvertTo-Json -InputObject @($argsList) -Compress);return (($r -join "`n")|ConvertFrom-Json)}
$slot=B 'fixture-storage-air-check' @('blockdamage','x=50','y=41','z=-255')
if($slot.block -ne 'air' -and -not ($slot.block -like 'treeTallGrass*' -and $slot.maxDamage -le 1)){throw "Refusing to replace existing $($slot.block) with fixture storage"}
$null=B 'fixture-storage-place' @('setblock','name=cntIronWritableCrateInsecure','x=50','y=41','z=-255')
Start-Sleep -Seconds 1
$null=B 'fixture-storage-open' @('activate','x=50','y=41','z=-255','block=1')
Start-Sleep -Seconds 1
$ui=B 'fixture-storage-ui' @('uitree')
if($ui.openWindows -notcontains 'looting'){throw 'Fixture storage did not open'}
& (Join-Path $PSScriptRoot 'Stash-BatchFixtureInventory.ps1') -Stage $Stage -Count 24 -Prefix fixturecrate

