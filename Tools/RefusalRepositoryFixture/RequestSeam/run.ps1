$ErrorActionPreference='Stop'
$root=Resolve-Path "$PSScriptRoot/../../.."
[IO.File]::WriteAllText("$PSScriptRoot/ActualRequestCandidate.cs",(Get-Content -Raw "$root/Scripts/Survivor/Network/RebirthGearPreparationRequestNetPackage.cs"))
dotnet run --project "$PSScriptRoot/Fixture.csproj"
if($LASTEXITCODE){throw 'actual request failed'}
