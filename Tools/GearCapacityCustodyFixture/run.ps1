$ErrorActionPreference='Stop'
$projectRoot=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$source=[IO.File]::ReadAllText((Join-Path $projectRoot 'Scripts/Survivor/Support/RebirthSurvivorGearService.cs'))
$start=$source.IndexOf('    public static bool ReconcilePhysicalBagCapacity(')
$end=$source.IndexOf('    public static string BuildDebugSummary(', $start)
if($start -lt 0 -or $end -le $start){throw 'Actual method extraction failed'}
$method=$source.Substring($start,$end-$start)
[IO.File]::WriteAllText((Join-Path $PSScriptRoot 'ActualMethod.cs'),'using System; public static class RebirthSurvivorGearService { public const int BasePhysicalBagSlots=52,MaxPhysicalBagSlots=169;'+$method+'}',[Text.UTF8Encoding]::new($false))
dotnet run --project (Join-Path $PSScriptRoot 'Fixture.csproj') -c Debug
if($LASTEXITCODE -ne 0){throw 'Fixture failed'}