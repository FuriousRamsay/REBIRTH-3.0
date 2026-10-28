$ErrorActionPreference='Stop'
$fixtureRoot=$PSScriptRoot
$productionPacket=Join-Path $fixtureRoot '../../Scripts/Network/RebirthMetabolismNetPackages.cs'
$packetText=Get-Content -LiteralPath $productionPacket -Raw
$boundary=$packetText.IndexOf('public enum RebirthHydrationSlotOperation',[StringComparison]::Ordinal)
if($boundary -lt 0){throw 'Actual consume packet extraction boundary absent.'}
$outputRoot=Join-Path $fixtureRoot 'generated'
New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null
[IO.File]::WriteAllText((Join-Path $outputRoot 'ActualConsumePackets.cs'),$packetText.Substring(0,$boundary),(New-Object Text.UTF8Encoding($false)))
