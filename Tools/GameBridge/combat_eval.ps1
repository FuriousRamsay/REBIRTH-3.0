param(
    [string]$Phases = 'melee1,melee2,bow,poi',   # comma list of: melee1 melee2 bow poi
    [int]$Reps = 3,                              # repetitions of the melee1 phase
    [double]$BowDrop = 1.0,
    [double]$Lead = 0,                         # melee anticipation: seconds from click to impact
    [double]$AimSmooth = 0.06,                   # melee camera smoothing time
                         # arrow drop compensation (fraction of free fall)
    [ValidateSet('day', 'night')][string]$When = 'day',   # time of day for the fights (night = dark, zombies fast)
    [int]$DaySpeed = 0,                          # zombie move speed in daylight: 0 walk, 1 jog, 2 run, 3 sprint, 4 nightmare
    [int]$NightSpeed = 3,                        # ... in the dark
    [string[]]$FightArgs = @(),                  # extra fight options, e.g. dance=1 reachMin=1.4 reachMax=1.95 aimSmooth=0.035
    [string]$Zombie = 'zombieArlene',
    [string]$Zombie2 = '',                       # second zombie class for the pair phase (melee2); default = same as -Zombie
    [switch]$NoLoot,                             # do not pick up the dropped loot bags after a cleared fight            # entity class to fight (zombieBiker has more health, zombieFatCop, zombieSoldier, ...)
    [int]$FightSeconds = 0,                      # time limit per fight (0 = 70, or 150 for a tough zombie such as a biker: be patient)
    [switch]$NaturalSpawns,                      # leave the game's own enemy spawning on (default: off during evals)
    [switch]$NoBow,                              # melee tests: do not give a bow or arrows\r
    [switch]$Profile,                            # run the game under the Rebirth profiler: every fight is a labelled segment; report path printed at the end
    [switch]$NoLaunch                            # the game is already running with the test save
)
# Combat / looting evaluation for the virtual player.
#   melee1: one zombie with a baseball bat, Reps times          -> kills, health lost, incidents
#   melee2: two zombies from different sides                    -> strategy (string them out), survival
#   bow:    one zombie from ~22 m with a bow, then the bat      -> weapon switch at range, arrows hitting
#   poi:    a tier-1 POI with nothing around it: raid it        -> sleepers sniped / drawn out, zombies killed, containers looted
# The player is NOT in god mode (that would hide everything). Death handling: after 2 deaths the game is relaunched
# from a fresh copy of the test save, so a bad run never turns into a death loop.
# Output: a summary on screen and Tools\GameBridge\out\combat_eval_<time>.log (+ fight traces in out\traces).
$ErrorActionPreference = 'Continue'
$FightArgs = @($FightArgs | ForEach-Object { $_ -split ',' } | Where-Object { $_ })   # allow  -FightArgs dance=1,reachMin=1.4
$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$gb = Join-Path $PSScriptRoot 'gamebridge.ps1'
$plog = "$env:USERPROFILE\AppData\LocalLow\The Fun Pimps\7 Days To Die\Player.log"
$outDir = Join-Path $PSScriptRoot 'out'; New-Item -ItemType Directory -Force $outDir | Out-Null
$log = Join-Path $outDir ("combat_eval_{0:yyyyMMdd-HHmmss}.log" -f (Get-Date))
function L($m) { $line = "{0:HH:mm:ss} {1}" -f (Get-Date), $m; $line | Add-Content $log; Write-Host $line }
function B { param([string[]]$a) (& powershell -ExecutionPolicy Bypass -File $gb @a 2>$null) -join '' }
function J($text) { try { $text | ConvertFrom-Json } catch { $null } }
$deaths = 0
$script:CurBowDrop = $BowDrop
$script:CurLead = $Lead
$summary = New-Object System.Collections.ArrayList
$priorSession = $env:REBIRTH_BRIDGE_SESSION
$priorReceipt = $env:REBIRTH_BRIDGE_LAUNCH_RECEIPT
$priorChores = $env:REBIRTH_BRIDGE_NOCHORES
$script:watchdog = $null
$script:profiler = $null
$script:profilerStartUtc = $null

# Watch the pinned session. Only an eval-launched game can be asked to quit.
function Stop-Watchdog {
    if ($script:watchdog) {
        Stop-Job $script:watchdog -ErrorAction SilentlyContinue
        Remove-Job $script:watchdog -Force -ErrorAction SilentlyContinue
        $script:watchdog = $null
    }
}

