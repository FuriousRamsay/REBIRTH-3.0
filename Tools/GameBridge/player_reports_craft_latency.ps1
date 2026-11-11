[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$RunRoot,
    [Parameter(Mandatory)][ValidateSet('Personal','StationInventory','StationInput')][string]$Route,
    [Parameter(Mandatory)][string]$Recipe,
    [Parameter(Mandatory)][string]$OutputItem,
    [int[]]$StationPosition,
    [ValidateRange(1,30)][int]$WaitSeconds = 8,
    [switch]$Solo,
    [switch]$FullTrace,
    [string]$NativeRecipePath,
    [string]$NativeCraftPath
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'player_reports_bridge.ps1')
$root = [IO.Path]::GetFullPath($RunRoot)
$run = Get-Content -LiteralPath (Join-Path $root 'run.json') -Raw | ConvertFrom-Json
if ($run.root -ine $root -or $run.gameName -notmatch '^CodexTestReports-[a-f0-9]{10}$') { throw 'Invalid isolated run receipt.' }
$client = Get-ReportBridge $run.client
$local = (Invoke-ReportBridge $client '/auditstate?domain=topology').domains.topology
if ($Solo) {
    if ($local.remote -or $local.dedicated -or !$local.server -or !$local.localActor -or
        $local.gameName -ne $run.gameName -or !$local.creationId) { throw 'Open the isolated single-player Purge save.' }
    $authority = $client
    Assert-ReportPurgeRules $authority $run $local.entityId
} else {
    $authority = Get-ReportBridge $run.server
    $peer = Assert-ReportPurgePeer $authority $run $local.entityId
    if (!$local.remote -or !$local.connected -or $local.worldKey -ne $peer.worldKey -or
        $local.ownerKey -ne $peer.ownerKey -or $local.creationId -ne $peer.creationId) { throw 'Client and host do not match.' }
}

$ui = Invoke-ReportBridge $client '/ui/tree'
if (@($ui.openWindows | Where-Object { $_ -in 'loadingScreen','progress','messageBox' }).Count) { throw 'Client UI is not ready.' }
$stationWindow = @($ui.openWindows | Where-Object { $_ -like 'workstation_*' })
if ($Route -eq 'Personal') {
    if ($stationWindow.Count -or $StationPosition -or 'crafting' -notin $ui.openWindows) { throw 'Open personal Crafting.' }
} elseif ($stationWindow.Count -ne 1 -or $StationPosition.Count -ne 3) {
    throw 'Open the target station and supply its three block coordinates.'
}
if ($NativeRecipePath -or $NativeCraftPath) {
    if (!$NativeRecipePath -or !$NativeCraftPath) {
        throw 'Native UI paths require both observed selectors.'
    }
    $selected = @($ui.nodes | Where-Object { $_.path -ceq $NativeRecipePath -and $_.text -eq $Recipe })
    $button = @($ui.nodes | Where-Object { $_.path -ceq $NativeCraftPath -and $_.clickable })
    $clickSelector = 'path=' + [uri]::EscapeDataString($NativeCraftPath)
} else {
    $selected = @($ui.nodes | Where-Object { $_.id -eq 'rebirthCraftingSelectedRecipeName' -and $_.text -eq $Recipe })
    $button = @($ui.nodes | Where-Object { $_.id -eq 'btnRebirthCraftingCraft' })
    $clickSelector = 'id=btnRebirthCraftingCraft'
}
if ($selected.Count -ne 1 -or $button.Count -ne 1) { throw 'Select the exact recipe with its Craft button visible.' }

