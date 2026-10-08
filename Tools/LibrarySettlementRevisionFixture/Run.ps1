$ErrorActionPreference='Stop'
$taskRoot=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$taskSource=[IO.File]::ReadAllText((Join-Path $taskRoot 'Scripts/Survivor/Network/RebirthBackpackLibraryClientOffers.cs'))
$taskStart=$taskSource.IndexOf('    public static bool TryGetSettlement(World world')
$taskEnd=$taskSource.IndexOf('    public static void Reset()', $taskStart)
if($taskStart -lt 0 -or $taskEnd -lt $taskStart){throw 'Production method bounds missing'}
$taskPath=Join-Path $PSScriptRoot 'ActualSettlementReadback.cs'
$taskFixture=[IO.File]::ReadAllText($taskPath)
$taskHeaderEnd=$taskFixture.IndexOf('    public static bool TryGetSettlement(World world')
if($taskHeaderEnd -lt 0){throw 'Fixture header missing'}
[IO.File]::WriteAllText($taskPath,$taskFixture.Substring(0,$taskHeaderEnd)+$taskSource.Substring($taskStart,$taskEnd-$taskStart)+'}',(New-Object Text.UTF8Encoding($false)))
# Callers must obtain fresh shared-PC coordination clearance before invoking this CPU job.
dotnet run --project (Join-Path $PSScriptRoot 'Fixture.csproj') -c Release
exit $LASTEXITCODE