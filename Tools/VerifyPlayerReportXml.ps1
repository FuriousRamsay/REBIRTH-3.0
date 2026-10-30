$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
[xml]$native = Get-Content -LiteralPath "$root\..\..\Data\Config\items.xml" -Raw
[xml]$patch = Get-Content -LiteralPath "$root\Config\_Healing\basic_bandage.xml" -Raw
foreach ($op in $patch.DocumentElement.ChildNodes) {
    if ($op.LocalName -eq 'remove') {
        foreach ($node in @($native.SelectNodes($op.xpath))) { $null = $node.ParentNode.RemoveChild($node) }
    }
    if ($op.LocalName -eq 'append') {
        $parents = @($native.SelectNodes($op.xpath))
        if ($parents.Count -ne 1) { throw 'Bandage target is not unique' }
        foreach ($node in $op.ChildNodes) { $null = $parents[0].AppendChild($native.ImportNode($node, $true)) }
    }
}
$bandage = $native.SelectSingleNode("/items/item[@name='medicalBandage']")
foreach ($trigger in 'onSelfPrimaryActionEnd','onSelfSecondaryActionEnd') {
    $heal = @($bandage.SelectNodes("effect_group/triggered_effect[@trigger='$trigger' and @cvar='medicalRegHealthAmount']"))
    if ($heal.Count -ne 1 -or $heal[0].value -ne '6' -or $heal[0].operation -ne 'add') { throw "Incorrect healing: $trigger" }
    if (!$bandage.SelectSingleNode("effect_group/triggered_effect[@trigger='$trigger' and @action='RemoveBuff' and @buff='buffInjuryBleeding']")) { throw "Bleeding removal missing: $trigger" }
}
if ($bandage.SelectSingleNode("property[@class='Action0']/requirement[@cvar='lacerationBleedingStatus']")) { throw 'Bandage remains bleeding-only' }
[xml]$recipes = Get-Content -LiteralPath "$root\Config\_Survivor\recipes.xml" -Raw
$recipe = $recipes.SelectSingleNode("/configs/append/recipe[@name='rebirthGearWalkmanHeadphones']")
if (!$recipe -or $recipe.always_unlocked -ne 'true' -or $recipe.HasAttribute('craft_area') -or $recipe.HasAttribute('craft_tool')) { throw 'Walkman unlock/station constraints wrong' }
$ingredients = @($recipe.ingredient)
if ($ingredients.Count -ne 2 -or !$recipe.SelectSingleNode("ingredient[@name='FuriousRamsayWalkman' and @count='1']") -or !$recipe.SelectSingleNode("ingredient[@name='FuriousRamsayHeadphones' and @count='1']")) { throw 'Walkman components wrong' }
'PASS current native bandage patch replay:6HP primary/other, bleeding removal retained, nonbleeding use enabled; Walkman1+1 assembly unlocked/no station/tool. XML evidence only; live input/consumption not tested.'
