$ErrorActionPreference='Stop'
$root=Resolve-Path (Join-Path $PSScriptRoot '../..')
$base=Join-Path $root '_Documentation/MultiplayerAudit/ProjectWide_20261014'
$latest=@{}
foreach($directory in Get-ChildItem -LiteralPath $base -Directory -Filter 'offline_batch_20261018*'){
 foreach($file in Get-ChildItem -LiteralPath $directory.FullName -File -Filter 'mp*.mjs.json'){
  $result=Get-Content -LiteralPath $file.FullName -Raw|ConvertFrom-Json
  if($result.result -notin @('PASS','FAIL') -or $result.name -eq 'mp018_existing_cohort_boundaries.mjs'){continue}
  if(!$latest.ContainsKey($result.name) -or $result.finished -gt $latest[$result.name].finished){
   $latest[$result.name]=[pscustomobject]@{name=$result.name;result=$result.result;finished=$result.finished;evidence=$file.FullName}
  }
 }
}
$hashes=@{};$manifests=@{};$coverage=@()
foreach($row in $latest.Values|Sort-Object name){
 $manifestPath=Join-Path (Split-Path $row.evidence) 'manifest.json'
 if(!$manifests.ContainsKey($manifestPath)){$manifests[$manifestPath]=Get-Content -LiteralPath $manifestPath -Raw|ConvertFrom-Json}
 $record=@($manifests[$manifestPath].runners|Where-Object name -eq $row.name)
 if($record.Count -ne 1){throw "Unique manifest record missing: $($row.name)"}
 $inputs=@([pscustomobject]@{path='Tools/GameBridge/'+$row.name;sha256=$record[0].sha256})+@($record[0].literal_inputs)
 $changed=@()
 foreach($input in $inputs){
  if(!$input.path){continue}
  if(!$hashes.ContainsKey($input.path)){
   $path=Join-Path $root $input.path
   $hashes[$input.path]=if(Test-Path -LiteralPath $path){(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash}else{'MISSING'}
  }
  if($hashes[$input.path] -ne $input.sha256){$changed+=$input.path}
 }
 $coverage+=[pscustomobject]@{name=$row.name;result=$row.result;current=($changed.Count -eq 0);changed_inputs=$changed;evidence=$row.evidence}
}
$report=[ordered]@{recorded=[DateTime]::UtcNow.ToString('o');scope='Declared literal fixture inputs and runner hashes; unlisted dynamic inputs/native/topology excluded';total=$coverage.Count;passed=@($coverage|Where-Object result -eq PASS).Count;failed=@($coverage|Where-Object result -eq FAIL).Count;current=@($coverage|Where-Object current).Count;stale=@($coverage|Where-Object {-not $_.current}).Count;runners=$coverage}
$report|ConvertTo-Json -Depth 6|Set-Content -LiteralPath (Join-Path $base 'OFFLINE_CURRENT_INPUT_RECONCILIATION_20261018.json')
[ordered]@{generated=$report.recorded;scope=$report.scope;executed=$report.total;passed=$report.passed;failed=$report.failed;remaining_failures=@($coverage|Where-Object result -eq FAIL|ForEach-Object name);runners=@($latest.Values|Sort-Object name)}|ConvertTo-Json -Depth 5|Set-Content -LiteralPath (Join-Path $base 'OFFLINE_LATEST_ROLLUP_20261018.json')
[ordered]@{total=$report.total;passed=$report.passed;failed=$report.failed;current=$report.current;stale=$report.stale;rerun=@($coverage|Where-Object {-not $_.current -or $_.result -eq 'FAIL'}|ForEach-Object name)}|ConvertTo-Json -Depth 4
