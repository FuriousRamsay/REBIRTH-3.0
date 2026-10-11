$ErrorActionPreference='Stop'
$root=(Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$checks=0
function Check([bool]$ok,[string]$name){if(-not $ok){throw $name};$script:checks++}
$minimums=Import-Csv "$root/_Documentation/COOKING_SKILL_MINIMUMS_20261009.csv"
$caps=[xml](Get-Content "$root/Config/_Survivor/capabilities.xml" -Raw)
$manifest=[xml](Get-Content "$root/Config/_Survivor/crafting_progression.xml" -Raw)
$reading=[xml](Get-Content "$root/Config/_CraftingDiscovery/recipe_policy.xml" -Raw)
$lit=[xml](Get-Content "$root/Config/_Survivor/literature.xml" -Raw)
Check ($minimums.Count -eq 81) 'Expected 81 minimums'
foreach($r in $minimums){$m=$manifest.SelectSingleNode("//recipe[@id='$($r.RecipeId)']");$cap=$caps.SelectSingleNode("//capability[@id='$($m.GetAttribute('capability'))']");Check ($m.GetAttribute('policy') -eq 'gated' -and $cap.GetAttribute('target_id') -eq $r.RecipeId) "Manifest capability $($r.RecipeId)"; $skill=$cap.SelectSingleNode("requires_all/skill[@id='$($r.Skill)']");Check ($skill.GetAttribute('minimum') -eq $r.Minimum) "Minimum $($r.RecipeId)";$rule=$reading.SelectSingleNode("//recipe[@name='$($r.RecipeId)']");Check ([bool]$lit.SelectSingleNode("//item[@knowledge='$($rule.GetAttribute('requires_read'))']")) "Reading source $($r.RecipeId)"}
$exclusions=[xml](Get-Content "$root/Config/_Survivor/loot_food_exclusions.xml" -Raw)
$ids=Get-Content "$root/_Documentation/LOOT_FOOD_EXCLUSIONS_20261009.txt"
Check (@($ids|Where-Object {$_ -match 'Seed|Card|Book|Schematic|^foodCan'}).Count -eq 0) 'Seeds, references and canned food are not excluded'
$native=[xml](Get-Content "$root/../../Data/Config/loot.xml" -Raw)
$seedBefore=$native.SelectNodes('//item[contains(@name,"Seed") or contains(@name,"seed")]').Count
foreach($remove in $exclusions.SelectNodes('/configs/remove')){foreach($n in @($native.SelectNodes($remove.GetAttribute('xpath')))){$null=$n.ParentNode.RemoveChild($n)}}
Check ($native.SelectNodes('//lootgroup[starts-with(@name,"groupBirdNest")]//item[@name="foodEgg"]').Count -gt 0) 'Bird nest eggs preserved'
Check ($native.SelectNodes('//item[@name="foodEgg" and not(ancestor::lootgroup[starts-with(@name,"groupBirdNest")]) and not(ancestor::lootcontainer[@name="birdNest"])]').Count -eq 0) 'Eggs removed outside bird nests'
Check ($seedBefore -eq $native.SelectNodes('//item[contains(@name,"Seed") or contains(@name,"seed")]').Count) 'Native seed drops preserved'
foreach($file in Get-ChildItem "$root/Config" -Recurse -Filter '*loot*.xml'){$doc=[xml](Get-Content $file.FullName -Raw);foreach($remove in $exclusions.SelectNodes('/configs/remove')){foreach($n in @($doc.SelectNodes($remove.GetAttribute('xpath')))){$null=$n.ParentNode.RemoveChild($n)}};foreach($id in $ids){Check ($doc.SelectNodes("//item[@name='$id']").Count -eq 0) "Forbidden loot reference $($file.Name):$id"}}
$junk=[xml](Get-Content "$root/Config/_Survivor/container_junk_balance.xml" -Raw)
Check ([bool]$junk.SelectSingleNode('/configs/insertBefore[@xpath="/lootcontainers/lootgroup[1]"]')) 'Junk group must be declared before existing groups and containers'
Check ($junk.SelectNodes('//lootgroup[@name="rebirthBoundedContainerJunk"]/item[@group]').Count -eq 0) 'Junk pool cannot fan out into nested groups'
Check ($junk.SelectSingleNode('//lootgroup[@name="rebirthBoundedContainerJunk"]').GetAttribute('count') -eq '1') 'One junk item per roll'
foreach($p in @('windows.xml','station_templates.xml','trader_workspace.xml','quest_turnin_workspace.xml')){$doc=[xml](Get-Content "$root/Config/XUi_InGame/$p" -Raw);$g=$doc.SelectSingleNode('//grid[@controller="RebirthCraftingInventory, RebirthUtils"]');Check ($g.GetAttribute('cols') -eq '11' -and [int]$g.GetAttribute('rows')*11 -ge 169) "Legacy-safe grid $p"}
$entry=[xml](Get-Content "$root/Resources/Journal/entries.xml" -Raw);Check ([bool]$entry.SelectSingleNode('//entry[@id="FoodPreparation"]')) 'Food preparation journal entry present'
$loc=Import-Csv "$root/Config/Localization.csv";$lookup=@{};foreach($r in $loc){$lookup[$r.Key]=$r.english};foreach($t in $lit.SelectNodes('//item[@kind="theory"]')){Check (-not [string]::IsNullOrWhiteSpace($lookup[$t.GetAttribute('id')+'Desc'])) "Theory description $($t.GetAttribute('id'))"}
# Native loot parsing is document-order sensitive: declarations must precede every reference.
$ordered=[xml](Get-Content "$root/../../Data/Config/loot.xml" -Raw)
$first=$ordered.SelectSingleNode('/lootcontainers/lootgroup[1]')
$definition=$junk.SelectSingleNode('/configs/insertBefore/lootgroup')
$null=$first.ParentNode.InsertBefore($ordered.ImportNode($definition,$true),$first)
$dumpster=$ordered.SelectSingleNode('/lootcontainers/lootcontainer[@name="dumpster"]')
$names=@($ordered.DocumentElement.ChildNodes|Where-Object {$_.NodeType -eq 'Element'}|ForEach-Object {$_.GetAttribute('name')})
Check ([array]::IndexOf($names,'rebirthBoundedContainerJunk') -lt [array]::IndexOf($names,'dumpster')) 'Actual composed junk declaration precedes dumpster'
$allUi=[xml]'<audit />'
foreach($file in Get-ChildItem "$root/Config/XUi_InGame" -Filter '*.xml'){$d=[xml](Get-Content $file.FullName -Raw);$null=$allUi.DocumentElement.AppendChild($allUi.ImportNode($d.DocumentElement,$true))}
$windows=[xml](Get-Content "$root/Config/XUi_InGame/windows.xml" -Raw)
foreach($patch in $windows.SelectNodes('//setattribute[starts-with(@xpath,"//button[@name=")]')){$selector=$patch.GetAttribute('xpath');if($selector -notmatch '@defaultcolor') {Check ($allUi.SelectNodes($selector).Count -gt 0) "Scrollbar/style selector has a real target: $selector"}}
"PASS $checks offline data assertions; game behavior remains user-tested."