function Start-Watchdog {
    $script:watchdog = Start-Job -ScriptBlock {
        param($gb, $log, $ownsGame)
        $badSince = $null
        while ($true) {
            Start-Sleep 20
            $s = (& powershell -ExecutionPolicy Bypass -File $gb status 2>$null) -join ''
            $state = if ($s -match '"state":"([a-z]+)"') { $Matches[1] } else { 'unknown' }
            if ($state -eq 'spawning' -or $state -eq 'dead') {
                if (-not $badSince) { $badSince = Get-Date }
                if (((Get-Date) - $badSince).TotalSeconds -gt 25) {
                    ("{0:HH:mm:ss} WATCHDOG: stuck in '{1}' - clicking Spawn" -f (Get-Date), $state) | Add-Content $log
                    & powershell -ExecutionPolicy Bypass -File $gb click 'text=Spawn near my backpack' 2>$null | Out-Null
                    & powershell -ExecutionPolicy Bypass -File $gb click 'text=Spawn' 2>$null | Out-Null
                    $badSince = Get-Date
                }
            } else { $badSince = $null }
            if (((Get-Date) - (Get-Item -LiteralPath $log).LastWriteTime).TotalMinutes -gt 8) {
                ("{0:HH:mm:ss} WATCHDOG: no progress for 8 minutes" -f (Get-Date)) | Add-Content $log
                if ($ownsGame) { & powershell -ExecutionPolicy Bypass -File $gb quit 2>&1 | Add-Content -LiteralPath $log }
                break
            }
        }
    } -ArgumentList $gb, $log, (-not $NoLaunch)
}

function Invoke-EvalBridge {
    param([string[]]$a)
    $output = & powershell -ExecutionPolicy Bypass -File $gb @a 2>&1
    if ($LASTEXITCODE -ne 0) { throw ("Game Bridge " + $a[0] + " failed: " + ($output -join ' ')) }
    $output | Out-Host
}

function Confirm-EvalAttach([string]$configPath, [int]$expectedPid) {
    $config = Get-Content -LiteralPath $configPath -Raw -ErrorAction Stop | ConvertFrom-Json -ErrorAction Stop
    if ($config.save -cnotmatch '^(CodexTest|RebirthBridge)[A-Za-z0-9_-]*$' -or
        [string]::IsNullOrWhiteSpace($config.world)) {
        throw '-NoLaunch requires a configured disposable Bridge world and save.'
    }

    $status = J (B 'status')
    if ($status.state -cne 'ingame' -or $status.pid -ne $expectedPid) {
        throw '-NoLaunch needs the selected live Bridge session in game.'
    }

    $role = (J (B 'call', 'GET', '/auditstate', 'domain=topology')).domains.topology
    $state = J (B 'state', '-Sections', 'world')
    if ($role.remote -ne $false -or $role.server -ne $true -or $role.dedicated -ne $false -or
        $role.localActor -ne $true -or $role.connected -ne $false -or
        $role.gameName -cne $config.save -or $state.state -cne 'ingame' -or
        $state.world.gameName -cne $config.save -or $state.world.gameWorld -cne $config.world) {
        throw '-NoLaunch needs the configured disposable world and save on the local authority.'
    }
}

function Stop-EvalGame {
    if ($NoLaunch -or -not $env:REBIRTH_BRIDGE_LAUNCH_RECEIPT -or
        -not (Test-Path -LiteralPath $env:REBIRTH_BRIDGE_LAUNCH_RECEIPT)) { return }
    Invoke-EvalBridge 'quit'
}

function Get-EvalPlayReport([string]$launchReceipt, [string]$profileOut, [int]$profilerPid, [datetime]$profilerStartUtc) {
    try {
        if ([IO.Path]::GetFileName($launchReceipt) -cnotmatch '^(combat-eval-[0-9a-f]{32})\.launch\.json\z') { return }
        $expectedPath = [IO.Path]::GetFullPath((Join-Path $profileOut ('play_' + $Matches[1] + '.json')))
        $owner = Get-Content -LiteralPath $launchReceipt -Raw -ErrorAction Stop | ConvertFrom-Json -ErrorAction Stop
        $report = Get-Content -LiteralPath ($launchReceipt + '.play-report.json') -Raw -ErrorAction Stop | ConvertFrom-Json -ErrorAction Stop
        $gamePid = [int]$owner.pid
        if ($gamePid -le 0 -or [int]$report.gamePid -ne $gamePid -or
            ([datetime]$report.gameStartUtc).ToUniversalTime().Ticks -ne ([datetime]$owner.processStartUtc).ToUniversalTime().Ticks -or
            $profilerPid -le 0 -or [int]$report.profilerPid -ne $profilerPid -or
            ([datetime]$report.profilerStartUtc).ToUniversalTime().Ticks -ne $profilerStartUtc.Ticks -or
            $report.path -isnot [string] -or $report.path -ine $expectedPath -or -not [IO.File]::Exists($expectedPath)) { return }
        return $report.path
    } catch { return }
}

function Get-LoadErrors {
    if (-not (Test-Path $plog)) { return @() }
    Select-String -Path $plog -Pattern ' EXC |XML loader.*failed' -ErrorAction SilentlyContinue | Select-Object -First 3 | ForEach-Object { $_.Line }
}

