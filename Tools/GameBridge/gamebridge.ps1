<#
.SYNOPSIS
  REBIRTH Game Bridge client: build, launch, drive and inspect 7 Days to Die for automated testing.

.DESCRIPTION
  Talks to the in-game bridge (Scripts\GameBridge) over http://127.0.0.1:<port>/.
  The bridge only runs when the game is started with -rebirthbridge, which `launch` does for you.
  All query commands print JSON to stdout. Exit code is non-zero on failure.

  Commands:
    help                                 Show this help
    build                                Build RebirthUtils.dll (Debug)
    launch  [-Reset] [-NoBuild] [-NoWait] [-World W] [-Save S]
                                         Build, (re)create test save if needed, start game, wait until in-game
    restart [-Reset] [-NoBuild]          Quit the running game, then launch
    wait    [-Timeout 900]               Wait until the player is in-game
    status | ping                        Bridge liveness + game state
    state   [-Sections player,stats,buffs,cvars,inventory,equipment,skills,world,ui]
    console "<command>" ["<command>" ...]  Run console commands (e.g. "give meleeToolAxeT1IronFireaxe 1")
    log     [-Since SEQ] [-Level error] [-Limit 200] [-Contains text]
    errors  [-Since SEQ]                 Shortcut for log -Level error
    cvar    [name1,name2]                Read CVars (all if omitted)
    setcvar <name> <value>               Set a CVar on the local player
    screenshot [label] [-MaxWidth 1600]  Capture the game window to a PNG; prints its path
    ui      [open|close <window>]        List open windows or open/close a window group
    entities [radius=40] [contains=zombie] [limit=100]
    look    <yaw> [pitch]                Rotate the player camera
    quit                                 Quit the game cleanly
    test    <file.json>                  Run a scripted test scenario (see Tools\GameBridge\tests\README.md)
    reset-save                           Re-clone the test save from the configured baseline save

  Playing (arguments are key=value pairs; the game receives real input actions):
    target                               What is under the crosshair (block, activation prompt, entity)
    findblocks name=campfire [radius=24] [limit=10]
    lookat  x= y= z= [block=1] | entity=ID | yaw= pitch=
    walkto  x= z= [y=] [run=1] [radius=1.5] [maxSeconds=60]
    move    forward=1 [strafe=0] [seconds=1] [run=1] [jump=1] [crouch=1]
    activate [x= y= z= block=1 | entity=ID] [hold=SECONDS]      Press E (aims first if given a target)
    key     name=Escape|Tab|E|R|F|Space|"Left Shift"|1..0 [seconds=]   Press a physical key (all actions bound to it)
    press   action=Primary|Secondary|Jump|Reload|permanent.Cancel [seconds=] [hold=1]
    release [action=]   |   stop         Release held inputs / cancel walking
    actions                              List every input action name
    select  slot=1..unlocked             Toolbelt slot (native Shift+number above 10)
    damage  amount=10 [type=Bashing]     Hurt yourself
    give    item=NAME [count=1] [quality=1] [toolbelt=1..10]   Test setup: item into a toolbelt slot or backpack
    fight   [entity=ID | radius=30 contains=zombie] [range=2.1] [power=1] [retreatStamina=15] [resumeStamina=45] [fleeHealth=0] [maxSeconds=60]
                                         In-game reflex loop: faces target each frame, closes in, swings, backs off to recover stamina
    flee    [entity=ID] [distance=20] [maxSeconds=20]   Run away from an enemy
    spawn   entity=zombieArlene [distance=8] [angle=0]   Test setup: spawn an entity in front of you
    guard   [enabled=0|1] [distance=6] [heal=0|1] [reload=0|1]   Instincts: fight back/flee when an enemy gets close, heal when hurt and clear, reload when quiet (status + events)
    restore                              Test setup: full health and stamina
    cleararea [radius=40]                Test setup: kill enemies around you so a scenario starts from a known state (never disable instincts instead)
    surroundings [radius=30]             Enemies around you: distance, side (ahead/left/right/behind), awake, approaching
    pois [maxTier=1] [radius=400]        Nearby POIs (name, tier, distance, direction, visited)
    gotopoi|clearpoi|lootpoi|raidpoi|enterpoi [id=N | maxTier=1]   Travel to / kill zombies in / loot / all three for a POI (nearest match by default)
    terrain [distance=4]                 Ground in 8 directions: safe?, drop/wall/cliff, flatness of this spot

  UI (selectors: path= text= id= item= recipe= ctrl= [index=N] [window=ID], or x= y= [scale=]):
    uitree  [window=ID] [all=1]          Visible UI nodes with text/items/recipes/queue and screen rects
    uifind  <selector>
    click   <selector> [button=right] [shift=1] [ctrl=1] [double=1] [clicks=N]
    hover   <selector>                   Hover and read the tooltip
    drag    from.<selector> to.<selector>
    type    <selector> value=TEXT [submit=1]
    scroll  <selector> delta=-1
    godmode [on=1|0]                     Test setup: the player cannot be hurt (stress tests without a death loop)
    aggro   [radius=80]                  Test setup: every hostile within the radius targets the player now
    call    GET|POST /path [key=value ...]   Raw call to any endpoint

  Configuration: Tools\GameBridge\gamebridge.config.json (port, world, save, baselineSave, window size, extra args).
