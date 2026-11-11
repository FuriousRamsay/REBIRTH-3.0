[CmdletBinding()]
param(
    [string]$RunRoot,
    [string]$Fixtures,
    [int[]]$Cases,
    [ValidateSet('creator','remote','vehicle','reconnect','craft-hold','roster','solo')][string]$Phase,
    [switch]$List
)
$ErrorActionPreference = 'Stop'
$matrix = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'tests/player_reports_batch.json') -Raw | ConvertFrom-Json
$phases = [ordered]@{
    creator = @(2)
    remote = @(3,8,19,16,7,18,11,4,14,12,13,15)
    vehicle = @(1,21)
    reconnect = @(5,6)
    'craft-hold' = @(10,9)
    roster = @(17)
    solo = @(20)
}
$plan = @(foreach ($entry in $phases.GetEnumerator()) {
    foreach ($id in $entry.Value) {
        $row = $matrix | Where-Object id -eq $id
        if (@($row).Count -ne 1) { throw "Missing or duplicate report $id." }
        $row | Add-Member -NotePropertyName phase -NotePropertyValue $entry.Key -PassThru
    }
})
if ($matrix.Count -ne 21 -or @($plan.id | Select-Object -Unique).Count -ne 21) { throw 'Incomplete report matrix or batch plan.' }
if ($Cases -and @($Cases | Where-Object { $_ -notin $plan.id }).Count) { throw 'Unknown report ID.' }
$selected = @($plan | Where-Object { (!$Cases -or $_.id -in $Cases) -and (!$Phase -or $_.phase -eq $Phase) })
if (!$selected.Count) { throw 'No reports match the selected phase and cases.' }
if ($List) { $selected | Select-Object id,title,phase,mode,setup | ConvertTo-Json -Depth 6; return }
if (!$RunRoot -or !$Fixtures) { throw 'Supply -RunRoot and -Fixtures. Use -List to see setup requirements.' }
$run = Get-Content -LiteralPath (Join-Path $RunRoot 'run.json') -Raw | ConvertFrom-Json
if ($run.root -ine [IO.Path]::GetFullPath($RunRoot) -or $run.gameName -notmatch '^CodexTestReports-[a-f0-9]{10}$') { throw 'Invalid isolated run receipt.' }
$fixtureData = Get-Content -LiteralPath $Fixtures -Raw | ConvertFrom-Json
. (Join-Path $PSScriptRoot 'player_reports_bridge.ps1')
$client = Get-ReportBridge $run.client
$ping = Invoke-ReportBridge $client '/ping'
$activeWindows = (Invoke-ReportBridge $client '/ui/tree').openWindows
$top = $null
$server = $null
$hostTop = $null
$transientWindows = @('loadingScreen','progress','spawnselection','messageBox')
if ('rebirthSurvivorCreator' -in $activeWindows -and 'progress' -notin $activeWindows -and 'messageBox' -notin $activeWindows) {
    $mode = 'creator'
} elseif ($ping.state -eq 'ingame' -and !@($activeWindows | Where-Object { $_ -in $transientWindows }).Count) {
    $top = (Invoke-ReportBridge $client '/auditstate?domain=topology').domains.topology
    if ($top.remote) {
        $server = Get-ReportBridge $run.server
        $hostTop = Assert-ReportPurgePeer $server $run $top.entityId
        if (!$top.connected -or !$top.localActor -or $top.processIdentity -eq $hostTop.processIdentity -or $top.worldKey -ne $hostTop.worldKey -or $top.ownerKey -ne $hostTop.ownerKey -or !$top.creationId -or $top.creationId -ne $hostTop.creationId) {
            throw 'Bridge peers do not agree on the Purge owner, world and character.'
        }
        $mode = 'remote'
    } else {
        if ($top.gameName -ne $run.gameName -or !$top.localActor -or !$top.server -or $top.dedicated) { throw 'Refusing to run in a non-test solo save.' }
        $mode = 'solo'
    }
} else { $mode = 'unready' }
function Resolve-Fixture($value,$data) {
    if ($value -is [array]) {
        $items = [Collections.Generic.List[object]]::new()
        foreach ($v in $value) {
            $resolved = Resolve-Fixture $v $data
            if ($v -is [pscustomobject] -and $v.PSObject.Properties['fixture'] -and $resolved -is [array]) { foreach ($step in $resolved) { $items.Add($step) } }
            else { $items.Add($resolved) }
        }
        return ,$items.ToArray()
    }
    if ($value -is [pscustomobject]) {
        if ($value.PSObject.Properties['fixture']) {
            $key = [string]$value.fixture
            $property = $data.PSObject.Properties[$key]
            if (!$property -or $null -eq $property.Value -or ($property.Value -is [array] -and $property.Value.Count -eq 0)) { throw "Fixture requires '$key'." }
            return ,$property.Value
        }
        $out = [ordered]@{}
        foreach ($p in $value.PSObject.Properties) { $out[$p.Name] = Resolve-Fixture $p.Value $data }
        return [pscustomobject]$out
    }
    return $value
}
$stamp = (Get-Date).ToString('yyyyMMdd-HHmmss-fff')
$out = Join-Path $run.root ('batch-' + $stamp)
$null = New-Item -ItemType Directory -Path $out
$boundaryFailure = $null
$result = foreach ($row in $selected) {
    if ($row.mode -eq 'roster') {
        $rosterPath = Join-Path $run.root 'purge-roster.json'
        $roster = if (Test-Path -LiteralPath $rosterPath) { Get-Content -LiteralPath $rosterPath -Raw | ConvertFrom-Json }
        $count = @($roster.rows | Where-Object { $_ }).Count
        $outcome = if (!$roster -or $count -lt $roster.expected) { 'BLOCKED' } elseif (@($roster.rows | Where-Object { !$_.passed }).Count) { 'FAIL' } else { 'PASS' }
        Write-Host ("[{0}] {1}. {2}" -f $outcome,$row.id,$row.title)
        [pscustomobject]@{id=$row.id;title=$row.title;phase=$row.phase;status=$outcome;reason="$count independent players recorded";scenario=$null;evidence=$rosterPath}
        continue
    }
    $caseFile = $null
    $status = 'BLOCKED'
    $reason = ''
    $requiredMode = if ($row.phase -eq 'creator') { 'creator' } else { $row.mode }
    if ($requiredMode -ne $mode) { $reason = "Requires $requiredMode session. Current session is $mode." }
    elseif ($boundaryFailure) { $reason = $boundaryFailure }
    else {
        try {
            $data = $fixtureData.PSObject.Properties[[string]$row.id]
            if (!$data -or $data.Value.ready -ne $true) { throw $row.setup }
            $steps = Resolve-Fixture $row.steps $data.Value
            if ($data.Value.prepare) { $steps = @($data.Value.prepare) + @($steps) }
            $steps += @([pscustomobject]@{expectNoErrors=$true})
            if (@($steps | Where-Object { $_ -isnot [pscustomobject] -or $_.PSObject.Properties.Count -ne 1 }).Count) { throw 'Invalid fixture step shape.' }
            $caseFile = Join-Path $out ('{0:d2}.json' -f $row.id)
            @{name=('Report {0} - {1}' -f $row.id,$row.title);stopOnFailure=($row.id -notin 1,6,7,9,10,12,16,20);steps=$steps} | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath $caseFile -Encoding UTF8
        } catch { $reason = $_.Exception.Message }
    }
    $summary = $null
    if ($caseFile) {
        $seq = if ($server) { (Invoke-ReportBridge $server '/ping').lastLogSeq }
        if ($server) {
            try { Invoke-ReportBridge $server "/auditstate?domain=$($row.domain)&player=$($top.entityId)" | ConvertTo-Json -Depth 25 | Set-Content -LiteralPath "$caseFile.server-before" }
            catch { $reason = 'Server before observation unavailable.' }
        }
        $transcript = @(& powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'gamebridge.ps1') test $caseFile -SessionPath $run.client.session 2>&1)
        $exitCode = $LASTEXITCODE
        $transcript | Set-Content -LiteralPath "$caseFile.transcript"
        foreach ($line in $transcript) {
            if ([string]$line -match '^\{') { try { $summary = $line | ConvertFrom-Json } catch {} }
        }
        if ($exitCode -ne 0 -or !$summary.passed) { $status = 'FAIL'; $reason = 'See Bridge step results.' }
        elseif ($row.review) { $status = 'REVIEW'; $reason = 'Inspect screenshots and paired observations before declaring a pass.' }
        else { $status = 'PASS' }
        if ($server) {
            try {
                Invoke-ReportBridge $server "/auditstate?domain=$($row.domain)&player=$($top.entityId)" | ConvertTo-Json -Depth 25 | Set-Content -LiteralPath "$caseFile.server-after"
                $errors = Invoke-ReportBridge $server "/log?since=$seq&level=error&limit=50"
                $errors | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath "$caseFile.server-errors"
                if ($errors.count -gt 0) { $status = 'FAIL'; $reason = 'New server errors were captured.' }
            } catch { if ($status -eq 'PASS') { $status = 'REVIEW'; $reason = 'Server evidence unavailable.' } }
        }
        if ($mode -in 'remote','solo') {
            try {
                $state = Invoke-ReportBridge $client '/ping'
                $windows = (Invoke-ReportBridge $client '/ui/tree').openWindows
                $current = (Invoke-ReportBridge $client '/auditstate?domain=topology').domains.topology
                if ($state.state -ne 'ingame' -or @($windows | Where-Object { $_ -in $transientWindows }).Count -or
                    !$current.localActor -or $current.worldKey -ne $top.worldKey -or $current.ownerKey -ne $top.ownerKey -or
                    $current.remote -ne $top.remote -or $current.creationId -ne $top.creationId) {
                    throw 'The client is no longer ready in the original world and character.'
                }
                $top = $current
            } catch { $boundaryFailure = "Stopped after report $($row.id): $($_.Exception.Message)" }
        }
    }
    Write-Host ("[{0}] {1}. {2}" -f $status,$row.id,$row.title)
    [pscustomobject]@{id=$row.id;title=$row.title;phase=$row.phase;status=$status;reason=$reason;scenario=$caseFile;evidence=$summary.evidenceDir}
    $summary = $null
}
$result | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $out 'summary.json') -Encoding UTF8
$result | ConvertTo-Json -Depth 8
if (@($result | Where-Object status -ne 'PASS').Count) { exit 1 }
