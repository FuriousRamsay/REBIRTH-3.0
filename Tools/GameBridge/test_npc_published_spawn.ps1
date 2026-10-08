#requires -Version 7.0
$ErrorActionPreference='Stop'
$modRoot=Join-Path $PSScriptRoot '../..'
$source=Get-Content (Join-Path $modRoot 'Scripts/NPC/WorldIntegration/RebirthNpcWorldIntegration.cs') -Raw
$start=$source.IndexOf('    internal static bool TryObservePublishedSpawn(')
$end=$source.IndexOf('    // Final-file intent witness only;', $start)
if($start -lt 0 -or $end -le $start){throw 'Production observation not found'}
$method=$source.Substring($start,$end-$start)
$fixture=Get-Content (Join-Path $PSScriptRoot 'test_npc_published_spawn_fixture.cs') -Raw
Add-Type -TypeDefinition $fixture.Replace('// ACTUAL_METHOD',$method)
[PublicationHost]::Run()