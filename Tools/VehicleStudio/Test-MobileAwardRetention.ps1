#requires -Version 7.0
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Initialize-TemporaryFiles.ps1')
$camperSourceRoot=Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$camperStoreText=[IO.File]::ReadAllText((Join-Path $camperSourceRoot 'Scripts/Survivor/Progression/RebirthTeachingOutcomeStore.cs'))
$camperPlanText=[IO.File]::ReadAllText((Join-Path $camperSourceRoot 'Scripts/Vehicles/Camper/RebirthCamperStationTrainingPlan.cs'))
function Get-CamperMethod([string]$source,[string]$pattern){
    $match=[regex]::Match($source,'(?s)    '+$pattern+'.*?\r?\n    }')
    if(!$match.Success){throw "Current production method missing: $pattern"}
    return $match.Value
}
$camperFixtureCode="using System;`nusing System.Collections.Generic;`nusing System.Runtime.CompilerServices;`nusing System.Security.Cryptography;`nusing System.Text;`nusing System.Xml.Linq;`n"
$camperFixtureCode+=@'
public static class RebirthTeachingOutcomeStore {
 static readonly object Gate=new object();
 static readonly HashSet<Func<HashSet<string>,ISet<string>>> ReceiptProtectionProviders=new HashSet<Func<HashSet<string>,ISet<string>>>();
 static bool loadFailed=false,backupUnknown=false;
 static readonly Dictionary<string,int> Pending=new Dictionary<string,int>();
 static readonly HashSet<string> BackupOutcomeIds=new HashSet<string>();
 static void EnsureLoadedLocked(){}
'@
$camperFixtureCode+=Get-CamperMethod $camperStoreText 'public static void RegisterReceiptProtection\('
$camperFixtureCode+=Get-CamperMethod $camperStoreText 'public static void PruneAwardReceipts\('
$camperFixtureCode+="}`ninternal static class RebirthCamperStationTrainingPlan {`n"
$camperFixtureCode+=Get-CamperMethod $camperPlanText 'internal static string ReceiptFor\('
$camperFixtureCode+="}`n"
foreach($camperSource in @('Scripts/Vehicles/Camper/RebirthCamperStationAwardReceiptRetention.cs','Tools/VehicleStudio/mobile_award_retention_fixture.cs')){
    $camperText=[IO.File]::ReadAllText((Join-Path $camperSourceRoot $camperSource))
    $camperFixtureCode += [regex]::Replace($camperText,'(?m)^using [^;]+;\r?\n','') + "`n"
}
Add-Type -TypeDefinition $camperFixtureCode
[MobileAwardRetentionFixture]::Run()
