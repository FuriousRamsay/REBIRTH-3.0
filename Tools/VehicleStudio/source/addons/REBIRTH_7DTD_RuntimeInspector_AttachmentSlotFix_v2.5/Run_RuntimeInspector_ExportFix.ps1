# REBIRTH Vehicle Studio 7DTD read-only inspector v2.5 — build or guarded upgrade.
# Build compiles without installing; Upgrade compiles, verifies and backs up the owned
# v2.0 developer DLL before replacing it. Neither modifies REBIRTH or vanilla game DLLs.
[CmdletBinding()]
param(
  [ValidateSet('Build','Upgrade')][string]$Mode='Build',
  [string]$GamePath='C:\Program Files (x86)\Steam\steamapps\common\7 Days To Die',
  [string]$ReportsRoot=(Join-Path $env:USERPROFILE 'Downloads\REBIRTH_RuntimeInspector_ExportFix_Reports'),
  [switch]$ConfirmEacOffAndTestWorld
)
$ErrorActionPreference='Stop';Set-StrictMode -Version Latest
$runId=[guid]::NewGuid().ToString('N')
$run=Join-Path $ReportsRoot $runId
$null=New-Item -ItemType Directory -Path $run -Force
$zip=Join-Path $ReportsRoot ('REBIRTH_RuntimeInspector_ExportFix_Report_'+$runId+'.zip')
$gameDll=Join-Path $GamePath '7DaysToDie_Data\Managed\Assembly-CSharp.dll'
$vehiclesXml=Join-Path $GamePath 'Data\Config\vehicles.xml'
$project=Join-Path $PSScriptRoot 'src\VehicleInspector\VehicleInspector.csproj'
$builtDll=Join-Path $PSScriptRoot 'src\VehicleInspector\bin\Release\RebirthVehicleInspector.dll'
$target=Join-Path $env:APPDATA '7DaysToDie\Mods\REBIRTH_VehicleInspector_Dev'
$oldDll=Join-Path $target 'RebirthVehicleInspector.dll'
$marker=Join-Path $target 'REBIRTH_DEV_INSTALL.sha256'
$oldManifest=Join-Path $target 'ModInfo.xml'
$shippedManifest=Join-Path $PSScriptRoot 'Mod\ModInfo.xml'
$outLog=Join-Path $run 'dotnet_stdout.log'
$errLog=Join-Path $run 'dotnet_stderr.log'
$exitCode=$null;$status='FAIL';$stage='preflight';$backupDir=$null;$newHash=$null
$start=[DateTime]::UtcNow
function Stage([int]$number,[string]$label){Write-Host ('[{0}/6] {1}' -f $number,$label)}
function Sha([string]$p){return (Get-FileHash -LiteralPath $p -Algorithm SHA256).Hash.ToLowerInvariant()}
try {
  Stage 1 'Checking installed game and SDK'
  foreach($p in @($gameDll,$vehiclesXml,$project,$shippedManifest)) {
    if(-not (Test-Path -LiteralPath $p -PathType Leaf)){throw "Required file missing: $p"}
  }
  $dotnet=Get-Command 'dotnet.exe' -ErrorAction Stop
  Write-Host "PASS: game assemblies, vehicles.xml, inspector source, .NET SDK"
  if($Mode -eq 'Upgrade') {
    if(-not $ConfirmEacOffAndTestWorld){throw 'Upgrade requires explicit -ConfirmEacOffAndTestWorld and EAC-off disposable-world usage.'}
    if(Get-Process -Name '7DaysToDie','7DaysToDie_EAC','7DaysToDieServer' -ErrorAction SilentlyContinue){throw 'Close 7DTD completely before upgrading the DLL.'}
    foreach($p in @($oldDll,$marker,$oldManifest)) {
      if(-not (Test-Path -LiteralPath $p -PathType Leaf)){throw "Previous owned dev installation missing: $p"}
    }
    if((Get-Content -LiteralPath $marker -Raw).Trim().ToLowerInvariant() -ne (Sha $oldDll)) {throw 'Existing developer DLL differs from installed ownership marker. Refusing to overwrite.'}
    if((Sha $oldManifest) -ne (Sha $shippedManifest)){throw 'Existing developer mod manifest differs from expected source. Refusing to overwrite.'}
    Write-Host "PASS: owned developer modlet verified at $target"
  }
  Stage 2 'Verifying compiler project'
  Write-Host "Project: $project"
  Write-Host "Game: $GamePath"
  Stage 3 'Compiling with live elapsed-time updates'
  $stage='compile'
  $buildArguments='build "{0}" -c Release -p:Game7D2D="{1}" -nologo -v:minimal' -f $project,$GamePath
  $proc=Start-Process -FilePath $dotnet.Source -ArgumentList $buildArguments -PassThru -WorkingDirectory $PSScriptRoot -NoNewWindow -RedirectStandardOutput $outLog -RedirectStandardError $errLog
  $elapsed=[DateTime]::UtcNow;$heartbeat=0
  while(-not $proc.WaitForExit(1000)) {
    if(([DateTime]::UtcNow-$elapsed).TotalSeconds -ge 10){
      $elapsed=[DateTime]::UtcNow
      $heartbeat++
      Write-Host ('  Build active: {0:N0}s elapsed; heartbeat #{1}. Logs: {2}' -f ([DateTime]::UtcNow-$start).TotalSeconds,$heartbeat,$run)
    }
  }
  $proc.WaitForExit();$proc.Refresh();$exitCode=[int]$proc.ExitCode
  Write-Host "dotnet build exit code: $exitCode"
  if(Test-Path -LiteralPath $outLog){Get-Content -LiteralPath $outLog -Tail 35 | ForEach-Object {Write-Host $_}}
  if(Test-Path -LiteralPath $errLog){Get-Content -LiteralPath $errLog -Tail 25 | ForEach-Object {Write-Host $_}}
  if($exitCode -ne 0){throw "C# compilation failed (exit $exitCode). See $run"}
  Stage 4 'Verifying compiled DLL'
  if(-not (Test-Path -LiteralPath $builtDll -PathType Leaf) -or (Get-Item $builtDll).Length -lt 4096){throw "Missing/invalid output DLL: $builtDll"}
  $newHash=Sha $builtDll
  $null=New-Item -ItemType Directory -Force -Path (Join-Path $run 'compiled')
  Copy-Item -LiteralPath $builtDll -Destination (Join-Path $run 'compiled\RebirthVehicleInspector.dll')
  if((Sha (Join-Path $run 'compiled\RebirthVehicleInspector.dll')) -ne $newHash){throw 'Build report copy SHA mismatch'}
  Write-Host "PASS: compiled DLL SHA-256 $newHash"
  Stage 5 'Applying requested action'
  if($Mode -eq 'Build'){
    $status='BUILD_PASS_NOT_INSTALLED'
    Write-Host 'No installed DLL modified (Build-only mode).'
  }else{
    $stage='upgrade'
    $backupDir=Join-Path (Join-Path $env:USERPROFILE 'Downloads\REBIRTH_RuntimeInspector_UpgradeBackups') $runId
    $null=New-Item -ItemType Directory -Force -Path $backupDir
    Copy-Item -LiteralPath $oldDll -Destination (Join-Path $backupDir 'RebirthVehicleInspector.dll')
    Copy-Item -LiteralPath $oldManifest -Destination (Join-Path $backupDir 'ModInfo.xml')
    Copy-Item -LiteralPath $marker -Destination (Join-Path $backupDir 'REBIRTH_DEV_INSTALL.sha256')
    (Get-Content -LiteralPath $marker -Raw).Trim().ToLowerInvariant() | Set-Content -LiteralPath (Join-Path $backupDir 'previous.sha256') -Encoding ASCII
    $newHash | Set-Content -LiteralPath (Join-Path $backupDir 'upgrade.sha256') -Encoding ASCII
    $temp=Join-Path $target ('RebirthVehicleInspector.dll.new_'+$runId)
    Copy-Item -LiteralPath $builtDll -Destination $temp
    try{
      if((Sha $temp) -ne $newHash){throw 'Staged DLL checksum mismatch'}
      Copy-Item -LiteralPath $temp -Destination $oldDll -Force
      $newHash | Set-Content -LiteralPath $marker -Encoding ASCII
      if((Sha $oldDll) -ne $newHash){throw 'Installed DLL checksum mismatch after upgrade'}
      $status='UPGRADE_PASS_RESTART_GAME_TO_TEST'
      Write-Host 'PASS: v2.5 DLL installed into isolated dev modlet only.'
      Write-Host "Previous DLL backup: $backupDir"
    }catch{
      Write-Warning ('Upgrade failed; restoring original dev DLL: '+$_.Exception.Message)
      Copy-Item -LiteralPath (Join-Path $backupDir 'RebirthVehicleInspector.dll') -Destination $oldDll -Force
      Copy-Item -LiteralPath (Join-Path $backupDir 'REBIRTH_DEV_INSTALL.sha256') -Destination $marker -Force
      throw
    }finally{if(Test-Path -LiteralPath $temp){Remove-Item -LiteralPath $temp -Force}}
  }
  Stage 6 'Writing verification report'
}catch{
  Write-Warning ('FAIL during '+$stage+': '+$_.Exception.Message)
  $_ | Out-String | Set-Content -LiteralPath (Join-Path $run 'exception.txt')
}finally{
  $report=[ordered]@{tool='REBIRTH runtime inspector export fix';version='2.5';runId=$runId;mode=$Mode;status=$status;stage=$stage;exitCode=$exitCode;newDllSha256=$newHash;backupDirectory=$backupDir;gameAssemblySha256=$(if(Test-Path $gameDll){Sha $gameDll}else{$null});vehiclesXmlSha256=$(if(Test-Path $vehiclesXml){Sha $vehiclesXml}else{$null});modPath=$target;capturedUtc=[DateTime]::UtcNow.ToString('o')}
  $report | ConvertTo-Json -Depth 7 | Set-Content -LiteralPath (Join-Path $run 'result.json') -Encoding UTF8
  if(-not (Test-Path -LiteralPath $zip)){Compress-Archive -Path (Join-Path $run '*') -DestinationPath $zip}
  Write-Host "STATUS: $status"
  Write-Host "Report: $run\result.json"
  Write-Host "Report ZIP: $zip"
  if($Mode -eq 'Upgrade'){Write-Host "Developer mod path (REBIRTH unchanged): $target"}
  Write-Host 'The original camper assets, REBIRTH mod and vanilla 7DTD DLLs were not altered.'
}
if($status -notin @('BUILD_PASS_NOT_INSTALLED','UPGRADE_PASS_RESTART_GAME_TO_TEST')){exit 1}