function Start-Game {
    if ($NoLaunch) { throw 'The existing session ended during -NoLaunch. Resume it manually before another eval.' }
    Stop-Watchdog
    Stop-EvalGame
    if ($script:profiler -and -not $script:profiler.WaitForExit(20000)) { throw 'The previous eval profiler has not finished its report.' }
    $env:REBIRTH_BRIDGE_NOCHORES = '1'
    $env:REBIRTH_BRIDGE_SESSION = $null
    $env:REBIRTH_BRIDGE_LAUNCH_RECEIPT = Join-Path $outDir ("combat-eval-{0}.launch.json" -f [guid]::NewGuid().ToString('N'))
    $script:arena = $null
    L 'launching the game with a fresh copy of the test save'
    if ($Profile) {
        Invoke-EvalBridge 'build'
        Invoke-EvalBridge 'reset-save'
        $profilerExe = Join-Path $root 'Tools/Profiler/bin/RebirthProfiler.exe'
        $script:profiler = Start-Process -FilePath $profilerExe -ArgumentList @('--launch','--keep-save','--record=40','--threads',
            ('"--eval-launch-receipt=' + $env:REBIRTH_BRIDGE_LAUNCH_RECEIPT + '"')) -WindowStyle Hidden -PassThru
        $script:profilerStartUtc = $script:profiler.StartTime.ToUniversalTime()
        $deadline = (Get-Date).AddSeconds(30)
        while (-not (Test-Path -LiteralPath $env:REBIRTH_BRIDGE_LAUNCH_RECEIPT)) {
            if ($script:profiler.HasExited -or (Get-Date) -ge $deadline) { throw 'The eval profiler did not publish its owned launch receipt.' }
            Start-Sleep -Milliseconds 250
        }
        Invoke-EvalBridge 'wait','-Timeout','480'
    } else {
        Invoke-EvalBridge 'launch','-Reset'
    }
    $errs = Get-LoadErrors
    if ($errs) { L ('LOAD ERRORS (stopping): ' + ($errs -join ' | ')); throw 'load errors' }
    L 'in game, no load errors'
    if (-not $NaturalSpawns) { B 'enemyspawns', 'on=0' | Out-Null }
    for ($i = 0; $i -lt 25; $i++) { $g = B 'guard'; if ($g -notmatch '"busy":true') { break }; Start-Sleep 1 }
    Set-Conditions
    B 'console', 'killall' | Out-Null
    Start-Watchdog
}

function Set-Kit {
    # Spawn in what the scenario needs: bat, bow, bandages in the toolbelt, arrows in the bag.
    B 'give', 'item=meleeWpnClubT1BaseballBat', 'toolbelt=1', 'quality=3' | Out-Null
    if (-not $NoBow) { B 'give', 'item=gunBowT1WoodenBow', 'toolbelt=2', 'quality=3' | Out-Null }
    B 'give', 'item=medicalFirstAidBandage', 'count=6', 'toolbelt=3' | Out-Null
    B 'give', 'item=medicalBandage', 'count=6', 'toolbelt=4' | Out-Null
    if (-not $NoBow) { B 'give', 'item=ammoArrowIron', 'count=60' | Out-Null }
    B 'select', 'slot=1' | Out-Null
}

function Test-Dead {
    $s = J (B 'status')
    return ($s -and $s.state -eq 'dead') -or ($s -and $s.state -eq 'spawning')
}

function Recover {
    # After a death: respawn; after two deaths: relaunch from a clean save (no death loops).
    $script:deaths++
    L "player died (death $script:deaths)"
    if ($script:deaths -ge 2) { $script:deaths = 0; Start-Game; Set-Kit; Go-OpenGround; return }
    # The respawn screen takes a moment to appear after dying: keep clicking Spawn until the player is back in the world.
    $back = $false
    for ($i = 0; $i -lt 30 -and -not $back; $i++) {
        Start-Sleep 3
        $s = J (B 'status')
        if ($s -and $s.state -eq 'ingame') { $back = $true; break }
        B 'click', 'text=Spawn near my backpack' | Out-Null
        B 'click', 'text=Spawn' | Out-Null
    }
    if (-not $back) { L 'respawn did not work - relaunching the game'; $script:deaths = 0; Start-Game; Set-Kit; Go-OpenGround; return }
    Start-Sleep 5
    B 'console', 'killall' | Out-Null
    Set-Kit
}

$script:arena = $null
function Wait-Grounded {
    # True when the player has stood still (y stable) for 2 s at a plausible height; false if still falling / fell through.
    $last = $null; $stable = 0
    for ($i = 0; $i -lt 30; $i++) {
        $s = J (B 'state', '-Sections', 'player')
        if ($s -and $s.player -and $s.player.position) {
            $y = [double]$s.player.position[1]
            if ($y -lt 1) { return $false }
            if ($last -ne $null -and [math]::Abs($y - $last) -lt 0.03) { $stable++ } else { $stable = 0 }
            $last = $y
            if ($stable -ge 2) { return $true }
        }
        Start-Sleep 1
    }
    return $false
}

