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
    energy entity=ID [limit=256]          Read existing Energy sources/devices, native wiring and restoration baselines
    radial                               Read native radial context, active commands, selection and icon RGB (no input)
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
    guard   [enabled=0|1] [distance=6] [heal=0|1] [reload=0|1] [bleed=0|1]   Instincts: fight back/flee when an enemy gets close, heal when hurt and clear, reload when quiet; bleed controls the independent bleeding reflex (status + events)
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
    [int] $MaxWidth = 1600,
    [string] $SessionPath = $env:REBIRTH_BRIDGE_SESSION
)

$ErrorActionPreference = 'Stop'
$RebirthTempDir = if (Test-Path -LiteralPath 'E:\_Haven\shared') { 'E:\_Haven\shared\REBIRTHTemp' } else { Join-Path $env:LOCALAPPDATA 'Temp\REBIRTHTemp' }
New-Item -ItemType Directory -Path $RebirthTempDir -Force | Out-Null
$env:TEMP = $RebirthTempDir
$env:TMP = $RebirthTempDir
$BridgeDir  = $PSScriptRoot
$ModDir     = (Resolve-Path (Join-Path $BridgeDir '..\..')).Path
$GameDir    = (Resolve-Path (Join-Path $ModDir '..\..')).Path
$GameExe    = Join-Path $GameDir '7DaysToDie.exe'
$OutDir     = Join-Path $BridgeDir 'out'
$SessionFile = if ($SessionPath) { [IO.Path]::GetFullPath($SessionPath) } else { Join-Path $OutDir 'session.json' }
$LaunchFile = Join-Path $OutDir 'launch.json'
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

function Get-OwnedGameProcess {
    if (-not (Test-Path -LiteralPath $LaunchFile)) { return $null }
    $owner = Get-Content -LiteralPath $LaunchFile -Raw | ConvertFrom-Json
    $p = Get-Process -Id $owner.pid -ErrorAction SilentlyContinue
    if (-not $p -or $p.Path -ine $GameExe -or $p.StartTime.ToUniversalTime().ToString('o') -ne $owner.processStartUtc) { return $null }
    return $p
}

function Get-Session {
    if (-not (Test-Path -LiteralPath $SessionFile)) { return $null }
    $s = Get-Content -LiteralPath $SessionFile -Raw | ConvertFrom-Json
    $p = Get-Process -Id $s.pid -ErrorAction SilentlyContinue
    if (-not $p -or $p.Path -ine $GameExe -or -not $s.processStartUtc -or
        $p.StartTime.ToUniversalTime().Ticks -ne ([datetime]$s.processStartUtc).ToUniversalTime().Ticks) { return $null }
    if (-not $SessionPath) {
        $owned = Get-OwnedGameProcess
        if (-not $owned -or $owned.Id -ne $p.Id) { return $null }
    }
    # Pin this invocation to the immutable session, never keep following discovery.
    $unique = Join-Path $s.outputDir 'session.json'
    if (-not (Test-Path -LiteralPath $unique)) { return $null }
    $pinned = Get-Content -LiteralPath $unique -Raw | ConvertFrom-Json
    foreach ($field in @('pid','port','token','processStartUtc','startedUtc','outputDir')) {
        if ([string]$pinned.$field -cne [string]$s.$field) { return $null }
    }
    $script:SessionFile = $unique
    return $pinned
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
    if ($env:REBIRTH_BRIDGE_RUN_TOKEN) { $req.Headers.Add('X-Bridge-Run', $env:REBIRTH_BRIDGE_RUN_TOKEN) }
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
    $cachedFramework = Join-Path $env:USERPROFILE '.nuget\packages\microsoft.netframework.referenceassemblies.net481\1.0.3\build'
    if (Test-Path -LiteralPath $cachedFramework) {
        $out = & dotnet build (Join-Path $ModDir 'RebirthUtils.csproj') -c $cfg.buildConfiguration -nologo -v q "-p:TargetFrameworkRootPath=$cachedFramework" 2>&1
    } else {
        $out = & dotnet build (Join-Path $ModDir 'RebirthUtils.csproj') -c $cfg.buildConfiguration -nologo -v q 2>&1
    }
    $out | Where-Object { $_ -match 'error|warning CS' } | ForEach-Object { Write-Host $_ }
    if ($LASTEXITCODE -ne 0) { Fail 'build failed' }
    Write-Host 'Build succeeded.'
}

