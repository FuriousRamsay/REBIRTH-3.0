#requires -Version 7.0
$ErrorActionPreference='Stop'
if(Get-Process 7DaysToDie* -ErrorAction SilentlyContinue){throw 'Game must be closed'}
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$source=[IO.File]::ReadAllText((Join-Path $root 'Scripts/Vehicles/Camper/RebirthCamperStationSaveEvidence.cs'))
$start=$source.IndexOf('    private static Request BeginCore(')
$end=$source.IndexOf('    public static bool TryObserve(',$start)
if($start -lt 0 -or $end -le $start){throw 'Actual BeginCore missing'}
$method=$source.Substring($start,$end-$start)
$old=$method.Replace('BeginCore(','BeginCoreBeforeFix(').Replace('        state.lastTickTime=0;','')
$fixture=[IO.File]::ReadAllText((Join-Path $PSScriptRoot 'mobile_save_clock_fixture.cs'))
Add-Type -TypeDefinition $fixture.Replace('// ACTUAL_BEGIN',$method).Replace('// OLD_COUNTEREXAMPLE',$old) -IgnoreWarnings -WarningAction SilentlyContinue
[MobileSaveClockFixture]::Run()
