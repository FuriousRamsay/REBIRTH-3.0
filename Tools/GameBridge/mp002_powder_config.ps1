# AUTHORED_NOT_RUN. Configuration contract only; no native/remote gameplay claim.
$ErrorActionPreference = 'Stop'
$modRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
[xml]$powderItems = Get-Content -Raw -LiteralPath (Join-Path $modRoot 'Config/_Fire/items.xml')
[xml]$powderBuffs = Get-Content -Raw -LiteralPath (Join-Path $modRoot 'Config/_Fire/buffs.xml')
[xml]$nativeBuffs = Get-Content -Raw -LiteralPath (Join-Path $modRoot '../../Data/Config/buffs.xml')
function Require($condition, [string]$message) { if (-not $condition) { throw $message } }
$buff = $powderBuffs.SelectNodes("/configs/append/buff[@name='buffExtinguisher']")
Require ($buff.Count -eq 1) 'Powder must define exactly one silent protection buff'
Require ($buff[0].duration.value -eq '8') 'Protection duration changed'
Require ($buff[0].stack_type.value -eq 'ignore') 'Repeated spray must not stack protection'
Require ($buff[0].SelectNodes(".//triggered_effect[@action='PlaySound']").Count -eq 0) 'Powder must remain silent'
$removes = $buff[0].SelectNodes(".//triggered_effect[@action='RemoveBuff']")
Require ($removes.Count -eq 5) 'Every currently supported burning buff needs removal'
foreach ($effect in $removes) {
    Require ($null -ne $nativeBuffs.SelectSingleNode("/buffs/buff[@name='$($effect.buff)']")) "Undefined native burn: $($effect.buff)"
}
$ammo = $powderItems.SelectSingleNode("/configs/append/item[@name='resourcePotassiumBicarbonate']")
$adds = $ammo.SelectNodes(".//triggered_effect[@action='AddBuff']")
Require ($adds.Count -eq 2) 'Powder requires self and actual hit recipient protection'
foreach ($effect in $adds) {
    Require ($effect.trigger -eq 'onSelfPrimaryActionRayHit') 'Protection must use scoped hit context'
    Require ($effect.buff -eq 'buffExtinguisher') 'Powder protection reference changed'
}
Require ($ammo.SelectNodes(".//triggered_effect[@action='AddBuff' and @target='other']").Count -eq 1) 'Actual hit recipient missing'
Require ($ammo.SelectNodes(".//triggered_effect[@action='RemoveFire, RebirthUtils' and @trigger='onSelfPrimaryActionRayHit' and @range='5']").Count -eq 1) 'Physical fire removal contract changed'
Write-Output 'PASS MP002 powder configuration contract (not gameplay validation)'
