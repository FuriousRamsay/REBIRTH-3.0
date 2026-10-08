$ErrorActionPreference='Stop'
$root=(Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$source=[IO.File]::ReadAllText((Join-Path $root 'Scripts/Survivor/Persistence/RebirthWorldCharacterRepository.cs'))
$methods=@()
foreach($signature in @('    internal static bool HasSavedStationDiscoveryAdmission(','    internal static bool TryGetSavedStationPreparationIntent(')){
$s=$source.IndexOf($signature);if($s-lt 0){throw 'actual method missing'};$b=$source.IndexOf('{',$s);$d=1;$e=$b+1;while($d-gt 0){if($source[$e]-eq '{'){$d++};if($source[$e]-eq '}'){$d--};$e++}
$method=$source.Substring($s,$e-$s);$methods+=$method
Write-Output ($signature.Trim()+' UTF8SHA '+[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($method))))
}
[IO.File]::WriteAllText((Join-Path $PSScriptRoot 'ActualPairedSavedWitness.cs'),"using System;using System.Xml.Linq;`npartial class RebirthWorldCharacterRepository{`n"+($methods-join "`n")+"`n}")
dotnet run --project (Join-Path $PSScriptRoot 'Fixture.csproj') -c Release
if($LASTEXITCODE-ne 0){throw 'paired fixture failed'}