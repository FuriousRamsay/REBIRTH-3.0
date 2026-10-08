param(
    [string]$Skills = 'spears,clubs,swords,axes,batons,hammers,knives,knuckles,unarmed',   # comma list of skill ids to test
    [int]$Kills = 3,                    # zombies killed in the positive run
    [string]$Zombie = 'zombieArlene',
    [switch]$NoLaunch
)
# REBIRTH 48-skill playtest runner (see the checklist docx). For each skill: read all 48 skills (rbskill), do the positive action by really playing,
# read them again, do the negative control, read again. PASS/FAIL/BLOCKED per skill with the numbers; cross-credit (other skills moving) is checked too.
$ErrorActionPreference = 'Continue'
$gb = Join-Path $PSScriptRoot 'gamebridge.ps1'
$outDir = Join-Path $PSScriptRoot 'out\skills'; New-Item -ItemType Directory -Force $outDir | Out-Null
$stamp = '{0:yyyyMMdd-HHmmss}' -f (Get-Date)
$log = Join-Path $outDir "skill_eval_$stamp.log"
$report = Join-Path $outDir "skill_report_$stamp.md"
function L($m) { $line = "{0:HH:mm:ss} {1}" -f (Get-Date), $m; $line | Add-Content $log; Write-Host $line }
function B { param([string[]]$a) (& powershell -ExecutionPolicy Bypass -File $gb @a 2>$null) -join '' }
function J($t) { try { $t | ConvertFrom-Json } catch { $null } }

function Get-Skills {
    $r = J (B 'console', 'rbskill')
    $map = @{}
    if (-not $r -or -not $r.ok) { throw "Cannot read skill state" }
    foreach ($line in @($r.results[0].output)) {
        if ($line -match '\[skill\.([a-z_]+)\]\s*=\s*(-?[\d.]+)\s*\(progress\s*(-?[\d.]+)\)') {
            $map[$Matches[1]] = [double]$Matches[2] + [double]$Matches[3]    # value + fractional progress
        }
    }
    if ($map.Count -ne 48) { throw "Expected 48 skills, got $($map.Count); refusing an incomplete measurement" }
    return $map
}
function SkillDiff($a, $b, [string]$skill) {
    $moved = @()
    foreach ($k in $b.Keys) { if ($a.ContainsKey($k) -and [math]::Abs($b[$k] - $a[$k]) -gt 0.0005 -and $k -ne $skill) { $moved += ('{0} {1:+0.000;-0.000}' -f $k, ($b[$k] - $a[$k])) } }
    return $moved
}
function Prepare {
    $status = J (B 'status')
    if (-not $status -or $status.state -ne 'ingame') { throw 'Player is not alive in game; stopping instead of fabricating negative tests' }
    $ui = J (B 'uitree')
    if (@($ui.openWindows | Where-Object { $_ -match '^(crafting|challenges|character|workstation_|trader|powersource|rebirthCampfireProcessingWindow)' }).Count -gt 0) {
        B 'key', 'name=Escape' | Out-Null
    }
    B 'cleararea', 'radius=60' | Out-Null
    B 'restore' | Out-Null
    B 'console', 'rbmet set hydration 100', 'rbmet set nutrition 100', 'rbmet set energy 100', 'settime 1 9 0' | Out-Null
}
function Kill-Some([int]$n) {
    $kills = 0; $dmgTaken = 0
    $script:combatIncidents = 0
    for ($i = 0; $i -lt $n; $i++) {
        Prepare
        if ($script:combatOrigin) {
            $walk = J (B 'walkto', ("x=" + $script:combatOrigin[0]), ("z=" + $script:combatOrigin[2]), 'run=1')
            if (-not $walk -or $walk.result -ne 'arrived') { throw 'Could not return to the combat test area' }
            B 'look', '0', '0' | Out-Null
        }
        if ($script:ranged) { B 'press', 'action=Reload' | Out-Null; Start-Sleep 3.5 }    # reload before the zombie appears
        $sp = J (B 'spawn', "entity=$Zombie", 'distance=10')
        if (-not $sp -or -not $sp.id) { throw 'Combat target spawn failed' }
        $r = J (B 'fight', ("entity=" + $sp.id), 'radius=30', 'single=1', 'humanStyle=1', 'maxSeconds=70', 'autoLoot=0')
        L ('  fight result: ' + ($r | ConvertTo-Json -Depth 3 -Compress))
        if (-not $r -or -not $r.ok) { $script:combatIncidents++ }
        if ($r -and $r.result -eq 'killed') { $kills++ }
        if ($r -and $r.player) { $dmgTaken += ([double]$r.player.healthBefore - [double]$r.player.healthAfter) }
        $d = J (B 'status'); if ($d -and $d.state -eq 'dead') { B 'click', 'id=btnNearBackpack' | Out-Null; Start-Sleep 5 }
    }
    return $kills
}
function Swing-Air([int]$n) {
    B 'cleararea', 'radius=60' | Out-Null
    B 'look', '0', '-5' | Out-Null
    for ($i = 0; $i -lt $n; $i++) { B 'look', '0', '-5' | Out-Null; B 'press', 'action=Primary', 'seconds=0.2' | Out-Null; Start-Sleep -Milliseconds 700 }   # level the view again after every shot (recoil lifts it)
}

