$ErrorActionPreference='Stop'
$taskSource=[IO.File]::ReadAllText("$PSScriptRoot/../../Scripts/Crafting/UI/RebirthStationDiscoveryPreparationPair.cs")
$taskStart=$taskSource.IndexOf('internal static bool Matches(');if($taskStart -lt 0){throw 'Pair seam absent'}
$taskOpen=$taskSource.IndexOf('{',$taskStart);$taskDepth=1;$taskEnd=$taskOpen+1
while($taskEnd -lt $taskSource.Length -and $taskDepth -gt 0){if($taskSource[$taskEnd] -eq '{'){$taskDepth++};if($taskSource[$taskEnd] -eq '}'){$taskDepth--};$taskEnd++}
if($taskDepth -ne 0){throw 'Pair boundary invalid'}
[IO.File]::WriteAllText("$PSScriptRoot/ActualPreparationPairMatches.cs",'using System.Xml.Linq; static class RebirthStationDiscoveryPreparationPair{'+$taskSource.Substring($taskStart,$taskEnd-$taskStart)+'}')
$taskGate=[IO.File]::ReadAllText("$PSScriptRoot/../../Scripts/Survivor/Capability/RebirthRecipeCapabilityIntegration.cs")
$taskGateStart=$taskGate.IndexOf('public static bool AuthorizeActiveQueue(');if($taskGateStart -lt 0){throw 'Closed gate absent'}
$taskGateOpen=$taskGate.IndexOf('{',$taskGateStart);$taskGateDepth=1;$taskGateEnd=$taskGateOpen+1
while($taskGateEnd -lt $taskGate.Length -and $taskGateDepth -gt 0){if($taskGate[$taskGateEnd] -eq '{'){$taskGateDepth++};if($taskGate[$taskGateEnd] -eq '}'){$taskGateDepth--};$taskGateEnd++}
if($taskGateDepth -ne 0){throw 'Closed gate boundary invalid'}
$taskGateBody=$taskGate.Substring($taskGateStart,$taskGateEnd-$taskGateStart).Replace('public static bool AuthorizeActiveQueue','internal static bool AuthorizeActiveQueue')
[IO.File]::WriteAllText("$PSScriptRoot/ActualClosedGate.cs",'using System;partial class RebirthRecipeCapabilityIntegration{'+$taskGateBody+'}')
dotnet run --project "$PSScriptRoot/Fixture.csproj" -c Release
if($LASTEXITCODE -ne 0){throw 'Queued discovery fixture failed'}
