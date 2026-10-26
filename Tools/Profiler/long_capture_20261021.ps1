param(
    [string]$SessionPath = (Join-Path $PSScriptRoot '..\GameBridge\out\session.json'),
    [string]$ProfilePath = (Join-Path $PSScriptRoot 'out\profile_20261021_140738.json'),
    [switch]$SkipBaseline,
    [string[]]$Only
)

$ErrorActionPreference = 'Stop'
$session = Get-Content -LiteralPath $SessionPath -Raw | ConvertFrom-Json
$headers = @{ 'X-Bridge-Token' = $session.token }
$base = "http://127.0.0.1:$($session.port)"
$captureDir = Join-Path $PSScriptRoot ('out\long_capture_' + (Get-Date -Format yyyyMMdd_HHmmss))
New-Item -ItemType Directory -Path $captureDir -Force | Out-Null
$results = New-Object System.Collections.Generic.List[object]

function Bridge([string]$path, [string]$method = 'POST') {
    $response = Invoke-RestMethod -Method $method -Uri ($base + $path) -Headers $headers -TimeoutSec 60
    if ($response.ok -eq $false) { throw "Bridge failed for $path : $($response | ConvertTo-Json -Compress)" }
    return $response
}
function Action([string]$action) {
    $path = if ($action.StartsWith('key:')) { '/key?name=' + [uri]::EscapeDataString($action.Substring(4)) }
        elseif ($action.StartsWith('post:')) { $action.Substring(5) }
        else { '/ui/click?id=' + [uri]::EscapeDataString($action) }
    for ($i = 0; $i -lt 3; $i++) {
        try { return Bridge $path }
        catch { if ($i -eq 2) { throw }; Start-Sleep -Seconds 2 }
    }
}
function Dump([string]$name) {
    New-Item -ItemType File -Path ($ProfilePath + '.dump') -Force | Out-Null
    for ($i = 0; $i -lt 80 -and (Test-Path -LiteralPath ($ProfilePath + '.dump')); $i++) { Start-Sleep -Milliseconds 250 }
    if (Test-Path -LiteralPath ($ProfilePath + '.dump')) { throw 'Profiler did not acknowledge dump request' }
    Start-Sleep -Milliseconds 200
    $dest = Join-Path $captureDir ($name + '.json')
    Copy-Item -LiteralPath $ProfilePath -Destination $dest -Force
    return $dest
}
function Reset-Profile { New-Item -ItemType File -Path ($ProfilePath + '.reset') -Force | Out-Null; Start-Sleep -Milliseconds 500 }
function Return-To-Gameplay {
    for ($i = 0; $i -lt 5; $i++) {
        $ui = Bridge '/ui' 'GET' | ConvertTo-Json -Compress -Depth 8
        if ($ui -notmatch 'windowpaging|crafting|rebirthSurvivorCharacter|rebirthJournal|rebirthProgressionExplorer|rebirthCamperStorageManagement|rebirthCamperEnergyManagement|rebirthCamperUseStation|workstation_campfire|ingameMenu|quests|challenges|players|"map"') { return }
        Action 'key:Escape' | Out-Null
        Start-Sleep -Milliseconds 600
    }
    throw 'Could not return to gameplay'
}
function Capture([string]$label, [string[]]$prepare, [string]$measure, [int]$seconds = 16) {
    Return-To-Gameplay
    foreach ($action in $prepare) { Action $action | Out-Null; Start-Sleep -Milliseconds 850 }
    Reset-Profile
    Start-Sleep -Seconds 4
    $before = Dump ($label + '_before')
    Reset-Profile
    $started = Get-Date
    Action $measure | Out-Null
    # A read-only UI request keeps the bridge's idle menu housekeeping from
    # closing a window during a long steady-state measurement.
    $remaining = $seconds
    while ($remaining -gt 0) {
        $pause = [Math]::Min(20, $remaining)
        Start-Sleep -Seconds $pause
        $remaining -= $pause
        if ($remaining -gt 0) { Bridge '/ui' 'GET' | Out-Null }
    }
    $after = Dump ($label + '_after')
    $ui = Bridge '/ui' 'GET'
    $errors = Bridge '/log?level=error&limit=15' 'GET'
    $item = [ordered]@{ label=$label; started=$started.ToString('o'); seconds=$seconds; before=$before; after=$after; windows=$ui; errors=$errors }
    $results.Add($item)
    $item | ConvertTo-Json -Compress -Depth 8
}

$ping = Bridge '/ping' 'GET'
if ($ping.state -ne 'ingame') { throw "Game state is $($ping.state), expected ingame" }
Bridge '/surroundings?radius=30' 'GET' | ConvertTo-Json -Compress -Depth 5
Bridge '/cleararea?radius=40' | Out-Null
Bridge '/restore' | Out-Null
Bridge ('/console?cmd=' + [uri]::EscapeDataString('settime 1 9 0')) | Out-Null
if (-not $SkipBaseline) {
    Return-To-Gameplay
    Reset-Profile
    Start-Sleep -Seconds 45
    $baseline = Dump 'gameplay_baseline'
    "BASELINE $baseline"
}

$steps = @(
    @{ label='inventory_open'; prepare=@(); measure='key:Tab' },
    @{ label='crafting_tab'; prepare=@('key:Tab','btnRebirthCraftingTabQuests'); measure='btnRebirthCraftingTabCrafting' },
    @{ label='map_tab'; prepare=@('key:Tab'); measure='btnRebirthCraftingTabMap' },
    @{ label='crafting_explorer'; prepare=@('key:Tab','btnRebirthCraftingTabCrafting'); measure='btnRebirthCraftingExplorer' },
    @{ label='character_overview'; prepare=@('key:Tab','btnRebirthCraftingTabQuests'); measure='btnRebirthCraftingTabCharacter' },
    @{ label='character_progression'; prepare=@('key:Tab','btnRebirthCraftingTabCharacter'); measure='btnSurvivorTabProgression' },
    @{ label='character_condition'; prepare=@('key:Tab','btnRebirthCraftingTabCharacter'); measure='btnSurvivorTabCondition' },
    @{ label='character_statistics'; prepare=@('key:Tab','btnRebirthCraftingTabCharacter','btnSurvivorTabOverview'); measure='btnSurvivorTabStatistics' },
    @{ label='character_metabolism'; prepare=@('key:Tab','btnRebirthCraftingTabCharacter','btnSurvivorTabOverview'); measure='btnSurvivorTabMetabolism' },
    @{ label='quests_tab'; prepare=@('key:Tab','btnRebirthCraftingTabCrafting'); measure='btnRebirthCraftingTabQuests' },
    @{ label='challenges_tab'; prepare=@('key:Tab','btnRebirthCraftingTabQuests'); measure='btnRebirthCraftingTabChallenges' },
    @{ label='players_tab'; prepare=@('key:Tab','btnRebirthCraftingTabQuests'); measure='btnRebirthCraftingTabPlayers' },
    @{ label='journal_tab'; prepare=@('key:Tab','btnRebirthCraftingTabQuests'); measure='btnRebirthCraftingTabJournal' }
)
if ($Only) { $steps = $steps | Where-Object { $Only -contains $_.label } }
foreach ($step in $steps) {
    try { Capture $step.label $step.prepare $step.measure }
    catch {
        $failure = [ordered]@{ label=$step.label; error=$_.Exception.Message; time=(Get-Date).ToString('o') }
        $results.Add($failure)
        Write-Warning ($failure | ConvertTo-Json -Compress)
    }
}

$results | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $captureDir 'manifest.json') -Encoding UTF8
"COMPLETE $captureDir"
