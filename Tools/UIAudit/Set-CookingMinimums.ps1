$ErrorActionPreference='Stop'
$root=(Get-Location).Path
$defs=@{}
Get-ChildItem Config -Recurse -Filter '*items*.xml' | ForEach-Object { $doc=[xml](Get-Content $_.FullName -Raw); foreach($i in $doc.SelectNodes('//item[@name][property]')){ $defs[$i.GetAttribute('name')]=$i } }
function Value($item,$name){$p=$item.SelectSingleNode("property[@name='$name']");if($p){return [double]::Parse($p.GetAttribute('value'),[cultureinfo]::InvariantCulture)};return 0}
$runtime=[xml](Get-Content Config/_Cooking/runtime.xml -Raw)
$manifestPath=Join-Path $root 'Config/_Survivor/crafting_progression.xml';$manifest=[xml](Get-Content $manifestPath -Raw)
$capPath=Join-Path $root 'Config/_Survivor/capabilities.xml';$caps=[xml](Get-Content $capPath -Raw)
$rows=Import-Csv _Documentation/COOKING_MISSING_REQUIREMENTS_20261009.csv
$added=@();$report=@()
foreach($row in $rows){
 $id=$row.RecipeId; $item=$defs[$id];if(-not $item){throw "Missing item $id"};$dish=$runtime.SelectSingleNode("//dish[@item='$id']");$recipe=$manifest.SelectSingleNode("//recipe[@id='$id']");if(-not $recipe){throw "Missing manifest $id"}
 $nutrition=Value $item 'RebirthNutritionUnits'; $water=Value $item 'RebirthFoodWaterMl';$comfort=Value $item 'RebirthMoodInfluence';$skill=$recipe.GetAttribute('primary_skill');$level=-20
 $score=$nutrition+[math]::Min($water,350)/100
 if($score -gt 12){$level=-10};if($score -gt 18){$level=0};if($score -gt 24){$level=10};if($score -gt 30){$level=15};if($score -gt 38){$level=25}
 $station=$dish.GetAttribute('station');$reason='Per-serving nutrition plus water (maximum 3.5 points); existing basic meals -20/-10, baking 10, meals 15, advanced meals 25.'
 if($station -eq 'cold'){$level=[math]::Min($level,0);$reason+=' Simple cold assembly capped at 0.'}
 if($station -match 'Oven|GasStove'){$level=[math]::Max($level,10);$reason+=' Baking/stove technique floor 10.'}
 if($comfort -ge 12){$level=[math]::Max($level,15);$reason+=' High comfort floor 15.'}
 if($skill -eq 'skill.drink_preparation'){$level=-20;if($nutrition -gt 12 -or $comfort -ge 8){$level=10};$reason='Basic drink anchor -20; nutritious/high-comfort specialty drinks 10; water volume does not raise the minimum.'}
 $capId='capability.recipe.'+$id.ToLowerInvariant();if($caps.SelectSingleNode("//capability[@id='$capId']")){throw "Existing capability $capId"}
 $added+="  <capability id=`"$capId`" category=`"$($skill.Replace('skill.',''))`" target_type=`"recipe`" target_id=`"$id`" visibility=`"visible`"><requires_all><skill id=`"$skill`" minimum=`"$level`" recommended=`"$($level+20)`" /></requires_all></capability>"
 $recipe.SetAttribute('policy','gated');$recipe.SetAttribute('implementation','existing_capability');$recipe.SetAttribute('capability',$capId);$recipe.SetAttribute('decision_status','approved_value_based_20261009')
 $report+=[pscustomobject]@{RecipeId=$id;Name=$row.Name;Skill=$skill;Minimum=$level;Nutrition=$nutrition;WaterMl=$water;Comfort=$comfort;Station=$station;Basis=$reason}
}
$capText=[IO.File]::ReadAllText($capPath);$end=$caps.DocumentElement.Name;$capText=$capText.Replace('</'+$end+'>',($added -join "`r`n")+"`r`n</"+$end+'>');[IO.File]::WriteAllText($capPath,$capText,[Text.UTF8Encoding]::new($false))
$settings=[Xml.XmlWriterSettings]::new();$settings.Indent=$true;$settings.Encoding=[Text.UTF8Encoding]::new($false);$writer=[Xml.XmlWriter]::Create($manifestPath,$settings);$manifest.Save($writer);$writer.Dispose()
$report|Export-Csv _Documentation/COOKING_SKILL_MINIMUMS_20261009.csv -NoTypeInformation
"Implemented $($report.Count) capability skill gates"
$report|Group-Object Skill,Minimum|Select-Object Name,Count