#requires -Version 7.0
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Initialize-TemporaryFiles.ps1')
$camperSourceRoot=Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$camperFixtureCode="using System;`nusing System.Collections.Generic;`n"
foreach($camperSource in @('Scripts/Vehicles/Camper/RebirthCamperStationPublicationWatch.cs','Scripts/Vehicles/Camper/RebirthCamperStationCompletionWatch.cs','Tools/VehicleStudio/mobile_publication_watch_fixture.cs')){
    $camperText=[IO.File]::ReadAllText((Join-Path $camperSourceRoot $camperSource))
    $camperFixtureCode += [regex]::Replace($camperText,'(?m)^using [^;]+;\r?\n','') + "`n"
}
Add-Type -TypeDefinition $camperFixtureCode
[MobilePublicationWatchFixture]::Run()
[MobileCompletionWatchFixture]::Run()