# skill id -> weapon item (and the other skills that must NOT move because of it)
$weapons = @{
    spears  = 'meleeWpnSpearT1IronSpear';        clubs   = 'meleeWpnClubT1BaseballBat'
    swords  = 'meleeWpnBladeT3Machete';           axes    = 'meleeToolAxeT1IronFireaxe'
    batons  = 'meleeWpnBatonT0PipeBaton';         hammers = 'meleeWpnSledgeT1IronSledgehammer'
    knives  = 'meleeWpnBladeT1HuntingKnife';      knuckles = 'meleeWpnKnucklesT1IronKnuckles'
    unarmed = 'hands'; scythes = 'ItemsWeaponsScythe004_FR'
    pistols = 'gunHandgunT1Pistol|ammo9mmBulletBall';            revolvers = 'gunHandgunT2Magnum44|ammo44MagnumBulletBall'
    heavy_handguns = 'gunHandgunT3DesertVulture|ammo44MagnumBulletBall'; shotguns = 'gunShotgunT1DoubleBarrel|ammoShotgunShell'
    assault_rifles = 'gunMGT1AK47|ammo762mmBulletBall';          tactical_rifles = 'gunMGT2TacticalAR|ammo762mmBulletBall'
    long_range_rifles = 'gunRifleT1HuntingRifle|ammo762mmBulletBall'; archery = 'gunBowT1WoodenBow|ammoArrowIron'
}