function Teleport-Safe($x, $z) {
    # Surface teleport (x z), wait for the ground to load and the player to settle; retry a few times.
    for ($try = 1; $try -le 4; $try++) {
        B 'console', "teleport $x $z" | Out-Null
        Start-Sleep 5
        if (Wait-Grounded) { return $true }
        L "teleport to $x,$z : not standing on solid ground yet (try $try)"
    }
    return $false
}

function Go-OpenGround($dx = 0) {
    # Fight on flat, bare ground: grass, bushes and debris in front of (or under) a zombie eat melee swings and arrows,
    # and a knocked-down zombie lying in debris can not be hit at all. The start area is forest, so sample several
    # points of the map and keep the barest one (stopping early when one is good enough).
    if (-not $script:arena) {
        $s = J (B 'state', '-Sections', 'player'); $sx = $s.player.position[0]; $sz = $s.player.position[2]
        $offsets = @(@(0, 0), @(450, 0), @(-450, 0), @(0, 450), @(0, -450), @(900, 450), @(-900, -450), @(450, -900), @(-450, 900))
        $best = $null; $bestScore = 1e9
        foreach ($o in $offsets) {
            $x = [int]($sx + $o[0]); $z = [int]($sz + $o[1])
            if ($o[0] -ne 0 -or $o[1] -ne 0) { if (-not (Teleport-Safe $x $z)) { L "arena candidate $x,$z : no solid ground, skipping"; continue } }
            $og = J (B 'openground', 'radius=300', 'size=12')   # a 24 m square outside every POI that is flat to a few cm: room to hold distance and retreat 8 m
            if (-not $og -or -not $og.position) { L "arena candidate $x,$z : nothing loaded"; continue }
            L ('arena candidate {0},{1}: spot {2}, flat range {5} m, surface {6}, clutter {3}, score {4}' -f $x, $z, ($og.position -join ','), $og.clutterThere, $og.score, $og.flatRange, $og.surface)
            if ($og.score -lt $bestScore) { $bestScore = $og.score; $best = $og }
            if ($og.clutterThere -le 6 -and $og.flatRange -le 0.35) { break }
        }
        $script:arena = $best
    }
    $og = $script:arena
    if (-not $og -or -not $og.position) { L "no open ground found, staying here"; return }
    L ('using arena at {0} (clutter {1}, score {2})' -f ($og.position -join ','), $og.clutterThere, $og.score)
    if (-not (Teleport-Safe ([int]$og.position[0] + $dx) ([int]$og.position[2]))) { L 'could not stand at the arena' }
}
function Set-Conditions {
    # Time of day and zombie speed (the game reads both live).
    $hour = if ($When -eq 'night') { 22 } else { 9 }
    B 'console', "settime 1 $hour 0" | Out-Null
    B 'zombiespeed', "day=$DaySpeed", "night=$NightSpeed" | Out-Null
}

function Prepare-Body {
    Set-Conditions
    # Food and water drive both the stamina cap and the health maximum, so a fight starts with them full; coffee adds stamina regen.
    B 'console', 'rbmet set hydration 100', 'rbmet set nutrition 100', 'rbmet set digestivehealth 100', 'rbmet set energy 100' | Out-Null
    B 'give', 'item=drinkJarCoffee', 'toolbelt=4' | Out-Null
    B 'use', 'item=drinkJarCoffee', 'unlessBuff=buffCoffee' | Out-Null
    B 'give', 'item=medicalBandage', 'count=6', 'toolbelt=4' | Out-Null
    B 'select', 'slot=1' | Out-Null
}

function Reset-Arena {
    B 'console', 'killall' | Out-Null
    Prepare-Body
    # Treat injuries with the right items (bandage, first aid kit, splint, cast, painkillers, antibiotics) like a player would: untreated
    # injuries are what lowers the maximum health. Missing items are spawned in. Then top up current health.
    $tr = J (B 'treat')
    if ($tr -and (@($tr.used).Count -gt 0 -or @($tr.untreated).Count -gt 0)) { L ('treated: {0}; still untreated: {1}; max health {2}' -f ((@($tr.used)) -join ', '), ((@($tr.untreated)) -join ', '), $tr.maxHealth) }
    B 'restore' | Out-Null; Start-Sleep 1.5; $rs = J (B 'restore'); if ($rs -and [double]$rs.maxHealth -lt 99) { L ('max health still ' + $rs.maxHealth + ' after restore'); Start-Sleep 3; B 'restore' | Out-Null }
    # after treating, also lift any leftover health-capacity penalty (it recovers over minutes) so every fight starts identical
    B 'select', 'slot=1' | Out-Null
    Start-Sleep 2
}

