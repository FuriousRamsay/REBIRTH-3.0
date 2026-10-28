#requires -Version 7.0
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Initialize-TemporaryFiles.ps1')
$root=Join-Path $PSScriptRoot '../..'
$base=[IO.File]::ReadAllText((Join-Path $root 'Tools/VehicleStudio/mobile_station_admission_fixture.cs'))
$base=$base.Substring(0,$base.IndexOf('public static class MobileStationAdmissionFixture'))
$base=$base.Replace('public ItemStack Clone()=>','public bool IsEmpty()=>count<=0;public ItemStack Clone()=>')
$code="using RebirthWorldCharacterRecord=Owner;using RebirthStablePlayerIdentity=Identity;using System;using System.IO;using System.Text;using System.Security.Cryptography;using System.Linq;using System.Xml.Linq;using System.Globalization;using System.Collections.Generic;`n"+$base
$code += [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'camper_relocation_service_fixture.cs'))
foreach($file in @('Scripts/Vehicles/Camper/RebirthCamperStationAddress.cs','Scripts/Survivor/Network/RebirthSurvivorRequestScope.cs','Scripts/Crafting/UI/RebirthStationGridAdmission.cs','Scripts/Vehicles/Camper/RebirthCamperStationRelocationCorrelation.cs','Scripts/Vehicles/Camper/RebirthCamperStationPublicationRecord.cs','Scripts/Vehicles/Camper/RebirthCamperStationRelocationService.cs','Scripts/Vehicles/Camper/RebirthCamperStationRelocationRecovery.cs','Scripts/Vehicles/Camper/RebirthCamperStationQueueRecovery.cs')){$code+=[regex]::Replace([IO.File]::ReadAllText((Join-Path $root $file)),'(?m)^using [^;]+;\r?\n','')+"`n"}
# Compile the actual remote queue-observation branch, excluding unrelated terminal
# handlers. The watcher adapter counts dispatches; lease resolution remains real.
$actions=[IO.File]::ReadAllText((Join-Path $root 'Scripts/Vehicles/Camper/RebirthCamperRemoteStationActions.cs'))
$start=$actions.IndexOf('foreach(var job in new List<string>(owner.Progression.CamperStationPublications.Keys))')
$end=$actions.IndexOf('foreach(var pair in owner.Progression.CamperStationTrainingPlans)',$start)
if($start -lt 0 -or $end -le $start){throw 'Remote queue observer source segment not found'}
$code+='public static class RemoteQueueObserverChecks {public static void Observe(EntityPlayer player,Owner owner,RebirthCamperStationState observed,RebirthCamperStationAddress address){'+$actions.Substring($start,$end-$start)+'}}'
$code+='public static class RebirthCamperStationQueueRecoveryWatch {public static int Calls;public static bool TryObserveRemote(EntityPlayer player,string job){Calls++;return true;}}'
Add-Type -TypeDefinition $code
[RelocationServiceChecks]::Run()
