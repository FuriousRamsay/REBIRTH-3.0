#requires -Version 7.0
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Initialize-TemporaryFiles.ps1')
$camperSourceRoot=Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$camperSources=@('Scripts/Vehicles/Camper/RebirthCamperStationAddress.cs','Scripts/Survivor/Network/RebirthSurvivorRequestScope.cs','Scripts/Crafting/UI/RebirthStationGridAdmission.cs','Scripts/Survivor/Persistence/RebirthStationTerminalIntent.cs','Scripts/Crafting/UI/RebirthStationCancellationRefund.cs','Tools/VehicleStudio/mobile_station_admission_fixture.cs')
$camperFixtureCode="using System.IO;`nusing System.Text;`nusing System.Security.Cryptography;`nusing System;`nusing System.Collections.Generic;`nusing System.Globalization;`nusing System.Linq;`nusing System.Xml.Linq;`n"
foreach($camperSource in $camperSources){
    $camperText=[IO.File]::ReadAllText((Join-Path $camperSourceRoot $camperSource))
    $camperFixtureCode += [regex]::Replace($camperText,'(?m)^using [^;]+;\r?\n','') + "`n"
}
$camperReservationText=[IO.File]::ReadAllText((Join-Path $camperSourceRoot 'Scripts/Crafting/UI/RebirthStationPreparationReservation.cs'))
$camperKeyMatch=[regex]::Match($camperReservationText,'(?s)    private static string StationKey\(RebirthStationGridAdmission admission\).*?\r?\n    }')
if(!$camperKeyMatch.Success){throw 'Reservation key source was not found.'}
$camperFixtureCode += "public static class ExactReservationKey {`n"+$camperKeyMatch.Value.Replace('private static string','public static string')+"`n}`n"
Add-Type -TypeDefinition $camperFixtureCode
[MobileStationAdmissionFixture]::Run()
