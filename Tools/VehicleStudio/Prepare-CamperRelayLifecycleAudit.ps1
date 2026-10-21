# Production algorithm copies differ only by an isolated namespace. No assets
# are edited. Run the resulting executeMethod in the coordinated Unity session.
$ErrorActionPreference='Stop'
$destination='E:/_Haven/shared/REBIRTHVehicleStudio_Camper/Assets/Editor'
[IO.Directory]::CreateDirectory($destination)|Out-Null
$sources=@('../../Scripts/Vehicles/Camper/Energy/RebirthCamperRelayVisuals.cs','../../Scripts/Vehicles/Camper/Energy/RebirthCamperRelayGeometry.cs')
$text="using System;using System.Linq;using System.Runtime.CompilerServices;using UnityEngine;using Object=UnityEngine.Object;`nnamespace RebirthRelayLifecycleFixture {`n"
foreach($path in $sources){$source=[IO.File]::ReadAllText((Join-Path $PSScriptRoot $path));$text+=[regex]::Replace($source,'(?m)^using [^\r\n]+\r?\n','')+"`n"}
$text+="}`n"
[IO.File]::WriteAllText((Join-Path $destination 'ProductionRelayLifecycleAlgorithms.cs'),$text)
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'UnitySources/RebirthCamperRelayLifecycleAudit.cs') -Destination (Join-Path $destination 'RebirthCamperRelayLifecycleAudit.cs')
Write-Output 'Prepared production RelayVisuals/RelayGeometry namespace copies and Unity lifecycle audit. Execute RebirthCamperRelayLifecycleAudit.Run; no Unity launched.'
