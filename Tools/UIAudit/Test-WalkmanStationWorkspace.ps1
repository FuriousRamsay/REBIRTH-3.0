$ErrorActionPreference='Stop'
[xml]$routing=Get-Content (Join-Path $PSScriptRoot '../../Config/XUi_InGame/xui.xml') -Raw
[xml]$windows=Get-Content (Join-Path $PSScriptRoot '../../Config/XUi_InGame/windows.xml') -Raw
[xml]$stations=Get-Content (Join-Path $PSScriptRoot '../../Config/XUi_InGame/station_workspace.xml') -Raw
[xml]$templates=Get-Content (Join-Path $PSScriptRoot '../../Config/XUi_InGame/station_templates.xml') -Raw
foreach($name in 'workbench','cementMixer','chemistryStation','forge','forge_nosmelting'){
 $xpath="/xui/window_group[@name='workstation_$name']"
 $entries=@($routing.SelectNodes('//append')|Where-Object {$_.GetAttribute('xpath') -eq $xpath});$last=$entries[-1]
 if(!$last.SelectSingleNode('window[starts-with(@name,"rebirthStationRoot")]') -or $last.SelectNodes('window').Count -ne 2){throw "Station still routes native panels: $name"}
}
$roots=$stations.SelectNodes('//window');if($roots.Count -ne 5){throw 'Station variants missing'}
foreach($r in $roots){if($r.SelectNodes('rebirth_station_core').Count -ne 1 -or $r.SelectNodes('.//*[@controller="CraftingListInfo"]').Count){throw 'Duplicated station controls'}}
$core=$templates.SelectSingleNode('//rebirth_station_core/rect')
foreach($name in 'windowCraftingList','craftingInfoPanel','stationItemInfo','stationEmpty','rebirthCraftingInventoryRegion','rebirthCraftingPagerScroll'){
 if(!$core.SelectSingleNode(".//*[@name='$name']")){throw "Shared station control missing: $name"}
}
if(!$core.SelectSingleNode('.//rect[@name="stationRecipeDescription"]//defaultscrollbar') -or !$core.SelectSingleNode('.//rect[@name="stationItemDescriptionReader"]//defaultscrollbar')){throw 'Station description scrolling absent'}
$music=$windows.SelectSingleNode('//window[@name="rebirthMusicLibraryRoot"]')
foreach($prefix in @(@('library',24),@('cassetteBag',60))){for($i=0;$i -lt $prefix[1];$i++){
 if(!$music.SelectSingleNode(".//button[@name='$($prefix[0])$i']") -or !$music.SelectSingleNode(".//item_stack[@name='$($prefix[0])Display$i' and @controller='RebirthReadonlyItemSlot, RebirthUtils']")){throw 'Cassette pool control missing'}
}}
foreach($name in 'cassetteSelectedDisplay','cassetteDragDisplay'){if(!$music.SelectSingleNode(".//item_stack[@name='$name' and @controller='RebirthReadonlyItemSlot, RebirthUtils']")){throw 'Manual cassette preview remains'}}
foreach($name in 'libraryScrollbar','cassetteBagScrollbar'){if(!$music.SelectSingleNode(".//rect[@name='$name' and @controller='RebirthDiscordScrollbar, RebirthUtils']/defaultscrollbar")){throw 'Cassette shared scrollbar missing'}}
$bag=$music.SelectSingleNode('rect[@name="cassetteBackpackPanel"]');if($bag.SelectNodes('button').Count -ne 1 -or !$bag.SelectSingleNode('button[@name="cassetteSort"]')){throw 'Cassette Backpack toolbar must contain only Sort'}
[xml]$audio=Get-Content (Join-Path $PSScriptRoot '../../Config/_Survivor/audiobooks.xml') -Raw
[xml]$tracks=Get-Content (Join-Path $PSScriptRoot '../../Config/_Survivor/music_cassettes.xml') -Raw
$audioCount=$audio.SelectNodes('//audiobook').Count;$musicCount=$tracks.SelectNodes('//cassette').Count
$s=Get-Content (Join-Path $PSScriptRoot '../../Scripts/Survivor/Domain/RebirthAudiobookLibraryState.cs') -Raw
$capacity=[int][regex]::Match($s,'Capacity = (\d+)').Groups[1].Value
if($capacity -lt [math]::Ceiling(1.5*$audioCount) -or 24 -lt $musicCount -or $capacity -gt 255){throw 'Capacity/wire count insufficient'}
"PASS: five native station routes, one shared core, native cassette cells/previews, both shared scrollbars, Sort-only cassette toolbar; $capacity audio slots plus 24 music slots cover $audioCount audiobooks and $musicCount songs. Source/layout audit; runtime remains manual."

if($stations.SelectSingleNode('//window[@name="rebirthStationRootForgeNoSmelting"]//rect[@name="windowForgeInput"]')){throw 'Non-smelting forge exposes smelting input'}
foreach($grid in $stations.SelectNodes('//*[@controller="WorkstationToolGrid"]')){
 foreach($slot in $grid.SelectNodes('item_stack')){
  if($slot.controller -ne 'RequiredItemStack'){throw 'Native tool grid child must derive from RequiredItemStack'}
 }
}
foreach($slot in $music.SelectNodes('.//item_stack[@controller="RebirthReadonlyItemSlot, RebirthUtils"]')){
 if(!$slot.HasAttribute('repeat_i')){throw "Undefined native item_stack repeat_i: $($slot.name)"}
}
[xml]$stationItems=Get-Content (Join-Path $PSScriptRoot '../../Config/_Workstations/items.xml') -Raw
[xml]$stationBlocks=Get-Content (Join-Path $PSScriptRoot '../../Config/_Workstations/blocks.xml') -Raw
$recycler=$stationItems.SelectSingleNode('//item[@name="FuriousRamsayCollapsedSonjaAmmoRecyclerStation"]/property[@name="Meshfile"]')
if($recycler.value -ne '#@modfolder:Resources/SonjaAmmoRecyler.unity3d?SonjaAmmoRecyclerPrefab.prefab'){throw 'Recycler item asset does not match installed block asset'}
if(!(Test-Path (Join-Path $PSScriptRoot '../../Resources/SonjaAmmoRecyler.unity3d'))){throw 'Recycler bundle absent'}
'PASS: loading regressions covered: all native tool-grid child types, 86 explicit cassette indices, installed recycler bundle reference.'