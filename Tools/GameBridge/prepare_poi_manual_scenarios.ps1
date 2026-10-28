param([Parameter(Mandatory=$true)][string]$FixturePath,[string]$OutputDirectory=(Join-Path $PSScriptRoot 'tests/generated_poi_manual'))
$ErrorActionPreference='Stop'
# JSON preparation only. Never contacts, starts or controls the game.
$f=Get-Content -LiteralPath $FixturePath -Raw|ConvertFrom-Json
if($f.entity-le0-or$f.supported-lt2-or$f.slider.leftX-ge$f.slider.rightX-or
   $f.commandMode-notin@('single','radial')-or$f.fixtureKind-notin@('PrefabPlain','PlacedPlain','PlacedPowered')-or
   [string]::IsNullOrWhiteSpace($f.light.block)-or[string]::IsNullOrWhiteSpace($f.sourceOnText)-or[string]::IsNullOrWhiteSpace($f.sourceOffText)){
 throw 'Supply an observed disposable fixture: entity, supported>=2, slider leftX/rightX/y, commandMode single|radial, fixtureKind, light x/y/z/block, approach x/z and actual sourceOnText/sourceOffText.'
}
New-Item -ItemType Directory -Path $OutputDirectory -Force|Out-Null
function SavePhase([string]$name,[string]$preconditions,[object[]]$steps){
 $data=[ordered]@{name=$name;stopOnFailure=$true;preconditions=('NOT RUN. Guard stays enabled. '+$preconditions);steps=$steps}
 $path=Join-Path $OutputDirectory ($name+'.json');$data|ConvertTo-Json -Depth 15|Set-Content -LiteralPath $path
 Get-Content -LiteralPath $path -Raw|ConvertFrom-Json|Out-Null
}
function SnapshotStep{ @{action=@{command='energy';args=@{entity=$f.entity}}} }
function LightSteps([bool]$powered){
 $steps=@(@{action=@{command='walkto';args=@{x=$f.approach.x;z=$f.approach.z}}},
  @{action=@{command='lookat';args=@{x=$f.light.x;y=$f.light.y;z=$f.light.z;block=1}}})
 if($f.commandMode-eq'single'){$steps+=@{key=@{name='E'}}}
 else{$steps+=@{action=@{command='radialselect';args=@{x=$f.light.x;y=$f.light.y;z=$f.light.z;block=1;command='light'}}}}
 $steps+=@{wait=2}
 $row=@{x=$f.light.x;y=$f.light.y;z=$f.light.z;block=$f.light.block;powered=$powered}
 if($f.fixtureKind-eq'PrefabPlain'){$steps+=@{expectEnergy=@{entity=$f.entity;lamps=@($row);timeout=30}}}
 elseif($f.fixtureKind-eq'PlacedPowered'){$row.owned=$true;$steps+=@{expectEnergy=@{entity=$f.entity;nodes=@($row);timeout=30}}}
 # Placed plain lamps are in devices.ordinaryPoiLights, whereas the existing
 # runner's lamp assertion is authored-discovery based. Capture that row and
 # inspect nativeLightOn/ownedByCamper; do not fabricate an automated PASS.
 $steps+=SnapshotStep;$steps+=@{screenshot=('poi_native_light_'+$powered)};$steps+=@{expectNoErrors=$true};return $steps
}
function UiCountSteps([int]$count,[bool]$right=$false){
 @(@{expectWindowOpen='rebirthCamperEnergyManagement'},
   @{click=@{x=$(if($right){$f.slider.rightX}else{$f.slider.leftX});y=$f.slider.y}},
   @{waitForUi=@{id='energyPoiCount';window='rebirthCamperEnergyManagement';text=('POI lights selected: '+$count+' / '+$f.supported);timeout=30}},
   (SnapshotStep),@{screenshot=('poi_slider_count_'+$count)},@{expectNoErrors=$true})
}
$ready='Lead authorizes disposable runtime; cleararea+restore completed with guard enabled. One owned Camp camper; one enabled fueled generator/charged bank with >2min reserve for the FULL pool. No independent native wiring. No initial manual choices. Record supported count and real fixture coordinates/UI screen points. '
SavePhase '01_poi_zero_selection' ($ready+'Energy open; global POI request ON; verify left-edge native pointer selects zero.') (UiCountSteps 0)
SavePhase '01b_poi_global_off_before_individual' 'After phase01 wait until the saved energy observation is settled (held=false and leaseWitnessPending=false); Energy remains open with the global request ON. This phase explicitly clears the request before the individual ON test.' @(
 @{expectWindowOpen='rebirthCamperEnergyManagement'},
 @{waitForUi=@{id='energyPoiLabel';window='rebirthCamperEnergyManagement';text='Turn POI Lights Off';timeout=30}},
 @{click=@{id='energyPoiLights';window='rebirthCamperEnergyManagement'}},
 @{waitForUi=@{id='energyPoiLabel';window='rebirthCamperEnergyManagement';text='Turn POI Lights On';timeout=30}},
 (SnapshotStep),@{expectNoErrors=$true})
