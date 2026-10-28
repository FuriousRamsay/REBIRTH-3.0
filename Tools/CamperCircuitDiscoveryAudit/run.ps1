$ErrorActionPreference='Stop'
$project=Split-Path -Parent $PSCommandPath
$source=Get-Content (Join-Path $project '../../Scripts/Vehicles/Camper/Energy/RebirthCamperEnergyDeviceAdapter.Circuits.cs') -Raw
$start=$source.IndexOf('    private static PowerItem[] CircuitCandidates(Session session)')
$end=$source.IndexOf('    private static bool TryCircuit(', $start)
if($start-lt 0-or $end-le $start){throw 'Actual source anchors missing'}
[IO.File]::WriteAllText((Join-Path $project 'ActualDiscovery.cs'),"using System;`nusing System.Linq;`nusing System.Collections.Generic;`npartial class Audit {`n"+$source.Substring($start,$end-$start)+"`n}",[Text.UTF8Encoding]::new($false))
dotnet run --project (Join-Path $project 'audit.csproj')
if($LASTEXITCODE-ne 0){throw 'Discovery fixture failed'}
