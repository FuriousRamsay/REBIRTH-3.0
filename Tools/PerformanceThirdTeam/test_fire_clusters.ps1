$ErrorActionPreference='Stop'
$root=Resolve-Path (Join-Path $PSScriptRoot '../..')
function ExtractMember([string]$source,[string]$signature) {
 $start=$source.IndexOf($signature); if($start -lt 0){throw "missing $signature"}
 $open=$source.IndexOf('{',$start); $depth=1; $end=$open+1
 while($depth -gt 0 -and $end -lt $source.Length){if($source[$end] -eq '{'){$depth++};if($source[$end] -eq '}'){$depth--};$end++}
 if($depth -ne 0){throw 'unbalanced source'}
 return $source.Substring($start,$end-$start)
}
$current=Get-Content -Raw (Join-Path $root 'Scripts/Fire/RebirthFireVisualManager.cs')
$baseline=(git -C $root show HEAD:Scripts/Fire/RebirthFireVisualManager.cs) -join "`n"
$shared='private const int ClusterLinkRadius=2;public static readonly HashSet<Vector3i> FireCandidatePositions=new HashSet<Vector3i>();public static readonly HashSet<Vector3i> NormalCoverage=new HashSet<Vector3i>();public static readonly Dictionary<Vector3i,int> ClusterByPosition=new Dictionary<Vector3i,int>();public static readonly List<int> ClusterNormalCounts=new List<int>();private static readonly List<Vector3i> ClusterQueue=new List<Vector3i>();public static void Run(){BuildCandidateClusters();}'
$before='public static class Before {'+$shared+(ExtractMember $baseline 'private static void BuildCandidateClusters()')+'}'
$after='public static class After {'+$shared+'private static readonly Vector3i[] ClusterNeighborOffsets=CreateClusterNeighborOffsets();'+(ExtractMember $current 'private static Vector3i[] CreateClusterNeighborOffsets()')+(ExtractMember $current 'private static void BuildCandidateClusters()')+'}'
$fixture=Get-Content -Raw (Join-Path $PSScriptRoot 'fire_cluster_fixture.cs')
Add-Type -TypeDefinition ($fixture.Replace('// PRODUCTION_CLASSES',$before+"`n"+$after))
[FireClusterChecks]::Run()
'PASS actual HEAD/current clustering:256 layouts; exact cluster IDs/counts and ordered24offsets; Vector3i doubled; no native rendering/profiling'
