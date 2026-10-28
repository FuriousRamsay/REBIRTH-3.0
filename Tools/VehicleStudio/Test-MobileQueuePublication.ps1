#requires -Version 7.0
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Initialize-TemporaryFiles.ps1')
$camperSourceRoot=Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$camperFixtureCode="using System;`nusing System.IO;`nusing System.Collections.Generic;`nusing System.Linq;`nusing System.Text;`nusing System.Security.Cryptography;`nusing System.Xml.Linq;`n"
foreach($camperSource in @('Scripts/Vehicles/Camper/RebirthCamperStationAddress.cs','Scripts/Crafting/UI/RebirthStationInputLayout.cs','Scripts/Vehicles/Camper/RebirthCamperQueuedPublicationMatch.cs','Scripts/Vehicles/Camper/RebirthCamperStationPublicationRecord.cs','Scripts/Vehicles/Camper/RebirthCamperStationCompletionRecord.cs','Scripts/Vehicles/Camper/RebirthCamperStationTrainingRecord.cs','Tools/VehicleStudio/mobile_queue_publication_fixture.cs')){
    $camperText=[IO.File]::ReadAllText((Join-Path $camperSourceRoot $camperSource))
    $camperFixtureCode += [regex]::Replace($camperText,'(?m)^using [^;]+;\r?\n','') + "`n"
}
$camperPlanText=[IO.File]::ReadAllText((Join-Path $camperSourceRoot 'Scripts/Vehicles/Camper/RebirthCamperStationTrainingPlan.cs'))
$camperFixtureCode += [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'publication_relocation_adapter.cs'))
$camperReceiptMethod=[regex]::Match($camperPlanText,'(?s)    internal static string ReceiptFor\(.*?\r?\n    }')
if(!$camperReceiptMethod.Success){throw 'Training receipt method changed.'}
$camperFixtureCode += "public partial class RebirthCamperStationTrainingPlan {`n"+$camperReceiptMethod.Value+"`n}`n"
$camperFixtureCode="using System.Globalization;`n"+$camperFixtureCode
Add-Type -TypeDefinition $camperFixtureCode
[MobileQueuePublicationFixture]::Run()
