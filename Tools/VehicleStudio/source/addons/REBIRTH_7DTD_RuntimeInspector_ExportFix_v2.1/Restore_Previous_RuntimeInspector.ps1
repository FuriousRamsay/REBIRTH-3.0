# Restores only the owned development inspector DLL from a v2.1 upgrade backup.
# Never deletes saves, Unity assets, REBIRTH files, or vanilla 7DTD resources.
[CmdletBinding()]
param(
  [Parameter(Mandatory=$true)][string]$BackupDirectory,
  [switch]$ConfirmEacOffAndTestWorld
)
$ErrorActionPreference='Stop';Set-StrictMode -Version Latest
if(-not $ConfirmEacOffAndTestWorld){throw 'Rollback requires -ConfirmEacOffAndTestWorld approval.'}
if(Get-Process -Name '7DaysToDie','7DaysToDie_EAC','7DaysToDieServer' -ErrorAction SilentlyContinue){throw 'Close 7DTD before rollback.'}
$target=Join-Path $env:APPDATA '7DaysToDie\Mods\REBIRTH_VehicleInspector_Dev'
$installed=Join-Path $target 'RebirthVehicleInspector.dll'
$marker=Join-Path $target 'REBIRTH_DEV_INSTALL.sha256'
$old=Join-Path $BackupDirectory 'RebirthVehicleInspector.dll'
$oldMarker=Join-Path $BackupDirectory 'REBIRTH_DEV_INSTALL.sha256'
$expectedNew=Join-Path $BackupDirectory 'upgrade.sha256'
$oldManifest=Join-Path $BackupDirectory 'ModInfo.xml'
$manifest=Join-Path $target 'ModInfo.xml'
foreach($p in @($installed,$marker,$old,$oldMarker,$expectedNew,$oldManifest,$manifest)){
  if(-not (Test-Path -LiteralPath $p -PathType Leaf)){throw "Required rollback evidence missing: $p"}
}
$targetHash=(Get-FileHash -LiteralPath $installed -Algorithm SHA256).Hash.ToLowerInvariant()
$expected=(Get-Content -LiteralPath $expectedNew -Raw).Trim().ToLowerInvariant()
if($targetHash -ne $expected -or (Get-Content $marker -Raw).Trim().ToLowerInvariant() -ne $expected){throw 'Current installed DLL differs from the v2.1 upgrade; refusing rollback.'}
$oldHash=(Get-FileHash -LiteralPath $old -Algorithm SHA256).Hash.ToLowerInvariant()
if($oldHash -ne (Get-Content $oldMarker -Raw).Trim().ToLowerInvariant()){throw 'Previous DLL backup hash does not match ownership marker.'}
if((Get-FileHash $oldManifest -Algorithm SHA256).Hash -ne (Get-FileHash $manifest -Algorithm SHA256).Hash){throw 'ModInfo changed after backup; refusing rollback.'}
Write-Host '[1/2] Restoring previous development inspector DLL (REBIRTH unchanged)...'
Copy-Item -LiteralPath $old -Destination $installed -Force
Copy-Item -LiteralPath $oldMarker -Destination $marker -Force
if((Get-FileHash $installed -Algorithm SHA256).Hash.ToLowerInvariant() -ne $oldHash){throw 'Restore checksum verification failed.'}
Write-Host '[2/2] PASS: previous DLL restored. Game must be restarted to load it.'
Write-Host "Dev mod: $target"
Write-Host "Backup: $BackupDirectory"