$dir = Join-Path $root ('craft-latency-' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
$null = New-Item -ItemType Directory -Path $dir
function Save($name,$data) { $data | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath (Join-Path $dir "$name.json") }
function Save-Station($name) {
    if ($Route -ne 'Personal') {
        $x,$y,$z = $StationPosition
        Save $name (Invoke-ReportBridge $authority "/station?x=$x&y=$y&z=$z")
    }
}
function Read-Timestamp($entry) {
    $match = [regex]::Match($entry.msg, '\bt=([0-9]+(?:\.[0-9]+)?)')
    if (!$match.Success) { throw 'Missing engine timestamp in craft trace.' }
    [double]::Parse($match.Groups[1].Value,[Globalization.CultureInfo]::InvariantCulture)
}

Save 'ui-before' $ui
Save 'inventory-before' (Invoke-ReportBridge $client '/state?sections=inventory')
Save-Station 'station-before'
$hostSeq = (Invoke-ReportBridge $authority '/ping').lastLogSeq
$queuePattern = 'EXIT XUiC_RecipeStack\.SetRecipe finalResult=true args=\[_recipe=' +
    [regex]::Escape($OutputItem) + '#Recipe@.*;_count=[1-9][0-9]*;'
$filter = if ($FullTrace) { 'CraftTrace36' } else { 'CraftLatency' }
$arm = if ($FullTrace) { 'rbcrafttrace on' }
       elseif ($NativeCraftPath) { "rbcraftlatency arm $OutputItem native" }
       else { "rbcraftlatency arm $OutputItem" }
$disarm = if ($FullTrace) { 'rbcrafttrace off' } else { 'rbcraftlatency off' }
$armSeq = (Invoke-ReportBridge $client '/ping').lastLogSeq
try {
    $null = Invoke-ReportBridge $client '/console' $arm
    if (!$FullTrace) {
        $armed = Invoke-ReportBridge $client "/log?since=$armSeq&contains=CraftLatency&limit=5"
        if (!($armed.entries.msg -match '\[REBIRTH CraftLatency\] armed item=')) {
            throw 'The lightweight probe did not arm. Use a compatible Debug build or pass -FullTrace for the old tracer.'
        }
    }
    $seq = (Invoke-ReportBridge $client '/ping').lastLogSeq
    $input = Invoke-RestMethod -Method Post -Uri "http://127.0.0.1:$($client.port)/ui/click?$clickSelector" -Headers @{'X-Bridge-Token'=$client.token} -TimeoutSec 10
    Save 'input' $input
    if (!$input.ok) { throw 'Native Craft gesture failed.' }
    $until = (Get-Date).AddSeconds($WaitSeconds)
    do {
        $log = Invoke-ReportBridge $client "/log?since=$seq&contains=$filter&limit=350"
        if ($FullTrace) {
            $entry = $log.entries | Where-Object msg -Match 'ENTER ItemActionEntryCraft\.OnActivated' | Select-Object -First 1
            $row = $log.entries | Where-Object {
                $entry -and $_.seq -gt $entry.seq -and $_.msg -match $queuePattern
            } | Select-Object -First 1
        } else {
            $row = $log.entries | Where-Object msg -Match '\[REBIRTH CraftLatency\] queue .* elapsedMs=' | Select-Object -First 1
        }
        if (!$row) { Start-Sleep -Milliseconds 200 }
    } while (!$row -and (Get-Date) -lt $until)
    Save 'client-log' $log
    if (!$Solo) { Save 'server-log' (Invoke-ReportBridge $authority "/log?since=$hostSeq&limit=200") }
    Save 'inventory-after' (Invoke-ReportBridge $client '/state?sections=inventory')
    Save-Station 'station-after'
    $result = [pscustomobject]@{
        mode = if ($Solo) { 'Single-player Purge' } else { 'Dedicated Purge' }
        route = $Route
        recipe = $Recipe
        outputItem = $OutputItem
        dllHash = $run.dllHash
        clock = if ($FullTrace -or $NativeCraftPath) { 'native activation to accepted queue row' }
                else { 'Craft button to accepted queue row' }
        queueDelayMs = if ($row -and $FullTrace) { [Math]::Round(1000 * ((Read-Timestamp $row) - (Read-Timestamp $entry))) }
                       elseif ($row) {
                           $ms = [regex]::Match($row.msg, 'elapsedMs=([0-9.]+)').Groups[1].Value
                           [double]::Parse($ms,[Globalization.CultureInfo]::InvariantCulture)
                       }
        observation = if ($row) { 'Native queue call observed. Review row identity and conservation in the evidence.' }
                      elseif ($entry) { 'Native Craft activation observed without a queue call in the observation window.' }
                      elseif ($FullTrace) { 'No native Craft activation observed.' }
                      else { 'No matching queue result in the observation window.' }
        evidence = $dir
    }
    Save 'summary' $result
    $result | ConvertTo-Json
} finally {
    $null = Invoke-ReportBridge $client '/console' $disarm
}
