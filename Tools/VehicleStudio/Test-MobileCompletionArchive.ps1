#requires -Version 7.0
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Initialize-TemporaryFiles.ps1')
$fixtureRoot=Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$fixtureCode="using System;`nusing System.Collections.Generic;`nusing System.Xml.Linq;`n"
foreach($fixtureFile in @('Scripts/Vehicles/Camper/RebirthCamperStationCompletionArchive.cs','Tools/VehicleStudio/mobile_completion_archive_fixture.cs')){
    $fixtureCode += [regex]::Replace([IO.File]::ReadAllText((Join-Path $fixtureRoot $fixtureFile)),'(?m)^using [^;]+;\r?\n','')+"`n"
}
Add-Type -TypeDefinition $fixtureCode
[MobileCompletionArchiveFixture]::Run()
