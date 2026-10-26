# OPTIONAL. Do not run until BUILD_PASS_NOT_INSTALLED is confirmed and game is fully closed.
# Installs a *separate dev-only modlet* under %APPDATA%\7DaysToDie\Mods.
[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$BuildReportDirectory,
    [switch]$ConfirmEacOffAndTestWorld,
    [switch]$RemoveOwnedTestMod
)
$ErrorActionPreference='Stop'; Set-StrictMode -Version Latest
$target=Join-Path $env:APPDATA '7DaysToDie\Mods\REBIRTH_VehicleInspector_Dev'
$marker=Join-Path $target 'REBIRTH_DEV_INSTALL.sha256'
if($RemoveOwnedTestMod) {
    if(-not (Test-Path -LiteralPath $marker -PathType Leaf)) {throw 'No owned developer installation marker: refusing removal.'}
    if(-not $ConfirmEacOffAndTestWorld) {throw 'Use -ConfirmEacOffAndTestWorld for explicit removal approval.'}
    $original=(Get-Content -LiteralPath $marker -Raw).Trim().ToLowerInvariant()
    $dll=Join-Path $target 'RebirthVehicleInspector.dll'
    if(-not (Test-Path -LiteralPath $dll -PathType Leaf) -or (Get-FileHash $dll -Algorithm SHA256).Hash.ToLowerInvariant() -ne $original) {throw 'Installed DLL differs from our original; refusing removal.'}
    $installedManifest=Join-Path $target 'ModInfo.xml'
    $shippedManifest=Join-Path $PSScriptRoot 'Mod\ModInfo.xml'
    if(-not (Test-Path -LiteralPath $installedManifest -PathType Leaf) -or (Get-FileHash $installedManifest -Algorithm SHA256).Hash -ne (Get-FileHash $shippedManifest -Algorithm SHA256).Hash) {throw 'ModInfo.xml was altered; refusing removal.'}
    Remove-Item -LiteralPath $dll -Force
    Remove-Item -LiteralPath $installedManifest -Force
    Remove-Item -LiteralPath $marker -Force
    Write-Host 'Developer DLL and ModInfo removed; export records and folder retained.'
    exit 0
}
if(-not $ConfirmEacOffAndTestWorld) {throw 'Explicit opt-in required: -ConfirmEacOffAndTestWorld. Use EAC disabled and a disposable local world.'}
$result=Join-Path $BuildReportDirectory 'result.json'
if(-not (Test-Path -LiteralPath $result -PathType Leaf)) {throw "Missing build result: $result"}
$r=Get-Content -LiteralPath $result -Raw | ConvertFrom-Json
if($r.status -ne 'BUILD_PASS_NOT_INSTALLED') {throw 'Build did not pass; refusing installation.'}
$dll=Join-Path $BuildReportDirectory 'compiled\RebirthVehicleInspector.dll'
if(-not (Test-Path -LiteralPath $dll -PathType Leaf)) {throw 'Compiled DLL not present in report folder.'}
if([string]::IsNullOrWhiteSpace($r.compiledDllSha256) -or (Get-FileHash -LiteralPath $dll -Algorithm SHA256).Hash.ToLowerInvariant() -ne $r.compiledDllSha256.ToLowerInvariant()) {throw 'Compiled DLL checksum differs from build report; refusing installation.'}
$modSource=Join-Path $PSScriptRoot 'Mod\ModInfo.xml'
if(-not (Test-Path -LiteralPath $modSource -PathType Leaf)) {throw 'Missing ModInfo.xml'}
if(Test-Path -LiteralPath $target) {throw "Existing modlet $target; refusing to overwrite anything. Use the removal script only for an owned installation."}
if(Get-Process -Name '7DaysToDie','7DaysToDie_EAC','7DaysToDieServer' -ErrorAction SilentlyContinue) {throw '7DTD is still running; close it before installing.'}
$null=New-Item -ItemType Directory -Force -Path $target
try {
    Copy-Item -LiteralPath $dll -Destination (Join-Path $target 'RebirthVehicleInspector.dll')
    Copy-Item -LiteralPath $modSource -Destination (Join-Path $target 'ModInfo.xml')
    $hash=(Get-FileHash -LiteralPath (Join-Path $target 'RebirthVehicleInspector.dll') -Algorithm SHA256).Hash.ToLowerInvariant()
    $hash | Set-Content -LiteralPath $marker -Encoding Ascii
    $null=New-Item -ItemType Directory -Force -Path (Join-Path $target 'Exports')
    Write-Host "Installed dev-only inspector: $target"
    Write-Host 'Use EAC OFF and a DISPOSABLE LOCAL test world. This is a dev DLL, NOT part of REBIRTH.'
} catch {
    Write-Warning ('Installation incomplete: '+$_.Exception.Message)
    throw
}
