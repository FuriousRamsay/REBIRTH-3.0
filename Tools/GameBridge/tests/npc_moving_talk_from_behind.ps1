param([int]$MaxAttempts = 12)
$ErrorActionPreference = 'Stop'
$bridge = Join-Path $PSScriptRoot '..\gamebridge.ps1'

function Invoke-GameBridge([string[]]$Arguments) {
    $output = @(& powershell -ExecutionPolicy Bypass -File $bridge @Arguments 2>&1)
    $json = $output | Where-Object { $_ -is [string] -and $_.TrimStart().StartsWith('{') } | Select-Object -Last 1
    if (-not $json) { throw "Bridge returned no JSON for $($Arguments -join ' '): $($output -join ' ')" }
    return ($json | ConvertFrom-Json)
}

$null = Invoke-GameBridge @('cleararea', 'radius=50')
$null = Invoke-GameBridge @('restore')
$spawn = Invoke-GameBridge @('spawn', 'entity=npcRebirthDiagnosticMeleeSurvivorManSDCS', 'distance=4')
if (-not $spawn.ok) { throw "NPC spawn failed: $($spawn | ConvertTo-Json -Compress)" }
$npcId = [int]$spawn.id
$null = Invoke-GameBridge @('console', "rbnpcaitrace on 90 $npcId 0.2")

$moving = $false
for ($poll = 0; $poll -lt 16; $poll++) {
    $found = Invoke-GameBridge @('log', '-Contains', "entity=$npcId task=wander action=start", '-Limit', '5')
    if ($found.count -gt 0) { $moving = $true; break }
    Start-Sleep -Milliseconds 500
}
if (-not $moving) { throw "NPC $npcId did not start Wander before the interaction check" }

for ($attempt = 1; $attempt -le $MaxAttempts; $attempt++) {
    $npc = (Invoke-GameBridge @('entities', 'radius=100', 'contains=npcRebirthDiagnosticMeleeSurvivorManSDCS')).entities |
        Where-Object { $_.id -eq $npcId } | Select-Object -First 1
    if (-not $npc) { throw "NPC $npcId left the loaded area" }
    $player = Invoke-GameBridge @('surroundings')
    if ($player.count -gt 0) {
        $null = Invoke-GameBridge @('cleararea', 'radius=50')
        $null = Invoke-GameBridge @('restore')
        continue
    }
    $radians = [double]$npc.yaw * [Math]::PI / 180.0
    $behindX = [double]$npc.position[0] - [Math]::Sin($radians) * 0.9
    $behindZ = [double]$npc.position[2] - [Math]::Cos($radians) * 0.9
    $dx = $behindX - [double]$player.position[0]
    $dz = $behindZ - [double]$player.position[2]
    $distance = [Math]::Sqrt($dx * $dx + $dz * $dz)
    $xText = $behindX.ToString('F2', [Globalization.CultureInfo]::InvariantCulture)
    $zText = $behindZ.ToString('F2', [Globalization.CultureInfo]::InvariantCulture)
    if ($distance -gt 10.0) {
        $x = [int][Math]::Round($behindX)
        $y = [int][Math]::Round([double]$npc.position[1] + 1.0)
        $z = [int][Math]::Round($behindZ)
        $null = Invoke-GameBridge @('console', "teleport $x $y $z")
        continue
    }
    $null = Invoke-GameBridge @('walkto', "x=$xText", "z=$zText", 'radius=0.7', 'run=1', 'maxSeconds=8')

    $motionClock = [Diagnostics.Stopwatch]::StartNew()
    $npc = (Invoke-GameBridge @('entities', 'radius=20', 'contains=npcRebirthDiagnosticMeleeSurvivorManSDCS')).entities |
        Where-Object { $_.id -eq $npcId } | Select-Object -First 1
    if (-not $npc) { continue }
    $firstAt = $motionClock.Elapsed.TotalSeconds
    $player = Invoke-GameBridge @('surroundings')
    $npcAfter = (Invoke-GameBridge @('entities', 'radius=20', 'contains=npcRebirthDiagnosticMeleeSurvivorManSDCS')).entities |
        Where-Object { $_.id -eq $npcId } | Select-Object -First 1
    if (-not $npcAfter) { continue }
    $elapsed = [Math]::Max(0.01, $motionClock.Elapsed.TotalSeconds - $firstAt)
    $travelX = [double]$npcAfter.position[0] - [double]$npc.position[0]
    $travelZ = [double]$npcAfter.position[2] - [double]$npc.position[2]
    $npcSpeed = [Math]::Sqrt($travelX * $travelX + $travelZ * $travelZ) / $elapsed
    $npc = $npcAfter
    $radians = [double]$npc.yaw * [Math]::PI / 180.0
    $relativeX = [double]$player.position[0] - [double]$npc.position[0]
    $relativeZ = [double]$player.position[2] - [double]$npc.position[2]
    $centerDistance = [Math]::Sqrt($relativeX * $relativeX + $relativeZ * $relativeZ)
    $front = $relativeX * [Math]::Sin($radians) + $relativeZ * [Math]::Cos($radians)
    Write-Output "attempt=$attempt npc=$npcId distance=$([Math]::Round($centerDistance,2)) front=$([Math]::Round($front,2)) speed=$([Math]::Round($npcSpeed,2)) yaw=$($npc.yaw)"
    if ($centerDistance -gt 5.0) { continue }
    $result = Invoke-GameBridge @('radial', 'select', 'command=talk', "entity=$npcId", 'expectWindow=rebirthCompanionInteraction', 'approachBehind=1', 'approachSeconds=15', 'requireEntitySpeed=0.4', 'requireBehind=0.25')
    if ($result.ok) {
        Write-Output "PASS moving-behind Talk npc=$npcId speedBeforePress=$($result.entitySpeedBeforePress) behindDistance=$($result.behindDistanceBeforePress) method=$($result.method) hitDistance=$($result.target.distance)"
        $null = Invoke-GameBridge @('key', 'name=Escape')
        exit 0
    }
    Write-Output ("Talk failed: " + ($result | ConvertTo-Json -Compress -Depth 7))
}
throw "Could not prove moving-behind Talk for NPC $npcId within $MaxAttempts approach attempts"
