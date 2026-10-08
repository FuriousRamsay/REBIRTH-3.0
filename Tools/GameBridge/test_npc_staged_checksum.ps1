#requires -Version 7.0
$ErrorActionPreference='Stop'
$root=Join-Path $PSScriptRoot '../..'
$source=Get-Content (Join-Path $root 'Scripts/NPC/Persistence/RebirthNpcAggregatePersistence.cs') -Raw
$start=$source.IndexOf('    private static void ValidateStagedAggregate(')
$end=$source.IndexOf('    public static bool ValidateAtomicConsistency',$start)
if($start -lt 0 -or $end -le $start){throw 'Staged validator extraction failed'}
$fixture=Get-Content (Join-Path $PSScriptRoot 'test_npc_staged_checksum_fixture.cs') -Raw
Add-Type -TypeDefinition $fixture.Replace('// ACTUAL_METHOD',$source.Substring($start,$end-$start))
[StagedAggregateFixture]::Run()