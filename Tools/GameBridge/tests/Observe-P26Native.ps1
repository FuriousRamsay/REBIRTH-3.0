[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$ClientStage,
    [string]$ServerStage,
    [Parameter(Mandatory=$true)][ValidatePattern('^Codex[A-Za-z0-9_-]+$')][string]$SaveName,
    [Parameter(Mandatory=$true)][ValidatePattern('^[A-Za-z0-9_-]{1,36}$')][string]$Label,
    [Parameter(Mandatory=$true)][ValidateSet('Idle','Burning','Retired','Thermal')][string]$Phase,
    [Parameter(Mandatory=$true)][int]$X,
    [Parameter(Mandatory=$true)][int]$Y,
    [Parameter(Mandatory=$true)][int]$Z,
    [ValidateSet('none','campfire','woodstove','greenhouse')][string]$HeatSource = 'none',
    [ValidatePattern('^[A-Za-z0-9_-]+$')][string]$CropBlock,
    [Parameter(Mandatory=$true)][string]$Workspace,
    [switch]$RequireClientVisible,
    [switch]$RequireCooldown
)
$ErrorActionPreference = 'Stop'
$helper = Join-Path $Workspace 'Tools/GameBridge/Invoke-PurgeRegression.ps1'
if ($RequireClientVisible -and (-not $ServerStage -or $Phase -ne 'Burning')) {
    throw 'Visible fire requires the Burning phase and a real dedicated client.'
}
if ($RequireCooldown -and $Phase -ne 'Retired') { throw 'Cooldown requires the Retired phase.' }
function Observe([string]$stage, [string]$step, [string[]]$arguments) {
    $result = & $helper -Stage $stage -Label ("p26-{0}-{1}" -f $Label,$step) -Direct -ArgumentsJson (ConvertTo-Json -InputObject $arguments -Compress)
    $response = ($result -join [Environment]::NewLine) | ConvertFrom-Json
    if ($response.ok -eq $false -or $response.hadError) { throw "Bridge refused observation $step." }
    return $response
}
function Get-ConsoleLines($response) {
    foreach ($result in $response.results) {
        $result.output
        foreach ($entry in $result.log) {
            if ($entry.msg) { $entry.msg }
        }
    }
}
$start = Observe $ClientStage 'client-start' @('state','-Sections','player')
if ($start.state -ne 'ingame' -or -not $start.guard.enabled -or $start.player.dead) {
    throw 'Prepared, live actor and enabled Guard required.'
}
$client = (Observe $ClientStage 'client-topology' @('call','GET','/auditstate','domain=topology')).domains.topology
if (!$client.ownerKey -or !$client.worldKey -or !$client.creationId -or !$client.localActor) {
    throw 'Current native created actor required.'
}
$id = [int]$client.entityId
$authorityStage = $ClientStage
if ($ServerStage) {
    if (-not $client.remote -or -not $client.connected) { throw 'Current remote client required.' }
    $authorityStage = $ServerStage
    $authority = (Observe $ServerStage 'server-topology' @('call','GET','/auditstate','domain=topology',"player=$id")).domains.topology
    if (-not $authority.dedicated -or -not $authority.authoritativePeer -or $authority.remote -or
        $client.ownerKey -ne $authority.ownerKey -or $client.worldKey -ne $authority.worldKey -or
        $client.creationId -ne $authority.creationId -or $client.entityId -ne $authority.entityId) {
        throw 'Authenticated peer and same world/actor required.'
    }
} else {
    if ($client.remote -or $client.dedicated) { throw 'Solo mode requires local authority.' }
    $authority = $client
}
if (-not $authority.server -or $authority.gameName -ne $SaveName) { throw 'Disposable authority save did not match.' }
$purgeResponse = Observe $authorityStage 'purge' @('console',"rbpurgetestobserve player $id")
$purgeLines = @($purgeResponse.results | ForEach-Object { $_.output } | Where-Object { $_ -is [string] -and $_.StartsWith('{') })
if ($purgeLines.Count -ne 1) { throw 'Expected exactly one native Purge observation.' }
$purge = $purgeLines[0] | ConvertFrom-Json
if ($purge.purgeEnabled -ne $true -or $purge.saveName -ne $SaveName -or $purge.player -ne $id) {
    throw 'Require enabled Purge on the matching disposable world and actor.'
}
$position = @("x=$X","y=$Y","z=$Z")
$report = [ordered]@{phase=$Phase; save=$SaveName; player=$id; dedicated=[bool]$ServerStage; position=$position}
if ($Phase -eq 'Thermal') {
    if (!$CropBlock) { throw 'Specify the exact prepared crop block.' }
    $command = Observe $authorityStage 'thermal' @('console',"rbfarming heattest $X $Y $Z")
    $lines = @(Get-ConsoleLines $command | Select-Object -Unique)
    $row = @($lines | Where-Object { $_ -match '\[AdvancedFarming HeatTest\] label=direct ' })
    $registry = @($lines | Where-Object { $_ -match '\[AdvancedFarming HeatRegistry\] registeredHeatSources=' })
    if ($row.Count -ne 1 -or $registry.Count -ne 1 -or
        $row[0] -notmatch ('(?:^|\s)block=' + [regex]::Escape($CropBlock) + '(?:\s|$)') -or
        $row[0] -notmatch ('(?:^|\s)heatSource=' + $HeatSource + '(?:\s|$)') -or
        $registry[0] -notmatch 'resultCacheSeconds=2\+jitterSeconds=0') {
        throw 'Exact crop, requested authored heat and cache contract were not observed. Inspect the retained thermal JSON.'
    }
    $report.thermal = $row[0]
    $report.heatRegistry = $registry[0]
} else {
    $radius = if ($Phase -eq 'Idle') { 'radius=8' } else { 'radius=0' }
    $query = @('call','GET','/auditstate','domain=fire',"player=$id",$radius) + $position
    $snapshot = (Observe $authorityStage 'authority-fire' $query).domains.fire
    if (!$snapshot.available -or !$snapshot.authoritative -or !$snapshot.enabled -or $snapshot.truncated) {
        throw 'Current exact authority fire observation required.'
    }
    $fires = @($snapshot.fires | Where-Object { $null -ne $_ })
    $cooldowns = @($snapshot.cooldowns | Where-Object { $null -ne $_ })
    if ($Phase -eq 'Burning' -and $fires.Count -ne 1) { throw 'Exact native active fire missing.' }
    if ($Phase -ne 'Burning' -and $fires.Count -ne 0) { throw 'Unexpected native active fire.' }
    if ($Phase -eq 'Idle' -and ($cooldowns.Count -ne 0 -or $snapshot.queued -ne 0)) { throw 'Idle fixture has retained fire work.' }
    if ($RequireCooldown -and ($Phase -ne 'Retired' -or $cooldowns.Count -ne 1)) { throw 'Expected exactly one retained cooldown.' }
    $report.authority = $snapshot
    if ($ServerStage) {
        $projection = $null
        for ($i = 0; $i -lt 8; $i++) {
            $candidate = (Observe $ClientStage "client-fire-$i" (@('call','GET','/auditstate','domain=fire','radius=0') + $position)).domains.fire
            if ($candidate.available -and $candidate.projection -and -not $candidate.snapshotPending -and
                $candidate.active -eq ($Phase -eq 'Burning') -and
                (-not $RequireClientVisible -or $candidate.visible) -and
                ($Phase -eq 'Burning' -or -not $candidate.visible) -and
                (-not $RequireCooldown -or [uint64]$candidate.cooldown -eq [uint64]$cooldowns[0].expires)) {
                $projection = $candidate
                break
            }
            Start-Sleep -Milliseconds 350
        }
        if (!$projection) { throw 'Exact client projection did not settle to required state.' }
        $report.client = $projection
    }
}
$log = Observe $ClientStage 'client-errors' @('errors','-Since',([string]$start.lastLogSeq))
if ($log.count -ne 0) { throw 'New client errors occurred during this probe.' }
[pscustomobject]$report | ConvertTo-Json -Depth 12