#>
[CmdletBinding()]
param(
    [Parameter(Position = 0)] [string] $Command = 'help',
    [Parameter(Position = 1, ValueFromRemainingArguments = $true)] [string[]] $Rest,
    [string] $World,
    [string] $Save,
    [switch] $Reset,
    [switch] $NoBuild,
    [switch] $NoWait,
    [int] $Timeout = 900,
    [string[]] $Sections,
    [long] $Since = 0,
    [string] $Level,
    [int] $Limit = 200,
    [string] $Contains,
    [float] $Radius = 40,
    [int] $MaxWidth = 1600
)

$ErrorActionPreference = 'Stop'
$BridgeDir  = $PSScriptRoot
$ModDir     = (Resolve-Path (Join-Path $BridgeDir '..\..')).Path
$GameDir    = (Resolve-Path (Join-Path $ModDir '..\..')).Path
$GameExe    = Join-Path $GameDir '7DaysToDie.exe'
$OutDir     = Join-Path $BridgeDir 'out'
$SessionFile = Join-Path $OutDir 'session.json'
$SavesRoot  = Join-Path $env:APPDATA '7DaysToDie\Saves'
$PlayerLog  = Join-Path $env:USERPROFILE 'AppData\LocalLow\The Fun Pimps\7 Days To Die\Player.log'

function Get-Config {
    $cfgPath = Join-Path $BridgeDir 'gamebridge.config.json'
    $cfg = [ordered]@{
        port = 8765; world = 'West Xuyofu Territory'; save = 'CodexTest'; baselineSave = $null
        windowed = $true; width = 1280; height = 720; buildConfiguration = 'Debug'; extraArgs = @(); startHour = $null
    }
    if (Test-Path $cfgPath) {
        $j = Get-Content $cfgPath -Raw | ConvertFrom-Json
        foreach ($p in $j.PSObject.Properties) { $cfg[$p.Name] = $p.Value }
    }
    if ($World) { $cfg.world = $World }
    if ($Save)  { $cfg.save = $Save }
    return $cfg
}

function Fail([string] $msg) {
    [Console]::Error.WriteLine("gamebridge: $msg")
    exit 1
}

function Get-GameProcess { Get-Process -Name '7DaysToDie' -ErrorAction SilentlyContinue | Select-Object -First 1 }

function Get-Session {
    if (-not (Test-Path $SessionFile)) { return $null }
    $s = Get-Content $SessionFile -Raw | ConvertFrom-Json
    if (-not (Get-Process -Id $s.pid -ErrorAction SilentlyContinue)) { return $null }
    return $s
}

# Returns @{ Status = int; Body = string; Json = object }. Never throws on HTTP error codes.
function Invoke-Bridge([string] $Method, [string] $Path, [hashtable] $Query = @{}, [string] $Body = $null, [int] $TimeoutSec = 90) {
    $s = Get-Session
    if (-not $s) { Fail 'game/bridge is not running (no live session). Use: gamebridge.ps1 launch' }
    $qs = ($Query.GetEnumerator() | Where-Object { $null -ne $_.Value -and "$($_.Value)" -ne '' } |
        ForEach-Object { [Uri]::EscapeDataString($_.Key) + '=' + [Uri]::EscapeDataString([string]$_.Value) }) -join '&'
    $url = "http://127.0.0.1:$($s.port)$Path" + $(if ($qs) { "?$qs" } else { '' })
    $req = [System.Net.HttpWebRequest]::Create($url)
    $req.Method = $Method
    $req.Timeout = $TimeoutSec * 1000
    $req.ReadWriteTimeout = $TimeoutSec * 1000
    $req.Headers.Add('X-Bridge-Token', $s.token)
    if ($Body) {
        $bytes = [Text.Encoding]::UTF8.GetBytes($Body)
        $req.ContentType = 'text/plain; charset=utf-8'
        $req.ContentLength = $bytes.Length
        $st = $req.GetRequestStream(); $st.Write($bytes, 0, $bytes.Length); $st.Close()
    } elseif ($Method -eq 'POST') { $req.ContentLength = 0 }
    try { $resp = $req.GetResponse() }
    catch [System.Net.WebException] {
        $resp = $_.Exception.Response
        if (-not $resp) { Fail "request to $Path failed: $($_.Exception.Message)" }
    }
    $reader = New-Object IO.StreamReader($resp.GetResponseStream(), [Text.Encoding]::UTF8)
    $text = $reader.ReadToEnd(); $reader.Close()
    $status = [int]$resp.StatusCode; $resp.Close()
    $json = $null; try { $json = $text | ConvertFrom-Json } catch { }
    return @{ Status = $status; Body = $text; Json = $json }
}

# Print raw JSON and set exit code from HTTP status.
function Out-Bridge($r) {
    [Console]::Out.WriteLine($r.Body)
    if ($r.Status -ge 400) { exit 1 }
}

