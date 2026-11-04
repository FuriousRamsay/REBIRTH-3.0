param([switch]$VerifyOldFails)
$ErrorActionPreference='Stop'
$source=Join-Path $PSScriptRoot '../../Scripts/NPC/Work/RebirthNpcWorkReservationsAndCoordinator.cs'
$text=[IO.File]::ReadAllText($source)
$begin=$text.IndexOf('public static void Tick(int maximumSessions)')
if($begin-lt 0){throw 'Actual production Tick method missing'}
$brace=$text.IndexOf('{',$begin);$level=1;$end=$brace+1
while($level-gt 0-and$end-lt$text.Length){if($text[$end]-eq'{'){$level++};if($text[$end]-eq'}'){$level--};$end++}
if($level-ne 0){throw 'Unbalanced Tick method'}
$method=$text.Substring($begin,$end-$begin)
if($VerifyOldFails){$guard='lock (Sync) if (Sessions.Count == 0 && SessionSchedule.Count == 0) return;';if(!$method.Contains($guard)){throw 'Regression guard not found'};$method=$method.Replace($guard,'')}
[IO.File]::WriteAllText((Join-Path $PSScriptRoot 'GeneratedTick.cs'),'using System; public static partial class RebirthNpcWorkAssignmentCoordinator {'+$method+'}')
& dotnet build (Join-Path $PSScriptRoot 'Fixture.csproj') -c Release --nologo -v quiet
if($LASTEXITCODE){throw 'Actual Tick fixture did not compile; not a regression result'}
$output=@(& dotnet (Join-Path $PSScriptRoot 'bin/Release/net9.0/Fixture.dll') 2>&1)
$result=$LASTEXITCODE
$output
if($VerifyOldFails){if($result-ne 1-or!($output -match 'FAIL simulated filesystem probe')){throw 'Old method did not reproduce the expected idle filesystem probe'};'EXPECTED: old method fails the idle I/O assertion';exit 0}
exit $result
