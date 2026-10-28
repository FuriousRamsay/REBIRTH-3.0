param(
 [Parameter(Mandatory=$true)][string]$ObservationPath,
 [int]$Pinned=-1,[int]$Selected=-1,[int]$Selectable=-1,[int]$Supported=-1,
 [ValidateSet('any','on','off')][string]$Requested='any',
 [int]$RequiredWatts=-1,[int]$ActiveDemand=-1,
 [switch]$AllOwnedLightsOff,[switch]$RequireSettled,
 [int]$X,[int]$Y,[int]$Z,
 [ValidateSet('any','on','off')][string]$LightState='any',
 [ValidateSet('any','Automatic','On','Off')][string]$Choice='any'
)
$ErrorActionPreference='Stop'
# Offline assertion of an existing Bridge /energy JSON capture only.
$o=Get-Content -LiteralPath $ObservationPath -Raw|ConvertFrom-Json
if($o.ok-ne$true-or$o.authoritative-ne$true){throw 'An authoritative successful energy observation is required.'}
$checks=0
function Expect($actual,$expected,[string]$label){if($null-eq$actual-or$actual-ne$expected){throw "$label expected=$expected actual=$actual"};$script:checks++}
foreach($pair in @(@('pinnedOnLights',$Pinned),@('selectedLights',$Selected),@('selectableLights',$Selectable),@('supportedLights',$Supported),@('requiredWatts',$RequiredWatts),@('activeMeteredDemand',$ActiveDemand))){if($pair[1]-ge0){Expect $o.poi.($pair[0]) $pair[1] ('poi.'+$pair[0])}}
if($Requested-ne'any'){Expect $o.poi.requestedOn ($Requested-eq'on') 'poi.requestedOn'}
if($RequireSettled){Expect $o.held $false 'runtime hold';Expect $o.devices.leaseWitnessPending $false 'device witness'}
if($AllOwnedLightsOff){
 Expect $o.devices.truncated $false 'complete powered node list';Expect $o.devices.ordinaryPoiLightsTruncated $false 'complete ordinary list'
 foreach($row in $o.devices.nodes){if($row.ownedByCamper-eq$true-and$row.poiLight-eq$true){Expect $row.powered $false ('owned native light '+($row.position-join','))}}
 foreach($row in $o.devices.ordinaryPoiLights){if($row.ownedByCamper-eq$true){Expect $row.nativeLightOn $false ('owned ordinary light '+($row.position-join','))}}
}
if($LightState-ne'any'-or$Choice-ne'any'){
 if(!$PSBoundParameters.ContainsKey('X')-or!$PSBoundParameters.ContainsKey('Y')-or!$PSBoundParameters.ContainsKey('Z')){throw 'Light assertions require explicit X Y Z.'}
 $rows=@(@($o.devices.nodes)+@($o.devices.ordinaryPoiLights)|Where-Object{$_.position[0]-eq$X-and$_.position[1]-eq$Y-and$_.position[2]-eq$Z})
 Expect $rows.Count 1 'exact current light row'
 if($Choice-ne'any'){Expect $rows[0].manualChoice $Choice 'manual choice'}
 if($LightState-ne'any'){$physical=if($null-ne$rows[0].PSObject.Properties['nativeLightOn']){$rows[0].nativeLightOn}else{$rows[0].powered};Expect $physical ($LightState-eq'on') 'native physical light state'}
}
if($checks-eq0){throw 'No expectations were supplied.'}
"PASS $checks saved authoritative energy assertions; no game actions performed."
