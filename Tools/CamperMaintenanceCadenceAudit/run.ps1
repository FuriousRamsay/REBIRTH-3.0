$ErrorActionPreference='Stop'
$project=Split-Path -Parent $PSCommandPath
$source=Get-Content (Join-Path $project '../../Scripts/Vehicles/Camper/Energy/RebirthCamperEnergyMaintenance.cs') -Raw
$start=$source.IndexOf('    private static void Update(ref ModEvents.SGameUpdateData data)')
$end=$source.IndexOf('    private static bool Prepare(', $start)
if($start-lt 0-or $end-le $start){throw 'Actual Update source anchors missing'}
[IO.File]::WriteAllText((Join-Path $project 'ActualUpdate.cs'),"using System;`nusing System.Linq;`npartial class Audit {`n"+$source.Substring($start,$end-$start)+"`n}",[Text.UTF8Encoding]::new($false))
dotnet run --project (Join-Path $project 'audit.csproj')
if($LASTEXITCODE-ne 0){throw 'Cadence fixture failed'}
