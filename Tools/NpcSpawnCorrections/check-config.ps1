$ErrorActionPreference='Stop'
$script:checks=0
function Check($condition,$label){if(-not $condition){throw $label};$script:checks++;"PASS $label"}
[xml]$entities=Get-Content Config/_NPC/entityclasses.xml -Raw
[xml]$groups=Get-Content Config/_NPC/entitygroups.xml -Raw
[xml]$theory=Get-Content Config/_Survivor/theory_specialists.xml -Raw
[xml]$windows=Get-Content Config/XUi_InGame/windows.xml -Raw
[xml]$xui=Get-Content Config/XUi_InGame/xui.xml -Raw
[xml]$items=Get-Content '../../Data/Config/items.xml' -Raw
Check ($entities.SelectNodes('//entity_class[starts-with(@name,"npcRebirthSpecialist")]').Count -eq 0) 'no specialist entity classes'
Check ($groups.SelectNodes('//entitygroup').Count -eq 0) 'no specialist spawn groups'
Check ($theory.SelectNodes('//specialist').Count -eq 0) 'no orphan specialist expertise mappings'
foreach($name in @('rebirthNpcInteraction','rebirthNpcDialogue','rebirthNpcTrade','rebirthNpcProgression')){
 Check ($xui.SelectNodes('//window_group[@name="'+$name+'"]').Count -eq 0) "no generic registration $name"
 Check ($windows.SelectNodes('//window[@name="'+$name+'Window"]').Count -eq 0) "no generic layout $name"
 Check (-not ((Get-Content Config/XUi_InGame/windows.xml -Raw).Contains($name+'Window'))) "no dangling patch $name"
}
foreach($name in @('rebirthCompanions','rebirthCompanionInteraction')){
 Check ($xui.SelectNodes('//window_group[@name="'+$name+'"]').Count -eq 1) "existing group retained $name"
 Check ($windows.SelectNodes('//window[@name="'+$name+'Window"]').Count -eq 1) "existing layout retained $name"
}
$base=$entities.SelectSingleNode('//entity_class[@name="npcRebirthBanditManSDCS"]')
$armor=$base.SelectSingleNode('property[@name="ArmorItems"]').value.Split(',')
Check ($armor.Count -eq 64) '64 standard base-game armor pieces'
foreach($name in $armor){Check ($items.SelectNodes('/items/item[@name="'+$name+'"]').Count -eq 1) "available armor $name"}
foreach($name in $base.SelectSingleNode('property[@name="HeldWeapon"]').value.Split(',')){Check ($items.SelectNodes('/items/item[@name="'+$name+'"]').Count -eq 1) "available weapon $name"}
Check ($base.SelectSingleNode('property[@name="RandomArmorItemsMissing"]').value -eq '2') 'original missing-piece budget retained'
foreach($path in @('Config/Localization.csv','Config/Localization.txt')){Check (@(Select-String -Path $path -Pattern '^xuiRbNpcDismissEmptyEquipment,').Count -eq 1) "unique dismissal localization $path"}
"PASS $script:checks configuration checks; no game execution."
