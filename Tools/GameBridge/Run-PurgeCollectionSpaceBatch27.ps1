param([Parameter(Mandatory=$true)][string]$Stage,[switch]$Existing)
$ErrorActionPreference='Stop';$h=Join-Path $PSScriptRoot 'Invoke-PurgeRegression.ps1'
function B($label,$a){$prefix=if($Existing){'weapon-space-'}else{'collection-space-'};$r=& $h -Stage $Stage -Label ($prefix+$label) -Direct -ArgumentsJson (ConvertTo-Json -InputObject @($a) -Compress);return (($r -join "`n")|ConvertFrom-Json)}
$obs=B 'owner-proof' @('console','rbpurgetestobserve');$o=$obs.results[0].output[0]|ConvertFrom-Json
if($o.saveName -ne 'CodexTestPurgeFresh20261102'){throw 'Wrong disposable save'}
if(-not $Existing -and @($o.nativeSupplyCrates|Where-Object {$_.id -eq 644 -and $_.ownedLoadedCrate}).Count -ne 1){throw 'Wrong disposable crate'}
if($Existing){
 if(-not(Test-Path (Join-Path $Stage 'collection-space-storage-fixture.json'))){throw 'No prior fixture receipt'}
 $null=B 'return-to-storage' @('console','teleport 189 43 248');$null=B 'arrival-storage' @('surroundings')
}
$cell=B 'cell' @('blockdamage','x=191','y=42','z=248');$floor=B 'floor' @('blockdamage','x=191','y=41','z=248')
if($floor.block -ne 'terrForestGround'){throw 'Fixture support changed'}
if($Existing){if($cell.block -ne 'cntWoodWritableCrate'){throw 'Expected existing test storage'}}
else{
 if($cell.block -ne 'treeShortGrass'){throw 'Fixture cell changed: no overwrite'}
 $null=B 'storage-fixture' @('setblock','x=191','y=42','z=248','name=cntWoodWritableCrate')
}
$null=B 'storage-open' @('activate','x=191','y=42','z=248','block=1')
Start-Sleep -Seconds 2
$ui=B 'storage-ui' @('uitree');$items=@($ui.nodes|Where-Object {$_.id -like 'contextBackpackGridSlot*' -and $_.item.name -in @('gunRifleT0PipeRifle','gunMGT0PipeMachineGun','gunHandgunPistolAdmin')}|Select-Object -First 8)
if($items.Count -ne 8){throw 'Need eight actual test weapons; no arbitrary item movement'}
$i=0
foreach($source in $items){
 $ui=B ('before-'+$i) @('uitree');$destination=@($ui.nodes|Where-Object {$_.id -like 'contextContainerGridSlot*' -and -not $_.item}|Select-Object -First 1)
 if($destination.Count -ne 1 -or $destination[0].item){throw 'Storage destination not empty'}
 $null=B ('store-'+$i) @('drag',('from.path='+$source.path),('to.path='+$destination[0].path));$i++
}
$null=B 'stored-ui' @('uitree');$null=B 'close' @('key','name=Escape');$null=B 'after-inventory' @('state','-Sections','player,inventory')
$null=B 'crate-approach' @('walkto','x=189','z=248');$null=B 'arrival' @('surroundings')