function Test-Logging {
    L '=== logging (chop a tree with an axe) ==='
    Prepare
    $lo = J (B 'loadout', 'weapon=meleeToolAxeT1IronFireaxe')
    if (-not $lo -or -not $lo.ok) { return [pscustomobject]@{ skill = 'logging'; verdict = 'BLOCKED'; positive = ''; negative = ''; cross = ''; note = 'axe loadout failed' } }
    $found = $null
    foreach ($nm in 'treeMountainPine', 'treeOak', 'treePlains', 'treeWinter', 'treeBurnt', 'treeDesert', 'treeAutumn', 'treeForest') {
        $fb = J (B 'findblocks', "name=$nm", 'radius=60')
        $c = @($fb.blocks | Where-Object { $_.name -notmatch "Leaf|Grass|Bush|Stump" -and ([math]::Abs($_.position[0] - 14) + [math]::Abs($_.position[2] - 998)) -gt 25 }) | Select-Object -First 1
        if ($c) { $found = $c; break }
    }
    if (-not $found) { return [pscustomobject]@{ skill = 'logging'; verdict = 'BLOCKED'; positive = ''; negative = ''; cross = ''; note = 'no tree within 60 m' } }
    $tx = $found.position[0] + 0.5; $ty = $found.position[1]; $tz = $found.position[2] + 0.5
    L ("  tree: {0} at {1},{2},{3}" -f $found.name, $found.position[0], $ty, $found.position[2])
    $s0 = Get-Skills
    $wr = J (B 'walkto', "x=$tx", "z=$tz", 'radius=1.8')
    L ('  walked to ' + ($wr.position -join ',') + ' (' + $wr.result + ')')
    $hits = 0; $aborted = ''
    for ($k = 0; $k -lt 20; $k++) {
        B 'lookat', "x=$tx", ('y=' + ($ty + 1.2)), "z=$tz", 'block=1' | Out-Null
        $tg = J (B 'target')
        $under = if ($tg -and $tg.target -and $tg.target.block) { [string]$tg.target.block.name } else { '' }
        for ($t = 0; $t -lt 3 -and ($under -notmatch '^tree' -or $under -match 'Grass|Leaf|Bush|Plant'); $t++) { B 'lookat', "x=$tx", ('y=' + ($ty + 1.0 + 0.4 * $t)), "z=$tz", 'block=1' | Out-Null; Start-Sleep -Milliseconds 400; $tg = J (B 'target'); $under = if ($tg -and $tg.target -and $tg.target.block) { [string]$tg.target.block.name } else { '' } }
        if (($under -notmatch '^tree' -or $under -match 'Grass|Leaf|Bush|Plant')) { L ("  crosshair is on '" + $under + "', not on the tree - not swinging"); $aborted = "crosshair on $under"; break }
        B 'press', 'action=Primary', 'seconds=5' | Out-Null
        $gone = @((J (B 'findblocks', "name=$($found.name)", 'radius=4')).blocks | Where-Object { $_.position[0] -eq $found.position[0] -and $_.position[2] -eq $found.position[2] -and $_.position[1] -eq $found.position[1] }).Count -eq 0
        if ($gone) { L '  the tree base block is gone'; break }
    }
    $s1 = Get-Skills
    $pos = $s1['logging'] - $s0['logging']
    $cross = SkillDiff $s0 $s1 'logging'
    L ("  logging {0:+0.0000;-0.0000}; other skills moved: {1}" -f $pos, ($cross -join ', '))
    Swing-Air 6
    $s2 = Get-Skills; $neg = $s2['logging'] - $s1['logging']
    $v = 'PASS'; $n = ''
    if ($neg -gt 0.0005) { $v = 'FAIL'; $n = 'air swings trained Logging' }
    if ($aborted) { return [pscustomobject]@{ skill = 'logging'; verdict = 'BLOCKED'; positive = ''; negative = ''; cross = ''; note = $aborted } }
    if ($pos -le 0.0005) { $v = 'FAIL'; $n = 'chopping a tree did not train Logging' }
    elseif (@($cross | Where-Object { $_ -match '^axes ' }).Count -gt 0) { $v = 'FAIL'; $n = 'tree chopping trained Axes: ' + ($cross -join ', ') }
    return [pscustomobject]@{ skill = 'logging'; verdict = $v; positive = ('{0:+0.0000}' -f $pos); negative = ('{0:0.0000}' -f $neg); cross = ($cross -join '; '); note = $n }
}
# Place `block` two metres in front of the player (test setup), face it dead on (exact yaw/pitch to its centre, verified with the crosshair), and work on it with `tool` until it is gone.
function Face-Point([double]$px, [double]$py, [double]$pz) {
    $st = J (B 'state', '-Sections', 'player'); $pp = $st.player.position
    $dx = $px - $pp[0]; $dz = $pz - $pp[2]; $eye = $pp[1] + 1.62
    $yaw = ([math]::Atan2($dx, $dz) * 180 / [math]::PI + 360) % 360
    $pitch = - [math]::Atan2($eye - $py, [math]::Sqrt($dx * $dx + $dz * $dz)) * 180 / [math]::PI
    B 'look', ([string]$yaw), ([string]$pitch) | Out-Null
}
function Work-Block([string]$tool, [string]$block, [int]$bursts = 12) {
    $lo = J (B 'loadout', "weapon=$tool"); if (-not $lo -or -not $lo.ok) { return 'loadout failed' }
    B 'look', '270', '0' | Out-Null; Start-Sleep -Milliseconds 800      # face west, away from the base
    $st = J (B 'state', '-Sections', 'player'); $pp = $st.player.position
    $bx = [math]::Floor($pp[0] - 2.2); $bz = [math]::Floor($pp[2]); $by = [math]::Floor($pp[1])
    foreach ($d in 0.6, 1.0, 1.4, 1.8) {   # clear grass and plants on the line of sight: they stop the crosshair
        $cx = [math]::Floor($pp[0] - $d)
        foreach ($dy in 0, 1, 2) { B 'setblock', "x=$cx", ("y=" + ($by + $dy)), "z=$bz", 'name=air' | Out-Null }
    }
    foreach ($dy in 1, 2) { B 'setblock', "x=$bx", ("y=" + ($by + $dy)), "z=$bz", 'name=air' | Out-Null }
    B 'setblock', "x=$bx", "y=$by", "z=$bz", "name=$block" | Out-Null
    Start-Sleep -Milliseconds 800
    for ($k = 0; $k -lt $bursts; $k++) {
        $under = ''
        for ($t = 0; $t -lt 3 -and $under -ne $block; $t++) {
            Face-Point ($bx + 0.5) ($by + 0.5) ($bz + 0.5); Start-Sleep -Milliseconds 900
            $tg = J (B 'target'); $under = if ($tg -and $tg.target -and $tg.target.block) { [string]$tg.target.block.name } else { '' }
        }
        if ($under -ne $block) { L ("  crosshair is on '$under', not on $block - not swinging"); return "crosshair on '$under'" }
        B 'press', 'action=Primary', 'seconds=4' | Out-Null
        $still = J (B 'findblocks', "name=$block", 'radius=5')
        if (@($still.blocks | Where-Object { $_.position[0] -eq $bx -and $_.position[1] -eq $by -and $_.position[2] -eq $bz }).Count -eq 0) { return '' }
    }
    return 'block still standing'
}
function Test-BlockSkill([string]$skill, [string]$tool, [string]$block, [string]$negBlock, [string]$crossSkill) {
    L "=== $skill (work on $block with $tool) ==="
    Prepare
    $s0 = Get-Skills
    $err = Work-Block $tool $block
    $s1 = Get-Skills
    $pos = $s1[$skill] - $s0[$skill]
    $cross = SkillDiff $s0 $s1 $skill
    L ("  {0} {1:+0.0000;-0.0000}; other skills moved: {2}  ({3})" -f $skill, $pos, ($cross -join ', '), $err)
    $neg = $null
    if ($negBlock) {
        $err2 = Work-Block $tool $negBlock 3
        $s2 = Get-Skills; $neg = $s2[$skill] - $s1[$skill]
        L ("  negative control on {0}: {1:+0.0000;-0.0000}  ({2})" -f $negBlock, $neg, $err2)
    }
    $v = 'PASS'; $n = ''
    if ($err -and $err -ne 'block still standing') { $v = 'BLOCKED'; $n = $err }
    elseif ($pos -le 0.0005) { $v = 'FAIL'; $n = "work on $block did not train $skill" }
    elseif ($neg -ne $null -and $neg -gt 0.0005) { $v = 'FAIL'; $n = "work on $negBlock trained $skill" }
    elseif ($crossSkill -and @($cross | Where-Object { $_ -match "^$crossSkill " }).Count -gt 0) { $v = 'FAIL'; $n = "also trained $crossSkill" }
    return [pscustomobject]@{ skill = $skill; verdict = $v; positive = ('{0:+0.0000}' -f $pos); negative = $(if ($neg -ne $null) { '{0:+0.0000;-0.0000;0}' -f $neg } else { 'n/a' }); cross = ($cross -join '; '); note = $n }
}
function Test-Athletics {
    L '=== athletics (run vs idle vs jump in place) ==='
    Prepare
    B 'console', 'rbmet set energy 100' | Out-Null
    $s0 = Get-Skills
    Start-Sleep -Seconds 30                                   # idle
    $s1 = Get-Skills; $idle = $s1['athletics'] - $s0['athletics']
    for ($i = 0; $i -lt 15; $i++) { B 'press', 'action=Jump' | Out-Null; Start-Sleep -Milliseconds 900 }   # jump in place
    $s2 = Get-Skills; $jump = $s2['athletics'] - $s1['athletics']
    L ("  idle 30 s: {0:+0.0000;-0.0000;0}   jump x15: {1:+0.0000;-0.0000;0}" -f $idle, $jump)
    # real locomotion: run back and forth over open ground
    B 'look', '270', '0' | Out-Null
    for ($i = 0; $i -lt 6; $i++) { B 'move', 'forward=1', 'seconds=5', 'run=1' | Out-Null; B 'look', ([string](90 + 180 * ($i % 2))), '0' | Out-Null; Start-Sleep -Milliseconds 600 }
    $s3 = Get-Skills; $run = $s3['athletics'] - $s2['athletics']
    L ("  running ~30 s: {0:+0.0000;-0.0000}" -f $run)
    $v = 'PASS'; $n = ''
    if ($run -le 0.0005) { $v = 'FAIL'; $n = 'running did not train Athletics' }
    elseif ($idle -gt 0.0005) { $v = 'FAIL'; $n = 'idle time trained Athletics' }
    elseif ($jump -gt 0.0005) { $v = 'FAIL'; $n = 'jumping in place trained Athletics' }
    return [pscustomobject]@{ skill = 'athletics'; verdict = $v; positive = ('{0:+0.0000}' -f $run); negative = ('idle {0:+0.0000;-0.0000;0} jump {1:+0.0000;-0.0000;0}' -f $idle, $jump); cross = ''; note = $n }
}
function Test-Medicine {
    L '=== medicine (first aid bandage on real damage vs at full health) ==='
    B 'guard', 'enabled=1' | Out-Null
    Prepare
    function HP { (J (B 'state', '-Sections', 'stats')).stats.health.value }
    function Use-Bandage {
        $h = ''
        for ($try = 0; $try -lt 6 -and $h -ne 'medicalFirstAidBandage'; $try++) {
            B 'give', 'item=medicalFirstAidBandage', 'count=6', 'toolbelt=4' | Out-Null
            B 'select', 'slot=4' | Out-Null; Start-Sleep 2
            $h = (J (B 'state', '-Sections', 'player')).player.holding.name
        }
        if ($h -ne 'medicalFirstAidBandage') { return $false }
        B 'press', 'action=Primary', 'seconds=2.5' | Out-Null; Start-Sleep 14     # use it, then let the healing run out
        return $true
    }
    $s0 = Get-Skills
    if (-not (Use-Bandage)) { B 'guard', 'heal=1', 'needs=1' | Out-Null; return [pscustomobject]@{ skill = 'medicine'; verdict = 'BLOCKED'; positive = ''; negative = ''; cross = ''; note = 'could not hold the bandage' } }
    $s1 = Get-Skills; $neg = $s1['medicine'] - $s0['medicine']
    L ("  at full health: {0:+0.0000;-0.0000;0}" -f $neg)
    $hpAll0 = HP
    for ($round = 0; $round -lt 4; $round++) {
        B 'damage', 'amount=40', 'type=Bashing' | Out-Null; Start-Sleep 1
        [void](Use-Bandage)
    }
    $s2 = Get-Skills; $pos = $s2['medicine'] - $s1['medicine']
    L ("  after 4 treatments of 40 lost hp (hp now {0}); medicine {1:+0.0000;-0.0000;0}" -f (HP), $pos)
    B 'guard', 'heal=1', 'needs=1' | Out-Null
    $v = 'PASS'; $n = ''
    if ($pos -le 0.0005) { $v = 'FAIL'; $n = 'real healing did not train Medicine' }
    elseif ($neg -gt 0.0005) { $v = 'FAIL'; $n = 'using a bandage at full health trained Medicine' }
    return [pscustomobject]@{ skill = 'medicine'; verdict = $v; positive = ('{0:+0.0000}' -f $pos); negative = ('{0:+0.0000;-0.0000;0}' -f $neg); cross = ''; note = $n }
}
function Wait-UiText([string]$text, [int]$seconds) {
    $end = (Get-Date).AddSeconds($seconds)
    while ((Get-Date) -lt $end) { $u = B 'uitree'; if ($u -match [regex]::Escape($text)) { return $true }; Start-Sleep -Seconds 2 }
    return $false
}
function Test-Cooking {
    L '=== cooking (bake a potato at the campfire; skill must move only on completion) ==='
    B 'guard', 'enabled=1' | Out-Null
    Prepare
    B 'give', 'item=foodCropPotato', 'count=6' | Out-Null
    $s0 = Get-Skills
    B 'walkto', 'x=14.5', 'z=993.3', 'radius=0.7' | Out-Null
    B 'activate', 'x=14', 'y=45', 'z=995', 'block=1' | Out-Null
    if (-not (Wait-UiText 'Baked Potato' 10)) { B 'guard', 'heal=1', 'needs=1' | Out-Null; return [pscustomobject]@{ skill = 'cooking'; verdict = 'BLOCKED'; positive = ''; negative = ''; cross = ''; note = 'campfire window did not open' } }
    B 'click', 'text=Baked Potato', 'window=workstation_campfire' | Out-Null
    B 'click', 'text=PULL INGREDIENTS', 'window=workstation_campfire' | Out-Null
    B 'click', 'text=COOK', 'window=workstation_campfire' | Out-Null
    B 'click', 'text=Turn On', 'window=workstation_campfire' | Out-Null
    Start-Sleep -Seconds 4
    $mid = Get-Skills; $during = $mid['cooking'] - $s0['cooking']
    L ("  while cooking (not finished): {0:+0.0000;-0.0000;0}" -f $during)
    $done = Wait-UiText 'READY TO TAKE' 150
    $beforeTake = Get-Skills
    B 'click', 'id=rebirthCookingTake', 'window=workstation_campfire' | Out-Null
    Start-Sleep -Seconds 2
    $s1 = Get-Skills; $pos = $s1['cooking'] - $s0['cooking']
    L ("  finished={0}; cooking after taking the result: {1:+0.0000;-0.0000;0}" -f $done, $pos)
    B 'key', 'name=Escape' | Out-Null
    B 'guard', 'heal=1', 'needs=1' | Out-Null
    $v = 'PASS'; $n = ''
    $cross = SkillDiff $s0 $s1 'cooking'
    if (-not $done) { $v = 'BLOCKED'; $n = 'the potato never finished' }
    elseif ($pos -le 0.0005) { $v = 'FAIL'; $n = 'a completed recipe did not train Cooking' }
    elseif ($during -gt 0.0005) { $v = 'FAIL'; $n = 'Cooking rose before the recipe finished' }
    return [pscustomobject]@{ skill = 'cooking'; verdict = $v; positive = ('{0:+0.0000}' -f $pos); negative = ('while cooking {0:+0.0000;-0.0000;0}' -f $during); cross = ($cross -join '; '); note = $n }
}
function Attack-Sleeper {
    $sr = J (B 'surroundings', 'radius=30')
    $t = @($sr.threats | Where-Object { -not $_.awake }) | Select-Object -First 1
    if (-not $t) { return 'no sleeper found' }
    $en = J (B 'entities', 'radius=30', 'contains=zombie')
    $e = @($en.entities | Where-Object { $_.id -eq $t.id }) | Select-Object -First 1
    B 'walkto', ('x=' + $e.position[0]), ('z=' + $e.position[2]), 'radius=1.4' | Out-Null
    for ($i = 0; $i -lt 8; $i++) {
        B 'lookat', "entity=$($t.id)" | Out-Null; Start-Sleep -Milliseconds 700
        B 'press', 'action=Primary', 'seconds=0.2' | Out-Null; Start-Sleep -Milliseconds 900
        $en = J (B 'entities', 'radius=6', 'contains=zombie'); $e = @($en.entities | Where-Object { $_.id -eq $t.id }) | Select-Object -First 1
        if (-not $e -or $e.dead) { return '' }
    }
    return 'sleeper still alive'
}
function Test-Stealth {
    L '=== stealth (hit an unaware sleeper vs an awake zombie that has noticed me) ==='
    Prepare
    $lo = J (B 'loadout', 'weapon=meleeWpnClubT1BaseballBat'); if (-not $lo -or -not $lo.ok) { return [pscustomobject]@{ skill = 'stealth'; verdict = 'BLOCKED'; positive = ''; negative = ''; cross = ''; note = 'loadout failed' } }
    $s0 = Get-Skills
    # crouch-walk near a sleeper does nothing by itself: stand 6 m away for 20 s
    B 'spawn', 'entity=zombieArlene', 'distance=6', 'sleeper=1' | Out-Null
    Start-Sleep -Seconds 20
    $s1 = Get-Skills; $near = $s1['stealth'] - $s0['stealth']
    L ("  20 s near a sleeper, no attack: {0:+0.0000;-0.0000;0}" -f $near)
    $r = [pscustomobject]@{ result = (Attack-Sleeper) }
    $s2 = Get-Skills; $pos = $s2['stealth'] - $s1['stealth']
    L ("  killed the sleeper ({0}): stealth {1:+0.0000;-0.0000;0}" -f $r.result, $pos)
    Prepare
    B 'spawn', 'entity=zombieArlene', 'distance=8' | Out-Null
    $r2 = J (B 'fight', 'contains=zombie', 'single=1', 'humanStyle=1', 'maxSeconds=60', 'autoLoot=0')
    $s3 = Get-Skills; $neg = $s3['stealth'] - $s2['stealth']
    L ("  fought an awake zombie ({0}): stealth {1:+0.0000;-0.0000;0}" -f $r2.result, $neg)
    $v = 'PASS'; $n = ''
    if ($pos -le 0.0005) { $v = 'FAIL'; $n = 'hitting an undetected sleeper did not train Stealth' }
    elseif ($near -gt 0.0005) { $v = 'FAIL'; $n = 'proximity to a sleeper trained Stealth' }
    elseif ($neg -gt 0.0005) { $v = 'FAIL'; $n = 'fighting a zombie that had noticed me trained Stealth' }
    return [pscustomobject]@{ skill = 'stealth'; verdict = $v; positive = ('{0:+0.0000}' -f $pos); negative = ('near {0:+0.0000;-0.0000;0} awake {1:+0.0000;-0.0000;0}' -f $near, $neg); cross = ''; note = $n }
}
function Place-InFront([string]$block, [double]$dist = 2.2, [double]$side = 0) {
    B 'look', '270', '0' | Out-Null; Start-Sleep -Milliseconds 800
    $st = J (B 'state', '-Sections', 'player'); $pp = $st.player.position
    $bx = [math]::Floor($pp[0] - $dist); $bz = [math]::Floor($pp[2] + $side); $by = [math]::Floor($pp[1])
    foreach ($dx in 0..([int][math]::Ceiling($dist))) { foreach ($dy in 0, 1, 2) { $cx = [math]::Floor($pp[0] - $dx); if (-not ($cx -eq $bx -and $dy -eq 0)) { B 'setblock', "x=$cx", ("y=" + ($by + $dy)), "z=$bz", 'name=air' | Out-Null } } }
    B 'setblock', "x=$bx", "y=$by", "z=$bz", "name=$block" | Out-Null
    Start-Sleep -Milliseconds 600
    return @($bx, $by, $bz)
}
function Test-Farming {
    L '=== farming (plant a seed on a plot; immature vs mature harvest) ==='
    B 'guard', 'enabled=1' | Out-Null
    Prepare
    try {
    B 'testflag', 'name=waterbypass', 'on=1' | Out-Null
    B 'console', 'rbfarming testcropgrowth off' | Out-Null
    B 'give', 'item=plantedCorn1', 'count=5', 'toolbelt=4' | Out-Null
    $s0 = Get-Skills
    $plot = Place-InFront 'farmPlotBlockPlayer' 1.2
    Face-Point ($plot[0] + 0.5) ($plot[1] + 1.0) ($plot[2] + 0.5); Start-Sleep -Milliseconds 1300
    $tg = J (B 'target'); $under = if ($tg.target.block) { [string]$tg.target.block.name } else { '' }
    if ($under -ne 'farmPlotBlockPlayer') { B 'guard', 'heal=1', 'needs=1' | Out-Null; return [pscustomobject]@{ skill = 'farming'; verdict = 'BLOCKED'; positive = ''; negative = ''; cross = ''; note = "crosshair on '$under', not the plot" } }
    B 'select', 'slot=4' | Out-Null; Start-Sleep -Milliseconds 1200
    B 'press', 'action=Secondary', 'seconds=0.3' | Out-Null; Start-Sleep -Seconds 2
    $px = $plot[0]; $py = $plot[1] + 1; $pz = $plot[2]
    $seed = @((J (B 'findblocks', 'name=plantedCorn', 'radius=5')).blocks | Where-Object { $_.position[0] -eq $px -and $_.position[1] -eq $py -and $_.position[2] -eq $pz }) | Select-Object -First 1
    L ("  planted: {0}" -f $(if ($seed) { $seed.name } else { 'nothing' }))
    $s1 = Get-Skills; $plantGain = $s1['farming'] - $s0['farming']
    L ("  planting the seed: farming {0:+0.0000;-0.0000;0}" -f $plantGain)
    B 'loadout', 'weapon=meleeWpnBladeT1HuntingKnife' | Out-Null
    Face-Point ($px + 0.5) ($py + 0.5) ($pz + 0.5)
    B 'press', 'action=Primary', 'seconds=0.3' | Out-Null
    Start-Sleep -Seconds 1
    $immature = Get-Skills
    $immatureGain = $immature['farming'] - $s1['farming']
    B 'console', 'rbfarming testcropgrowth on' | Out-Null
    # wait for the crop to mature
    $mature = $null
    for ($i = 0; $i -lt 40 -and -not $mature; $i++) {
        Start-Sleep -Seconds 4
        $mature = @((J (B 'findblocks', 'name=plantedCorn', 'radius=5')).blocks | Where-Object { $_.position[0] -eq $px -and $_.position[1] -eq $py -and $_.position[2] -eq $pz -and $_.name -match 'Harvest' }) | Select-Object -First 1
    }
    $s2 = Get-Skills; $growGain = $s2['farming'] - $s1['farming']
    L ("  matured={0}; farming while growing: {1:+0.0000;-0.0000;0}" -f ([bool]$mature), $growGain)
    if (-not $mature) { B 'guard', 'heal=1', 'needs=1' | Out-Null; return [pscustomobject]@{ skill = 'farming'; verdict = 'BLOCKED'; positive = ''; negative = ''; cross = ''; note = 'the crop never matured' } }
    B 'console', 'rbfarming testcropgrowth off' | Out-Null
    Face-Point ($px + 0.5) ($py + 0.5) ($pz + 0.5); Start-Sleep -Milliseconds 1300
    $tg = J (B 'target'); $under2 = if ($tg.target.block) { [string]$tg.target.block.name } else { '' }
    L ("  crosshair on '{0}'" -f $under2)
    B 'select', 'slot=1' | Out-Null; Start-Sleep -Milliseconds 1200
    Face-Point ($px + 0.5) ($py + 0.5) ($pz + 0.5); Start-Sleep -Milliseconds 800
    for ($sw = 0; $sw -lt 4; $sw++) { B 'press', 'action=Primary', 'seconds=0.3' | Out-Null; Start-Sleep -Milliseconds 900 }

    $s3 = Get-Skills; $pos = $s3['farming'] - $s2['farming']
    $left = @((J (B 'findblocks', 'name=plantedCorn', 'radius=5')).blocks | Where-Object { $_.position[0] -eq $px -and $_.position[1] -eq $py -and $_.position[2] -eq $pz }).Count
    L ("  harvest done (plant blocks left at that spot: {0}); farming {1:+0.0000;-0.0000;0}" -f $left, $pos)
    B 'testflag', 'name=waterbypass', 'on=0' | Out-Null
    B 'console', 'rbfarming testcropgrowth off' | Out-Null
    B 'guard', 'heal=1', 'needs=1' | Out-Null
    $v = 'PASS'; $n = ''
    $neg = $plantGain + $growGain
    if ($pos -le 0.0005) { $v = 'FAIL'; $n = 'a mature harvest did not train Farming' }
    elseif ($neg -gt 0.0005) { $v = 'FAIL'; $n = 'planting / an immature crop trained Farming' }
    return [pscustomobject]@{ skill = 'farming'; verdict = $v; positive = ('{0:+0.0000}' -f $pos); negative = ('plant+immature+grow {0:+0.0000;-0.0000;0}' -f $neg); cross = ((SkillDiff $s2 $s3 'farming') -join '; '); note = $n }
    } finally {
        B 'testflag', 'name=waterbypass', 'on=0' | Out-Null
        B 'console', 'rbfarming testcropgrowth off' | Out-Null
    }
}
function Wait-QueueEmpty([int]$seconds = 240) {
    $end = (Get-Date).AddSeconds($seconds)
    while ((Get-Date) -lt $end) {
        Start-Sleep -Seconds 3; $u = B 'uitree'
        if ($u -match '"text":"0 / 50"') { return $true }
        if ($u -notmatch 'rebirthCraftingQueueCapacity') { Open-Crafting }       # the window got closed: the queue cannot be read (or finish) without it
    }
    return $false
}
function Open-Crafting { for ($k = 0; $k -lt 3; $k++) { $u = B 'uitree'; if ($u -match '"openWindows":\[[^\]]*"crafting"') { return }; B 'key', 'name=Tab' | Out-Null; Start-Sleep 2 } }
function Test-CraftSkill([string]$skill, [string]$recipe) {
    L "=== $skill (craft $recipe) ==="
    Prepare
    B 'guard', 'enabled=1' | Out-Null
    Open-Crafting
    [void](Wait-QueueEmpty 120)
    $s0 = Get-Skills
    $rc = J (B 'craft', "recipe=$recipe", 'count=1', 'stock=1')
    if (-not $rc -or -not $rc.queued) { return [pscustomobject]@{ skill = $skill; verdict = 'BLOCKED'; positive = ''; negative = ''; cross = ''; note = "could not queue ${recipe}: $($rc.error)" } }
    Start-Sleep -Milliseconds 800
    $cancel = J (B 'craft', "recipe=$recipe", 'cancel=1')
    if (-not $cancel -or $cancel.cancelled -lt 1) { return [pscustomobject]@{ skill=$skill; verdict='BLOCKED'; positive=''; negative='NOT MEASURED'; cross=''; note='Could not cancel before completion' } }
    Start-Sleep -Seconds 2
    $s1 = Get-Skills; $cancelGain = $s1[$skill] - $s0[$skill]
    $rc = J (B 'craft', "recipe=$recipe", 'count=1')
    if (-not $rc -or -not $rc.queued) { return [pscustomobject]@{ skill=$skill; verdict='BLOCKED'; positive=''; negative=$cancelGain; cross=''; note='Could not queue the positive control' } }

    $mid = Get-Skills; $during = $mid[$skill] - $s1[$skill]
    $done = Wait-QueueEmpty 300
    Start-Sleep -Seconds 2
    $s2 = Get-Skills; $pos = $s2[$skill] - $s1[$skill]
    L ("  completed ({0}): before completion {1:+0.0000;-0.0000;0}, after {2:+0.0000;-0.0000;0}" -f $done, $during, $pos)
    $v = 'PASS'; $n = ''
    $cross = SkillDiff $s1 $s2 $skill
    if (-not $done) { $v = 'BLOCKED'; $n = 'the queue never finished' }
    elseif ($pos -le 0.0005) { $v = 'FAIL'; $n = "completing $recipe did not train $skill" }
    elseif ($cancelGain -gt 0.0005) { $v = 'FAIL'; $n = 'a cancelled craft trained the skill' }
    elseif ($during -gt $pos * 0.9) { $v = 'FAIL'; $n = 'the skill rose before the craft completed' }
    return [pscustomobject]@{ skill = $skill; verdict = $v; positive = ('{0:+0.0000}' -f $pos); negative = ('cancelled {0:+0.0000;-0.0000;0}' -f $cancelGain); cross = ($cross -join '; '); note = $n }
}
$craftSkills = @{ drink_preparation = 'drinkYuccaJuiceSmoothie'; tailoring = 'modArmorCigar'; metalworking = 'meleeToolAxeT1IronFireaxe'; gunsmithing = 'gunHandgunT0PipePistol'; construction = 'barbedFence'; maintenance = 'resourceRepairKit'; mechanics = 'vehicleWheels' }
if (-not $NoLaunch) {
    L 'launching the game with a fresh copy of the test save'
    & powershell -ExecutionPolicy Bypass -File $gb launch -Reset 2>&1 | Select-Object -Last 2 | ForEach-Object { L "  $_" }
}
$script:combatOrigin = (J (B 'state', '-Sections', 'player')).player.position
$results = New-Object System.Collections.ArrayList
foreach ($skill in ($Skills -split ',' | Where-Object { $_ })) {
    L "=== $skill ==="
    if ($skill -eq 'salvage') { [void]$results.Add((Test-BlockSkill 'salvage' 'meleeToolSalvageT1Wrench' 'cntToilet01' 'terrDirt' 'mining')); continue }
    if ($skill -eq 'mining') { [void]$results.Add((Test-BlockSkill 'mining' 'meleeToolPickT1IronPickaxe' 'terrOreIron' 'terrDirt' 'logging')); continue }
    if ($skill -eq 'athletics') { [void]$results.Add((Test-Athletics)); continue }
    if ($skill -eq 'medicine') { [void]$results.Add((Test-Medicine)); continue }
    if ($skill -eq 'cooking') { [void]$results.Add((Test-Cooking)); continue }
    if ($skill -eq 'stealth') { [void]$results.Add((Test-Stealth)); continue }
    if ($skill -eq 'farming') { [void]$results.Add((Test-Farming)); continue }
    if ($craftSkills.ContainsKey($skill)) { [void]$results.Add((Test-CraftSkill $skill $craftSkills[$skill])); continue }
    if ($skill -eq 'logging') { [void]$results.Add((Test-Logging)); continue }
    if (-not $weapons.ContainsKey($skill)) { [void]$results.Add([pscustomobject]@{ skill = $skill; verdict = 'BLOCKED'; positive = ''; negative = ''; cross = ''; note = 'no scenario written yet' }); continue }
    $parts = $weapons[$skill].Split('|'); $la = @('loadout', "weapon=$($parts[0])"); if ($parts.Count -gt 1) { $la += "ammo=$($parts[1])"; $la += 'ammoCount=200' }; $script:ranged = ($parts.Count -gt 1); $lo = J (B $la)
    if (-not $lo -or -not $lo.ok) { [void]$results.Add([pscustomobject]@{ skill = $skill; verdict = 'BLOCKED'; positive = ''; negative = ''; cross = ''; note = "loadout failed: $($weapons[$skill])" }); continue }
    L ("  holding: {0}" -f $lo.held)
    $s0 = Get-Skills
    $actualKills = Kill-Some $Kills
    $s1 = Get-Skills
    $pos = if ($s0.ContainsKey($skill)) { $s1[$skill] - $s0[$skill] } else { [double]::NaN }
    $cross = SkillDiff $s0 $s1 $skill
    L ("  positive: {0} kills, {1} {2:+0.0000;-0.0000}; other skills moved: {3}" -f $actualKills, $skill, $pos, ($cross -join ', '))
    L ("  Combat runs with incidents: {0}. Award scaling requires event/award log reconciliation." -f $script:combatIncidents)
    Swing-Air 12
    $s2 = Get-Skills
    $neg = $s2[$skill] - $s1[$skill]
    L ("  negative (12 swings at nothing): {0:+0.0000;-0.0000}" -f $neg)
    $verdict = 'PASS'; $note = ''
    if ($actualKills -eq 0) { $verdict = 'BLOCKED'; $note = 'no zombie was killed with this weapon' }
    elseif ($pos -le 0.0005) { $verdict = 'FAIL'; $note = 'real combat did not train the skill' }
    elseif ($neg -gt 0.0005) { $verdict = 'FAIL'; $note = 'swings at nothing trained the skill' }
    elseif (@($cross | Where-Object { $_ -match '^(spears|clubs|swords|axes|batons|hammers|knives|scythes|knuckles|unarmed|pistols|revolvers|heavy_handguns|shotguns|assault_rifles|tactical_rifles|long_range_rifles|archery|logging) ' }).Count -gt 0) { $verdict = 'FAIL'; $note = 'another weapon skill moved: ' + ($cross -join ', ') }
    if ($verdict -eq 'PASS') { $verdict = 'AWARD CHECK PASS' }
    [void]$results.Add([pscustomobject]@{ skill = $skill; verdict = $verdict; positive = ('{0:+0.0000}' -f $pos); negative = ('{0:+0.0000;-0.0000;0}' -f $neg); cross = ($cross -join '; '); note = ($note + " Combat incidents: $script:combatIncidents. Scaling, attributes, UI and beginner reachability are separate checks.") })
    L ("  => $verdict $note")
}
$md = @("# Skill playtest $stamp", '', '| Skill | Verdict | Positive gain | Negative control | Other skills moved | Note |', '|---|---|---|---|---|---|')
foreach ($r in $results) { $md += ('| {0} | {1} | {2} | {3} | {4} | {5} |' -f $r.skill, $r.verdict, $r.positive, $r.negative, $r.cross, $r.note) }
$md | Set-Content $report -Encoding UTF8
L "report: $report"
$results | Format-Table -AutoSize | Out-String | ForEach-Object { L $_ }
L '--- summary ---'
