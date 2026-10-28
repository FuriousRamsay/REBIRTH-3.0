#requires -Version 7.0
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Initialize-TemporaryFiles.ps1')
$fixtureRoot=Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$fixtureCode="using System;`nusing System.Collections.Generic;`nusing System.Xml.Linq;`n"
foreach($fixtureFile in @('Scripts/Vehicles/Camper/RebirthCamperStationRefundRecoveryWatch.cs','Tools/VehicleStudio/mobile_refund_recovery_watch_fixture.cs')){
    $fixtureCode += [regex]::Replace([IO.File]::ReadAllText((Join-Path $fixtureRoot $fixtureFile)),'(?m)^using [^;]+;\r?\n','')+"`n"
}
Add-Type -TypeDefinition $fixtureCode
[MobileRefundRecoveryWatchFixture]::Run()
