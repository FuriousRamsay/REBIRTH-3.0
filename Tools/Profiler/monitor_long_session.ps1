param([int]$Minutes = 60)

$ErrorActionPreference = 'Continue'
$root = Split-Path -Parent $PSScriptRoot
$out = Join-Path $PSScriptRoot ('out\monitor_' + (Get-Date -Format yyyyMMdd_HHmmss))
New-Item -ItemType Directory -Path $out -Force | Out-Null
$request = Join-Path $PSScriptRoot 'out\metrics.request'
$metrics = Join-Path $PSScriptRoot 'out\metrics.json'
$csv = Join-Path $out 'samples.csv'
$started = Get-Date

for ($i = 0; $i -lt $Minutes; $i++) {
    $game = Get-Process 7DaysToDie -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($null -eq $game) { 'Game exited at ' + (Get-Date).ToString('o') | Set-Content (Join-Path $out 'ended.txt'); break }
    $before = if (Test-Path $metrics) { (Get-Item $metrics).LastWriteTimeUtc } else { [datetime]::MinValue }
    New-Item -ItemType File -Path $request -Force | Out-Null
    for ($j = 0; $j -lt 20; $j++) {
        Start-Sleep -Milliseconds 250
        if ((Test-Path $metrics) -and (Get-Item $metrics).LastWriteTimeUtc -gt $before) { break }
    }
    if (Test-Path $metrics) {
        try {
            $m = Get-Content $metrics -Raw | ConvertFrom-Json
            $sample = [pscustomobject]@{
                time = (Get-Date).ToString('o'); pid = $game.Id; fps = $m.fps; frameMs = $m.frameMs
                p99Ms = $m.p99Ms; worstMs = $m.worstMs; mainThreadPct = $m.mainThreadPct
                processCores = $m.processCores; gpu3dPct = $m.gpu3dPct; gpuVramMb = $m.gpuVramMb
                workingSetMb = $m.workingSetMb; eventsSeen = $m.eventsSeen
                presentCount = @($m.presentTicks).Count; frameError = $m.frameError
            }
            $sample | Export-Csv -Path $csv -NoTypeInformation -Append
            if (($i % 10) -eq 0) { Copy-Item $metrics (Join-Path $out ('frame_ticks_' + $i + '.json')) -Force }
        }
        catch { $_ | Out-String | Add-Content (Join-Path $out 'monitor_errors.txt') }
    }
    if ($i -lt $Minutes - 1) { Start-Sleep -Seconds 55 }
}

'Monitor finished at ' + (Get-Date).ToString('o') | Set-Content (Join-Path $out 'complete.txt')
