$ErrorActionPreference = 'Stop'
$source = Join-Path $PSScriptRoot '../../Scripts/Crafting/Cooking'
Add-Type -Path (Join-Path $source 'RebirthCookingRules.cs')
Add-Type -Path (Join-Path $source 'RebirthCookingMemory.cs')
function Check($value, $message) { if (-not $value) { throw $message } }
function Near($a, $b, $message) { Check ([Math]::Abs($a-$b) -lt 0.001) $message }

# A time technique changes processing time without also granting enjoyment.
Near (100 * [RebirthCookingRules]::TechniqueTime('T')) 80 '100-second recipe must take 80 seconds'
Near ([RebirthCookingRules]::TechniqueComfort('T', $true)) 0 'Time technique incorrectly grants comfort'
Near ([RebirthCookingRules]::TechniqueTime('Q')) 1 'Texture technique must preserve time'
Near ([RebirthCookingRules]::TechniqueComfort('Q', $false)) 1 'Texture technique missing comfort'
Near ([RebirthCookingRules]::TechniqueComfort('H', $false)) 0 'Herb technique works without compatible herbs'
Near ([RebirthCookingRules]::TechniqueComfort('H', $true)) 1 'Compatible herb technique missing comfort'

# Beginners still make useful food. Skill cannot multiply nutrition beyond the authored ceiling.
Near ([RebirthCookingRules]::Nutrition($false,40,40,40,0)) 36 'Novice meal retention incorrect'
Near ([RebirthCookingRules]::Nutrition($false,40,40,40,100)) 40 'Expert meal retention incorrect'
Near ([RebirthCookingRules]::Nutrition($false,400,40,40,100)) 44 'Substitution nutrition cap broken'
Near ([RebirthCookingRules]::Nutrition($false,1,40,40,100)) 36 'Substitution penalty exceeds bound'
Near ([RebirthCookingRules]::Nutrition($true,40,0,0,100)) 44 'Improvised expert nutrition bonus exceeds ten percent'
Near ([RebirthCookingRules]::Nutrition($true,40,0,0,0)) 42 'Improvised novice nutrition bonus must be five percent'
Check ([RebirthCookingRules]::Nutrition($true,40,0,0,0) -gt 0) 'Novice improvised meal is inedible'
Near ([RebirthCookingRules]::Comfort(20,100,$true,'H')) 23 'Herb plus technique should add three comfort'
Near ([RebirthCookingRules]::Comfort(-5,0,$false,'')) -5 'Low skill magnifies negative food comfort'

$memory = New-Object RebirthCookingMemory
$ready = New-Object 'RebirthCookingMemory+Ready'
$ready.Book='book1'; $ready.Magazine='magazine1'; $ready.Expires=[DateTime]::UtcNow.AddSeconds(300).Ticks
$memory.Recipes['stew']=$ready
$token=[Guid]::NewGuid().ToString('N')
$ticket=New-Object 'RebirthCookingMemory+Ticket'
$ticket.Recipe='stew'; $ticket.Portions=5; $ticket.OutputCount=2; $ticket.XpMultiplier=1.2
$ticket.StationPosition='100,65,-20'
$memory.Tickets[$token]=$ticket
$found=$null; $newPortions=0
Check ($memory.Acknowledge($token,'stew',2,1,[ref]$found,[ref]$newPortions)) 'First completion rejected'
Check ($newPortions -eq 1) 'First completion amount wrong'
Check (-not $memory.Acknowledge($token,'stew',2,1,[ref]$found,[ref]$newPortions)) 'Duplicate awards XP'
Check (-not $memory.Acknowledge($token,'pie',2,2,[ref]$found,[ref]$newPortions)) 'Receipt accepted for another dish'
Check (-not $memory.Acknowledge($token,'stew',99,2,[ref]$found,[ref]$newPortions)) 'Receipt invents output quantity'
Check (-not $memory.Acknowledge($token,'stew',2,6,[ref]$found,[ref]$newPortions)) 'Receipt exceeds registered batch'

$saved=[RebirthCookingMemory]::Read([System.Xml.Linq.XElement]::Parse($memory.Write().ToString()))
Check ($saved.Recipes['stew'].Remaining -gt 290) 'Prepared timer lost on save/load'
Check (-not $saved.Recipes.ContainsKey('pie')) 'Preparation leaked to a different recipe'
Check ($saved.Recipes['stew'].Book -eq 'book1') 'Prepared reference lost'
Check ($saved.Tickets[$token].StationPosition -eq '100,65,-20') 'Station HUD location lost on save/load'
Check (-not $saved.Acknowledge($token,'stew',2,1,[ref]$found,[ref]$newPortions)) 'Save/load permits duplicate XP'
Check ($saved.Acknowledge($token,'stew',2,3,[ref]$found,[ref]$newPortions)) 'Catch-up completion rejected'
Check ($newPortions -eq 2) 'Catch-up double-counts previous completion'
$other=New-Object RebirthCookingMemory
Check (-not $other.Acknowledge($token,'stew',2,1,[ref]$found,[ref]$newPortions)) 'Receipt leaked across characters/saves'
$clone=$saved.Clone(); $clone.Recipes['stew'].Book='changed'
Check ($saved.Recipes['stew'].Book -eq 'book1') 'Preview clone mutates real character'
$expired = New-Object 'RebirthCookingMemory+Ready';$expired.Expires=[DateTime]::UtcNow.AddSeconds(-1).Ticks
$saved.Recipes['expired']=$expired
Check (-not ([RebirthCookingMemory]::Read($saved.Write())).Recipes.ContainsKey('expired')) 'Expired preparation revived on load'
Write-Output 'Cooking mechanics checks passed: techniques, nutrition, comfort, preparation persistence, batch ownership, duplicate/catch-up XP.'
