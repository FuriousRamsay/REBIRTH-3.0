$ErrorActionPreference='Stop'
$root=Resolve-Path (Join-Path $PSScriptRoot '../..')
function Member([string]$s,[string]$signature){$a=$s.IndexOf($signature);if($a-lt 0){throw "missing $signature"};$open=$s.IndexOf('{',$a);$depth=1;$b=$open+1;while($depth-gt 0-and$b-lt$s.Length){if($s[$b]-eq'{'){$depth++};if($s[$b]-eq'}'){$depth--};$b++};if($depth-ne 0){throw 'unbalanced'};return $s.Substring($a,$b-$a)}
$current=Get-Content -Raw (Join-Path $root 'Scripts/Spawning/RebirthSpawnCompositionService.cs')
$baseline=(git -C $root show HEAD:Scripts/Spawning/RebirthSpawnCompositionService.cs)-join "`n"
function WeightClass([string]$s,[string]$name){return 'public static class '+$name+'{private static RebirthCompiledSpawnData s_data;public static double Run(RebirthCompiledSpawnData d,string prefab,RebirthEntityRule e){s_data=d;return GetThemeMultiplier(prefab,e);}'+(Member $s 'private static double GetThemeMultiplier(')+(Member $s 'private static bool IsFinite(double v)')+(Member $s 'private static bool IsFinitePositive(double v)')+'}'}
$modelStart=$current.IndexOf('internal sealed class RebirthWeightPoint');$modelEnd=$current.IndexOf('public static class RebirthSpawnCompositionService');$models=$current.Substring($modelStart,$modelEnd-$modelStart).Replace('internal sealed class','public sealed class')
$category=Member $current 'public enum RebirthSpawnCategory';$models=$category+$models
$helper=Get-Content -Raw (Join-Path $root 'Scripts/Performance/RebirthThemeScratchLease.cs')
$helper=[regex]::Replace($helper,'(?m)^using [^;]+;\r?\n','')
$numeric=Get-Content -Raw (Join-Path $PSScriptRoot 'theme_weight_fixture.cs')
$scratch=Get-Content -Raw (Join-Path $PSScriptRoot 'theme_scratch_fixture.cs');$scratch=[regex]::Replace($scratch,'(?m)^using [^;]+;\r?\n','').Replace('// SCRATCH_HELPER','')
Add-Type -TypeDefinition $numeric.Replace('// MODELS',$models).Replace('// HELPER',$helper).Replace('// CLASSES',((WeightClass $baseline 'Before')+(WeightClass $current 'After'))).Replace('// SCRATCH_CHECKS',$scratch)
[ThemeWeightChecks]::Run();[ThemeScratchChecks]::Run()
'PASS actual theme-weight numerical bits and scratch lifetime fixture; no native gameplay/profiling.'
