$ErrorActionPreference='Stop'
$root=(Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$source=[IO.File]::ReadAllText((Join-Path $root 'Scripts/Survivor/Persistence/RebirthWorldCharacterRepository.cs'))
$start=$source.IndexOf('    internal static bool HasSavedRecipeDiscovery(')
if($start -lt 0){throw 'Missing actual witness'}
$brace=$source.IndexOf('{',$start);$depth=1;$end=$brace+1
while($depth -gt 0){if($source[$end]-eq '{'){$depth++};if($source[$end]-eq '}'){$depth--};$end++}
$method=$source.Substring($start,$end-$start)
[IO.File]::WriteAllText((Join-Path $PSScriptRoot 'ActualSavedWitness.cs'),"using System;using System.Xml.Linq;`npartial class RebirthWorldCharacterRepository{`n"+$method+"`n}")
$hash=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($method)))
Write-Output ('Actual method UTF8 SHA256 '+$hash)
dotnet run --project (Join-Path $PSScriptRoot 'Fixture.csproj') -c Release
if($LASTEXITCODE -ne 0){throw 'Fixture failed'}