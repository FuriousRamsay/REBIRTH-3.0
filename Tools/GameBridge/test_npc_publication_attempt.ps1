#requires -Version 7.0
$ErrorActionPreference='Stop'
$root=Join-Path $PSScriptRoot '../..'
$source=Get-Content (Join-Path $root 'Scripts/NPC/WorldIntegration/RebirthNpcWorldIntegration.cs') -Raw
$start=$source.IndexOf('    internal static bool TryPublishConstructedSpawn(')
$end=$source.IndexOf('    // Construction alone',$start)
if($start -lt 0 -or $end -le $start){throw 'Publication method extraction failed'}
$fixture=Get-Content (Join-Path $PSScriptRoot 'test_npc_publication_attempt_fixture.cs') -Raw
Add-Type -TypeDefinition $fixture.Replace('// ACTUAL_METHOD',$source.Substring($start,$end-$start))
[PublishFixture]::Run()