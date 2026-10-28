param([string]$Source='C:/Users/Etienne/.codex/worktrees/compatibility-34-review/zzz_REBIRTH__3_0/Scripts/Vehicles/Tuning/RebirthVehicleTuning.Ui.cs')
$ErrorActionPreference='Stop'
$sourceBytes=[IO.File]::ReadAllBytes($Source)
$text=[Text.Encoding]::UTF8.GetString($sourceBytes)
$sourceHash=[BitConverter]::ToString([Security.Cryptography.SHA256]::Create().ComputeHash($sourceBytes)).Replace('-','')
$start=$text.IndexOf('#if REBIRTH_DEBUG')
$end=$text.IndexOf('[Preserve]',$start)
if($start -lt 0 -or $end -lt 0){throw 'Exact service extraction boundary missing'}
$actual=$text.Substring($start,$end-$start)+[Environment]::NewLine+'#endif'+[Environment]::NewLine
[IO.File]::WriteAllText((Join-Path $PSScriptRoot 'ActualService.cs'),$actual)
$receipt=[ordered]@{Source=$Source;SourceSHA256=$sourceHash;ExtractedSHA256=(Get-FileHash (Join-Path $PSScriptRoot 'ActualService.cs') -Algorithm SHA256).Hash;ExtractedAtUtc=[DateTime]::UtcNow.ToString('O');Boundary='Start through before [Preserve] console class; actual static service unmodified';Limitations='Native API stubs; exact service compiled, no game/runtime acceptance'}
$receipt|ConvertTo-Json|Set-Content (Join-Path $PSScriptRoot 'SOURCE_RECEIPT.json')
Push-Location $PSScriptRoot
try{dotnet run --project Fixture.csproj --configuration Debug --no-launch-profile --property:RestoreIgnoreFailedSources=true; if($LASTEXITCODE -ne 0){throw 'Fixture failed'}}finally{Pop-Location}