function Run-Fight($label, [scriptblock]$spawn, $maxSeconds = 70, $radius = 30) {
    if ($FightSeconds -gt 0) { $maxSeconds = $FightSeconds } elseif ($Zombie -match 'Biker|Soldier|Hazmat|Demolition|Fat|Lumberjack|Mutated|Screamer|Feral|Radiated|Charged|Infernal') { $maxSeconds = [math]::Max($maxSeconds, 150) }
    Reset-Arena
    # Every fight must start from full health: a hard hit leaves injuries that keep cutting health/max health for a while.
    for ($g = 0; $g -lt 8; $g++) {
        $h = (J (B 'restore')).health
        if (-not $h -or $h -ge 85) { break }
        L ('health is ' + $h + ' before the fight: treating again (' + ($g + 1) + ')')
        B 'treat' | Out-Null; Start-Sleep 2; B 'restore' | Out-Null; Start-Sleep (3 + 4 * $g)   # wait longer each round: injuries that cut max health fade with time
    }
    if (Test-Dead) { Recover }
    & $spawn
    if ($script:prof) { Set-Content -Path ($script:prof.FullName + '.mark') -Value $label }   # profiler segment
    $t0 = Get-Date
    # Screenshots during the fight (a background job, so the blocking fight call is not disturbed).
    $tag = ($label -replace '[^A-Za-z0-9]+', '_')
    $shotJob = Start-Job -ScriptBlock { param($gb, $tag) foreach ($d in 4, 5, 6) { Start-Sleep $d; & powershell -ExecutionPolicy Bypass -File $gb screenshot "${tag}_mid" 2>$null | Out-Null } } -ArgumentList $gb, $tag
    $hp0 = (J (B 'state', '-Sections', 'player')).player.health
    $r = J (B (@('fight', 'contains=zombie', "radius=$radius", "maxSeconds=$maxSeconds", "bowDrop=$script:CurBowDrop", "lead=$script:CurLead") + $FightArgs))
    $sec = [math]::Round(((Get-Date) - $t0).TotalSeconds)
    if (-not $NoLoot -and $r -and ($r.result -eq 'area_clear' -or $r.result -eq 'killed')) {
        # only once out of danger: collect the bag(s) the kills dropped; throw junk away if that overburdens us
        $lb = J (B 'lootbags')
        if ($lb) { L ('loot bags: {0} looted, encumbered={1}; {2}' -f $lb.bagsLooted, $lb.encumbered, ((@($lb.events) | Select-Object -First 4) -join ' | ')) }
    }
    Stop-Job $shotJob -ErrorAction SilentlyContinue; Remove-Job $shotJob -Force -ErrorAction SilentlyContinue
    B 'screenshot', "${tag}_end" | Out-Null
    if (-not $r) { L "${label}: no result"; return }
    $kills = if ($r.kills) { @($r.kills).Count } else { 0 }
    $inc = if ($r.incidents) { @($r.incidents).Count } else { 0 }
    $line = "{0}: {1} in {2}s, kills={3}, hits={4}/{5} swings, grazes {11}, damage taken {6} hits, hp {7}->{8}, retreats={9}, incidents={10}" -f $label, $r.result, $sec, $kills, $r.hits, $r.swingsOrShots, $r.hitsTaken, $r.player.healthBefore, $r.player.healthAfter, $r.retreats, $inc, $r.grazes
    L $line
    if ($r.swingAnalysis) { $sa = $r.swingAnalysis; L ("    swings {0} (avg dist {1}): on-target n/hit {2}, off-target {3}, blind {4}, head-aimed {5}, chest-aimed {6}" -f $sa.swings, $sa.avgDistance, $sa.onTarget, $sa.offTarget, $sa.blind, $sa.aimedHead, $sa.aimedChest) }
    # Every swing with its conditions, for offline analysis of what decides a hit.
    if ($r.swingDetail) {
        $csv = Join-Path $outDir 'swings.csv'
        if (-not (Test-Path $csv)) { 'run,label,t,onTarget,blind,aimErr,dist,head,hit,zombieSwinging,speed,stamina,walkType' | Set-Content $csv }
        foreach ($d in @($r.swingDetail)) { ("{0},{1},{2}" -f (Split-Path -Leaf $log), ($label -replace ',',';'), $d) | Add-Content $csv }
    }
    if ($r.swingsRegistered -ne $null) { L ("    swing presses: {0} registered, {1} ignored by the game, cycle {2}s, press-to-hit delay {3}s ({4} samples)" -f $r.swingsRegistered, $r.swingsIgnored, $r.swingCycle, $r.hitDelay, $r.hitDelaySamples) }
    if ($r.bowShots) { L ("    bow: {0} arrow(s) loosed, {1} hit; (t,dist,aimErr,onTarget,loaded,ammo,hit,lift): {2}" -f $r.bowShots, $r.bowHits, ((@($r.bowDetail) | Select-Object -First 8) -join " | ")) }
    if ($r.takenDetail) {
        $tk = @($r.takenDetail)
        L ("    hits taken ({0}): {1}" -f $tk.Count, ((@($tk | Select-Object -First 8 | ForEach-Object { $f = $_ -split ','; "{0}hp at {1}m, zombie swinging={2}, my swing running={3}, doing: {4}" -f $f[1], $f[2], $f[3], $f[4], $f[6] })) -join " | "))
        $tkFile = Join-Path $outDir 'taken.csv'
        if (-not (Test-Path $tkFile)) { 'run,label,t,damage,dist,zombieSwinging,mySwingRunning,mySpeed,action' | Set-Content $tkFile }
        foreach ($d in $tk) { ("{0},{1},{2}" -f (Split-Path -Leaf $log), ($label -replace ',',';'), $d) | Add-Content $tkFile }
    }
    if ($r.rayProbe) {
        $rf = Join-Path $outDir 'rayprobe.csv'
        if (-not (Test-Path $rf)) { 'run,label,time,found,range,alongRay,perp,tag,hitDist,hitEntity,rayVsLookDeg,perpOfLookRay,vertMiss,attackTarget,hdist,myMotion,zMotion,rayPitch,zDy,firstPerson,originVsEye,originDy,rotX,camFwdY,camLocalX' | Set-Content $rf }
        foreach ($d in @($r.rayProbe)) { ("{0},{1},{2}" -f (Split-Path -Leaf $log), ($label -replace ',',';'), $d) | Add-Content $rf }
    }
    if ($r.bowTimeline) { L ("    bow timeline (bow out, first draw, first release, switch to melee, arrows): " + $r.bowTimeline) }
    if ($r.gait) { L ("    gait: " + $r.gait) }
    if ($r.incidents) { foreach ($i in $r.incidents) { L "    incident: $i" } }
    if ($r.events) { foreach ($e in @($r.events) | Where-Object { $_ -match 'string them out|switching|taking out|bow|healing|patched|backing off|stamina' } | Select-Object -First 6) { L "    event: $e" } }
    [void]$summary.Add([pscustomobject]@{ phase = $label; result = $r.result; seconds = $sec; kills = $kills; hpLost = ($r.player.healthBefore - $r.player.healthAfter); incidents = $inc })
    if ($r.result -eq 'player_died') { Recover }
}