function Invoke-Build($cfg) {
    Write-Host "Building RebirthUtils ($($cfg.buildConfiguration))..."
    if (Get-GameProcess) { Fail 'the game is running and locks RebirthUtils.dll; quit it first (gamebridge.ps1 quit)' }
    $out = & dotnet build (Join-Path $ModDir 'RebirthUtils.csproj') -c $cfg.buildConfiguration -nologo -v q 2>&1
    $out | Where-Object { $_ -match 'error|warning CS' } | ForEach-Object { Write-Host $_ }
    if ($LASTEXITCODE -ne 0) { Fail 'build failed' }
    Write-Host 'Build succeeded.'
}

function Reset-TestSave($cfg, [bool] $force) {
    $target = Join-Path (Join-Path $SavesRoot $cfg.world) $cfg.save
    if (-not $cfg.baselineSave) {
        if (-not (Test-Path $target)) { Write-Host "Save '$($cfg.world)/$($cfg.save)' does not exist and no baselineSave is configured; the game will try to create a new game." }
        return
    }
    if ($cfg.baselineSave -eq $cfg.save) { Fail 'baselineSave and save must be different (the test save gets overwritten)' }
    $source = Join-Path (Join-Path $SavesRoot $cfg.world) $cfg.baselineSave
    if (-not (Test-Path $source)) { Fail "baseline save not found: $source" }
    if ((Test-Path $target) -and -not $force) { return }
    if (Get-GameProcess) { Fail 'cannot reset the save while the game is running' }
    if (Test-Path $target) { Remove-Item $target -Recurse -Force }
    Copy-Item $source $target -Recurse
    Write-Host "Test save '$($cfg.save)' cloned from '$($cfg.baselineSave)' ($($cfg.world))."
}

function Get-LatestGameLog {
    # Launched directly (not via the Steam launcher), Unity writes its log here.
    Get-Item $PlayerLog -ErrorAction SilentlyContinue
}

function Wait-InGame([int] $timeoutSec) {
    $deadline = (Get-Date).AddSeconds($timeoutSec)
    $last = ''; $stateSince = Get-Date; $waitStart = Get-Date
    while ((Get-Date) -lt $deadline) {
        if (-not (Get-GameProcess)) {
            $log = Get-LatestGameLog
            [Console]::Error.WriteLine('The game process exited while waiting. Last log lines:')
            if ($log) { Get-Content $log.FullName -Tail 40 | ForEach-Object { [Console]::Error.WriteLine($_) } }
            exit 1
        }
        if (Get-Session) {
            $r = Invoke-Bridge 'GET' '/ping' @{} $null 10
            $state = $r.Json.state
            if ($state -ne $last) { Write-Host "  state: $state"; $last = $state; $stateSince = Get-Date }
            if ($state -eq 'ingame') {
                # Keep the player out of the death/spawn screen for the whole session (it ends when the game does).\r
                $watching = Get-CimInstance Win32_Process -ErrorAction SilentlyContinue | Where-Object { $_.Name -like 'powershell*' -and $_.CommandLine -like '*respawn_watch.ps1*' }
                if (-not $watching) { Start-Process powershell -WindowStyle Hidden -ArgumentList '-NoProfile','-ExecutionPolicy','Bypass','-File',(Join-Path $PSScriptRoot 'respawn_watch.ps1') }
                Set-StartTime; Out-Bridge $r; return
            }
            # Dead, or waiting on the "Ready to spawn" screen: press the spawn button like a player would.
            if ($state -in 'dead', 'spawning' -and ((Get-Date) - $stateSince).TotalSeconds -gt 6) {
                $c = Invoke-Bridge 'POST' '/ui/click' @{ id = 'btnNearBackpack'; timeout = 10 } $null 15
                if ($c.Status -ge 400) { $c = Invoke-Bridge 'POST' '/ui/click' @{ text = 'Spawn near'; timeout = 10 } $null 15 }
                if ($c.Status -ge 400) { $c = Invoke-Bridge 'POST' '/ui/click' @{ text = 'Spawn'; timeout = 10 } $null 15 }
                if ($c.Status -lt 400) { Write-Host '  pressed the Spawn button' }
                $stateSince = Get-Date
            }
        }
        else {
            # The game runs but the bridge never came up (e.g. it could not bind its port): do not wait the full timeout for nothing.
            if (((Get-Date) - $waitStart).TotalSeconds -gt 150) {
                $log = Get-LatestGameLog
                $why = if ($log) { (Select-String -Path $log.FullName -Pattern 'GameBridge\] Failed to start' -SimpleMatch:$false | Select-Object -Last 1).Line } else { '' }
                Fail ('the game is running but the bridge is not (150 s). ' + $why)
            }
        }
        Start-Sleep -Seconds 2
    }
    Fail "timed out after $timeoutSec s waiting for the game (last state: $last)"
}

# Optional: set the in-game clock once the player is in (config "startHour", e.g. 9 for good light).
function Set-StartTime {
    if ($null -eq $script:cfg.startHour) { return }
    $day = (Invoke-Bridge 'GET' '/state' @{ sections = 'world' }).Json.world.day
    if (-not $day) { $day = 1 }
    $null = Invoke-Bridge 'POST' '/console' @{ cmd = "settime $day $($script:cfg.startHour) 0" }
    Write-Host "  time set to day $day, $($script:cfg.startHour):00"
}

