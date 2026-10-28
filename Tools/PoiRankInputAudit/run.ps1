$ErrorActionPreference='Stop'
$project=Split-Path -Parent $PSCommandPath
$source=Get-Content (Join-Path $project '../../Scripts/Vehicles/Camper/Energy/RebirthCamperEnergyDeviceAdapter.cs') -Raw
$start=$source.IndexOf('    private static Vector3i[] RankedPoiLights(');$end=$source.IndexOf('    public static RebirthCamperEnergyPoiSnapshot ObservePoi(', $start)
if($start-lt 0-or $end-le $start){throw 'Actual rank method anchors missing'}
New-Item -ItemType Directory -Path (Join-Path $project 'out') -Force | Out-Null
[IO.File]::WriteAllText((Join-Path $project 'out/ActualRank.cs'),"using System;`nusing System.Linq;`nusing System.Collections.Generic;`npartial class Audit {`n"+$source.Substring($start,$end-$start)+"`n}")
dotnet run --project (Join-Path $project 'audit.csproj')
if($LASTEXITCODE-ne 0){throw 'Rank fixture failed'}