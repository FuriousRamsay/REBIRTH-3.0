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

# Watchdog: a screen the script does not know about (respawn, dead) must never eat hours unattended.
#  - stuck on the respawn/dead screen for 25 s: click Spawn
#  - no new line in the log for 8 minutes (long operations write heartbeat lines): stop the game so the run ends (the summary still prints)
$watchdog = Start-Job -ScriptBlock {
    param($gb, $log)
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
        if (Test-Path $log) {
            if (((Get-Date) - (Get-Item $log).LastWriteTime).TotalMinutes -gt 8) {
                ("{0:HH:mm:ss} WATCHDOG: no progress for 8 minutes - stopping the game" -f (Get-Date)) | Add-Content $log
                Get-Process 7DaysToDie -ErrorAction SilentlyContinue | Stop-Process -Force
                Start-Sleep 60
            }
        }
    }
} -ArgumentList $gb, $log

function Get-LoadErrors {
    if (-not (Test-Path $plog)) { return @() }
    Select-String -Path $plog -Pattern ' EXC |XML loader.*failed' -ErrorAction SilentlyContinue | Select-Object -First 3 | ForEach-Object { $_.Line }
}

function Start-Game {
    $env:REBIRTH_BRIDGE_NOCHORES = '1'     # no food/water/crafting chores at start: Prepare-Body tops everything up directly
    $script:arena = $null
    L 'launching the game with a fresh copy of the test save'
    Get-Process 7DaysToDie -ErrorAction SilentlyContinue | Stop-Process -Force
    Start-Sleep 3
    if ($Profile) {
        # Reset the test save and build with the normal launcher, stop that game at once, then start it again under the profiler.
        & powershell -ExecutionPolicy Bypass -File $gb launch -Reset -NoWait 2>&1 | Out-Null
        Start-Sleep 5; Get-Process 7DaysToDie -ErrorAction SilentlyContinue | Stop-Process -Force; Start-Sleep 5
        Get-Process RebirthProfiler -ErrorAction SilentlyContinue | Stop-Process -Force
        $pout = Join-Path $root 'Tools\Profiler\out'
        $before = @(Get-ChildItem $pout -Filter 'profile_2026*.json' -ErrorAction SilentlyContinue | ForEach-Object Name)
        Start-Process (Join-Path $root 'Tools\Profiler\bin\RebirthProfiler.exe') -ArgumentList '--launch', '--record=40', '--threads'
        $deadline = (Get-Date).AddMinutes(8)
        do { Start-Sleep 8; $st = B 'status' } until ($st -match '"state":"ingame"' -or (Get-Date) -gt $deadline)
        $script:prof = Get-ChildItem $pout -Filter 'profile_2026*.json' -ErrorAction SilentlyContinue | Where-Object { $before -notcontains $_.Name } | Sort-Object LastWriteTime -Descending | Select-Object -First 1
        L ('profiler attached, profile file: ' + $(if ($script:prof) { $script:prof.Name } else { 'NOT FOUND' }))
        $out = ''
    } else {
        $out = & powershell -ExecutionPolicy Bypass -File $gb launch -Reset 2>&1 | Out-String
    }
    $errs = Get-LoadErrors
    if ($errs) { L ('LOAD ERRORS (stopping): ' + ($errs -join ' | ')); throw 'load errors' }
    L 'in game, no load errors'
    # No eating/drinking chores for these tests: food, water and coffee are topped up directly (Prepare-Body). Switch the
    # instincts off at once and wait only for a chore that already started to finish.
    B 'guard', 'enabled=0', 'heal=0', 'needs=0' | Out-Null
    if (-not $NaturalSpawns) { B 'enemyspawns', 'on=0' | Out-Null }   # no sleepers/hordes/wanderers wandering into the tests; fights spawn their own zombies
    for ($i = 0; $i -lt 25; $i++) { $g = B 'guard'; if ($g -notmatch '"busy":true') { break }; Start-Sleep 1 }
    Set-Conditions
    B 'console', 'killall' | Out-Null
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
    if (-not $NoLaunch) { Start-Game }
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

Stop-Job $watchdog -ErrorAction SilentlyContinue; Remove-Job $watchdog -Force -ErrorAction SilentlyContinue
L '--- summary ---'
$summary | Format-Table -AutoSize | Out-String | ForEach-Object { $_.TrimEnd() } | ForEach-Object { L $_ }
$errs = Select-String -Path $plog -Pattern ' EXC ' -ErrorAction SilentlyContinue
L ("exceptions in Player.log: " + (@($errs)).Count)
if ($Profile) {
    Get-Process 7DaysToDie -ErrorAction SilentlyContinue | ForEach-Object { $_.CloseMainWindow() | Out-Null }
    Start-Sleep 30
    Get-Process 7DaysToDie, RebirthProfiler -ErrorAction SilentlyContinue | Stop-Process -Force
    $rep = Get-ChildItem (Join-Path $root 'Tools\Profiler\out') -Filter 'play_2026*.txt' -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    L ('profiler play report: ' + $(if ($rep) { $rep.FullName } else { 'not found' }))
}
L "log: $log"
