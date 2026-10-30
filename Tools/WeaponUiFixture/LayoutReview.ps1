$ErrorActionPreference='Stop';$root=(Resolve-Path "$PSScriptRoot/../..").Path;$doc="$root/_Documentation/WeaponUi_20261029"
New-Item -ItemType Directory -Force (Join-Path $PSScriptRoot 'out') | Out-Null
$templates=Get-Content "$root/Config/XUi_InGame/templates.xml" -Raw;$baseline=Get-Content "$doc/templates.xml.baseline" -Raw
$restored=[regex]::Replace($templates,'\r?\n<include filename="../_WeaponUi/hover_rows.xml" />\r?\n','')
if($restored.Replace("`r`n","`n").TrimEnd()-cne$baseline.Replace("`r`n","`n").TrimEnd()){throw 'Templates differ beyond one include'}
[xml]$source=$baseline;$definition=$source.SelectSingleNode('//rebirth_item_stats_popup');if(!$definition){throw 'Shared popup missing'}
[xml]$document='<templates/>';$document.DocumentElement.AppendChild($document.ImportNode($definition,$true))|Out-Null
[xml]$patch=Get-Content "$root/Config/_WeaponUi/hover_rows.xml" -Raw;$targets=@($document.SelectNodes($patch.configs.append.xpath));if($targets.Count-ne1){throw 'Ambiguous popup patch target'}
foreach($node in $patch.configs.append.ChildNodes){if($node.NodeType-eq[Xml.XmlNodeType]::Element){$targets[0].AppendChild($document.ImportNode($node,$true))|Out-Null}}
$views=@($document.SelectNodes("//panel[@name='characterStatsPopup']/rect[starts-with(@name,'statsPopupRow')]"));if($views.Count-ne31-or@($views.name|Sort-Object -Unique).Count-ne31){throw '31 unique popup rows required'}
for($i=0;$i-lt31;$i++){if(!$document.SelectSingleNode("//rect[@name='statsPopupRow$i']")){throw "Missing row $i"}}
$tooltip=Get-Content "$root/Scripts/Survivor/UI/RebirthCharacterItemStatsTooltip.cs" -Raw
if($tooltip-notmatch'PopupRowCapacity = 31'-or$tooltip-notmatch'TryBuildComparison'-or$tooltip-notmatch'if \(source != null\)'){throw 'Popup runtime wiring drift'}
# Read the actual changed comparison header rectangles and test their native UI bounds.
$headerRects=[regex]::Matches($tooltip,'Rect\(GetChildById\("([^"]+)"\)\?\.ViewComponent,(-?\d+),(-?\d+),(\d+),(\d+)\)')
foreach($m in $headerRects){$x=[int]$m.Groups[2].Value;$y=-[int]$m.Groups[3].Value;$w=[int]$m.Groups[4].Value;$h=[int]$m.Groups[5].Value;if($x-lt0-or$y-lt0-or$x+$w-gt730-or$y+$h-gt180){throw "Header clipping: $($m.Groups[1].Value)"}}
$widths=[regex]::Match($tooltip,'int hoverValueWidth=compare\?\(weaponRows\?(\d+):(\d+)\):202');$columns=[regex]::Match($tooltip,'int selectedValueX=weaponRows\?(\d+):(\d+),selectedValueWidth=weaponRows\?(\d+):(\d+)')
foreach($mode in @(0,1)){$hw=[int]$widths.Groups[1+$mode].Value;$sx=[int]$columns.Groups[1+$mode].Value;$sw=[int]$columns.Groups[3+$mode].Value;$width=if($mode-eq0){730}else{586};if(186+$hw-gt$sx-or$sx+$sw-gt$width-28){throw 'Comparison row overlaps/clips'}}
if($tooltip-notmatch'weaponRows\?730:586'-or$tooltip-notmatch'bodyCapacity=weaponRows\?rows.Length-2:StatisticsRowCapacity\+1'-or$tooltip-notmatch'StatisticsRowCapacity = 16'){throw 'Nonweapon/food capacity or width changed'}foreach($width in @(440,730)){foreach($height in @(450,900,1300)){foreach($parent in @(@(800,480),@(1418,813),@(2560,1080))){$scale=[Math]::Min([double]1,[Math]::Min(($parent[0]-24)/[double]$width,($parent[1]-24)/[double]$height));if([Math]::Ceiling($width*$scale)-gt$parent[0]-23-or[Math]::Ceiling($height*$scale)-gt$parent[1]-23){throw "Fit $width x $height parent $($parent -join 'x') scale $scale -> $([Math]::Ceiling($width*$scale)) x $([Math]::Ceiling($height*$scale))"}}}}
# Bound current authored comparable groups against the runtime budget.
[xml]$display=Get-Content 'C:/Program Files (x86)/Steam/steamapps/common/7 Days To Die/Data/Config/ui_display.xml' -Raw
$groups=@{};foreach($entry in $display.SelectNodes('//item_display_info')){if($entry.display_type-notmatch'^(ranged|melee|motorTool|explosive)'){continue};$group=[string]$entry.display_group;if(!$groups.ContainsKey($group)){$groups[$group]=[Collections.Generic.HashSet[string]]::new()};foreach($stat in $entry.display_entry){if($stat.name-ne'EntityDamage'){$groups[$group].Add([string]$stat.name)|Out-Null}}}
$maximum=0;foreach($group in $groups.Keys){$rows=5+$groups[$group].Count+10+3;$maximum=[Math]::Max($maximum,$rows);if($rows-gt31){throw "Authored comparable group $group can exceed row budget: $rows"}}
$document.Save("$PSScriptRoot/out/applied-popup.xml")
"PASS one-include reconstruction;31 unique applied popup rows; comparison headers/value columns nonoverlapping; full-card viewport fit; authored comparable group worst-case $maximum rows <=31. Native font/GPU rendering NOT RUN."





