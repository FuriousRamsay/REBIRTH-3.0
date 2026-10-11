$ErrorActionPreference='Stop'
$root=(Resolve-Path "$PSScriptRoot/../..").Path
function Check($ok,$message){if(!$ok){throw $message};Write-Output "PASS $message"}
[xml]$station=Get-Content "$root/Config/XUi_InGame/station_templates.xml"
Check ($station.SelectSingleNode("//*[@name='craftingInfoPanel']").GetAttribute('scale') -eq '1') 'Station result uses full shared column scale'
Check ($station.SelectSingleNode("//*[@name='ingredients']/grid").GetAttribute('cols') -eq '3') 'Station materials use three-column square grid'
Check ($station.SelectSingleNode('//rebirth_station_ingredient_slot/rect').GetAttribute('controller') -eq 'RebirthIngredientEntry, RebirthUtils') 'Native ingredient authority retained'
Check ([bool]$station.SelectSingleNode("//*[@name='stationRecipeDescription']/defaultscrollbar")) 'Description uses shared scrollbar'
[xml]$nests=Get-Content "$root/Config/_Workstations/birdnest_loot.xml"
Check ($nests.SelectNodes('//lootcontainer').Count -eq 3) 'Three native nest loot variants'
foreach($c in $nests.SelectNodes('//lootcontainer')){Check ($c.GetAttribute('destroy_on_close') -eq 'empty') 'Nest vanishes only after emptied';Check ([bool]$nests.SelectSingleNode("//lootgroup[@name='$($c.item.group)']")) 'Nest loot group resolves before container'}
Check ((Get-FileHash "$root/Resources/FR_Forest.unity3d").Hash -eq (Get-FileHash 'C:/Program Files (x86)/Steam/steamapps/common/7 Days To Die 2.6/Mods/zzz_REBIRTH__Core/Resources/FR_Forest.unity3d').Hash) '2.6 forest asset copied without alteration'
[xml]$exclude=Get-Content "$root/Config/_Survivor/loot_food_exclusions.xml"
foreach($patch in $exclude.SelectNodes('/configs/remove')){foreach($node in @($nests.SelectNodes($patch.GetAttribute('xpath')))){[void]$node.ParentNode.RemoveChild($node)}}
Check ($nests.SelectNodes('//item[@name="foodEgg"]').Count -eq 2) 'Loot exclusions preserve eggs in both authored bird-nest groups'
[xml]$mortar=Get-Content "$root/Config/_Workstations/mortar_recipes.xml"
[xml]$native=Get-Content "$root/../../Data/Config/recipes.xml"
$nodes=$native.SelectNodes($mortar.configs.setattribute.GetAttribute('xpath'))
Check (@($nodes|Where-Object {$_.GetAttribute('name') -eq 'resourceGunPowder'}).Count -gt 0) 'Gunpowder mapped to mortar'
Check (@($nodes|Where-Object {$_.GetAttribute('name').StartsWith('planted')}).Count -gt 0) 'Seed recipes mapped to mortar'
$keys=@{};foreach($line in [IO.File]::ReadAllLines("$root/Config/Localization.csv")){if($line -match '^(FuriousRamsayBirdNest[^,]*|xuiRebirthStationIngredients|xuiRebirthStationResult),(.+)$'){Check (!$keys.ContainsKey($Matches[1])) ('Unique localized key '+$Matches[1]);$keys[$Matches[1]]=$Matches[2]}}
Check ($keys.Count -eq 6) 'Nest and station labels localized'