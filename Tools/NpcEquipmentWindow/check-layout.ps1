$ErrorActionPreference='Stop'
$script:passed=0
function Check($ok,$name){if(-not $ok){throw $name};$script:passed++;Write-Output ('PASS '+$name)}
[xml]$source=Get-Content 'Config/XUi_InGame/windows.xml' -Raw
[xml]$patch=Get-Content 'Config/_NPC/equipment_windows.xml' -Raw
[xml]$test='<windows />'
$window=$source.SelectSingleNode('//window[@name="rebirthCompanionsWindow"]')
[void]$test.DocumentElement.AppendChild($test.ImportNode($window,$true))
foreach($op in $patch.DocumentElement.ChildNodes){if($op.NodeType -ne 'Element'){continue};$targets=@($test.SelectNodes($op.GetAttribute('xpath')));Check ($targets.Count -eq 1) ('patch target '+$op.LocalName+' '+$op.GetAttribute('xpath'));foreach($target in $targets){switch($op.LocalName){'remove'{[void]$target.ParentNode.RemoveChild($target)}'setattribute'{$target.SetAttribute($op.GetAttribute('name'),$op.InnerText)}'append'{foreach($child in $op.ChildNodes){if($child.NodeType -eq 'Element'){[void]$target.AppendChild($test.ImportNode($child,$true))}}}}}}
foreach($tab in @('Overview','Inventory','Equipment','Stats','Behavior')){Check ($test.SelectNodes('//button[@name="btnTab'+$tab+'"]').Count -eq 1) ('integrated tab '+$tab)}
Check ($test.SelectNodes('//*[@name="btnClose"]').Count -eq 0) 'no companion Close control'
foreach($name in @('companionRosterCard','companionSelectedCard','companionCommandCard','panelEquipment','npcEquipModel','npcEquipSelected','npcEquipInventory','npcEquipStorage')){Check ($test.SelectNodes('//*[@name="'+$name+'"]').Count -eq 1) ('unique controller target '+$name)}
for($i=0;$i -lt 10;$i++){Check ($test.SelectNodes('//*[@name="npcEquipSlot'+$i+'"]').Count -eq 1) ('dedicated equipment slot '+$i)}
for($i=0;$i -lt 52;$i++){Check ($test.SelectNodes('//*[@name="npcEquipPlayer'+$i+'"]').Count -eq 1) ('physical player view '+$i)}
Check ($test.SelectSingleNode('//*[@name="npcEquipModel"]').GetAttribute('keepsourceaspectratio') -eq 'true') 'character preview preserves source aspect ratio'
$ammo=@(6,7,8)|ForEach-Object {$test.SelectSingleNode('//*[@name="npcEquipSlot'+$_+'"]')}
Check (($ammo|ForEach-Object {$_.GetAttribute('pos').Split(',')[1]}|Select-Object -Unique).Count -eq 1) 'three ammo cells share one row'
$pack=$test.SelectSingleNode('//*[@name="npcEquipSlot9"]')
Check ([int]$pack.GetAttribute('pos').Split(',')[1] -lt [int]$ammo[0].GetAttribute('pos').Split(',')[1]) 'backpack sits below ammunition'
$upper=$test.SelectSingleNode('//*[@name="npcEquipSelected"]');$lower=$test.SelectSingleNode('//*[@name="npcEquipInventory"]')
Check ($upper.GetAttribute('width') -eq $lower.GetAttribute('width')) 'right panel widths align'
Check ([int]$upper.GetAttribute('pos').Split(',')[1]-[int]$upper.GetAttribute('height') -gt [int]$lower.GetAttribute('pos').Split(',')[1]) 'selected item panel above player inventory without overlap'
$keys=New-Object 'System.Collections.Generic.HashSet[string]'
$duplicates=New-Object 'System.Collections.Generic.List[string]'
foreach($line in Get-Content 'Config/Localization.txt'){if($line -match '^(xuiRbNpcEquipment[^,]*),'){if(-not $keys.Add($Matches[1])){$duplicates.Add($Matches[1])}}}
Check ($duplicates.Count -eq 0) 'new localization keys unique'
foreach($node in $patch.SelectNodes('//*[@text_key or @caption_key or @tooltip_key]')){foreach($attribute in @('text_key','caption_key','tooltip_key')){$value=$node.GetAttribute($attribute);if($value.StartsWith('xuiRbNpcEquipment')){Check ($keys.Contains($value)) ('localized '+$value)}}}
Check ($source.SelectNodes('//include[@filename="../_NPC/equipment_windows.xml"]').Count -eq 1) 'equipment patch included once'
$test.Save((Join-Path (Get-Location) 'obj/NpcEquipmentCompile/companions-expanded-fixture.xml'))
Write-Output ('RESULT: '+$script:passed+' XML target, control, layout and localization checks passed. No native UI execution.')


