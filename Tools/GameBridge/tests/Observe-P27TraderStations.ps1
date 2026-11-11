[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$Workspace,
    [Parameter(Mandatory=$true)][string]$ClientStage,
    [string]$ServerStage,
    [Parameter(Mandatory=$true)][ValidatePattern('^Codex[A-Za-z0-9_-]+$')][string]$SaveName,
    [Parameter(Mandatory=$true)][ValidatePattern('^[A-Za-z0-9_-]{1,36}$')][string]$Label,
    [Parameter(Mandatory=$true)][ValidateSet('bob','hugh','jen','joel','rekt')][string]$Trader,
    [Parameter(Mandatory=$true)][ValidateRange(0,3)][int]$Rotation,
    [Parameter(Mandatory=$true)][hashtable]$Expected,
    [switch]$BaseControl
)
$ErrorActionPreference = 'Stop'
$helper = Join-Path $Workspace 'Tools/GameBridge/Invoke-PurgeRegression.ps1'
$offsets = @{
    bob  = @{Modifier=@(52,1,35); Saw=@(8,1,25);  Shredder=@(37,2,30); Recycler=@(47,1,40)}
    hugh = @{Modifier=@(14,1,18); Saw=@(14,1,14); Shredder=@(16,1,13); Recycler=@(53,1,40)}
    jen  = @{Modifier=@(52,2,37); Saw=@(52,2,42); Shredder=@(52,2,44); Recycler=@(19,2,20)}
    joel = @{Modifier=@(53,1,39); Saw=@(19,1,39); Shredder=@(17,1,36); Recycler=@(41,1,50)}
    rekt = @{Modifier=@(39,1,16); Saw=@(41,1,34); Shredder=@(41,1,39); Recycler=@(45,1,9)}
}
if ($Expected.Count -lt 1 -or $Expected.Count -gt 4) { throw 'Specify one to four explicit expected block names.' }
foreach ($station in $Expected.Keys) {
    if (!$offsets[$Trader].ContainsKey($station) -or [string]::IsNullOrWhiteSpace($Expected[$station])) {
        throw "Unknown expected station or empty block name: $station"
    }
}
function Observe([string]$stage, [string]$step, [string[]]$arguments) {
    $raw = & $helper -Stage $stage -Label ("p27-{0}-{1}" -f $Label,$step) -Direct -ArgumentsJson (ConvertTo-Json -InputObject $arguments -Compress)
    $response = ($raw -join [Environment]::NewLine) | ConvertFrom-Json
    if ($response.ok -eq $false -or $response.hadError) { throw "Bridge refused observation $step." }
    return $response
}
function Get-WorldPosition([int[]]$origin,[int[]]$size,[int[]]$local,[int]$rotation) {
    $x = $local[0]
    $z = $local[2]
    switch ($rotation) {
        1 { $x=$size[2]-1-$local[2]; $z=$local[0] }
        2 { $x=$size[0]-1-$local[0]; $z=$size[2]-1-$local[2] }
        3 { $x=$local[2]; $z=$size[0]-1-$local[0] }
    }
    return @(($origin[0]+$x),($origin[1]+$local[1]),($origin[2]+$z))
}
function Assert-Block($observation,[int[]]$position,[string]$name,[bool]$authoritative) {
    if ($observation.authoritative -ne $authoritative -or $observation.block -cne $name -or
        $observation.x -ne $position[0] -or $observation.y -ne $position[1] -or $observation.z -ne $position[2]) {
        throw "Wrong native cell $($position -join ','): expected $name, observed $($observation.block). Inspect the saved receipt."
    }
}
$start = Observe $ClientStage 'client-start' @('state','-Sections','player')
if ($start.state -ne 'ingame' -or -not $start.guard.enabled -or $start.player.dead) {
    throw 'Prepared live actor and enabled Guard required.'
}
$client = (Observe $ClientStage 'client-topology' @('call','GET','/auditstate','domain=topology')).domains.topology
if (!$client.ownerKey -or !$client.worldKey -or !$client.localActor) { throw 'Current native local actor required.' }
if (-not $BaseControl -and !$client.creationId) { throw 'Current Survivor creation required.' }
$id = [int]$client.entityId
$authorityStage = $ClientStage
if ($ServerStage) {
    if (-not $client.remote -or -not $client.connected) { throw 'Current remote client required.' }
    $authorityStage = $ServerStage
    $authority = (Observe $ServerStage 'server-topology' @('call','GET','/auditstate','domain=topology',"player=$id")).domains.topology
    if (-not $authority.dedicated -or -not $authority.authoritativePeer -or $authority.remote -or
        $client.ownerKey -ne $authority.ownerKey -or $client.worldKey -ne $authority.worldKey -or
        $client.creationId -ne $authority.creationId -or $client.entityId -ne $authority.entityId) {
        throw 'Authenticated peer and same native world/actor required.'
    }
} else {
    if ($client.remote -or $client.dedicated) { throw 'Solo mode requires local authority.' }
    $authority = $client
}
if (-not $authority.server -or $authority.gameName -ne $SaveName) { throw 'Disposable authority save did not match.' }
$response = Observe $authorityStage 'native-prefab' @('console',"rbpurgetestobserve player $id")
$lines = @($response.results | ForEach-Object { $_.output } | Where-Object { $_ -is [string] -and $_.StartsWith('{') })
if ($lines.Count -ne 1) { throw 'Expected exactly one native Purge observation.' }
$world = $lines[0] | ConvertFrom-Json
$prefab = $world.scannerNativePoi
$requiredPurge = -not $BaseControl
if ($world.saveName -ne $SaveName -or $world.player -ne $id -or $world.purgeEnabled -ne $requiredPurge -or
    $prefab.prefab -cne "trader_$Trader" -or @($prefab.origin).Count -ne 3 -or @($prefab.size).Count -ne 3) {
    throw 'Wrong mode, authority actor or containing native trader prefab. Stand inside the intended trader before sampling.'
}
$origin = [int[]]$prefab.origin
$size = [int[]]$prefab.size
$rows = @(
    foreach ($station in @('Modifier','Saw','Shredder','Recycler')) {
        if (!$Expected.ContainsKey($station)) { continue }
        $position = @(Get-WorldPosition $origin $size $offsets[$Trader][$station] $Rotation)
        $blockQuery = @('call','GET','/blockdamage',"x=$($position[0])","y=$($position[1])","z=$($position[2])")
        $hostBlock = Observe $authorityStage "$station-authority" $blockQuery
        Assert-Block $hostBlock $position $Expected[$station] $true
        $clientBlock = $null
        if ($ServerStage) {
            $clientBlock = Observe $ClientStage "$station-client" $blockQuery
            Assert-Block $clientBlock $position $Expected[$station] $false
        }
        [pscustomobject]@{station=$station; position=$position; authority=$hostBlock; client=$clientBlock}
    }
)
$log = Observe $ClientStage 'client-errors' @('errors','-Since',([string]$start.lastLogSeq))
if ($log.count -ne 0) { throw 'New client errors occurred during this probe.' }
[pscustomobject]@{
    save=$SaveName; actor=$id; dedicated=[bool]$ServerStage; purge=$world.purgeEnabled
    prefab=$prefab.prefab; origin=$origin; size=$size; fixtureRotation=$Rotation; rows=$rows
} | ConvertTo-Json -Depth 12
