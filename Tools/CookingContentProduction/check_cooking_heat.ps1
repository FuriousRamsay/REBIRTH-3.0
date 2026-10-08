$ErrorActionPreference='Stop'
[void][Reflection.Assembly]::LoadFrom((Join-Path $PSScriptRoot '../../../../7DaysToDie_Data/Managed/UnityEngine.CoreModule.dll'))
[void][Reflection.Assembly]::LoadFrom((Join-Path $PSScriptRoot '../../../../7DaysToDie_Data/Managed/Assembly-CSharp.dll'))
[void][Reflection.Assembly]::LoadFrom((Join-Path $PSScriptRoot '../../RebirthUtils.dll'))
function Check($condition,$message){if(-not $condition){throw $message}}
function Near($actual,$expected,$message){Check ([Math]::Abs($actual-$expected) -lt 0.001) $message}
Near ([RebirthCookingHeatRules]::Retention(120,'Pan')) 1 'Grace period reduces food prematurely'
Near ([RebirthCookingHeatRules]::Retention(360,'Pan')) .525 'Skillet decay incorrect'
Near ([RebirthCookingHeatRules]::Retention(600,'Pan')) .05 'Skillet burn point incorrect'
Near ([RebirthCookingHeatRules]::Retention(900,'Soup')) .05 'Pot burn point incorrect'
Check ([RebirthCookingHeatRules]::Retention(600,'Soup') -gt .05) 'Pot burns too soon'
Near ([RebirthCookingHeatRules]::Retention(9000,'Baked')) .05 'Overdue nutrition becomes negative'
Near ([RebirthCookingHeatRules]::Duration('Pan',3)) 45 'Basic skillet timing changed'
Check ([RebirthCookingHeatRules]::Duration('Baked',9) -gt [RebirthCookingHeatRules]::Duration('Baked',3)) 'Complex meals take no longer'
[single]$elapsed=0;[single]$overdue=0
[RebirthCookingHeatRules]::Advance(60,[ref]$elapsed,[ref]$overdue,100,$true,$false,'Pan')
Near $elapsed 60 'Cooking failed to finish during catch-up'
Near $overdue 40 'Catch-up lost time past completion'
[RebirthCookingHeatRules]::Advance(60,[ref]$elapsed,[ref]$overdue,900,$false,$false,'Pan')
Near $overdue 40 'Heat-off food continued deteriorating'
[RebirthCookingHeatRules]::Advance(60,[ref]$elapsed,[ref]$overdue,1000,$true,$false,'Pan')
Near $overdue 600 'Catch-up missed burnt state'
$elapsed=0;$overdue=0
[RebirthCookingHeatRules]::Advance(60,[ref]$elapsed,[ref]$overdue,2000,$false,$true,'Soup')
Near $elapsed 60 'Cold preparation requires fuel'
Near $overdue 0 'Cold preparation burnt'

$type=[RebirthCookingIngredientPlan+Choice]
function Choice($id,$have,$need){$c=New-Object 'RebirthCookingIngredientPlan+Choice';$c.Type=$id;$c.Available=$have;$c.Required=$need;return $c}
$roles=New-Object 'System.Collections.Generic.List[RebirthCookingIngredientPlan+Choice][]' 3
0..2|ForEach-Object {$roles[$_]=New-Object 'System.Collections.Generic.List[RebirthCookingIngredientPlan+Choice]'}
$roles[0].Add((Choice 1 20 2))
$roles[1].Add((Choice 2 2 2));$roles[1].Add((Choice 3 6 2))
$roles[2].Add((Choice 4 20 1))
$types=$null;$maximum=[RebirthCookingIngredientPlan]::Solve($roles,[ref]$types)
Check ($maximum -eq 3 -and $types[1] -eq 3) 'Did not choose carrots for the largest batch'
$roles[2].Clear();$roles[2].Add((Choice 3 6 1))
$maximum=[RebirthCookingIngredientPlan]::Solve($roles,[ref]$types)
Check ($maximum -eq 1 -and $types[1] -eq 2 -and $types[2] -eq 3) 'Same carrots assigned to two ingredient roles'
$roles[0][0].Available=0
$maximum=[RebirthCookingIngredientPlan]::Solve($roles,[ref]$types)
Check ($maximum -eq 0) 'Missing essential ingredient produced a batch'
Write-Output 'Cooking checks passed: grace, decay, method timing, maximum substitutions and unique ingredient roles.'

