$ErrorActionPreference='Stop'
$root=(Resolve-Path "$PSScriptRoot/../..").Path
& (Join-Path $root 'Tools/StationTerminalCompletionFixture/Run.ps1')
if($LASTEXITCODE -ne 0){throw 'Native reader qualification failed'}
function Member([string]$s,[string]$sig){$a=$s.IndexOf($sig);if($a -lt 0){throw "Missing $sig"};$b=$s.IndexOf('{',$a);$i=$b+1;$d=1;while($d){if($s[$i] -eq '{'){$d++};if($s[$i] -eq '}'){$d--};$i++};$s.Substring($a,$i-$a)}
$base=[IO.File]::ReadAllText((Join-Path $root 'Tools/StationTerminalCompletionFixture/out/Check.cs'))
$base=$base.Replace((Member $base 'public static class TerminalCompletionCheck'), '')
$base=$base.Replace((Member $base 'public class RebirthStationGridAdmission'), '')
$base=$base.Replace('public static bool TryReadIdentity(CraftCompleteData d,out Identity id)', 'public static bool HasReservedMarker(CraftCompleteData d){return d.CraftedItemStack.itemValue.Metadata.Keys.Any(k=>k.StartsWith(Prefix,StringComparison.Ordinal));} public static bool TryReadIdentity(CraftCompleteData d,out Identity id)')
$base=$base.Replace('(char*)(void*)((UIntPtr)ptr + num2 * 2)','ptr + num2')
$source='using System.Xml.Linq;using Noemax.GZip;'+$base+[IO.File]::ReadAllText((Join-Path $PSScriptRoot 'Fixture.cs'))
[IO.File]::WriteAllText((Join-Path $PSScriptRoot 'ActualNativeSlices.cs'),$source)
& dotnet run --project (Join-Path $PSScriptRoot 'Fixture.csproj') -c Release
if($LASTEXITCODE -ne 0){throw 'Projection fixture failed'}