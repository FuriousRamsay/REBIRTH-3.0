param([Parameter(Mandatory=$true)][string]$Stage)
$ErrorActionPreference='Stop';$h=Join-Path $PSScriptRoot 'Invoke-PurgeRegression.ps1';$rows=@();$watch=[Diagnostics.Stopwatch]::StartNew()
for($i=0;$i -lt 35 -and $watch.Elapsed.TotalSeconds -lt 150;$i++){
 $raw=& $h -Stage $Stage -Label "supply-followup-$i" -Direct -ArgumentsJson '["console","rbpurgetestobserve"]'
 $r=($raw -join "`n")|ConvertFrom-Json;$o=$r.results[0].output[0]|ConvertFrom-Json
 $rows+=@{seconds=$watch.Elapsed.TotalSeconds;supply=$o.supply;diagnostic=$o.supplyDeliveryDiagnostic;crates=$o.nativeSupplyCrates;flight=$o.nativeSupplyFlightActive}
 $rows|ConvertTo-Json -Depth 10|Set-Content (Join-Path $Stage 'supply-followup-progress.json')
 if($o.supply.deliveredDrops -ge $o.supply.earnedDrops -and $o.supply.earnedDrops -ge 2){break}
 Start-Sleep -Seconds 4
}
$rows[-1]|ConvertTo-Json -Depth 10
