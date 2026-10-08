$ErrorActionPreference='Stop'
$root=Resolve-Path (Join-Path $PSScriptRoot '../..')
function Member([string]$s,[string]$signature){$a=$s.IndexOf($signature);if($a-lt 0){throw "missing $signature"};$open=$s.IndexOf('{',$a);$depth=1;$b=$open+1;while($depth-gt 0-and$b-lt$s.Length){if($s[$b]-eq'{'){$depth++};if($s[$b]-eq'}'){$depth--};$b++};if($depth-ne 0){throw 'unbalanced'};return $s.Substring($a,$b-$a)}
$current=Get-Content -Raw (Join-Path $root 'Scripts/Spawning/RebirthSpawnCompositionService.cs')
$baseline=(git -C $root show HEAD:Scripts/Spawning/RebirthSpawnCompositionService.cs)-join "`n"
function SelectorClass([string]$s,[string]$name){
 $code='internal static class '+$name+'{private static RebirthCompiledSpawnData s_data;private static object s_forbiddenRestrictedSpawnTags=new object();private static void Initialize(){} public static void Configure(RebirthCompiledSpawnData data){s_data=data;} public static bool Run(RebirthSpawnContext c,Func<double> rng,Dictionary<string,Queue<int>> history,bool trace,out RebirthSpawnTrace result){return TrySelectCore(c,rng,history,trace,out result);}'
 foreach($signature in @('private static bool TrySelectCore(','public static RebirthSleeperGroupPolicy GetSleeperGroupPolicy(','public static bool IsRestrictedNoSpecialSurface(','public static bool IsForbiddenRestrictedSpawnEntity(EntityClass entityClass)','public static bool IsForbiddenRestrictedSpawnEntity(int entityClassId)','private static bool IsRuntimeEntityRegistered(','private static double GetThemeMultiplier(','private static string GetThemeDescription(','private static double GetHistoryMultiplier(','private static void RecordHistory(','private static int WeightedIndex(','private static double Evaluate(','private static double GetBiomeWeight(','private static int CountEligible(','private static string NormalizeBiome(','private static bool IsFinite(double v)','private static bool IsFinitePositive(double v)')){$code+=Member $s $signature}
 return $code+'}'
}
$a=$current.IndexOf('public enum RebirthSpawnProgressionMode');$b=$current.IndexOf('public static class RebirthSpawnCompositionService');$models=$current.Substring($a,$b-$a)
$registry=Get-Content -Raw (Join-Path $root 'Scripts/Spawning/RebirthSpawnRegistry.cs');$models=(Member $registry 'public enum RebirthSpawnSurface')+$models
$helpers='';foreach($file in @('RebirthThemeScratchLease.cs','RebirthSpawnSelectionScratchLease.cs')){$raw=Get-Content -Raw (Join-Path $root ('Scripts/Performance/'+$file));$helpers += [regex]::Replace($raw,'(?m)^using [^;]+;\r?\n','')}
$fixture=Get-Content -Raw (Join-Path $PSScriptRoot 'spawn_selector_fixture.cs')
$scratch=Get-Content -Raw (Join-Path $PSScriptRoot 'selection_scratch_fixture.cs');$scratch=[regex]::Replace($scratch,'(?m)^using [^;]+;\r?\n','').Replace('// SCRATCH_HELPER','')
Add-Type -TypeDefinition $fixture.Replace('// MODELS',$models).Replace('// HELPERS',$helpers).Replace('// CLASSES',((SelectorClass $baseline 'Before')+(SelectorClass $current 'After'))).Replace('// SCRATCH_CHECKS',$scratch)
[SelectorChecks]::Run();[SelectionScratchChecks]::Run()
'PASS actual whole TrySelectCore/dependencies: choices/full trace/RNG counts/history, finite/extreme/default/refusal, nested/throw/oversize/theme-domain/thread lifetime checks. Native registry/tags doubled; no gameplay/FPS.'
