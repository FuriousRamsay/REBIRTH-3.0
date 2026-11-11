[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$RunRoot,
    [switch]$Initialize,
    [switch]$ArmNextJoin,
    [switch]$ResumeAfterRestart,
    [ValidateRange(1,2147483647)][int]$EntityId,
    [ValidateRange(2,5)][int]$ExpectedPlayers = 5,
    [ValidateRange(10,180)][int]$WaitSeconds = 100
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'player_reports_bridge.ps1')
$run = Get-Content -LiteralPath (Join-Path $RunRoot 'run.json') -Raw | ConvertFrom-Json
if ($run.root -ine [IO.Path]::GetFullPath($RunRoot) -or $run.gameName -notmatch '^CodexTestReports-[a-f0-9]{10}$') { throw 'Invalid isolated run receipt.' }
$server = Get-ReportBridge $run.server
$state = Invoke-ReportBridge $server '/state?sections=world'
if ($state.state -ne 'server' -or $state.world.gameName -ne $run.gameName -or $state.world.gameWorld -ne $run.world) { throw 'Wrong dedicated save.' }
$save = Join-Path $run.root "server-data/Saves/$($run.world)/$($run.gameName)"
$claims = Join-Path $save 'REBIRTH/PurgeRoadStart'
$path = Join-Path $run.root 'purge-roster.json'
function Get-OwnerKey([string]$owner) {
    $sha = [Security.Cryptography.SHA256]::Create()
    try { [BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($owner.Trim()))).Replace('-','').ToLowerInvariant() }
    finally { $sha.Dispose() }
}
function Get-JoinBaseline {
    $playerDir = Join-Path $save 'Player'
    $savedOwners = @(if (Test-Path -LiteralPath $playerDir) {
        Get-ChildItem -LiteralPath $playerDir -File -Filter '*.ttp' |
            ForEach-Object { Get-OwnerKey $_.BaseName }
    })
    [pscustomobject]@{
        savedOwners = $savedOwners
        serverLogSeq = (Invoke-ReportBridge $server '/ping').lastLogSeq
        armedUtc = [datetime]::UtcNow.ToString('o')
    }
}
function Get-Claims {
    foreach ($f in @(Get-ChildItem -LiteralPath $claims -File -Filter '*.json' -ErrorAction SilentlyContinue)) {
        $claim = Get-Content -LiteralPath $f.FullName -Raw | ConvertFrom-Json
        if (!$claim.Owner -or !$claim.World -or $null -eq $claim.Stage) { continue }
        $hash = Get-OwnerKey $claim.Owner
        [pscustomobject]@{file=$f.Name;ownerKey=$hash;worldKey=([guid]$claim.World).ToString('N');stage=[int]$claim.Stage;vehicle=[int]$claim.Vehicle}
    }
}
if ($Initialize) {
    if (Test-Path -LiteralPath $path) { throw 'Roster already initialized. Start a fresh run to repeat it.' }
    if (@(Get-Claims).Count -or @(Get-ChildItem (Join-Path $save 'Player') -Filter '*.ttp' -ErrorAction SilentlyContinue).Count) { throw 'Initialize the roster before the first player joins.' }
    @{expected=$ExpectedPlayers;serverPid=$server.pid;serverStarted=$server.processStartUtc;rows=@();nextJoin=(Get-JoinBaseline)} |
        ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $path -Encoding UTF8
    Write-Output "Roster initialized for $ExpectedPlayers distinct first joins."
    return
}
$roster = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
$previous = @($roster.rows)
if ($roster.serverPid -ne $server.pid -or
    ([datetime]$roster.serverStarted).ToUniversalTime().Ticks -ne ([datetime]$server.processStartUtc).ToUniversalTime().Ticks) {
    if (!$ResumeAfterRestart -or !$previous.Count -or !$previous[0].passed) {
        throw 'Roster belongs to another server process. Resume only with a completed first claim in the same saved world.'
    }
    $first = $previous[0]
    $savedFirst = @(Get-Claims | Where-Object {
        $_.ownerKey -eq $first.ownerKey -and $_.worldKey -eq $first.worldKey -and $_.stage -eq 2 -and $_.vehicle -eq $first.vehicle
    })
    if ($savedFirst.Count -ne 1) { throw 'The original first claim did not survive the restart.' }
    $roster.serverPid = $server.pid
    $roster.serverStarted = $server.processStartUtc
    if (!$ArmNextJoin) { throw 'After a restart, use -ResumeAfterRestart with -ArmNextJoin before the new account connects.' }
}
if ($previous.Count -ge $roster.expected) { throw 'Roster is already full.' }
if ($ArmNextJoin) {
    if ($EntityId) { throw 'Arm the observation before connecting the account. Record its entity ID afterward.' }
    $roster | Add-Member -NotePropertyName nextJoin -NotePropertyValue (Get-JoinBaseline) -Force
    $roster | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $path -Encoding UTF8
    Write-Output "Ready to observe first join $($previous.Count + 1) of $($roster.expected)."
    return
}
if (!$EntityId -or !$roster.nextJoin) { throw 'Arm with -ArmNextJoin before connecting the account, then supply its server entity ID.' }
$peer = Assert-ReportPurgePeer $server $run $EntityId
if ($peer.deaths -ne 0 -or $previous.ownerKey -contains $peer.ownerKey) { throw 'A distinct first-life player identity is required.' }
if ($peer.ownerKey -in $roster.nextJoin.savedOwners) { throw 'The owner already had a native player save before this join.' }
if ($previous.Count -and $previous[0].worldKey -ne $peer.worldKey) { throw 'The server world changed.' }
$spawnLog = Invoke-ReportBridge $server "/log?since=$($roster.nextJoin.serverLogSeq)&contains=PlayerSpawnedInWorld&limit=100"
$spawn = @($spawnLog.entries | Where-Object { $_.msg -match "PlayerSpawnedInWorld \(reason: (NewGame|EnterMultiplayer|JoinMultiplayer),.*EntityID=$EntityId\b" })
if ($spawn.Count -ne 1) { throw 'Expected one matching native join spawn since arming. Observe the account after its first spawn and before a reconnect.' }
$until = (Get-Date).AddSeconds($WaitSeconds)
do {
    $observed = @(Get-Claims)
    $matching = @($observed | Where-Object { $_.worldKey -eq $peer.worldKey -and $_.ownerKey -eq $peer.ownerKey -and $_.stage -eq 2 -and $_.vehicle -gt 0 })
    if ($matching.Count) { break }
    Start-Sleep -Seconds 2
} while ((Get-Date) -lt $until)
$recheck = Assert-ReportPurgePeer $server $run $EntityId
if ($recheck.ownerKey -ne $peer.ownerKey -or $recheck.creationId -ne $peer.creationId -or $recheck.deaths -ne $peer.deaths) { throw 'Player changed during observation.' }
$reason = if ($matching.Count -gt 1) { 'Duplicate completed claims' } elseif ($matching.Count -eq 1 -and $previous.vehicle -contains $matching[0].vehicle) { 'Truck entity reused across distinct owners' } elseif ($matching.Count -eq 1) { 'One completed owner claim' } elseif ($observed.Count -eq 1 -and $observed[0].file -eq 'initial-truck.json' -and $observed[0].ownerKey -ne $peer.ownerKey) { 'Only the earlier owner has the world starter claim' } else { 'No completed owner claim within observation window' }
$result = [pscustomobject]@{
    join=$previous.Count+1;entityId=$EntityId;worldKey=$peer.worldKey;ownerKey=$peer.ownerKey;creationId=$peer.creationId
    passed=($reason -eq 'One completed owner claim');vehicle=$(if ($matching.Count) { $matching[0].vehicle });reason=$reason
    nativeJoin=[pscustomobject]@{armedUtc=$roster.nextJoin.armedUtc;serverLogSeq=$spawn[0].seq;ownerSaveAbsent=$true}
    observedClaims=$observed;observedUtc=(Get-Date).ToUniversalTime().ToString('o')
}
$roster.rows = @($previous + $result)
$roster.nextJoin = $null
$roster | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $path -Encoding UTF8
$result | Select-Object join,entityId,vehicle,passed,reason | ConvertTo-Json
if (!$result.passed) { exit 1 }
