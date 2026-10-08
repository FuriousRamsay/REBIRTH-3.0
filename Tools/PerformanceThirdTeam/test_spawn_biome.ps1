$ErrorActionPreference='Stop'
$root=Resolve-Path (Join-Path $PSScriptRoot '../..')
$current=Get-Content -Raw (Join-Path $root 'Scripts/Spawning/RebirthSpawnCompositionService.cs')
$baseline=(git -C $root show HEAD:Scripts/Spawning/RebirthSpawnCompositionService.cs) -join "`n"
function BuildClass([string]$source,[string]$name){
 $start=$source.IndexOf('        Dictionary<RebirthSpawnCategory, List<RebirthEntityRule>> eligible =');$end=$source.IndexOf('        List<RebirthSpawnCategory> categories =',$start)
 if($start -lt 0 -or $end -le $start){throw 'actual eligibility extraction failed'}
 $normalize=[regex]::Match($source,'private static string NormalizeBiome\(string s\)\{[^\r\n]+').Value
 if(-not $normalize){throw 'normalization extraction failed'}
 $normalize=$normalize.Replace('{s=', '{NormalizeCalls++;s=')
 return 'public static class '+$name+'{ public static int NormalizeCalls;private static bool IsRuntimeEntityRegistered(RebirthEntityRule e){return e.Registered;}private static bool IsRestrictedNoSpecialSurface(int s){return s==1;}private static bool IsForbiddenRestrictedSpawnEntity(int id){return id<0;}'+$normalize+' public static Dictionary<RebirthSpawnCategory,List<RebirthEntityRule>> Run(RebirthSpawnContext context,RebirthCompiledSpawnData data){'+$source.Substring($start,$end-$start)+'return eligible;}}'
}
$fixture=Get-Content -Raw (Join-Path $PSScriptRoot 'spawn_biome_fixture.cs')
Add-Type -TypeDefinition $fixture.Replace('// PRODUCTION_CLASSES',((BuildClass $baseline 'Before')+(BuildClass $current 'After')))
[SpawnBiomeChecks]::Run()
'PASS actual eligibility block and NormalizeBiome:640 mode/surface/biome layouts; ordered candidate equivalence; normalization at most once and remains lazy. Native registration adapter doubled.'