try {
    L ('variant: fight args = ' + ($FightArgs -join ' ') + ', when = ' + $When)
    if ($NoLaunch) {
        $env:REBIRTH_BRIDGE_LAUNCH_RECEIPT = $null
        $discovery = if ($priorSession) { $priorSession } else { Join-Path $outDir 'session.json' }
        $session = Get-Content -LiteralPath $discovery -Raw -ErrorAction Stop | ConvertFrom-Json -ErrorAction Stop
        $pinned = Join-Path $session.outputDir 'session.json'
        $pinnedSession = Get-Content -LiteralPath $pinned -Raw -ErrorAction Stop | ConvertFrom-Json -ErrorAction Stop
        foreach ($field in 'pid','port','token','processStartUtc','startedUtc','outputDir') {
            if ([string]$session.$field -cne [string]$pinnedSession.$field) {
                throw '-NoLaunch Bridge discovery changed before attachment.'
            }
        }
        $env:REBIRTH_BRIDGE_SESSION = $pinned
        if (-not $priorSession) { $env:REBIRTH_BRIDGE_LAUNCH_RECEIPT = Join-Path $outDir 'launch.json' }
        Confirm-EvalAttach (Join-Path $PSScriptRoot 'gamebridge.config.json') $session.pid
        $env:REBIRTH_BRIDGE_LAUNCH_RECEIPT = $null
        Start-Watchdog
    } else {
        $env:REBIRTH_BRIDGE_LAUNCH_RECEIPT = $null
        Start-Game
    }
    Set-Kit
    $want = $Phases -split ','
    if ($want -contains 'melee1' -or $want -contains 'melee2' -or $want -contains 'bow' -or $want -contains 'bowcal' -or $want -contains 'bowstatic' -or $want -contains 'leadcal' -or $want -contains 'pillar' -or $want -contains 'pillardry') { Go-OpenGround }

    if ($want -contains 'melee1') {
        for ($i = 1; $i -le $Reps; $i++) { Run-Fight "melee 1 zombie #$i" { B 'spawn', "entity=$Zombie", 'distance=9' | Out-Null; B 'aggro', 'radius=60' | Out-Null } }
    }
    if ($want -contains 'melee2') {
        for ($i = 1; $i -le 2; $i++) { Run-Fight "melee 2 zombies #$i" { B 'spawn', "entity=$Zombie", 'distance=10', 'angle=-35' | Out-Null; B 'spawn', "entity=$(if ($Zombie2) { $Zombie2 } else { $Zombie })", 'distance=11', 'angle=35' | Out-Null } 90 }
    }
    if ($want -contains 'bow') {
        for ($i = 1; $i -le 2; $i++) { Run-Fight "bow then bat, 1 zombie #$i" { B 'spawn', "entity=$Zombie", 'distance=24' | Out-Null; B 'aggro', 'radius=90' | Out-Null } 90 }
    }
    if ($want -contains 'pillardry') {
        # Step 1, no zombies: can the player pillar up 4 blocks reliably? (verify before any fight depends on it)
        for ($i = 1; $i -le $Reps; $i++) {
            Go-OpenGround -dx (4 * $i)
            Reset-Arena
            B 'give', 'item=frameShapes:cube', 'count=40', 'toolbelt=3' | Out-Null
            $pr = J (B 'pillar', 'item=frameShapes:cube', 'height=4')
            $ok = $pr -and ([double]$pr.heightGain -ge 2.6)
            if ($pr) { L ('pillar dry run #{0}: placed {1} block(s), height gained {2}, {3} -> {4}' -f $i, $pr.placed, $pr.heightGain, $pr.error, $(if ($ok) { 'OK' } else { 'FAILED' })) } else { L "pillar dry run #${i}: no result" }
            B 'screenshot', "pillardry_$i" | Out-Null
            [void]$summary.Add([pscustomobject]@{ phase = "pillar dry run #$i"; result = $(if ($ok) { 'ok' } else { 'failed' }); seconds = 0; kills = 0; hpLost = 0; incidents = 0 })
        }
    }
    if ($want -contains 'pillar') {
        # Step 2: pillar up while nothing is near, THEN let two zombies come (far away: building takes a few seconds), fight from the top.
        $far = if ($When -eq 'night') { 45 } else { 28 }
        for ($i = 1; $i -le $Reps; $i++) {
            Go-OpenGround -dx (4 * $i)      # a fresh spot every time: the towers of earlier rounds are still standing
            Run-Fight ('pillar 2 zombies #{0}' -f $i) {
                B 'give', 'item=frameShapes:cube', 'count=40', 'toolbelt=3' | Out-Null
                $pr = J (B 'pillar', 'item=frameShapes:cube', 'height=4')
                $ok = $pr -and ([double]$pr.heightGain -ge 2.6)
                if ($pr) { L ('pillar: placed {0}, height gained {1}, {2} -> {3}' -f $pr.placed, $pr.heightGain, $pr.error, $(if ($ok) { 'OK' } else { 'FAILED - not spawning zombies' })) }
                if ($ok) {
                    B 'spawn', "entity=$Zombie", "distance=$far", 'angle=-30' | Out-Null
                    B 'spawn', "entity=$Zombie", "distance=$($far + 2)", 'angle=30' | Out-Null
                    B 'aggro', 'radius=90' | Out-Null      # by day they would not notice us from this far
                }
            } 120 70
        }
    }    if ($want -contains 'leadcal') {
        foreach ($l in 0.15, 0.3, 0.45) {
            $script:CurLead = $l
            for ($i = 1; $i -le $Reps; $i++) { Run-Fight ('lead={0} #{1}' -f $l, $i) { B 'spawn', "entity=$Zombie", 'distance=9' | Out-Null } }
        }
    }
    if ($want -contains 'bowcal') {
        foreach ($f in 0, 0.5, 1.0) {
            $script:CurBowDrop = $f
            for ($i = 1; $i -le 2; $i++) { Run-Fight ('bow drop={0} #{1}' -f $f, $i) { B 'spawn', "entity=$Zombie", 'distance=24' | Out-Null; B 'aggro', 'radius=90' | Out-Null } 90 }
        }
    }
    if ($want -contains 'bowstatic') {
        # Arrow accuracy on a target that stands still (spawned and NOT aggro'd, so it idles): separates drop/aim errors from lead errors.
        foreach ($f in 0, 0.5, 1.0) {
            $script:CurBowDrop = $f
            foreach ($d in 12, 18) {
                Run-Fight ('bowstatic drop={0} dist={1}' -f $f, $d) { B 'spawn', "entity=$Zombie", "distance=$d" | Out-Null } 22
            }
        }
    }    if ($want -contains 'poi') {
        Reset-Arena
        # A tier-1 POI with nothing else around it: the one whose nearest neighbour is furthest away.
        $p = J (B 'pois', 'maxTier=1', 'radius=3000', 'limit=40')
        $all = J (B 'pois', 'maxTier=6', 'radius=3000', 'limit=200')
        if (-not $p -or -not $p.pois) { L 'no tier-1 POI found'; }
        else {
            $best = $null; $bestIso = -1
            foreach ($c in $p.pois) {
                $iso = 99999
                foreach ($o in $all.pois) {
                    if ($o.id -eq $c.id) { continue }
                    $d = [math]::Sqrt([math]::Pow($o.center[0] - $c.center[0], 2) + [math]::Pow($o.center[1] - $c.center[1], 2))
                    if ($d -lt $iso) { $iso = $d }
                }
                if ($c.distance -lt 2200 -and $iso -gt $bestIso) { $bestIso = $iso; $best = $c }
            }
            L ("raiding tier-1 POI '{0}' (id {1}), nearest other POI {2:0} m away, {3:0} m from here" -f $best.label, $best.id, $bestIso, $best.distance)
            # Get near it (just outside), then let the raid walk the last bit like a player.
            $tx = [int]$best.center[0]; $tz = [int]$best.center[1]
            B 'console', "teleport $tx 200 $tz" | Out-Null
            Start-Sleep 12
            $s = J (B 'state', '-Sections', 'player'); L ("arrived at " + ($s.player.position -join ','))
            B 'restore' | Out-Null
            $t0 = Get-Date
            # A raid is one long blocking call: a heartbeat every 60 s keeps the log (and the watchdog) honest and shows where we are.
            $hb = Start-Job -ScriptBlock { param($gb, $log) while ($true) { Start-Sleep 60
                    $st = (& powershell -ExecutionPolicy Bypass -File $gb state -Sections player 2>$null) -join ''
                    $sr = (& powershell -ExecutionPolicy Bypass -File $gb surroundings radius=40 2>$null) -join ''
                    $pos = if ($st -match '"position":\[([^\]]*)\]') { $Matches[1] } else { '?' }
                    $cnt = if ($sr -match '"count":(\d+)') { $Matches[1] } else { '?' }
                    ('{0:HH:mm:ss}     raid heartbeat: position {1}, awake enemies within 40 m: {2}' -f (Get-Date), $pos, $cnt) | Add-Content $log } } -ArgumentList $gb, $log
            $r = J (B 'raidpoi', "id=$($best.id)", 'maxSeconds=1100')
            Stop-Job $hb -ErrorAction SilentlyContinue; Remove-Job $hb -Force -ErrorAction SilentlyContinue
            $sec = [math]::Round(((Get-Date) - $t0).TotalSeconds)
    if (-not $NoLoot -and $r -and ($r.result -eq 'area_clear' -or $r.result -eq 'killed')) {
        # only once out of danger: collect the bag(s) the kills dropped; throw junk away if that overburdens us
        $lb = J (B 'lootbags')
        if ($lb) { L ('loot bags: {0} looted, encumbered={1}; {2}' -f $lb.bagsLooted, $lb.encumbered, ((@($lb.events) | Select-Object -First 4) -join ' | ')) }
    }
            if ($r) {
                L ("raid finished in {0}s, remaining enemies: {1}" -f $sec, $r.remainingEnemies)
                foreach ($e in @($r.events)) { L "    $e" }
                $dead = Test-Dead
                [void]$summary.Add([pscustomobject]@{ phase = 'poi raid'; result = if ($dead) { 'died' } else { 'finished' }; seconds = $sec; kills = 0; hpLost = 0; incidents = $r.remainingEnemies })
            }
            else { L 'raid: no result (timed out?)' }
        }
    }
}
catch { L ("stopped: " + $_.Exception.Message) }

