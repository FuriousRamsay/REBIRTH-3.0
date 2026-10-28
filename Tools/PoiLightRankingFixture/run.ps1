$ErrorActionPreference='Stop'
$source=Get-Content -LiteralPath (Join-Path $PSScriptRoot '../../Scripts/Vehicles/Camper/Energy/RebirthCamperEnergyDeviceAdapter.cs') -Raw
$start=$source.IndexOf('    internal static Vector3i[] RankPoiLightPositions(')
$end=$source.IndexOf('    // END PURE POI SPREAD',$start)
if($start -lt 0 -or $end -lt 0){throw 'Production markers changed; review extractor.'}
New-Item -ItemType Directory -Force (Join-Path $PSScriptRoot out)|Out-Null
('using System;using System.Collections.Generic;partial class RankingFixture {'+$source.Substring($start,$end-$start)+'}')|Set-Content (Join-Path $PSScriptRoot out/RankExtract.cs)
dotnet run --project (Join-Path $PSScriptRoot fixture.csproj) -p:NuGetAudit=false
exit $LASTEXITCODE
