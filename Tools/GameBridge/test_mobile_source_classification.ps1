#requires -Version 7.0
$ErrorActionPreference='Stop'
if(Get-Process -Name '7DaysToDie*' -ErrorAction SilentlyContinue){throw 'Game must be closed'}
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$source=[IO.File]::ReadAllText((Join-Path $root 'Scripts/Crafting/RemoteCrafting/RebirthRemoteResourceSourceCustody.cs'))
$start=$source.IndexOf('    internal static bool Blocks(object target)')
if($start -lt 0){throw 'Actual Blocks method missing'}
$open=$source.IndexOf('{',$start);$end=$open+1;$depth=1
while($depth -gt 0){if($source[$end] -eq '{'){$depth++};if($source[$end] -eq '}'){$depth--};$end++}
$fixture=[IO.File]::ReadAllText((Join-Path $PSScriptRoot 'mobile_source_classification_fixture.cs'))
Add-Type -TypeDefinition $fixture.Replace('// ACTUAL_BLOCKS',$source.Substring($start,$end-$start)) -IgnoreWarnings -WarningAction SilentlyContinue
[MobileSourceClassificationFixture]::Run()
