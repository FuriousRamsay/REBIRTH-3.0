$ErrorActionPreference='Stop'
$root=(Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$source=[IO.File]::ReadAllText((Join-Path $root 'Scripts/Crafting/UI/RebirthStationGridQueue.cs'))
function Method([string]$signature){$s=$source.IndexOf($signature);if($s-lt 0){throw $signature};$b=$source.IndexOf('{',$s);$d=1;$e=$b+1;while($d-gt 0){if($source[$e]-eq '{'){$d++};if($source[$e]-eq '}'){$d--};$e++};return $source.Substring($s,$e-$s)}
$parts=@('public const string Prefix = "rebirth.station.queue.";','public static bool IsMarked(Recipe recipe) => recipe?.ingredients?.Count > 0 && recipe.ingredients[0]?.itemValue != null && recipe.ingredients[0].itemValue.HasMetadata(Prefix + "version");')
foreach($signature in @('private static string DefinitionKey(','public sealed class DefinitionBinding','public static bool TryGetDefinitionBinding(','public static bool TryGetJobId(','public static bool TryGetAdmittedSource(','private static bool TryResolveDefinition(')){$parts+=Method $signature}
[IO.File]::WriteAllText((Join-Path $PSScriptRoot 'ActualQueueSlice.cs'),"using System;using System.IO;using System.Collections.Generic;using System.Security.Cryptography;`npublic static class RebirthStationGridQueue{`n"+($parts-join "`n")+"`n}")
dotnet run --project (Join-Path $PSScriptRoot 'Fixture.csproj') -c Release
if($LASTEXITCODE -ne 0){throw 'Fixture failed'}