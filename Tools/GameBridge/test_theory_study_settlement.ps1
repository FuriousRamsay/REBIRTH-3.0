#requires -Version 7.0
$ErrorActionPreference='Stop'
$taskRoot=Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
Add-Type -Path @(
 (Join-Path $taskRoot 'Scripts/Survivor/Network/RebirthSurvivorRequestScope.cs'),
 (Join-Path $taskRoot 'Scripts/Survivor/Progression/RebirthTheoryStudyOutcome.cs'),
 (Join-Path $taskRoot 'Scripts/Survivor/Persistence/RebirthTheoryStudyPersistence.cs'),
 (Join-Path $taskRoot 'Scripts/Survivor/Progression/RebirthTheoryStudySettlement.cs'),
 (Join-Path $PSScriptRoot 'test_theory_study_settlement_fixture.cs'))
[TheoryStudySettlementFixture]::Run()