param([int]$EntityId = 192, [double]$MaxGapSeconds = 8.0)
$ErrorActionPreference = 'Stop'
$bridge = Join-Path $PSScriptRoot '..\gamebridge.ps1'
$line = & powershell -ExecutionPolicy Bypass -File $bridge log -Contains "entity=$EntityId task=wander action=" -Limit 300
$reply = $line | ConvertFrom-Json
if (-not $reply.ok) { throw "Bridge log query failed: $line" }
$distances = @()
$events = @($reply.entries | ForEach-Object {
    if ($_.msg -match 'action=start origin=\(([-0-9.]+), [-0-9.]+, ([-0-9.]+)\) goal=\(([-0-9.]+), [-0-9.]+, ([-0-9.]+)\)') {
        $dx = [double]$Matches[3] - [double]$Matches[1]
        $dz = [double]$Matches[4] - [double]$Matches[2]
        $distances += [math]::Round([math]::Sqrt($dx * $dx + $dz * $dz), 3)
    }
    if ($_.msg -match '\bt=([0-9.]+)\b.*\baction=(start|end-arrived|end-native|end-orbit|end-not-ready)\b') {
        [pscustomobject]@{ Time = [double]::Parse($Matches[1], [Globalization.CultureInfo]::InvariantCulture); Action = $Matches[2] }
    }
})
$gaps = @()
$previousEnd = $null
foreach ($event in $events) {
    if ($event.Action -eq 'start' -and $null -ne $previousEnd) {
        $gaps += [math]::Round($event.Time - $previousEnd, 3)
        $previousEnd = $null
    }
    elseif ($event.Action.StartsWith('end-')) { $previousEnd = $event.Time }
}
$ends = @($events | Where-Object { $_.Action.StartsWith('end-') })
$failures = @($ends | Where-Object { $_.Action -ne 'end-arrived' })
$maximum = if ($gaps.Count) { [double](($gaps | Measure-Object -Maximum).Maximum) } else { -1.0 }
$report = [pscustomobject]@{
    entity = $EntityId
    routeEnds = $ends.Count
    nonArrivalEnds = $failures.Count
    measuredGaps = $gaps.Count
    gapsSeconds = $gaps
    goalDistancesMeters = $distances
    meanGoalDistanceMeters = if ($distances.Count) { [math]::Round(($distances | Measure-Object -Average).Average, 3) } else { -1.0 }
    maximumGapSeconds = $maximum
    requiredMaximumGapSeconds = $MaxGapSeconds
    passed = $ends.Count -ge 5 -and $failures.Count -eq 0 -and $gaps.Count -ge 4 -and $maximum -le $MaxGapSeconds
}
$report | ConvertTo-Json -Compress
if (-not $report.passed) { exit 1 }
