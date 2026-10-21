#requires -Version 7.0
$ErrorActionPreference='Stop'
if(Get-Process 7DaysToDie* -ErrorAction SilentlyContinue){throw 'Game must be closed'}
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$temp=Join-Path ([IO.Path]::GetTempPath()) ('rebirth-admission-progress-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $temp | Out-Null
Copy-Item (Join-Path $root 'Scripts/Crafting/Cooking/RebirthCookingAdmissionProgress.cs') (Join-Path $temp 'actual.cs')
Copy-Item (Join-Path $PSScriptRoot 'admission_progress_fixture.cs') (Join-Path $temp 'fixture.cs')
Add-Type -Path (Join-Path $temp 'actual.cs'),(Join-Path $temp 'fixture.cs') -IgnoreWarnings -WarningAction SilentlyContinue
[AdmissionProgressFixture]::Run()