function Reset-TestSave($cfg, [bool] $force) {
    $saveRootFull = [IO.Path]::GetFullPath($SavesRoot).TrimEnd('\') + '\'
    $target = [IO.Path]::GetFullPath((Join-Path (Join-Path $SavesRoot $cfg.world) $cfg.save))
    if (-not $target.StartsWith($saveRootFull, [StringComparison]::OrdinalIgnoreCase) -or
        [IO.Path]::GetFileName($target) -ne $cfg.save -or $cfg.save -notmatch '^(CodexTest|RebirthBridge)[A-Za-z0-9_-]*$') {
        Fail 'reset/launch requires a disposable CodexTest* or RebirthBridge* save within SavesRoot'
    }
    # Never recursively remove through a junction/symlink into a real save.
    $cursor = $target
    while ($cursor -and $cursor.StartsWith($saveRootFull, [StringComparison]::OrdinalIgnoreCase)) {
        if (Test-Path -LiteralPath $cursor) {
            if ((Get-Item -LiteralPath $cursor).Attributes -band [IO.FileAttributes]::ReparsePoint) { Fail 'test save path contains a reparse point' }
        }
        $cursor = [IO.Path]::GetDirectoryName($cursor)
    }
    if (-not $cfg.baselineSave) {
        if (-not (Test-Path $target)) { Write-Host "Save '$($cfg.world)/$($cfg.save)' does not exist and no baselineSave is configured; the game will try to create a new game." }
        return
    }
    if ($cfg.baselineSave -eq $cfg.save) { Fail 'baselineSave and save must be different (the test save gets overwritten)' }
    $source = [IO.Path]::GetFullPath((Join-Path (Join-Path $SavesRoot $cfg.world) $cfg.baselineSave))
    if (-not $source.StartsWith($saveRootFull, [StringComparison]::OrdinalIgnoreCase) -or $source -eq $target) { Fail 'baseline must be distinct and inside SavesRoot' }
    if (-not (Test-Path $source)) { Fail "baseline save not found: $source" }
    if ((Test-Path $target) -and -not $force) { return }
    if (Get-GameProcess) { Fail 'cannot reset the save while the game is running' }
    if (Test-Path $target) { Remove-Item $target -Recurse -Force }
    Copy-Item $source $target -Recurse
    Write-Host "Test save '$($cfg.save)' cloned from '$($cfg.baselineSave)' ($($cfg.world))."
}

function Get-LatestGameLog {
    $session = Get-Session
    if ($session -and $session.gameLog -and (Test-Path -LiteralPath $session.gameLog)) { return Get-Item -LiteralPath $session.gameLog }
    # Launched directly (not via the Steam launcher), Unity writes its log here.
    Get-Item $PlayerLog -ErrorAction SilentlyContinue
}

function Wait-InGame([int] $timeoutSec) {
    $deadline = (Get-Date).AddSeconds($timeoutSec)
    $last = ''; $stateSince = Get-Date; $waitStart = Get-Date
    while ((Get-Date) -lt $deadline) {
        if (-not (Get-OwnedGameProcess)) {
            if ($script:launchedGameProcess -and $script:launchedGameProcess.HasExited) {
                [Console]::Error.WriteLine('Launched game exited with code ' + $script:launchedGameProcess.ExitCode)
            }
            $log = Get-LatestGameLog
            [Console]::Error.WriteLine('The game process exited while waiting.')
            if ($log -and (!$script:launchedGameProcess -or $log.LastWriteTime -ge $script:launchedGameProcess.StartTime)) {
                [Console]::Error.WriteLine('Last log lines:')
                Get-Content $log.FullName -Tail 40 | ForEach-Object { [Console]::Error.WriteLine($_) }
            }
            else { [Console]::Error.WriteLine('No new Player.log was written by this launch.') }
            exit 1
        }
        if (Get-Session) {
            $r = Invoke-Bridge 'GET' '/ping' @{} $null 10
            $state = $r.Json.state
            if ($state -ne $last) { Write-Host "  state: $state"; $last = $state; $stateSince = Get-Date }
            if ($state -eq 'ingame') {
                # Native AutoRespawnTick owns respawn; no competing watcher process.
                Set-StartTime; Out-Bridge $r; return
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
    # The game is the authorized interactive test surface; keep its window visible for rendering and user control.
    $env:REBIRTH_BRIDGE_NOCHORES = '1' # Native guard stays on; tests own setup and queues.
    if ($env:REBIRTH_BRIDGE_HUMAN -eq '1') { Fail 'human recording mode disables instincts; clear REBIRTH_BRIDGE_HUMAN before automated launch' }
    $launchedGame = Start-Process -FilePath $GameExe -ArgumentList $gameArgs -WorkingDirectory $GameDir -WindowStyle Normal -PassThru
    $script:launchedGameProcess = $launchedGame
    New-Item -ItemType Directory -Path $OutDir -Force | Out-Null
    @{ pid = $launchedGame.Id; processStartUtc = $launchedGame.StartTime.ToUniversalTime().ToString('o'); executable = $GameExe; world = $cfg.world; save = $cfg.save } |
        ConvertTo-Json | Set-Content -LiteralPath $LaunchFile -Encoding UTF8
    Write-Host "Launched process ID: $($launchedGame.Id)"
    if ($launchedGame.WaitForExit(1000)) {
        Fail "Game process $($launchedGame.Id) exited during startup (exit code $($launchedGame.ExitCode)); no bridge gameplay session was established."
    }
    if (-not $NoWait) { Wait-InGame $Timeout }
}

function Stop-Game {
    $p = Get-OwnedGameProcess
    if (-not $p) {
        if (Get-GameProcess) { Fail 'running game is not owned by this launcher; refusing to close it' }
        return
    }
    if (-not (Get-Session)) { Fail 'owned process has no validated bridge session; refusing ambiguous window close' }
    $reply = Invoke-Bridge 'POST' '/quit' @{} $null 10
    if ($reply.Status -ge 400) { Fail ('quit refused: ' + $reply.Body) }
    if (-not $p.WaitForExit(120000)) { Fail 'the owned game did not exit within 120 s' }
    Write-Host 'Owned game exited.'
}

# ------------------------------------------------------------------ key=value endpoints

# command -> method + endpoint; arguments are key=value pairs turned into the query string
$KvCommands = @{
    screenshot = @('POST', '/screenshot'); look = @('POST', '/look'); console = @('POST', '/console')
    state = @('GET', '/state') # Read-only scenario state capture, same endpoint as CLI state.
    camperturrets = @('GET', '/camperturrets') # Bounded native turret/pose/LOD observation; no injected input.
    skillsetup = @('POST', '/skillsetup')
    stationtoolsetup = @('POST', '/stationtoolsetup');
    purgeinformationpreview = @('POST', '/purgeinformationpreview');
    purgediscovery = @('POST', '/purgediscovery');
    radial = @('GET', '/radial'); energy = @('GET', '/energy'); camper = @('GET', '/camper'); station = @('GET', '/station'); target = @('GET', '/target');        findblocks = @('GET', '/findblocks');  lookat = @('POST', '/lookat')
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
    loadout = @('POST', '/loadout'); setblock = @('POST', '/setblock'); blockdamage = @('GET', '/blockdamage'); testflag = @('POST', '/testflag'); craft = @('POST', '/craft'); pois = @('GET', '/pois'); gotopoi = @('POST', '/gotopoi'); clearpoi = @('POST', '/clearpoi'); lootpoi = @('POST', '/lootpoi'); raidpoi = @('POST', '/raidpoi'); enterpoi = @('POST', '/enterpoi'); routeto = @('POST', '/routeto'); breakdoor = @('POST', '/breakdoor'); lootbags = @('POST', '/lootbags')
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
    if ($name -eq 'screenshot' -and $q.ContainsKey('label') -and -not $q.ContainsKey('name')) { $q.name = $q.label; $q.Remove('label') }
    $spec = $KvCommands[$name]
    $t = Get-KvTimeout $q
    return Invoke-Bridge $spec[0] $spec[1] $q $null $t
}

# ------------------------------------------------------------------ test runner

function Test-Compare($actual, [string] $op, $expected) {
    if ($null -eq $actual) { return $false }
    if ($expected -is [string] -or $expected -is [bool]) {
        switch ($op) {
            '==' { return $actual -ceq $expected }
            '!=' { return $actual -cne $expected }
            default { throw "Operator '$op' requires numeric values" }
        }
    }
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

function Get-StationTestValue($snapshot, $arg) {
    if ($null -eq $snapshot -or $snapshot.observationVersion -ne 1 -or $snapshot.ok -eq $false) { throw 'Station snapshot unavailable or unsupported' }
    if ([string]::IsNullOrWhiteSpace([string]$arg.station) -or $snapshot.station -ne $arg.station) { throw 'Expected station identity does not match' }
    if ($null -eq $snapshot.PSObject.Properties['accessed']) { throw 'Station access state missing' }
    if ($snapshot.accessed -and $arg.allowOpen -ne $true) { throw 'Close the station before asserting tile state, or explicitly allowOpen for a tile-only observation' }
    $field = [string]$arg.field
    if ($field -notin @('queueCapacity','queueCount','outputCapacity','tools','fuel','output')) { throw 'Unsupported station assertion field' }
    if ($null -eq $snapshot.PSObject.Properties[$field] -or $null -eq $snapshot.$field) { throw 'Station assertion data missing' }
    if ($field -in @('tools','fuel','output')) {
        if ([string]::IsNullOrWhiteSpace([string]$arg.item)) { throw 'Item name required for station slot assertion' }
        $total = 0
        foreach ($cell in @($snapshot.$field)) {
            if ($null -eq $cell.name -or $null -eq $cell.count -or $null -eq $cell.slot) { throw 'Incomplete station slot data' }
            if ($cell.name -eq $arg.item -and ($null -eq $arg.slot -or $cell.slot -eq $arg.slot)) { $total += [int]$cell.count }
        }
        return $total
    }
    return $snapshot.$field
}

function Get-PurgeTestObservation {
    $response = Invoke-Bridge 'POST' '/console' @{} 'rbpurgetestobserve' 10
    if ($response.Status -ge 400 -or $response.Json.ok -ne $true -or $response.Json.hadError) {
        throw 'Purge observation command failed'
    }
    $lines = @($response.Json.results | ForEach-Object { $_.output } | Where-Object { $_ -is [string] -and $_.StartsWith('{') })
    if ($lines.Count -ne 1) { throw 'Expected exactly one Purge JSON observation' }
    $observation = $lines[0] | ConvertFrom-Json
    if ($observation.schema -ne 1 -or $observation.observationOnly -ne $true) { throw 'Unsupported Purge observation schema' }
    return $observation
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
    $executable = @($test.steps | Where-Object { -not ($_.PSObject.Properties.Name.StartsWith('_')) })
    if ($executable.Count -eq 0) { Fail 'scenario contains only comments' }
    $startSeq = (Invoke-Bridge 'GET' '/ping').Json.lastLogSeq
    $results = @(); $failed = 0; $i = 0; $skillMarks = @{}; $itemMarks = @{}; $fenceMarks = @{}
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
                'expectEntityStudio' {
                    if ([string]::IsNullOrWhiteSpace([string]$arg.entityClass) -or @($arg.checks).Count -eq 0) { throw 'expectEntityStudio requires exact entityClass and checks' }
                    $nearby = Invoke-Bridge 'GET' '/entities' @{ radius = 80; contains = [string]$arg.entityClass }
                    Assert-TestResponseField $nearby 'entities'
                    $studioSpecimens = @($nearby.Json.entities | Where-Object { $_.class -ceq [string]$arg.entityClass -and -not $_.dead })
                    if ($studioSpecimens.Count -ne 1) { throw "Expected one live exact class '$($arg.entityClass)', found $($studioSpecimens.Count)" }
                    $r = Invoke-Bridge 'GET' '/entitystudio/physics' @{entity=$studioSpecimens[0].id}
                    Assert-TestResponseField $r 'entityId'
                    if (@($arg.checks | Where-Object { $_.field -like 'identity.*' }).Count) {
                        $studioIdentity = Invoke-Bridge 'GET' '/entitystudio/entity' @{entity=$studioSpecimens[0].id}
                        Assert-TestResponseField $studioIdentity 'entityId'
                        $r.Json | Add-Member -NotePropertyName identity -NotePropertyValue $studioIdentity.Json
                    }
                    $r.Json | ConvertTo-Json -Depth 50 | Set-Content (Join-Path $evidenceDir ('{0:D3}_entitystudio_physics.json' -f $i))
                    $details = @()
                    foreach ($check in $arg.checks) {
                        if ([string]::IsNullOrWhiteSpace([string]$check.field)) { throw 'Entity Studio check requires field' }
                        $actual = $r.Json
                        foreach ($part in ([string]$check.field).Split('.')) {
                            if ($null -eq $actual -or $null -eq $actual.PSObject.Properties[$part]) { throw "Missing Entity Studio field '$($check.field)'" }
                            $actual = $actual.$part
                        }
                        if ($null -eq $check.PSObject.Properties['value']) { throw 'Entity Studio check requires value' }
                        if ($null -ne $check.PSObject.Properties['tolerance']) {
                            if ([double]$check.tolerance -lt 0 -or $check.op -ne '==') { throw 'Tolerance requires nonnegative value and == operator' }
                            $passed = [Math]::Abs([double]$actual-[double]$check.value) -le [double]$check.tolerance
                        } else { $passed = Test-Compare $actual $check.op $check.value }
                        if (-not $passed) { $ok = $false }; $details += "$($check.field)=$actual => $passed"
                    }
                    if ($arg.capture) {
                        if ($arg.fullResolution) {
                            try {
                                $textureBefore = Invoke-Bridge 'GET' '/entitystudio/textures' @{entity=$studioSpecimens[0].id}
                                Assert-TestResponseField $textureBefore 'textures'
                                $textureBefore.Json | ConvertTo-Json -Depth 50 | Set-Content (Join-Path $evidenceDir ('{0:D3}_entitystudio_textures_before.json' -f $i))
                                $texturePrep = Invoke-Bridge 'POST' '/entitystudio/textureprepare' @{entity=$studioSpecimens[0].id}
                                Assert-TestResponseField $texturePrep 'mipLeaseActive'
                                $textureReady = $texturePrep.Json
                                $textureDeadline = [DateTime]::UtcNow.AddSeconds(15)
                                while (-not $textureReady.requestedLevelsLoaded -and [DateTime]::UtcNow -lt $textureDeadline) {
                                    Start-Sleep -Milliseconds 250
                                    $textureReply = Invoke-Bridge 'GET' '/entitystudio/textures' @{entity=$studioSpecimens[0].id}
                                    Assert-TestResponseField $textureReply 'textures'
                                    $textureReady = $textureReply.Json
                                }
                                $textureReady | ConvertTo-Json -Depth 50 | Set-Content (Join-Path $evidenceDir ('{0:D3}_entitystudio_textures.json' -f $i))
                                if (-not $textureReady.mipLeaseActive -or -not $textureReady.requestedLevelsLoaded) { throw 'Full-resolution texture request did not complete within its lease' }
                                if (@($textureReady.textures | Where-Object { $_.streaming -and ($_.requestedMipmapLevel -ne 0 -or $_.loadedMipmapLevel -ne 0) }).Count) { throw 'A streaming texture did not reach requested mip zero' }
                            } catch {
                                $null = Invoke-Bridge 'POST' '/entitystudio/textureprepare' @{entity=$studioSpecimens[0].id;release=1}
                                throw
                            }
                        }
                        $studioRenderQuery = @{entity=$studioSpecimens[0].id;pose='gpu'}
                        if ($arg.focus) {
                            if ($arg.focus -notin @('face','model')) { throw 'Entity Studio capture focus must be face or model' }
                            $studioRenderQuery.focus = $arg.focus
                        }
                        $render = Invoke-Bridge 'POST' '/entitystudio/render' $studioRenderQuery
                        Assert-TestResponseField $render 'path'
                        if ($arg.focus -and $render.Json.focus -ne $arg.focus) { throw 'Entity Studio capture focus was not applied' }
                        if ($arg.fullResolution) {
                            $textureAfter = Invoke-Bridge 'GET' '/entitystudio/textures' @{entity=$studioSpecimens[0].id}
                            Assert-TestResponseField $textureAfter 'textures'
                            $textureAfter.Json | ConvertTo-Json -Depth 50 | Set-Content (Join-Path $evidenceDir ('{0:D3}_entitystudio_textures_after.json' -f $i))
                            if ($textureAfter.Json.mipLeaseActive) { throw 'Capture did not release its mip request lease' }
                            foreach ($priorTexture in $textureBefore.Json.textures) {
                                $afterTexture = @($textureAfter.Json.textures | Where-Object instanceId -eq $priorTexture.instanceId)
                                if ($afterTexture.Count -ne 1 -or $afterTexture[0].requestedMipmapLevel -ne $priorTexture.requestedMipmapLevel) { throw "Capture did not restore texture request: $($priorTexture.name)" }
                            }
                        }
                        if (-not (Test-Path -LiteralPath $render.Json.path)) { throw 'Entity Studio capture file missing' }
                        $render.Json | ConvertTo-Json -Depth 20 | Set-Content (Join-Path $evidenceDir ('{0:D3}_entitystudio_render.json' -f $i))
                        $details += "render=$($render.Json.path)"
                    }
                    $detail = $details -join '; '
                }
                'expectCamper' {
                    if ($null -eq $arg.entity -or @($arg.checks).Count -eq 0) { throw 'expectCamper requires entity and checks' }
                    $deadline=(Get-Date).AddSeconds([Math]::Min(60,[Math]::Max(0,[double]$arg.timeout)))
                    do {
                        $r=Invoke-Bridge 'GET' '/camper' @{entity=$arg.entity} $null 10
                        if($r.Status -ne 200 -or $null -eq $r.Json){throw 'Camper observation failed'}
                        $observation=$r.Json;$ok=$true;$details=@()
                        foreach($check in $arg.checks){
                            if([string]::IsNullOrWhiteSpace([string]$check.field)){throw 'Camper check requires a field'}
                            $hasValue=$null -ne $check.PSObject.Properties['value']
                            $hasField=$null -ne $check.PSObject.Properties['expectedField']
                            if($hasValue -eq $hasField){throw 'Camper check requires exactly one of value or expectedField'}
                            $actual=$observation
                            foreach($part in ([string]$check.field).Split('.')){
                                if($null -eq $actual -or $null -eq $actual.PSObject.Properties[$part]){throw "Missing camper field $($check.field)"}
                                $actual=$actual.$part
                            }
                            $expected=$check.value
                            if($null -ne $check.PSObject.Properties['expectedField']){
                                $expected=$observation
                                foreach($part in ([string]$check.expectedField).Split('.')){
                                    if($null -eq $expected -or $null -eq $expected.PSObject.Properties[$part]){throw "Missing camper comparison field $($check.expectedField)"}
                                    $expected=$expected.$part
                                }
                            }elseif($null -eq $check.PSObject.Properties['value']){throw 'Camper check requires value or expectedField'}
                            $passed=Test-Compare $actual $check.op $expected
                            if(!$passed){$ok=$false};$details+="$($check.field): $actual $($check.op) $expected => $passed"
                        }
                        if($ok -or (Get-Date) -ge $deadline){break}
                        Start-Sleep -Milliseconds 500
                    }while($true)
                    $detail=$details -join '; '
                }
                'expectEnergy' {
                    if ($null -eq $arg.entity -or (@($arg.lamps).Count -eq 0 -and @($arg.nodes).Count -eq 0 -and @($arg.sources).Count -eq 0)) { throw 'expectEnergy requires entity and lamps, nodes, or sources' }
                    $deadline=(Get-Date).AddSeconds([Math]::Min(60,[Math]::Max(0,[double]$arg.timeout)))
                    do {
                        $r=Invoke-Bridge 'GET' '/energy' @{entity=$arg.entity} $null 10
                        if($r.Status -ne 200 -or $null -eq $r.Json -or $r.Json.ok -ne $true){throw 'Energy observation failed'}
                        $observation=$r.Json;$ok=$true;$details=@()
                        if($null -ne $arg.PSObject.Properties['sourceEnabled']){
                            $enabled=@($observation.sources)[0].enabled
                            $passed=$enabled -eq [bool]$arg.sourceEnabled
                            if(!$passed){$ok=$false};$details+="sourceEnabled: $enabled == $($arg.sourceEnabled) => $passed"
                        }
                        foreach($source in $arg.sources){
                            $rows=@($observation.sources | Where-Object { $_.position -eq $source.position })
                            $passed=$rows.Count -eq 1
                            if($passed -and $null -ne $source.PSObject.Properties['kind']){$passed=$rows[0].kind -eq [string]$source.kind}
                            if($passed -and $null -ne $source.PSObject.Properties['enabled']){$passed=$rows[0].enabled -eq [bool]$source.enabled}
                            if($passed -and $null -ne $source.PSObject.Properties['minFuel']){$passed=[double]$rows[0].fuel -ge [double]$source.minFuel}
                            if($passed -and $null -ne $source.PSObject.Properties['minAvailableWatts']){$passed=[double]$rows[0].availableWatts -ge [double]$source.minAvailableWatts}
                            foreach($component in $source.components){
                                $parts=@($rows[0].components | Where-Object { $_.slot -eq $component.slot })
                                $componentPassed=$rows.Count -eq 1 -and $parts.Count -eq 1 -and $parts[0].name -eq [string]$component.name
                                if(!$componentPassed){$passed=$false}
                                $details+="source $($source.position) component $($component.slot): $($parts[0].name) == $($component.name) => $componentPassed"
                            }
                            if(!$passed){$ok=$false}
                            $details+="source $($source.position): matched=$($rows.Count) kind=$($rows[0].kind) enabled=$($rows[0].enabled) fuel=$($rows[0].fuel) watts=$($rows[0].availableWatts) => $passed"
                        }
                        foreach($lamp in $arg.lamps){
                            $rows=@($observation.devices.discovery.blocks | Where-Object {
                                $_.position.x -eq $lamp.x -and $_.position.y -eq $lamp.y -and $_.position.z -eq $lamp.z
                            })
                            $passed=$rows.Count -eq 1 -and $rows[0].block -eq [string]$lamp.block -and
                                $rows[0].powered -eq [bool]$lamp.powered -and $rows[0].nativeTilePresent -eq $true
                            if(!$passed){$ok=$false}
                            $details+="lamp $($lamp.x),$($lamp.y),$($lamp.z): matched=$($rows.Count) powered=$($rows[0].powered) tile=$($rows[0].nativeTilePresent) => $passed"
                        }
                        foreach($node in $arg.nodes){
                            $rows=@($observation.devices.nodes | Where-Object {
                                $_.position[0] -eq $node.x -and $_.position[1] -eq $node.y -and $_.position[2] -eq $node.z
                            })
                            $passed=$rows.Count -eq 1 -and $rows[0].block -eq [string]$node.block -and
                                $rows[0].powered -eq [bool]$node.powered -and $rows[0].playerPlaced -eq $true -and
                                $rows[0].ownedByCamper -eq $true
                            if($passed -and $null -ne $node.PSObject.Properties['parent']){
                                $passed=@($rows[0].nativeParent).Count -eq 3 -and
                                    $rows[0].nativeParent[0] -eq $node.parent[0] -and
                                    $rows[0].nativeParent[1] -eq $node.parent[1] -and
                                    $rows[0].nativeParent[2] -eq $node.parent[2]
                            }
                            if(!$passed){$ok=$false}
                            $details+="node $($node.x),$($node.y),$($node.z): matched=$($rows.Count) powered=$($rows[0].powered) owner=$($rows[0].ownedByCamper) => $passed"
                        }
                        if($ok -or (Get-Date) -ge $deadline){break}
                        Start-Sleep -Milliseconds 500
                    }while($true)
                    $detail=$details -join '; '
                }                'expectPurge' {
                    $observation = Get-PurgeTestObservation
                    $r = [pscustomobject]@{ Json = $observation; Status = 200 }
                    if (@($arg.checks).Count -eq 0) { throw 'expectPurge requires checks' }
                    $details = @()
                    foreach ($check in $arg.checks) {
                        if ([string]::IsNullOrWhiteSpace([string]$check.field)) { throw 'Purge check requires a field' }
                        $actual = $observation
                        foreach ($part in ([string]$check.field).Split('.')) {
                            if ($null -eq $actual -or $null -eq $actual.PSObject.Properties[$part]) {
                                throw "Missing Purge observation field '$($check.field)'"
                            }
                            $actual = $actual.$part
                        }
                        $hasValue = $null -ne $check.PSObject.Properties['value']
                        $hasField = $null -ne $check.PSObject.Properties['expectedField']
                        if ($hasValue -eq $hasField) { throw 'Purge check requires exactly one of value or expectedField' }
                        $expected = $check.value
                        if ($hasField) {
                            if ([string]::IsNullOrWhiteSpace([string]$check.expectedField)) { throw 'Expected field cannot be empty' }
                            $expected = $observation
                            foreach ($part in ([string]$check.expectedField).Split('.')) {
                                if ($null -eq $expected -or $null -eq $expected.PSObject.Properties[$part]) {
                                    throw "Missing Purge comparison field '$($check.expectedField)'"
                                }
                                $expected = $expected.$part
                            }
                            if ($null -eq $actual -or $null -eq $expected) { throw 'Purge field comparison requires non-null observations' }
                        }
                        $passed = Test-Compare $actual $check.op $expected
                        if (-not $passed) { $ok = $false }
                        $details += "$($check.field): $actual $($check.op) $expected => $passed"
                    }
                    $detail = $details -join '; '
                }
                'action' {
                    if (-not $KvCommands.ContainsKey([string]$arg.command)) { throw "Unknown bridge action '$($arg.command)'" }
                    $query = @{}
                    if ($arg.args) { foreach ($property in $arg.args.PSObject.Properties) { $query[$property.Name] = [string]$property.Value } }
                    $r = Invoke-Kv $arg.command $query
                    $ok = $r.Status -lt 400 -and $r.Json.ok -eq $true
                    $detail = $r.Json | ConvertTo-Json -Depth 6 -Compress
                }
                'waitForGuardIdle' {
                    $seconds = if ($arg.timeout) { [double]$arg.timeout } else { 45.0 }
                    if ($seconds -lt 1 -or $seconds -gt 60) { throw 'Guard idle timeout must be 1..60 seconds' }
                    $timer = [Diagnostics.Stopwatch]::StartNew()
                    $idleSince = -1.0
                    $ok = $false
                    do {
                        $r = Invoke-Bridge 'GET' '/guard' @{} $null 5
                        Assert-TestResponseField $r 'enabled'
                        Assert-TestResponseField $r 'busy'
                        if ($r.Json.enabled -isnot [bool] -or $r.Json.busy -isnot [bool]) { throw 'Guard state must contain Boolean enabled/busy fields' }
                        if (-not $r.Json.enabled) { throw 'Guard must remain enabled during testing' }
                        if ($r.Json.busy) { $idleSince = -1.0 }
                        elseif ($idleSince -lt 0) { $idleSince = $timer.Elapsed.TotalSeconds }
                        elseif ($timer.Elapsed.TotalSeconds - $idleSince -ge 1.0) { $ok = $true; break }
                        Start-Sleep -Milliseconds 250
                    } while ($timer.Elapsed.TotalSeconds -lt $seconds)
                    $detail = if ($ok) { 'Guard enabled and idle for one second' } else { 'Guard did not become idle before timeout; no input takeover attempted' }
                }
                'expectStation' {
                    foreach ($axis in @('x','y','z')) { if ($null -eq $arg.PSObject.Properties[$axis]) { throw 'Station coordinates required' } }
                    $r = Invoke-Bridge 'GET' '/station' @{x=$arg.x;y=$arg.y;z=$arg.z}
                    Assert-TestResponseField $r 'observationVersion'
                    $actual = Get-StationTestValue $r.Json $arg
                    $ok = Test-Compare $actual $arg.op $arg.value
                    $detail = "$($arg.station) $($arg.field) $($arg.item)=$actual (expected $($arg.op) $($arg.value))"
                }
                'markItem' {
                    $itemMarks[[string]$arg] = Get-ItemCount (Invoke-Bridge 'GET' '/state' @{sections='inventory'}).Json ([string]$arg)
                    $detail = "$arg=$($itemMarks[[string]$arg])"
                }
                'markFenceWear' {
                    if ([string]::IsNullOrWhiteSpace([string]$arg.id)) { throw 'Fence wear mark requires an ID' }
                    foreach ($axis in @('x1','y1','z1','x2','y2','z2')) {
                        if ($null -eq $arg.PSObject.Properties[$axis]) { throw "Fence wear mark requires $axis" }
                    }
                    $first = Invoke-Bridge 'GET' '/blockdamage' @{x=$arg.x1;y=$arg.y1;z=$arg.z1}
                    $second = Invoke-Bridge 'GET' '/blockdamage' @{x=$arg.x2;y=$arg.y2;z=$arg.z2}
                    if ($first.Status -ne 200 -or $second.Status -ne 200 -or
                        $first.Json.block -ne 'electricfencepost' -or $second.Json.block -ne 'electricfencepost' -or
                        $first.Json.authoritative -ne $true -or $second.Json.authoritative -ne $true -or
                        $first.Json.damage -ge $first.Json.maxDamage -or $second.Json.damage -ge $second.Json.maxDamage) {
                        throw 'Fence wear baseline requires two authoritative loaded electric fence posts'
                    }
                    $fenceMarks[[string]$arg.id] = @{ first=$first.Json; second=$second.Json }
                    $detail = "$($arg.id): first=$($first.Json.damage), second=$($second.Json.damage)"
                }
                'expectFenceWearSplit' {
                    if (-not $fenceMarks.ContainsKey([string]$arg.id)) { throw 'Fence wear baseline was not recorded' }
                    $baseline = $fenceMarks[[string]$arg.id]
                    $first = Invoke-Bridge 'GET' '/blockdamage' @{x=$baseline.first.x;y=$baseline.first.y;z=$baseline.first.z}
                    $second = Invoke-Bridge 'GET' '/blockdamage' @{x=$baseline.second.x;y=$baseline.second.y;z=$baseline.second.z}
                    if ($first.Status -ne 200 -or $second.Status -ne 200 -or
                        $first.Json.block -ne 'electricfencepost' -or $second.Json.block -ne 'electricfencepost' -or
                        $first.Json.authoritative -ne $true -or $second.Json.authoritative -ne $true) {
                        throw 'Fence wear comparison requires the same two authoritative loaded posts'
                    }
                    $a = [int]$first.Json.damage - [int]$baseline.first.damage
                    $b = [int]$second.Json.damage - [int]$baseline.second.damage
                    $ok = $a -gt 0 -and $b -gt 0 -and [Math]::Abs($a-$b) -le 1
                    $detail = "$($arg.id): first delta=$a, second delta=$b, split balanced=$ok"
                }
                'expectItemDelta' {
                    if (-not $itemMarks.ContainsKey([string]$arg.name)) { throw 'No item baseline was recorded' }
                    $count = Get-ItemCount (Invoke-Bridge 'GET' '/state' @{sections='inventory'}).Json ([string]$arg.name)
                    $delta = $count - $itemMarks[[string]$arg.name]
                    $ok = Test-Compare $delta $arg.op $arg.value
                    $detail = "$($arg.name) delta=$delta (expected $($arg.op) $($arg.value))"
                }
                'expectItemStacks' {
                    if ([string]::IsNullOrWhiteSpace([string]$arg.name) -or $null -eq $arg.counts) {
                        throw 'Item name and exact stack counts are required'
                    }
                    $r = Invoke-Bridge 'GET' '/state' @{sections='inventory'}
                    Assert-TestResponseField $r 'backpack'
                    Assert-TestResponseField $r 'toolbelt'
                    $actual = @(@($r.Json.backpack) + @($r.Json.toolbelt) |
                        Where-Object { $_.name -eq $arg.name } |
                        ForEach-Object { [int]$_.count } | Sort-Object)
                    $wanted = @($arg.counts | ForEach-Object {
                        if ([int]$_ -lt 1) { throw 'Expected stack counts must be positive' }
                        [int]$_
                    } | Sort-Object)
                    $ok = ($actual.Count -eq $wanted.Count) -and
                        (($actual -join ',') -ceq ($wanted -join ','))
                    $detail = "$($arg.name) stacks=[$($actual -join ',')] expected=[$($wanted -join ',')] (backpack and toolbelt; cursor excluded)"
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
                'waitForWindowOpen' {
                    if ([string]::IsNullOrWhiteSpace([string]$arg.window)) { throw 'Window ID required' }
                    $seconds = if ($arg.timeout) { [double]$arg.timeout } else { 5.0 }
                    if ($seconds -lt 1 -or $seconds -gt 30) { throw 'Window timeout must be 1..30 seconds' }
                    $timer = [Diagnostics.Stopwatch]::StartNew()
                    $openSince = -1.0
                    $ok = $false
                    do {
                        $r = Invoke-Bridge 'GET' '/ui' @{} $null 5
                        Assert-TestResponseField $r 'openWindows'
                        $open = @($r.Json.openWindows)
                        if ($open -notcontains $arg.window) { $openSince = -1.0 }
                        elseif ($openSince -lt 0) { $openSince = $timer.Elapsed.TotalSeconds }
                        elseif ($timer.Elapsed.TotalSeconds - $openSince -ge 0.5) { $ok = $true; break }
                        Start-Sleep -Milliseconds 100
                    } while ($timer.Elapsed.TotalSeconds -lt $seconds)
                    $detail = if ($ok) { "Window '$($arg.window)' remained open for 0.5 seconds" } else { "Window '$($arg.window)' did not settle open; no repeated activation sent" }
                }
                'expectWindowOpen' {
                    $r = Invoke-Bridge 'GET' '/ui'
                    Assert-TestResponseField $r 'openWindows'
                    $open = @($r.Json.openWindows)
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
                'expectUiItem' {
                    if ([string]::IsNullOrWhiteSpace([string]$arg.path) -or [string]::IsNullOrWhiteSpace([string]$arg.item)) { throw 'expectUiItem requires exact path and item' }
                    $r = Invoke-Bridge 'GET' '/ui/find' @{path=[string]$arg.path;item=[string]$arg.item}
                    Assert-TestResponseField $r 'nodes'
                    $rows = @($r.Json.nodes)
                    $count = if ($rows.Count -eq 1 -and $null -ne $rows[0].item) { [int]$rows[0].item.count } else { -1 }
                    $ok = $rows.Count -eq 1 -and $count -ge [int]$arg.min -and $count -le [int]$arg.max
                    $detail = "path=$($arg.path) item=$($arg.item) count=$count expected=$($arg.min)..$($arg.max)"
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
        name = $test.name; evidenceDir = $evidenceDir; passed = ($failed -eq 0 -and $errs.count -eq 0); failedSteps = $failed; totalSteps = $i
        newErrorLogLines = $errs.count; errors = @($errs.entries | ForEach-Object { $_.msg }); steps = $results
    }
    [Console]::Out.WriteLine(($summary | ConvertTo-Json -Depth 6 -Compress))
    $summary | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $evidenceDir "summary.json") -Encoding UTF8
    if ($failed -gt 0 -or $errs.count -gt 0) { exit 1 }
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