# Debug thresholds use real seconds since completion, including one tick crossing completion.
[RebirthCookingHeatRules]::SetDebugTimes(5,15)
try {
    Near ([RebirthCookingHeatRules]::Retention(5,'Pan')) 1 'Debug grace boundary incorrect'
    Near ([RebirthCookingHeatRules]::Retention(10,'Soup')) .525 'Debug decay must apply to soup too'
    Near ([RebirthCookingHeatRules]::Retention(15,'Pan')) .05 'Debug burn boundary incorrect'
    [single]$elapsed=8; [single]$overdue=0
    [RebirthCookingHeatRules]::Advance(10,[ref]$elapsed,[ref]$overdue,9,$true,$false,'Pan')
    Near $elapsed 10 'Cooking did not complete'
    Near $overdue 7 'Completion-crossing tick lost or duplicated time'
    [RebirthCookingHeatRules]::Advance(10,[ref]$elapsed,[ref]$overdue,20,$false,$false,'Pan')
    Near $overdue 7 'Heat-off food continued deteriorating'
    [RebirthCookingHeatRules]::Advance(10,[ref]$elapsed,[ref]$overdue,20,$true,$false,'Pan')
    Near $overdue 15 'Burn timer must clamp at threshold'
    [single]$elapsed=8; [single]$overdue=0
    [RebirthCookingHeatRules]::Advance(10,[ref]$elapsed,[ref]$overdue,30,$false,$true,'Soup')
    Near $elapsed 10 'Cold meal cannot finish without fuel'
    Near $overdue 0 'Cold meal started burning'
} finally { [RebirthCookingHeatRules]::ResetDebugTimes() }
Near ([RebirthCookingHeatRules]::Grace) 120 'Reset did not restore grace'
Near ([RebirthCookingHeatRules]::BurnAfter('Soup')) 900 'Reset did not restore soup timer'
Write-Output 'Cooking debug timer checks passed: boundaries, crossing completion, heat off, cold meals, reset.'

Near ([RebirthCookingHeatRules]::FoodStat('nutrition',20,120,'Pan')) 20 'Ready nutrition changed'
Near ([RebirthCookingHeatRules]::FoodStat('nutrition',20,360,'Pan')) 10.5 'Collected overcooked nutrition does not match decay'
Near ([RebirthCookingHeatRules]::FoodStat('water',200,360,'Pan')) 105 'Collected water does not decay'
Near ([RebirthCookingHeatRules]::FoodStat('energy',100,360,'Pan')) 52.5 'Collected energy does not decay'
Near ([RebirthCookingHeatRules]::FoodStat('comfort',-2,360,'Pan')) -2 'Negative comfort was improved by overcooking'
Near ([RebirthCookingHeatRules]::FoodStat('nutrition',100,600,'Pan')) 2 'Burnt nutrition cap incorrect'
Near ([RebirthCookingHeatRules]::FoodStat('water',200,600,'Pan')) 0 'Burnt food retains water'
Near ([RebirthCookingHeatRules]::FoodStat('energy',100,600,'Pan')) 0 'Burnt food retains energy'
Near ([RebirthCookingHeatRules]::FoodStat('comfort',10,600,'Pan')) -5 'Burnt comfort incorrect'
Write-Output 'Collected food stat checks passed: ready, progressive decay, burnt penalties.'

# Native merging must retain cooked item metadata, rather than replacing it with a same-type stack.
function BareItem { [Runtime.Serialization.FormatterServices]::GetUninitializedObject([ItemValue]) }
$a=BareItem; $b=BareItem
Check ([RebirthCookingItemStats]::Compatible($a,$b)) 'Ordinary items cannot stack'
$a.SetMetadata('rebirth.cooking.nutrition',[single]2.2)
Check (-not [RebirthCookingItemStats]::Compatible($a,$b)) 'Cooked item merges into ordinary item'
$b.SetMetadata('rebirth.cooking.nutrition',[single]2.2)
Check ([RebirthCookingItemStats]::Compatible($a,$b)) 'Identical cooked stats cannot stack'
$b.SetMetadata('rebirth.cooking.nutrition',[single]1.1)
Check (-not [RebirthCookingItemStats]::Compatible($a,$b)) 'Different nutrition values merge'
$b.SetMetadata('rebirth.cooking.nutrition',[single]2.2)
$a.SetMetadata('rebirth.cooking.quality','Overcooked')
$b.SetMetadata('rebirth.cooking.quality','Standard')
Check (-not [RebirthCookingItemStats]::Compatible($a,$b)) 'Different qualities merge'
$text=[RebirthCookingItemStats]::Display(1,2,'')
Check ($text.Contains('[F07070](-1)') -and $text.Contains([char]0x25BC)) 'Loss indicator missing'
$text=[RebirthCookingItemStats]::Display(3,2,'')
Check ($text.Contains('[70DD70](+1)') -and $text.Contains([char]0x25B2)) 'Gain indicator missing'
Write-Output 'Cooking item checks passed: compatible stacks, differing stats/quality, signed comparison deltas.'
