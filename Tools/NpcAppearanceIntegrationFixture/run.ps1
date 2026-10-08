$ErrorActionPreference='Stop'
$root=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$source=[IO.File]::ReadAllText((Join-Path $root 'Scripts/NPC/Foundation/RebirthHumanNpcModelPipeline.cs'))
$source=$source.Substring(0,$source.IndexOf('public readonly struct RebirthHumanNpcModelPipelineResolution'))
[IO.File]::WriteAllText((Join-Path $PSScriptRoot 'ActualAppearance.cs'),$source,[Text.UTF8Encoding]::new($false))
$source=[IO.File]::ReadAllText((Join-Path $root 'Scripts/NPC/Foundation/RebirthNpcFoundation.cs'))
$start=$source.IndexOf('public static class RebirthNpcPersistenceCodec')
$end=$source.IndexOf('public static class RebirthNpcRuntimeRegistry',$start)
[IO.File]::WriteAllText((Join-Path $PSScriptRoot 'ActualRuntimeCodec.cs'),'using System; using System.IO;'+$source.Substring($start,$end-$start),[Text.UTF8Encoding]::new($false))
$source=[IO.File]::ReadAllText((Join-Path $root 'Scripts/NPC/Persistence/RebirthNpcAggregatePersistence.cs'))
$start=$source.IndexOf('    internal static bool TryCommitResolvedAppearance(')
$end=$source.IndexOf('    public static bool TryMutateExisting(',$start)
if($start -lt 0 -or $end -le $start){throw 'Actual commit source anchors missing'}
[IO.File]::WriteAllText((Join-Path $PSScriptRoot 'ActualAppearanceCommit.cs'),'using System; using System.IO; using System.Collections.Generic; partial class AppearanceCommitStore {'+$source.Substring($start,$end-$start)+'}',[Text.UTF8Encoding]::new($false))
dotnet run --project (Join-Path $PSScriptRoot 'Fixture.csproj') -c Release
if($LASTEXITCODE -ne 0){throw 'Appearance integration fixture failed'}