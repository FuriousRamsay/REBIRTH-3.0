$ErrorActionPreference='Stop'
$fixtureRoot=$PSScriptRoot
$source=[IO.Path]::GetFullPath((Join-Path $fixtureRoot '../../Scripts/Vehicles/Camper/Energy/RebirthCamperEnergyRuntimeEstimate.cs'))
$text=[IO.File]::ReadAllText($source)
$matches=[regex]::Matches($text,'(?m)^public enum RebirthCamperEnergyEstimateStatus[^\r\n]+')
if($matches.Count -ne 1){throw 'Exact current estimate enum boundary is missing/nonunique.'}
$output=Join-Path $fixtureRoot 'generated'
New-Item -ItemType Directory -Path $output -Force | Out-Null
$file=Join-Path $output 'ActualEstimateStatus.cs'
[IO.File]::WriteAllText($file,$matches[0].Value+[Environment]::NewLine,(New-Object Text.UTF8Encoding($false)))
$manifest=@{source=$source;sourceHash=(Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash;generatedHash=(Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash}
[IO.File]::WriteAllText((Join-Path $output 'estimate_manifest.json'),($manifest|ConvertTo-Json),(New-Object Text.UTF8Encoding($false)))
Write-Output 'Prepared the exact production estimate enum dependency only; no fixture executed.'
