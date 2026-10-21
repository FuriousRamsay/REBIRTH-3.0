$ErrorActionPreference='Stop'
$fixtureRoot=$PSScriptRoot
$projectRoot=[IO.Path]::GetFullPath((Join-Path $fixtureRoot '../..'))
$relative='Scripts/Vehicles/Camper/RebirthCamperModuleRuntime.cs'
$source=[IO.File]::ReadAllText((Join-Path $projectRoot $relative))
$start='    internal void TickStations()'
$end='    internal void BindStations()'
$first=$source.IndexOf($start,[StringComparison]::Ordinal)
if($first -lt 0 -or $source.IndexOf($start,$first+1,[StringComparison]::Ordinal) -ge 0){throw 'Station clock start boundary missing/nonunique.'}
$last=$source.IndexOf($end,$first,[StringComparison]::Ordinal)
if($last -lt 0){throw 'Station clock end boundary missing.'}
$slice=$source.Substring($first,$last-$first)
$output=Join-Path $fixtureRoot 'generated'
New-Item -ItemType Directory -Path $output -Force | Out-Null
$path=Join-Path $output 'ActualStationClock.cs'
[IO.File]::WriteAllText($path,('using System;'+[Environment]::NewLine+'public sealed partial class RebirthCamperModuleRuntime {'+[Environment]::NewLine+$slice+'}'),(New-Object Text.UTF8Encoding($false)))
$linked=@($relative,'Scripts/Vehicles/Camper/Energy/RebirthCamperEnergyModel.cs','Scripts/Vehicles/Camper/Energy/RebirthCamperEnergyState.cs','Scripts/Vehicles/Camper/Energy/RebirthCamperEnergyRuntime.cs','Scripts/Vehicles/Camper/Energy/RebirthCamperEnergyBounds.cs','Scripts/Vehicles/Camper/Energy/RebirthCamperEnergyImmobility.cs','Scripts/Vehicles/Camper/Energy/RebirthCamperEnergyConfigurationFile.cs','Scripts/UI/XUiC_RebirthVehicleHudStatBar.cs')
$slices=@(
    @{source='Scripts/Vehicles/Camper/Energy/RebirthCamperEnergyItems.cs';start='internal static class RebirthCamperEnergyItems';end='internal sealed class RebirthCamperEnergyActorImage';output='ActualEnergyItems.cs'},
    @{source='Scripts/Vehicles/Camper/RebirthCamperRemoteTransport.cs';start='internal static class CamperFrame';end=$null;output='ActualCamperFrame.cs'},
    @{source='Scripts/Vehicles/Camper/Energy/RebirthCamperEnergyMaintenanceReceipt.cs';start='internal sealed class RebirthCamperEnergyMaintenanceReceipt';end='internal static class RebirthCamperEnergyMaintenanceMarkers';output='ActualMaintenanceReceipt.cs'},
    @{source='Scripts/Vehicles/Camper/Energy/RebirthCamperEnergyDeviceAdapter.cs';start='public sealed class RebirthCamperEnergyPoiSnapshot';end='public static partial class RebirthCamperEnergyDeviceAdapter';output='ActualPoiSnapshot.cs'})
$extraHashes=@()
foreach($slice in $slices){
    $text=[IO.File]::ReadAllText((Join-Path $projectRoot $slice.source));$a=$text.IndexOf($slice.start,[StringComparison]::Ordinal)
    if($a -lt 0 -or $text.IndexOf($slice.start,$a+1,[StringComparison]::Ordinal) -ge 0){throw "Nonunique actual source boundary: $($slice.source)"}
    $b=if($null -eq $slice.end){$text.Length}else{$text.IndexOf($slice.end,$a,[StringComparison]::Ordinal)}
    if($b -le $a){throw "Missing actual source end: $($slice.source)"}
    $dest=Join-Path $output $slice.output
    [IO.File]::WriteAllText($dest,('using System;using System.IO;using System.Linq;using System.Text;'+[Environment]::NewLine+$text.Substring($a,$b-$a)),(New-Object Text.UTF8Encoding($false)))
    $extraHashes+=@{name=$slice.output;sha256=(Get-FileHash -LiteralPath $dest -Algorithm SHA256).Hash}
    $linked+=$slice.source
}
$linked+=@('Scripts/Vehicles/Camper/Energy/RebirthCamperEnergyRuntimeEstimate.cs','Scripts/Vehicles/Camper/Energy/RebirthCamperEnergySnapshotWire.cs','Scripts/Vehicles/Camper/Energy/RebirthCamperEnergyMaintenanceFrame.cs','Scripts/Vehicles/Camper/RebirthCamperTransferJournal.cs')
$linked+='Scripts/Vehicles/Camper/Energy/RebirthCamperBatteryCharging.cs'
$linked+=@('Tools/CamperEnergyFixture/NativeBatteryCharging.cs','Tools/CamperEnergyFixture/ChargingCases.cs','Tools/CamperEnergyFixture/CurrentBoundaryDoubles.cs')
$entries=@($linked | ForEach-Object { @{path=$_;sha256=(Get-FileHash -LiteralPath (Join-Path $projectRoot $_) -Algorithm SHA256).Hash} })
$manifest=@{projectRoot=$projectRoot;sources=$entries;generatedHash=(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash;extraction='TickStations only, unchanged unique-boundary slice; surrounding module ownership/state/native persistence is a fixture double.'}
$manifest.extracted=$extraHashes
[IO.File]::WriteAllText((Join-Path $output 'source_manifest.json'),($manifest|ConvertTo-Json -Depth 6),(New-Object Text.UTF8Encoding($false)))
Write-Output 'Prepared actual station-clock slice and fingerprints. No build or fixture execution performed.'

