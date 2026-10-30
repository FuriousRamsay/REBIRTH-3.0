param([switch]$LegacyModifierNegativeControl,[switch]$LegacyComparisonNegativeControl)
$ErrorActionPreference='Stop';$root=(Resolve-Path "$PSScriptRoot/../..").Path
function Strip-Usings([string]$text){return [regex]::Replace($text,'(?m)^using [^;]+;\r?\n','')}
$rows=Strip-Usings (Get-Content "$root/Scripts/UI/RebirthWeaponDetailRows.cs" -Raw)
if($LegacyComparisonNegativeControl){$rows=$rows.Replace('string text=RebirthItemStatColors.Format(Number(current,format)+suffix);','string text=RebirthItemStatColors.Format(Number(current,format)+suffix);if(baseline!=float.PositiveInfinity)return text;')}
$colors=Strip-Usings (Get-Content "$root/Scripts/Survivor/UI/RebirthItemStatColors.cs" -Raw)
$preview=Get-Content "$root/Scripts/Survivor/UI/RebirthSelectedDurability.cs" -Raw;$preview=$preview.Substring($preview.IndexOf('internal struct RebirthPreviewItemState'))
$tooltip=Get-Content "$root/Scripts/Survivor/UI/RebirthCharacterItemStatsTooltip.cs" -Raw;$start=$tooltip.IndexOf('    private static float TargetModifier(');$end=$tooltip.IndexOf('    private List<RowData> BuildFoodRows',$start);$target=$tooltip.Substring($start,$end-$start)
if($LegacyModifierNegativeControl){$target=$target.Replace('float baseline = EffectManager.GetValue(PassiveEffects.DamageModifier, item, damage','float baseline = EffectManager.GetValue(PassiveEffects.DamageModifier, item, 1f').Replace('float targeted = EffectManager.GetValue(PassiveEffects.DamageModifier, item, damage,','float targeted = EffectManager.GetValue(PassiveEffects.DamageModifier, item, 1f,')}
$selected=Strip-Usings (Get-Content "$root/Scripts/UI/RebirthSelectedItemRows.cs" -Raw)
$fixture=Get-Content "$PSScriptRoot/Checks.cs.txt" -Raw
Add-Type -TypeDefinition $fixture.Replace('/*ROWS*/',$rows).Replace('/*COLORS*/',$colors).Replace('/*PREVIEW*/',$preview).Replace('/*TARGET*/',$target).Replace('/*SELECTED*/',$selected).Replace('/*TARGETS*/',[regex]::Match($tooltip,'private static readonly string\[\] DamageTargets = [^;]+;').Value)
& "$PSScriptRoot/NativeRuntime.ps1"




