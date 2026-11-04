param([Parameter(Mandatory=$true)][string]$Stage)
$ErrorActionPreference='Stop';$h=Join-Path $PSScriptRoot 'Invoke-PurgeRegression.ps1'
function B($label,$a){$r=& $h -Stage $Stage -Label $label -Direct -ArgumentsJson (ConvertTo-Json -InputObject @($a) -Compress);return (($r -join "`n")|ConvertFrom-Json)}
function O($label){$r=B $label @('console','rbpurgetestobserve');return ($r.results[0].output[0]|ConvertFrom-Json)}
$before=O 'supply-collection-before';$crates=@($before.nativeSupplyCrates|Where-Object {$_.loaded -and $_.ownedLoadedCrate});if($crates.Count -ne 1){throw 'Expected one current owned delivered crate'}
$null=B 'supply-map-before-open' @('key','name=M');$null=B 'supply-map-before-ui' @('uitree');$null=B 'supply-map-before-shot' @('screenshot','b22-owned-supply-map');$null=B 'supply-map-before-close' @('key','name=M')
$c=$crates[0];$x=[int]$c.position[0]-2;$y=[int]$c.position[1]+1;$z=[int]$c.position[2]
$null=B 'supply-collection-stage' @('console',"teleport $x $y $z")
Start-Sleep -Seconds 1;$null=B 'supply-collection-surroundings' @('surroundings')
$open=B 'supply-collection-open' @('activate',"entity=$($c.id)")
Start-Sleep -Seconds 1;$ui=B 'supply-collection-ui' @('uitree')
if($ui.openWindows -notcontains 'looting'){throw 'Native owned crate did not open'}
$null=B 'supply-collection-shot' @('screenshot','b22-owned-supply-open')
$null=B 'supply-collection-takeall' @('key','name=R')
$null=B 'supply-collection-after-ui' @('uitree')
$null=B 'supply-collection-close' @('key','name=Escape')
Start-Sleep -Seconds 2;$after=O 'supply-collection-after'
$null=B 'supply-map-after-open' @('key','name=M');$null=B 'supply-map-after-ui' @('uitree');$null=B 'supply-map-after-shot' @('screenshot','b22-after-supply-map');$null=B 'supply-map-after-close' @('key','name=M')
@{before=$before.supply;after=$after.supply;remainingCrates=$after.nativeSupplyCrates;scope='Native owned access and take-all. UI snapshots/screenshots and remaining capacity require review before acceptance.'}|ConvertTo-Json -Depth 8|Set-Content (Join-Path $Stage 'supply-collection-verdict.json')
