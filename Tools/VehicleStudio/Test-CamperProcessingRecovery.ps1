#requires -Version 7.0
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Initialize-TemporaryFiles.ps1')
$root=Join-Path $PSScriptRoot '../..'
$code="using System;using System.Collections.Generic;using System.Runtime.CompilerServices;`n"+[IO.File]::ReadAllText((Join-Path $PSScriptRoot 'camper_processing_recovery_fixture.cs'))
foreach($file in @('Scripts/Vehicles/Camper/RebirthCamperStationAddress.cs','Scripts/Vehicles/Camper/RebirthCamperStationProcessingRecoveryWatch.cs')){$code+=[regex]::Replace([IO.File]::ReadAllText((Join-Path $root $file)),'(?m)^using [^;]+;\r?\n','')+"`n"}
# Compile the production persisted-identity factory with native identity adapters.
$identity=[IO.File]::ReadAllText((Join-Path $root 'Scripts/Survivor/Persistence/RebirthStablePlayerIdentity.cs'))
$start=$identity.IndexOf('internal static bool TryFromPersistentPlayer(')
$end=$identity.IndexOf('public static bool TryFromClientInfo(',$start)
if($start -lt 0 -or $end -le $start){throw 'Persisted identity method not found'}
$code+='public partial class RebirthStablePlayerIdentity {'+$identity.Substring($start,$end-$start)+'}'
Add-Type -TypeDefinition $code
[ProcessingRecoveryChecks]::Run()
