$ErrorActionPreference='Stop'
[xml]$b=Get-Content Config/_Workstations/blocks.xml -Raw
$known=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
foreach($p in @('../../Data/Config/items.xml','../../Data/Config/blocks.xml')+@(Get-ChildItem Config -Recurse -Filter '*.xml'|Select-Object -ExpandProperty FullName)){
 try{[xml]$d=Get-Content -LiteralPath $p -Raw;foreach($n in $d.SelectNodes('//item[property]|//block[property]')){[void]$known.Add($n.GetAttribute('name'))}}catch{}
}
[xml]$r=Get-Content Config/_Workstations/recipes.xml -Raw;$missing=@();foreach($n in $r.SelectNodes('//recipe|//ingredient')){if(!$known.Contains($n.GetAttribute('name'))){$missing+=$n.GetAttribute('name')}}
if($missing.Count){throw "Undefined recovery inputs/results: $($missing -join ',')"}
[xml]$pools=Get-Content Config/blockplaceholders.xml -Raw;foreach($n in $pools.SelectNodes('//append[contains(@xpath,"BustedRandomLootHelper") or contains(@xpath,"MicrowaveRandomLootHelper")]/block')){if(!$known.Contains($n.GetAttribute('name'))){throw "Undefined spawn $($n.GetAttribute('name'))"}}
foreach($n in $b.SelectNodes('//block[property[@name="Class" and starts-with(@value,"RebirthWorld")]]')){if($b.SelectNodes("//remove[contains(@xpath,'$($n.GetAttribute('name'))')]/following-sibling::append/block[@name='$($n.GetAttribute('name'))']").Count -eq 0){throw 'Alias must be appended after removal'}}
Write-Output 'PASS recovery recipe references, spawn candidate definitions and alias operation order.'

[xml]$items=Get-Content Config/_Workstations/items.xml -Raw
[xml]$xui=Get-Content Config/XUi_InGame/xui.xml -Raw
$loc=@{};foreach($line in Get-Content Config/Localization.csv){$key=($line -split ',',2)[0];if($key){$loc[$key]=$true}}
foreach($block in $b.SelectNodes('/configs/append/block[not(starts-with(@name,"RebirthLegacyStorage_")) and not(property[@name="Extends"])]')){
 $name=$block.GetAttribute('name');$shell=$block.SelectSingleNode('property[@name="RebirthSalvageShell"]')
 if(!$shell -or !$items.SelectSingleNode("//item[@name='$($shell.GetAttribute('value'))']")){throw "No repairable item for $name"}
 if(!$r.SelectSingleNode("//recipe[@name='$name']/ingredient[@name='$($shell.GetAttribute('value'))']")){throw "No recovery recipe for $name"}
 $group=$block.SelectSingleNode('property[@name="WorkstationWindow"]').GetAttribute('value')
 if(!$xui.SelectSingleNode("//window_group[@name='$group']")){throw "No shared workstation group for $name"}
 $model=$block.SelectSingleNode('property[@name="Model"]').GetAttribute('value')
 if($model -match '^#@modfolder:([^?]+)\?'){if(!(Test-Path -LiteralPath $Matches[1])){throw "Missing model bundle $model"}}
}
foreach($node in $items.SelectNodes('//item')){
 $name=$node.GetAttribute('name');if(!$loc.ContainsKey($name)){throw "Missing name localization $name"}
 $desc=$node.SelectSingleNode('property[@name="DescriptionKey"]');if($desc -and !$loc.ContainsKey($desc.GetAttribute('value'))){throw "Missing description $name"}
 $icon=$node.SelectSingleNode('property[@name="CustomIcon"]').GetAttribute('value')
 if(!(Test-Path "UIAtlases/ItemIconAtlas/$icon.png") -and !(Test-Path "../../Data/ItemIcons/$icon.png")){throw "Missing item icon $icon"}
}
foreach($recipe in $r.SelectNodes('//recipe[@craft_tool]')){if(!$known.Contains($recipe.GetAttribute('craft_tool'))){throw 'Unknown recovery tool'}}
if(!$r.SelectSingleNode('//recipe[@name="FuriousRamsayHammerPliers" and not(@craft_area)]')){throw 'Missing hand craft tool bootstrap'}
if(!$r.SelectSingleNode('//recipe[@name="WorkbenchToolbox001_FR" and not(@craft_area) and not(@craft_tool)]')){throw 'Missing hand toolbox bootstrap'}
if(!$r.SelectSingleNode('//recipe[@name="forge" and not(@craft_tool)]')){throw 'Forge repair must not depend on combined tool'}
foreach($legacy in $b.SelectNodes('//block[starts-with(@name,"RebirthLegacyStorage_")]')){
 if(!$legacy.SelectSingleNode('property[@name="Class" and @value="CompositeTileEntity"]') -or !$legacy.SelectSingleNode('property[@class="CompositeFeatures"]/property[@class="TEFeatureStorage"]')){throw 'Legacy storage schema must retain native feature'}
}
Write-Output 'PASS all restored stations have shell/recovery/shared window; assets, localization, recovery tool and legacy storage schemas exist.'