#requires -Version 7.0
$ErrorActionPreference='Stop'
$taskRoot=Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$fixturePath=Join-Path $PSScriptRoot 'test_station_preparation_generated_fixture.cs'
$fixture=[IO.File]::ReadAllText($fixturePath)
$boundary=$fixture.IndexOf('public struct Vector3i ')
if($boundary -lt 0){throw 'Explicit fixture doubles boundary missing'}
$production=[IO.File]::ReadAllText((Join-Path $taskRoot 'Scripts/Crafting/UI/RebirthStationPreparationService.cs'))
$layout=[IO.File]::ReadAllText((Join-Path $taskRoot 'Scripts/Crafting/UI/RebirthStationInputLayout.cs')).Replace('using System;','').Replace('using System.Collections.Generic;','')
$payment=[IO.File]::ReadAllText((Join-Path $taskRoot 'Scripts/Crafting/UI/RebirthStationInputPaymentImage.cs')).Replace('using System;','').Replace('using System.Collections.Generic;','')
$scope=[IO.File]::ReadAllText((Join-Path $taskRoot 'Scripts/Crafting/UI/RebirthStationDiscoveryScope.cs')).Replace('using System;','')
# Compile current production sources, not historical generated copy.
Add-Type -TypeDefinition ('using System.Collections.Generic;'+[Environment]::NewLine+$production+[Environment]::NewLine+$layout+[Environment]::NewLine+$payment+[Environment]::NewLine+$scope+[Environment]::NewLine+$fixture.Substring($boundary))
[StationPreparationFixture]::Run()