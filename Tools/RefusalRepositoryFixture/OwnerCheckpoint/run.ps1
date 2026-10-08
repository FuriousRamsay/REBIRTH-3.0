$ErrorActionPreference='Stop'
$root=Resolve-Path "$PSScriptRoot/../../.."
function Slice($s,$a,$b){$i=$s.IndexOf($a);$j=$s.IndexOf($b,$i);if($i -lt 0 -or $j -le $i){throw 'boundary'};$s.Substring($i,$j-$i)}
$s=Get-Content -Raw "$root/Scripts/Survivor/Support/RebirthGearOwnerReservation.cs"
$header=Slice $s '    private sealed class Entry' '    // Early hold only'
$methods=Slice $s '    internal static bool MatchesIntent(' '    private static bool TryCapture('
$methods+=Slice $s '    private static bool TrySavedWorld(' '    internal static bool BlocksInventory('
[IO.File]::WriteAllText("$PSScriptRoot/ActualReservation.cs","using System;using System.Collections.Generic;using System.Xml.Linq;internal static partial class FixtureReservation{"+$header+$methods+"}")
dotnet run --project "$PSScriptRoot/Fixture.csproj"
if($LASTEXITCODE){throw 'fixture failure'}