Stop-Watchdog
L '--- summary ---'
$summary | Format-Table -AutoSize | Out-String | ForEach-Object { $_.TrimEnd() } | ForEach-Object { L $_ }
$errs = Select-String -Path $plog -Pattern ' EXC ' -ErrorAction SilentlyContinue
L ("exceptions in Player.log: " + (@($errs)).Count)
try {
    Stop-EvalGame
    if ($Profile) {
        $rep = $null
        if ($script:profiler) {
            if (-not $script:profiler.WaitForExit(20000)) { L 'owned profiler is still writing its report' }
            elseif ($script:profiler.ExitCode -ne 0) { L ('owned profiler exited with code ' + $script:profiler.ExitCode) }
            else {
                $profileOut = Join-Path $root 'Tools/Profiler/out'
                $rep = Get-EvalPlayReport $env:REBIRTH_BRIDGE_LAUNCH_RECEIPT $profileOut $script:profiler.Id $script:profilerStartUtc
            }
        }
        L ('profiler play report: ' + $(if ($rep) { $rep } else { 'not found' }))
    }
} catch { L ('cleanup: ' + $_.Exception.Message) }
finally {
    $env:REBIRTH_BRIDGE_SESSION = $priorSession
    $env:REBIRTH_BRIDGE_LAUNCH_RECEIPT = $priorReceipt
    $env:REBIRTH_BRIDGE_NOCHORES = $priorChores
}
L "log: $log"