SavePhase '02_poi_native_on' 'After phase01b close Energy through native UI. Target fixture OFF and its light command is the sole enabled command for single mode; otherwise use radial mode. This ON must start only pins when the global request was OFF.' (LightSteps $true)
SavePhase '03_poi_pinned_minimum' 'After phase02 reopen Energy through native navigation; one pinned light and no other manual choices. Left edge must remain1.' (UiCountSteps 1)
SavePhase '04_poi_native_off' 'Close Energy; same fixture currently manually ON. Native action removes its pin and excludes it.' (LightSteps $false)
$maxSteps=UiCountSteps ($f.supported-1) $true
SavePhase '05_poi_off_excluded_at_maximum' 'Reopen Energy; master POI request ON with adequate reserve. Right edge excludes the manually OFF fixture; selected total must be supported-1. Inspect captured physical row: target remains OFF while other automatically selected lamps turn ON.' $maxSteps
SavePhase '06_poi_zero_after_off' 'Energy remains open after phase05. Zero is reachable because no pins remain.' (UiCountSteps 0)
SavePhase '07_poi_reenable_pin' 'Close Energy. Same excluded fixture is OFF. Native action explicitly enables it and pins it; total1.' (LightSteps $true)
$bankSteps=@((UiCountSteps $f.supported $true))+@(@{expectWindowOpen='rebirthCamperEnergyManagement'},
 @{click=@{id='energyPower';window='rebirthCamperEnergyManagement'}},
 @{waitForUi=@{id='energyPowerLabel';window='rebirthCamperEnergyManagement';text=$f.sourceOnText;timeout=30}},
 @{waitForUi=@{id='energyPoiLabel';window='rebirthCamperEnergyManagement';text='Turn POI Lights On';timeout=30}},
 (SnapshotStep),@{screenshot='poi_bank_off_pins_preserved'},
 @{click=@{id='energyPower';window='rebirthCamperEnergyManagement'}},
 @{waitForUi=@{id='energyPowerLabel';window='rebirthCamperEnergyManagement';text=$f.sourceOffText;timeout=30}},
 @{waitForUi=@{id='energyPoiLabel';window='rebirthCamperEnergyManagement';text='Turn POI Lights On';timeout=30}},
 @{wait=2},(SnapshotStep),@{screenshot='poi_bank_on_does_not_restart_lights'},@{expectNoErrors=$true})
SavePhase '08_poi_bank_off_on_no_restart' 'Reopen Energy after phase07; the sole bank is selected and ON. Both captures must show target OFF after bank OFF; bank ON alone keeps requestedOn=false and native light OFF. Inspect energy JSON and screenshot, not only the caption.' $bankSteps
SavePhase '09_poi_unpin_while_dark' 'Close Energy after phase08. Bank ON but master request remains OFF; target is dark with saved manual ON. Native action must unpin it despite actual OFF state.' (LightSteps $false)
SavePhase '10_poi_zero_after_dark_unpin' 'Reopen Energy after phase09. Left edge must reach0; capture poi.pinnedOnLights=0,selectedLights=0 and native target OFF.' (UiCountSteps 0)
'Prepared 11 phased JSON scenarios. NOT RUN. Staging between phases uses normal native navigation; inspect each energy snapshot. Repeat all fixture kinds, multiplayer ownership, replacement/reset/reload and unavailable-power cases from the feature report.'
