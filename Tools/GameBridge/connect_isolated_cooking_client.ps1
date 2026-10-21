param(
    [Parameter(Mandatory = $true)][int]$ClientPid,
    [int]$TimeoutSeconds = 600
)

$ErrorActionPreference = 'Stop'
$bridge = Join-Path $PSScriptRoot 'gamebridge.ps1'
$sessionRoot = Join-Path $PSScriptRoot 'out\sessions'
$sessionFile = Get-ChildItem -LiteralPath $sessionRoot -Filter session.json -Recurse |
    Where-Object { $_.Directory.Name -like "$ClientPid-*" } | Sort-Object LastWriteTime -Descending | Select-Object -First 1
if (-not $sessionFile) { throw "No live Bridge session for client PID $ClientPid" }
$session = Get-Content -LiteralPath $sessionFile.FullName -Raw | ConvertFrom-Json
if ($session.pid -ne $ClientPid) { throw 'Bridge session PID mismatch' }
if ($session.port -ne 8870) { throw 'This helper only controls the isolated cooking client on Bridge port 8870' }
$env:REBIRTH_BRIDGE_SESSION = $sessionFile.FullName

function Invoke-Bridge([string[]]$BridgeArguments) {
    $raw = & powershell -NoProfile -ExecutionPolicy Bypass -File $bridge @BridgeArguments
    if ($LASTEXITCODE -ne 0) { throw "Bridge command failed: $($BridgeArguments -join ' ')" }
    $value = $raw | ConvertFrom-Json
    if (-not $value.ok) { throw "Bridge command rejected: $($BridgeArguments -join ' ')" }
    return $value
}
function Click-Node($tree, [string]$id, [string]$pathPart = '') {
    $node = $tree.nodes | Where-Object { $_.id -eq $id -and ($pathPart -eq '' -or $_.path -like "*$pathPart*") } | Select-Object -First 1
    if (-not $node) { throw "UI node $id is absent" }
    $fraction = if ($id -eq 'btnPlayGame') { 0.65 } else { 0.5 }
    $x = [int]($node.rect[0] + $node.rect[2] * $fraction)
    $y = [int]($node.rect[1] + $node.rect[3] / 2)
    Invoke-Bridge @('click', "x=$x", "y=$y") | Out-Null
}

$deadline = (Get-Date).AddSeconds($TimeoutSeconds)
while ((Get-Date) -lt $deadline) {
    $status = Invoke-Bridge @('status')
    if ($status.pid -ne $ClientPid) { throw 'Bridge moved to a different process' }
    if ($status.state -eq 'ingame') {
        [pscustomobject]@{ clientPid = $ClientPid; state = 'ingame'; session = $sessionFile.FullName } | ConvertTo-Json -Compress
        exit 0
    }
    $tree = Invoke-Bridge @('uitree')
    $open = @($tree.openWindows)
    if ($open -contains 'messageBox') { throw 'Game displayed a message box; inspect its text before continuing' }
    if ($open -contains 'rebirthSurvivorProfiles') {
        Click-Node $tree 'btnSelectProfileForWorld'
    } elseif ($open -contains 'spawnselection') {
        if ($tree.nodes | Where-Object { $_.id -eq 'btnSpawn' }) { Click-Node $tree 'btnSpawn' }
        else { Click-Node $tree 'btnSpawnFirstTime' }
    } elseif ($open -contains 'serverBrowserDirectConnect') {
        $ip = $tree.nodes | Where-Object { $_.id -eq 'txtIp' } | Select-Object -First 1
        $port = $tree.nodes | Where-Object { $_.id -eq 'txtPort' } | Select-Object -First 1
        if ($ip.input -ne '127.0.0.1') { Invoke-Bridge @('type', 'id=txtIp', 'value=127.0.0.1') | Out-Null }
        if ($port.input -ne '26905') { Invoke-Bridge @('type', 'id=txtPort', 'value=26905') | Out-Null }
        Click-Node $tree 'btnConnect'
    } elseif ($open -contains 'serverBrowser') {
        Click-Node $tree 'btnDirectConnect'
    } elseif ($open -contains 'playGamePaging') {
        Click-Node $tree 'serverBrowser' 'tabButtons'
    } elseif ($open -contains 'newsScreen') {
        Click-Node $tree 'btnContinue'
    } elseif ($open -contains 'mainMenu') {
        Click-Node $tree 'btnPlayGame'
    }
    Start-Sleep -Seconds 2
}
throw "Client PID $ClientPid did not reach ingame within $TimeoutSeconds seconds"
