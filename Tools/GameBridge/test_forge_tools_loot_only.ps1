$ErrorActionPreference='Stop'
$root=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
function ReadXml($p){[xml]([IO.File]::ReadAllText((Join-Path $root $p)))}
function Check($v,$message){if(!$v){throw $message}}
$ids=@('toolAnvil','toolBellows','toolForgeAnvil','toolForgeBellows')
$native=ReadXml '../../Data/Config/recipes.xml';$entry=ReadXml 'Config/recipes.xml'
$remove=$entry.SelectSingleNode('/configs/conditional/if[@cond="character_progression(''Rebirth'')"]/remove[contains(@xpath,"toolAnvil")]')
Check ($null -ne $remove) 'missing REBIRTH scoped removal'
foreach($node in @($native.SelectNodes($remove.xpath))){[void]$node.ParentNode.RemoveChild($node)}
$policy=ReadXml 'Config/_Survivor/crafting_progression.xml';$knowledge=ReadXml 'Config/_Survivor/recipe_knowledge.xml';$caps=ReadXml 'Config/_Survivor/capabilities.xml'
foreach($id in $ids){
Check (@($native.SelectNodes("/recipes/recipe[@name='$id']")).Count -eq 0) "craftable native tool $id"
$n=$policy.SelectNodes("//*[@id='$id']");Check ($n.Count -eq 1 -and $n[0].policy -eq 'disabled') "runtime gate $id"
Check (@($knowledge.SelectNodes("//recipe[@name='$id']")).Count -eq 0) "knowledge advertises $id"
Check (@($caps.SelectNodes("//capability[@target_id='$id']")).Count -eq 0) "capability advertises $id"
}
$items=ReadXml 'Config/_Survivor/items.xml';$item=$items.SelectSingleNode('//item[@name="rebirthManualForgeTools"]')
Check ($null -ne $item -and $item.SelectSingleNode('property[@name="CreativeMode"]').value -eq 'None') 'saved manual absent or creative visible'
foreach($file in @('Config/_Survivor/loot.xml','Config/_Rebirth/progression_loot.xml','Config/_Survivor/traders.xml','Config/_Rebirth/progression_traders.xml')){$x=ReadXml $file;Check (@($x.SelectNodes('//item[@name="rebirthManualForgeTools"]')).Count -eq 0) "manual distributed by $file"}
$events=ReadXml 'Config/gameevents.xml';Check ($events.SelectSingleNode('//action_sequence[@name="rebirthLessonRewardGatherSection"]/action/property[@name="added_items"]').value -eq 'rebirthManualHeatTreatment') 'chapter reward wrong'
$rows=Import-Csv (Join-Path $root 'Config/Localization.csv');$label=@($rows|Where-Object Key -eq 'rebirthLessonRewardGatherSection');Check ($label.Count -eq 1 -and $label[0].english -match 'Heat Treatment') 'chapter label mismatch'
'PASS loot-only tools and retired manual cross-file contracts; native recipe XPath simulated, not full game config execution.'