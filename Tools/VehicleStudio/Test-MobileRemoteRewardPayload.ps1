#requires -Version 7.0
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Initialize-TemporaryFiles.ps1')
$fixtureRoot=Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$fixtureCode="using System;`nusing System.IO;`nusing System.Text;`nusing System.Xml;`nusing System.Xml.Linq;`nusing System.Collections.Generic;`n"
foreach($fixtureFile in @('Scripts/Vehicles/Camper/RebirthCamperStationRemoteRewardPayload.cs','Tools/VehicleStudio/mobile_remote_reward_payload_fixture.cs')){
    $fixtureCode += [regex]::Replace([IO.File]::ReadAllText((Join-Path $fixtureRoot $fixtureFile)),'(?m)^using [^;]+;\r?\n','')+"`n"
}
Add-Type -TypeDefinition $fixtureCode
[MobileRemoteRewardPayloadFixture]::Run()
