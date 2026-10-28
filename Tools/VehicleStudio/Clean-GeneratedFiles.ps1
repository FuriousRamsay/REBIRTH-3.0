[CmdletBinding(SupportsShouldProcess=$true)]
param()
$ErrorActionPreference='Stop'
# Delete duplicate report bundles only. Keep canonical builds, backups and all evidence.
$reportRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot 'reports'))
$receiptRoot=Join-Path $reportRoot 'cleanup'
New-Item -ItemType Directory -Path $receiptRoot -Force | Out-Null
$receipt=[ordered]@{utc=[DateTime]::UtcNow.ToString('o');bytesRecovered=0;deleted=@();status='NO_ELIGIBLE_FILES'}
function Get-CamperCleanupBusyProcesses {
  # Shared source audits and validation can consume report artifacts without a game.
  @(Get-Process Unity*,7DaysToDie*,dotnet,MSBuild,VBCSCompiler,python* -ErrorAction SilentlyContinue)
}
$busy=@(Get-CamperCleanupBusyProcesses)
if($busy.Count -gt 0){$receipt.status='SKIPPED_ACTIVE_PROCESS'}
else {
  $canonical=@{}
  $buildRoot=Join-Path $reportRoot 'playable_build'
  foreach($file in Get-ChildItem -LiteralPath $buildRoot -Filter 'rebirth_camper80s.unity3d' -File -Recurse){
    $hash=(Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
    $canonical[$hash]=$file.FullName
  }
  $candidates=@()
  foreach($category in @('dye_candidate_install','integration')){
    $categoryRoot=Join-Path $reportRoot $category
    if(Test-Path -LiteralPath $categoryRoot){$candidates+=@(Get-ChildItem -LiteralPath $categoryRoot -Filter 'rebirth_camper80s.unity3d' -File -Recurse)}
  }
  foreach($file in $candidates){
    # Recheck before every mutation; another chat may start work during hashing.
    if(@(Get-CamperCleanupBusyProcesses).Count -gt 0){
      $receipt.status=if($receipt.deleted.Count -gt 0){'CLEANED_THEN_DEFERRED_ACTIVE_PROCESS'}else{'SKIPPED_ACTIVE_PROCESS'}
      break
    }
    $target=[IO.Path]::GetFullPath($file.FullName)
    if(-not $target.StartsWith($reportRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Cleanup path escaped report directory.'}
    # Never remove backups, symlinks, recent outputs or any unique binary.
    if($target -match '(?i)backup' -or ($file.Attributes -band [IO.FileAttributes]::ReparsePoint) -or $file.LastWriteTimeUtc -gt [DateTime]::UtcNow.AddHours(-3)){continue}
    $hash=(Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash
    if(-not $canonical.ContainsKey($hash)){continue}
    $retained=$canonical[$hash]
    if(-not (Test-Path -LiteralPath $retained) -or (Get-FileHash -LiteralPath $retained -Algorithm SHA256).Hash -ne $hash){throw 'Retained duplicate changed; cleanup stopped.'}
    if($PSCmdlet.ShouldProcess($target,'Remove duplicate generated bundle')){
      Remove-Item -LiteralPath $target -Force
      $receipt.deleted+=@{path=$target;sha256=$hash;bytes=$file.Length;retainedIdenticalCopy=$retained}
      $receipt.bytesRecovered+=$file.Length
      $receipt.status='CLEANED'
    }
  }
}
$receipt | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $receiptRoot ([DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss-fff')+'.json')) -Encoding UTF8
$receipt | ConvertTo-Json -Depth 6