function Start-Game($cfg) {
    if (Get-GameProcess) { Fail 'the game is already running (use restart)' }
    if (-not $NoBuild) { Invoke-Build $cfg }
    Reset-TestSave $cfg $Reset.IsPresent
    if (Test-Path $SessionFile) { Remove-Item $SessionFile -Force }

    $gameArgs = @('-noeac', "-rebirthbridge=$($cfg.port)", '-LoadSaveGame=true',
        "`"-GameWorld=$($cfg.world)`"", "`"-GameName=$($cfg.save)`"", '-SkipNewsScreen=true', '-SkipSpawnButton=true', '-skipintro')
    if ($cfg.windowed) { $gameArgs += @('-screen-fullscreen', '0', '-screen-width', "$($cfg.width)", '-screen-height', "$($cfg.height)") }
    if ($cfg.extraArgs) { $gameArgs += $cfg.extraArgs }

    Write-Host "Launching: 7DaysToDie.exe $($gameArgs -join ' ')"
    Start-Process -FilePath $GameExe -ArgumentList $gameArgs -WorkingDirectory $GameDir | Out-Null
    if (-not $NoWait) { Wait-InGame $Timeout }
}

function Stop-Game {
    $p = Get-GameProcess
    if (-not $p) { return }
    if (Get-Session) { $null = Invoke-Bridge 'POST' '/quit' @{} $null 10 }
    else { Write-Host 'No bridge session; asking the game window to close.'; $null = $p.CloseMainWindow() }
    if (-not $p.WaitForExit(120000)) { Fail 'the game did not exit within 120 s' }
    Write-Host 'Game exited.'
}

# ------------------------------------------------------------------ key=value endpoints

# command -> method + endpoint; arguments are key=value pairs turned into the query string
$KvCommands = @{
    state = @('GET', '/state') # Read-only scenario state capture, same endpoint as CLI state.
    skillsetup = @('POST', '/skillsetup')
    target = @('GET', '/target');        findblocks = @('GET', '/findblocks');  lookat = @('POST', '/lookat')
    walkto = @('POST', '/walkto');       move = @('POST', '/move');             activate = @('POST', '/activate')
    press = @('POST', '/press');         release = @('POST', '/release');       stop = @('POST', '/stop')
    actions = @('GET', '/actions');      select = @('POST', '/select');         damage = @('POST', '/damage')
    uitree = @('GET', '/ui/tree');       uifind = @('GET', '/ui/find');         click = @('POST', '/ui/click')
    hover = @('POST', '/ui/hover');      drag = @('POST', '/ui/drag');          type = @('POST', '/ui/type')
    scroll = @('POST', '/ui/scroll');    key = @('POST', '/key');               entities = @('GET', '/entities')
    fight = @('POST', '/fight');         flee = @('POST', '/flee');             give = @('POST', '/give')
    spawn = @('POST', '/spawn');         guard = @('POST', '/guard');           restore = @('POST', '/restore')
    surroundings = @('GET', '/surroundings'); cleararea = @('POST', '/cleararea'); terrain = @('GET', '/terrain'); openground = @('GET', '/openground'); use = @('POST', '/use'); zombiespeed = @('POST', '/zombiespeed'); enemyspawns = @('POST', '/enemyspawns'); pillar = @('POST', '/pillar'); treat = @('POST', '/treat')
    godmode = @('POST', '/godmode'); aggro = @('POST', '/aggro'); mount = @('POST', '/mount'); dismount = @('POST', '/dismount'); drive = @('POST', '/drive'); fly = @('POST', '/fly')
    loadout = @('POST', '/loadout'); setblock = @('POST', '/setblock'); testflag = @('POST', '/testflag'); craft = @('POST', '/craft'); pois = @('GET', '/pois'); gotopoi = @('POST', '/gotopoi'); clearpoi = @('POST', '/clearpoi'); lootpoi = @('POST', '/lootpoi'); raidpoi = @('POST', '/raidpoi'); enterpoi = @('POST', '/enterpoi'); routeto = @('POST', '/routeto'); breakdoor = @('POST', '/breakdoor'); lootbags = @('POST', '/lootbags')
}

function ConvertTo-Query([string[]] $pairs) {
    $q = @{}
    foreach ($p in $pairs) {
        $i = $p.IndexOf('=')
        if ($i -lt 1) { Fail "expected key=value, got '$p'" }
        $q[$p.Substring(0, $i)] = $p.Substring($i + 1)
    }
    return $q
}

# Walking and long holds can take a while; give the HTTP call enough time.
function Get-KvTimeout([hashtable] $q) {
    $t = 90
    if ($q.ContainsKey('maxSeconds')) { $t = [Math]::Max($t, [int][double]$q.maxSeconds + 30) }
    if ($q.ContainsKey('seconds')) { $t = [Math]::Max($t, [int][double]$q.seconds + 30) }
    if (-not $q.ContainsKey('timeout')) { $q.timeout = $t - 5 }
    return $t
}

function Invoke-Kv([string] $name, [hashtable] $q) {
    $spec = $KvCommands[$name]
    $t = Get-KvTimeout $q
    return Invoke-Bridge $spec[0] $spec[1] $q $null $t
}

# ------------------------------------------------------------------ test runner

function Test-Compare($actual, [string] $op, $expected) {
    if ($null -eq $actual) { return $false }
    $a = [double]$actual; $e = [double]$expected
    switch ($op) {
        '==' { return [Math]::Abs($a - $e) -lt 0.0001 }
        '!=' { return [Math]::Abs($a - $e) -ge 0.0001 }
        '>'  { return $a -gt $e }
        '>=' { return $a -ge $e }
        '<'  { return $a -lt $e }
        '<=' { return $a -le $e }
        default { throw "unknown op '$op'" }
    }
}

function Get-ItemCount($state, [string] $name) {
    if ($null -eq $state -or $null -eq $state.PSObject.Properties['toolbelt'] -or
        $null -eq $state.PSObject.Properties['backpack'] -or
        $null -eq $state.toolbelt -or $null -eq $state.backpack -or $state.ok -eq $false) {
        throw 'Inventory snapshot is unavailable; absence of an item cannot be verified'
    }
    $n = 0
    foreach ($s in @($state.toolbelt) + @($state.backpack)) { if ($s -and $s.name -eq $name) { $n += [int]$s.count } }
    return $n
}

function Get-StatValue($state, [string] $name) {
    $s = $state.stats.$name
    if ($s) { return $s.value } else { return $null }
}

function Assert-TestResponseField($response, [string] $field) {
    if ($null -eq $response -or $null -eq $response.Status -or $response.Status -lt 200 -or $response.Status -ge 300 -or
        $null -eq $response.Json -or $response.Json.ok -eq $false -or
        $null -eq $response.Json.PSObject.Properties[$field] -or $null -eq $response.Json.$field) {
        throw "Bridge response missing valid '$field' data; assertion cannot be evaluated"
    }
}

function Invoke-TestFile([string] $file) {
    if (-not (Test-Path $file)) { Fail "test file not found: $file" }
    $test = Get-Content $file -Raw | ConvertFrom-Json
    # Reject malformed scenarios before contacting the game. Previously a step with
    # multiple keys silently ran only the first action, hiding missing validation.
    if ([string]::IsNullOrWhiteSpace([string]$test.name) -or @($test.steps).Count -eq 0) {
        Fail "scenario must have a name and at least one step: $file"
    }
    $stepNumber = 0
    foreach ($step in $test.steps) {
        $stepNumber++
        if ($null -eq $step -or $step -isnot [pscustomobject] -or @($step.PSObject.Properties).Count -ne 1) {
            Fail "scenario step $stepNumber must contain exactly one step type: $file"
        }
    }
    $startSeq = (Invoke-Bridge 'GET' '/ping').Json.lastLogSeq
    $results = @(); $failed = 0; $i = 0; $skillMarks = @{}; $itemMarks = @{}
    $evidenceDir = Join-Path $OutDir ('tests/' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff') + '_' + [IO.Path]::GetFileNameWithoutExtension($file))
    New-Item -ItemType Directory -Path $evidenceDir -Force | Out-Null
    Write-Host "TEST: $($test.name)"

    foreach ($step in $test.steps) {
        $i++
        $kind = ($step.PSObject.Properties | Select-Object -First 1).Name
        $arg = $step.$kind
        if ($kind.StartsWith('_')) { $i--; continue }   # "_note"/"_comment" steps are documentation only
        $ok = $true; $detail = ''; $r = $null
        try {
            switch ($kind) {
                'action' {
                    if (-not $KvCommands.ContainsKey([string]$arg.command)) { throw "Unknown bridge action '$($arg.command)'" }
                    $query = @{}
                    if ($arg.args) { foreach ($property in $arg.args.PSObject.Properties) { $query[$property.Name] = [string]$property.Value } }
                    $r = Invoke-Kv $arg.command $query
                    $ok = $r.Status -lt 400 -and $r.Json.ok -eq $true
                    $detail = $r.Json | ConvertTo-Json -Depth 6 -Compress
                }
                'markItem' {
                    $itemMarks[[string]$arg] = Get-ItemCount (Invoke-Bridge 'GET' '/state' @{sections='inventory'}).Json ([string]$arg)
                    $detail = "$arg=$($itemMarks[[string]$arg])"
                }
                'expectItemDelta' {
                    if (-not $itemMarks.ContainsKey([string]$arg.name)) { throw 'No item baseline was recorded' }
                    $count = Get-ItemCount (Invoke-Bridge 'GET' '/state' @{sections='inventory'}).Json ([string]$arg.name)
                    $delta = $count - $itemMarks[[string]$arg.name]
                    $ok = Test-Compare $delta $arg.op $arg.value
                    $detail = "$($arg.name) delta=$delta (expected $($arg.op) $($arg.value))"
                }
                'markSkill' {
                    $r = Invoke-Bridge 'GET' '/state' @{sections='rebirth'}
                    Assert-TestResponseField $r 'rebirth'
                    $snapshot = $r.Json.rebirth
                    $skill = @($snapshot.Skills | Where-Object { $_.Id -eq $arg })
                    if ($skill.Count -ne 1) { throw "Expected exactly one accepted skill '$arg'" }
                    if ($null -eq $skill[0].Value -or $null -eq $skill[0].Progress) { throw 'Incomplete skill baseline' }
                    $skillMarks[[string]$arg] = [double]$skill[0].Value + [double]$skill[0].Progress
                    $detail = "$arg=$($skillMarks[[string]$arg])"
                }
                'expectSkillGain' {
                    if (-not $skillMarks.ContainsKey([string]$arg.skill)) { throw 'No skill baseline was recorded' }
                    $r = Invoke-Bridge 'GET' '/state' @{sections='rebirth'}
                    Assert-TestResponseField $r 'rebirth'
                    $snapshot = $r.Json.rebirth
                    $skill = @($snapshot.Skills | Where-Object { $_.Id -eq $arg.skill })
                    if ($skill.Count -ne 1) { throw "Expected exactly one accepted skill '$($arg.skill)'" }
                    if ($null -eq $skill[0].Value -or $null -eq $skill[0].Progress) { throw 'Incomplete skill result' }
                    $gain = [double]$skill[0].Value + [double]$skill[0].Progress - $skillMarks[[string]$arg.skill]
                    $ok = Test-Compare $gain $arg.op $arg.value
                    $detail = "$($arg.skill) gain=$gain (expected $($arg.op) $($arg.value))"
                }
                'expectChallenge' {
                    $list = (Invoke-Bridge 'GET' '/state' @{sections='challenges'}).Json.challenges
                    $challenge = @($list | Where-Object { $_.id -eq $arg.id })
                    $ok = $challenge.Count -eq 1 -and $challenge[0].state -eq $arg.state
                    $detail = "$($arg.id): $($challenge.state), expected $($arg.state)"
                }
                'console' {
                    $r = Invoke-Bridge 'POST' '/console' @{} ((@($arg)) -join "`n")
                    $ok = ($r.Status -lt 400) -and -not $r.Json.hadError
                    $detail = (@($r.Json.results | ForEach-Object { ($_.output + ($_.log | Where-Object { $_.level -ne 'log' } | ForEach-Object { "[$($_.level)] $($_.msg)" })) }) -join ' | ')
                }
                'wait' { Start-Sleep -Milliseconds ([int]([double]$arg * 1000)); $detail = "$arg s" }
                'look' {
                    $r = Invoke-Bridge 'POST' '/look' @{ yaw = $arg.yaw; pitch = $arg.pitch }
                    $ok = $r.Status -lt 400 -and $r.Json.ok -eq $true
                    $detail = $r.Body
                }
                'expectCvar' {
                    $v = (Invoke-Bridge 'GET' '/cvar' @{ names = $arg.name }).Json.cvars.($arg.name)
                    $ok = Test-Compare $v $arg.op $arg.value; $detail = "$($arg.name)=$v (expected $($arg.op) $($arg.value))"
                }
                'waitFor' {
                    $limitAt = (Get-Date).AddSeconds($(if ($arg.timeout) { $arg.timeout } else { 30 }))
                    do {
                        $v = (Invoke-Bridge 'GET' '/cvar' @{ names = $arg.cvar }).Json.cvars.($arg.cvar)
                        $ok = Test-Compare $v $arg.op $arg.value
                        if (-not $ok) { Start-Sleep -Milliseconds 500 }
                    } while (-not $ok -and (Get-Date) -lt $limitAt)
                    $detail = "$($arg.cvar)=$v (waited for $($arg.op) $($arg.value))"
                }
                'expectStat' {
                    $st = (Invoke-Bridge 'GET' '/state' @{ sections = 'stats' }).Json
                    $v = Get-StatValue $st $arg.name
                    $ok = Test-Compare $v $arg.op $arg.value; $detail = "$($arg.name)=$v (expected $($arg.op) $($arg.value))"
                }
                'expectLivingEntities' {
                    $r = Invoke-Bridge 'GET' '/entities' @{ radius = $arg.radius; contains = $arg.contains }
                    Assert-TestResponseField $r 'entities'
                    $count = @($r.Json.entities | Where-Object { -not $_.dead }).Count
                    $ok = $r.Status -lt 400 -and (Test-Compare $count $arg.op $arg.value)
                    $detail = "living $($arg.contains)=$count (expected $($arg.op) $($arg.value))"
                }
                'activateNearestEntity' {
                    $nearby = (Invoke-Bridge 'GET' '/entities' @{ radius = $arg.radius; contains = $arg.contains }).Json.entities
                    $target = $nearby | Where-Object { -not $_.dead } | Sort-Object distance | Select-Object -First 1
                    if ($null -eq $target) { throw 'No living matching entity to activate' }
                    $r = Invoke-Bridge 'POST' '/activate' @{ entity = $target.id }
                    $ok = $r.Status -lt 400 -and $r.Json.ok -eq $true
                    $detail = $r.Body
                }
                'expectThreats' {
                    $r = Invoke-Bridge 'GET' '/surroundings' @{ radius = $arg.radius }
                    Assert-TestResponseField $r 'threats'
                    $count = @($r.Json.threats | Where-Object { $_.awake }).Count
                    $ok = $r.Status -lt 400 -and (Test-Compare $count $arg.op $arg.value)
                    $detail = "awake threats=$count (expected $($arg.op) $($arg.value))"
                }
                'expectBuff' {
                    $b = (Invoke-Bridge 'GET' '/state' @{ sections = 'buffs' }).Json.buffs | Where-Object { $_.name -eq $arg }
                    $ok = [bool]$b; $detail = "buff $arg active=$ok"
                }
                'expectNoBuff' {
                    $r = Invoke-Bridge 'GET' '/state' @{ sections = 'buffs' }
                    Assert-TestResponseField $r 'buffs'
                    $b = $r.Json.buffs | Where-Object { $_.name -eq $arg }
                    $ok = -not $b; $detail = "buff $arg active=$(-not $ok)"
                }
                'expectItem' {
                    $c = Get-ItemCount (Invoke-Bridge 'GET' '/state' @{ sections = 'inventory' }).Json $arg.name
                    $min = $(if ($null -ne $arg.min) { $arg.min } else { 1 })
                    $ok = $c -ge $min; $detail = "$($arg.name) count=$c (min $min)"
                    if ($null -ne $arg.max) { $ok = $ok -and $c -le $arg.max; $detail += " (max $($arg.max))" }
                }
                'expectNoItem' {
                    $c = Get-ItemCount (Invoke-Bridge 'GET' '/state' @{ sections = 'inventory' }).Json $arg
                    $ok = $c -eq 0; $detail = "$arg count=$c"
                }
                'ui' {
                    $r = Invoke-Bridge 'POST' '/ui' @{ action = $arg.action; window = $arg.window }
                    $ok = $r.Status -lt 400; $detail = $r.Body
                }
                'expectWindowOpen' {
                    $open = @((Invoke-Bridge 'GET' '/ui').Json.openWindows)
                    $ok = $open -contains $arg; $detail = "open: $($open -join ', ')"
                }
                'screenshot' {
                    $r = Invoke-Bridge 'POST' '/screenshot' @{ name = $arg; maxWidth = $MaxWidth }
                    $ok = $r.Status -lt 400; $detail = $r.Json.path
                }
                'expectLog' {
                    $r = Invoke-Bridge 'GET' '/log' @{ since = $startSeq; contains = $arg.contains; level = $arg.level; limit = 5 }
                    $ok = $r.Json.count -gt 0; $detail = "log contains '$($arg.contains)': $ok"
                }
                'expectNoErrors' {
                    $r = Invoke-Bridge 'GET' '/log' @{ since = $startSeq; level = 'error'; limit = 20 }
                    Assert-TestResponseField $r 'count'
                    $ok = $r.Json.count -eq 0
                    $detail = $(if ($ok) { 'no errors' } else { (@($r.Json.entries | ForEach-Object { $_.msg }) -join ' | ') })
                }
                'expectUi' {
                    $q = @{}; foreach ($p in $arg.PSObject.Properties) { $q[$p.Name] = $p.Value }
                    $r = Invoke-Bridge 'GET' '/ui/find' $q
                    $ok = $r.Json.count -gt 0; $detail = "matches: $($r.Json.count)"
                    if ($ok) { $detail += ' ' + ($r.Json.nodes[0] | ConvertTo-Json -Compress -Depth 4) }
                }
                'waitForUi' {
                    # { "waitForUi": { "text": "READY TO TAKE", "window": "workstation_campfire", "timeout": 90 } }
                    $q = @{}; foreach ($p in $arg.PSObject.Properties) { if ($p.Name -ne 'timeout') { $q[$p.Name] = $p.Value } }
                    $limitAt = (Get-Date).AddSeconds($(if ($arg.timeout) { $arg.timeout } else { 30 }))
                    do {
                        $r = Invoke-Bridge 'GET' '/ui/find' $q
                        $ok = $r.Json.count -gt 0
                        if (-not $ok) { Start-Sleep -Milliseconds 750 }
                    } while (-not $ok -and (Get-Date) -lt $limitAt)
                    $detail = $(if ($ok) { 'appeared: ' + ($r.Json.nodes[0] | ConvertTo-Json -Compress -Depth 4) } else { 'did not appear before timeout' })
                }
                'expectNoUi' {
                    $q = @{}; foreach ($p in $arg.PSObject.Properties) { $q[$p.Name] = $p.Value }
                    $r = Invoke-Bridge 'GET' '/ui/find' $q
                    Assert-TestResponseField $r 'count'
                    $ok = $r.Json.count -eq 0; $detail = "matches: $($r.Json.count)"
                }
                'expectTarget' {
                    $t = (Invoke-Bridge 'GET' '/target').Json.target
                    $name = "$($t.block.name) $($t.block.label) $($t.entity.class)"
                    $ok = $name -like "*$arg*"; $detail = "target: $($name.Trim())"
                }
                'expectSkill' {
                    $r = Invoke-Bridge 'GET' '/state' @{ sections = 'skills' }
                    Assert-TestResponseField $r 'skills'
                    $s = $r.Json.skills
                    # The bridge deliberately omits native skills at level zero.
                    $v = $(if ($s.($arg.name)) { $s.($arg.name) } else { 0 })
                    $ok = Test-Compare $v $arg.op $arg.value; $detail = "$($arg.name)=$v (expected $($arg.op) $($arg.value))"
                }
                default {
                    if ($KvCommands.ContainsKey($kind)) {
                        # Any key=value command as a step, e.g. { "walkto": { "x": 10, "z": -40, "run": 1 } }
                        $q = @{}
                        if ($arg -is [System.Management.Automation.PSCustomObject]) { foreach ($p in $arg.PSObject.Properties) { $q[$p.Name] = "$($p.Value)" } }
                        $r = Invoke-Kv $kind $q
                        $ok = $r.Status -lt 400 -and $r.Json.ok -ne $false
                        $detail = $r.Body.Substring(0, [Math]::Min(300, $r.Body.Length))
                    } else { $ok = $false; $detail = "unknown step type '$kind'" }
                }
            }
        } catch { $ok = $false; $detail = $_.Exception.Message }

        # Keep the complete reply, especially combat incidents; the compact console excerpt
        # must never be the only evidence retained for a failed test.
        if ($null -ne $r -and $null -ne $r.Body) {
            $r.Body | Set-Content -LiteralPath (Join-Path $evidenceDir ('{0:D3}-{1}.json' -f $i, $kind)) -Encoding UTF8
        }
        if (-not $ok) { $failed++ }
        $tag = $(if ($ok) { 'PASS' } else { 'FAIL' })
        Write-Host ("  [{0}] {1,2}. {2}: {3}" -f $tag, $i, $kind, $detail)
        $results += [ordered]@{ step = $i; type = $kind; ok = $ok; detail = $detail }
        if (-not $ok -and $test.stopOnFailure) { break }
    }

    $errs = (Invoke-Bridge 'GET' '/log' @{ since = $startSeq; level = 'error'; limit = 50 }).Json
    $summary = [ordered]@{
        name = $test.name; passed = ($failed -eq 0); failedSteps = $failed; totalSteps = $i
        newErrorLogLines = $errs.count; errors = @($errs.entries | ForEach-Object { $_.msg }); steps = $results
    }
    [Console]::Out.WriteLine(($summary | ConvertTo-Json -Depth 6 -Compress))
    if ($failed -gt 0) { exit 1 }
}

# ------------------------------------------------------------------ dispatch

$script:cfg = Get-Config
$cfg = $script:cfg
switch ($Command.ToLowerInvariant()) {
    'help'       { Get-Help $PSCommandPath -Detailed | Out-String | Write-Host }
    'build'      { Invoke-Build $cfg }
    'launch'     { Start-Game $cfg }
    'restart'    { Stop-Game; Start-Game $cfg }
    'wait'       { Wait-InGame $Timeout }
    'reset-save' { Reset-TestSave $cfg $true }
    'quit'       { Stop-Game }
    { $_ -in 'status', 'ping' } {
        if (-not (Get-Session)) {
            $p = Get-GameProcess
            [Console]::Out.WriteLine((@{ ok = $false; gameRunning = [bool]$p; bridge = 'not running' } | ConvertTo-Json -Compress))
            exit 1
        }
        Out-Bridge (Invoke-Bridge 'GET' '/ping')
    }
    'state'      { Out-Bridge (Invoke-Bridge 'GET' '/state' @{ sections = ($Sections -join ',') }) }
    'console' {
        if (-not $Rest) { Fail 'usage: console "<command>" ["<command>" ...]' }
        Out-Bridge (Invoke-Bridge 'POST' '/console' @{} ($Rest -join "`n") 300)
    }
    'log'        { Out-Bridge (Invoke-Bridge 'GET' '/log' @{ since = $Since; level = $Level; limit = $Limit; contains = $Contains }) }
    'errors'     { Out-Bridge (Invoke-Bridge 'GET' '/log' @{ since = $Since; level = 'error'; limit = $Limit; contains = $Contains }) }
    'cvar'       { Out-Bridge (Invoke-Bridge 'GET' '/cvar' @{ names = ($Rest -join ',') }) }
    'setcvar' {
        if ($Rest.Count -lt 2) { Fail 'usage: setcvar <name> <value>' }
        Out-Bridge (Invoke-Bridge 'POST' '/cvar' @{ name = $Rest[0]; value = $Rest[1] })
    }
    'screenshot' { Out-Bridge (Invoke-Bridge 'POST' '/screenshot' @{ name = ($Rest | Select-Object -First 1); maxWidth = $MaxWidth }) }
    'ui' {
        if ($Rest -and $Rest.Count -ge 2) { Out-Bridge (Invoke-Bridge 'POST' '/ui' @{ action = $Rest[0]; window = $Rest[1] }) }
        else { Out-Bridge (Invoke-Bridge 'GET' '/ui') }
    }
    'look' {
        if (-not $Rest) { Fail 'usage: look <yaw> [pitch]' }
        $q = @{ yaw = $Rest[0] }; if ($Rest.Count -ge 2) { $q.pitch = $Rest[1] }
        Out-Bridge (Invoke-Bridge 'POST' '/look' $q)
    }
    'test' {
        if (-not $Rest) { Fail 'usage: test <file.json>' }
        Invoke-TestFile $Rest[0]
    }
    'call' {
        if (-not $Rest -or $Rest.Count -lt 2) { Fail 'usage: call GET|POST /path [key=value ...]' }
        $q = ConvertTo-Query ($Rest | Select-Object -Skip 2)
        $t = Get-KvTimeout $q
        Out-Bridge (Invoke-Bridge $Rest[0].ToUpperInvariant() $Rest[1] $q $null $t)
    }
    default {
        $name = $Command.ToLowerInvariant()
        if ($KvCommands.ContainsKey($name)) { Out-Bridge (Invoke-Kv $name (ConvertTo-Query $Rest)) }
        else { Fail "unknown command '$Command' (try: help)" }
    }
}
