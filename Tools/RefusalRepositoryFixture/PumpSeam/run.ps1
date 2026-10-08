$ErrorActionPreference='Stop'
$root=Resolve-Path "$PSScriptRoot/../../.."
[IO.File]::WriteAllText("$PSScriptRoot/ActualPumpCandidate.cs",(Get-Content -Raw "$root/Scripts/Survivor/Network/RebirthGearClientTransferPump.cs"))
dotnet run --project "$PSScriptRoot/Fixture.csproj"
if($LASTEXITCODE){throw 'actual pump failed'}
