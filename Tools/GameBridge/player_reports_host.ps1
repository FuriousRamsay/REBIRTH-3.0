[CmdletBinding()]
param(
    [ValidateSet('Prepare','Start','Client','Join','Solo','StopClient','Stop')][string]$Mode = 'Prepare',
    [string]$RunRoot,
    [string]$SandboxCodeFile,
    [ValidateRange(1024,65520)][int]$ServerPort = 26915,
    [ValidateRange(1024,65520)][int]$ServerBridgePort = 8891,
    [ValidateRange(1024,65520)][int]$ClientBridgePort = 8892,
    [string]$World = 'Navezgane',
    [switch]$FreshClientData,
    [ValidateSet('FirstEntry','Resume')][string]$NativeSpawn,
    [ValidateRange(10,900)][int]$TimeoutSeconds = 300
)
$ErrorActionPreference = 'Stop'
$mod = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$game = (Resolve-Path (Join-Path $mod '../..')).Path
$exe = Join-Path $game '7DaysToDie.exe'
$dll = Join-Path $mod 'RebirthUtils.dll'
$sessions = Join-Path $PSScriptRoot 'out/sessions'
if (!$RunRoot) {
    if ($Mode -notin @('Prepare','Start')) { throw 'Supply -RunRoot from the Prepare or Start output.' }
    $RunRoot = Join-Path ([IO.Path]::GetTempPath()) ('RBQA-' + [Guid]::NewGuid().ToString('N').Substring(0,10))
}
$RunRoot = [IO.Path]::GetFullPath($RunRoot)
if ((Split-Path $RunRoot -Leaf) -notmatch '^RBQA-[a-f0-9]{10}$') { throw 'Use a launcher-generated RBQA run directory.' }
$receiptPath = Join-Path $RunRoot 'run.json'
function Save-Run { $run | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $receiptPath -Encoding UTF8 }
function Get-Owned($receipt) {
    if (!$receipt) { return }
    $p = Get-Process -Id $receipt.pid -ErrorAction SilentlyContinue
    if ($p -and $p.Path -ieq $exe -and $p.StartTime.ToUniversalTime().Ticks -eq ([datetime]$receipt.started).ToUniversalTime().Ticks) { $p }
}
function Bridge-Ping($p, $port, $wanted) {
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        $p.Refresh()
        if ($p.HasExited) { throw "Game PID $($p.Id) exited with code $($p.ExitCode). Check its log in $RunRoot." }
        foreach ($file in @(Get-ChildItem -LiteralPath $sessions -Directory -Filter "$($p.Id)-*" -ErrorAction SilentlyContinue)) {
            $path = Join-Path $file.FullName 'session.json'
            if (!(Test-Path -LiteralPath $path)) { continue }
            try {
                $s = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
                if ($s.pid -ne $p.Id -or $s.port -ne $port -or $p.StartTime.ToUniversalTime().Ticks -ne ([datetime]$s.processStartUtc).ToUniversalTime().Ticks) { continue }
                $ping = Invoke-RestMethod -Uri "http://127.0.0.1:$port/ping" -Headers @{'X-Bridge-Token'=$s.token} -TimeoutSec 2
                if (!$wanted -or $ping.state -eq $wanted) { return $path }
            } catch {}
        }
        Start-Sleep -Seconds 2
    }
    throw "Bridge readiness timed out for PID $($p.Id). Use Stop with this run directory to shut it down."
}
function Quote-Arg($name,$value) {
    if ($value -match '["\r\n]') { throw 'Invalid launch argument.' }
    return ('-' + $name + '="' + $value + '"')
}
function Wait-SoloSaveStart($run) {
    . (Join-Path $PSScriptRoot 'player_reports_bridge.ps1')
    $client = Get-ReportBridge $run.client
    $wanted = "Loading existing save game '$($run.gameName)' (world '$($run.world)')."
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $selectedContinue = $false
    $lastReadFailure = $null
    function Read-SoloStart([string]$path) {
        $seconds = [int][Math]::Ceiling(($deadline - (Get-Date)).TotalSeconds)
        if ($seconds -gt 0) {
            Invoke-ReportBridge -session $client -path $path -TimeoutSeconds ([Math]::Min(15,$seconds))
        }
    }
    while ((Get-Date) -lt $deadline) {
        $continuePath = $null
        $probe = '/log?contains=LoadSaveGame&limit=30'
        try {
            $log = Read-SoloStart $probe
            if (!$log) { continue }
            $selection = $log.entries | Where-Object { $_.msg.Contains($wanted) } | Select-Object -First 1
            if ($selection) {
                if ($log.entries | Where-Object { $_.seq -gt $selection.seq -and $_.msg -match 'Advanced to state Done \(was ContinueGamePlay\)' }) { return }
                $probe = '/ping'
                if (!$selectedContinue -and (Read-SoloStart $probe).state -eq 'menu') {
                    $probe = '/ui/tree'
                    $tree = Read-SoloStart $probe
                    $tab = @($tree.nodes | Where-Object { $_.id -eq 'continueGame' -and $_.clickable })
                    if ('playerProfiles' -in $tree.openWindows -and 'playGamePaging' -in $tree.openWindows -and $tab.Count -eq 1) {
                        $continuePath = $tab[0].path
                    }
                }
            }
        } catch [System.OperationCanceledException], [System.TimeoutException], [System.Net.Http.HttpRequestException] {
            $lastReadFailure = "$probe ($($_.Exception.GetType().Name)): $($_.Exception.Message)"
        } catch [System.Net.WebException] {
            if ($_.Exception.Status -notin @('Timeout','ConnectFailure','ConnectionClosed','ReceiveFailure','SendFailure','KeepAliveFailure')) { throw }
            $lastReadFailure = "$probe ($($_.Exception.GetType().Name)): $($_.Exception.Message)"
        }
        if ($continuePath -and (Get-Date) -lt $deadline) {
            $uri = "http://127.0.0.1:$($client.port)/ui/click?path=" + [uri]::EscapeDataString($continuePath)
            $reply = Invoke-RestMethod -Method Post -Uri $uri -Headers @{'X-Bridge-Token'=$client.token} -TimeoutSec 12
            if (!$reply.ok) { throw 'Native Continue Game tab could not be selected.' }
            $selectedContinue = $true
        }
        Start-Sleep -Milliseconds 500
    }
    $failure = 'The isolated solo save did not start loading within the launch timeout.'
    if ($lastReadFailure) { $failure += " Last startup read failure: $lastReadFailure" }
    throw $failure
}
function Assert-PortFree($port) {
    if ((Get-NetTCPConnection -State Listen -LocalPort $port -ErrorAction SilentlyContinue) -or
        (Get-NetUDPEndpoint -LocalPort $port -ErrorAction SilentlyContinue)) { throw "Port $port is already in use." }
}
if ($Mode -in @('Prepare','Start') -and !(Test-Path -LiteralPath $receiptPath)) {
    if (Test-Path -LiteralPath $RunRoot) { throw 'Run directory already exists without a receipt.' }
    if (!$SandboxCodeFile) {
        $known = Join-Path $mod 'RebirthSandboxCode.txt'
        if (!(Test-Path -LiteralPath $known)) { $known = 'E:/Worlds/RebirthSandboxCode.txt' }
        if (Test-Path -LiteralPath $known) { $SandboxCodeFile = $known }
    }
    if (!$SandboxCodeFile) { throw 'Supply a REBIRTH sandbox export with -SandboxCodeFile.' }
    $code = Get-Content -LiteralPath $SandboxCodeFile | ForEach-Object { $_.Trim() } | Where-Object { $_ -match '^RB\S+$' } | Select-Object -First 1
    if (!$code) { throw 'The sandbox file has no REBIRTH export line.' }
    $options = for ($i=3; $i+2 -lt $code.Length; $i+=3) { $code.Substring($i,3) }
    if (($code.Length-3) % 3 -ne 0 -or $options -cnotcontains 'CTB') { throw 'The REBIRTH export must enable Purge (theme option 71, value 1).' }
    $ports = @($ServerPort,($ServerPort+1),($ServerPort+2),$ServerBridgePort,$ClientBridgePort)
    if (@($ports | Select-Object -Unique).Count -ne $ports.Count) { throw 'Game and Bridge ports overlap.' }
    $null = New-Item -ItemType Directory -Path $RunRoot
    $run = [pscustomobject]@{
        root=$RunRoot; gameName=('CodexTestReports-' + (Split-Path $RunRoot -Leaf).Substring(5)); world=$World
        gamePort=$ServerPort; serverBridgePort=$ServerBridgePort; clientBridgePort=$ClientBridgePort
        dllHash=(Get-FileHash -LiteralPath $dll).Hash; server=$null; client=$null
    }
    [xml]$config = Get-Content -LiteralPath (Join-Path $game 'serverconfig.xml') -Raw
    $settings = @{
        ServerName=$run.gameName; ServerPort=$run.gamePort; ServerVisibility=0; ServerMaxPlayerCount=5
        ServerPassword=[Guid]::NewGuid().ToString('N').Substring(0,12)
        ServerDisabledNetworkProtocols='SteamNetworking'; EACEnabled='false'; TelnetEnabled='false'
        WebDashboardEnabled='false'; TerminalWindowEnabled='false'; GameName=$run.gameName
        GameWorld=$World; UserDataFolder=(Join-Path $RunRoot 'server-data')
    }
    foreach ($key in $settings.Keys) {
        $node = $config.SelectSingleNode("/ServerSettings/property[@name='$key']")
        if (!$node) { $node = $config.CreateElement('property'); $node.SetAttribute('name',$key); $null = $config.DocumentElement.AppendChild($node) }
        $node.SetAttribute('value',[string]$settings[$key])
    }
    $section = $config.CreateElement('RebirthSettings')
    $property = $config.CreateElement('property')
    $property.SetAttribute('name','RebirthSandboxCode'); $property.SetAttribute('value',$code)
    $null = $section.AppendChild($property); $null = $config.DocumentElement.AppendChild($section)
    $config.Save((Join-Path $RunRoot 'serverconfig.xml'))
    Save-Run
}
if (!(Test-Path -LiteralPath $receiptPath)) { throw 'No launcher receipt for this run.' }
$run = Get-Content -LiteralPath $receiptPath -Raw | ConvertFrom-Json
if ($run.root -ine $RunRoot -or $run.gameName -notmatch '^CodexTestReports-[a-f0-9]{10}$') { throw 'Run receipt mismatch.' }
if ($Mode -eq 'Prepare') { $run | ConvertTo-Json -Depth 8; return }
if ($Mode -eq 'Join') {
    if (!(Get-Owned $run.server) -or !(Get-Owned $run.client)) { throw 'Start both processes in this run first.' }
    . (Join-Path $PSScriptRoot 'player_reports_bridge.ps1')
    $client = Get-ReportBridge $run.client
    $hostBridge = Get-ReportBridge $run.server
    $hostState = Invoke-ReportBridge $hostBridge '/state?sections=world'
    if ($hostState.state -ne 'server' -or $hostState.world.gameName -ne $run.gameName) { throw 'Wrong dedicated save.' }
    function Ui-Post([string]$path) {
        $reply = Invoke-RestMethod -Method Post -Uri "http://127.0.0.1:$($client.port)/ui/$path" -Headers @{'X-Bridge-Token'=$client.token} -TimeoutSec 15
        if ($reply.ok -ne $true) { throw 'The requested join control could not be operated.' }
    }
    $readyWindows = @('mainMenu','playGamePaging','serverBrowser','serverBrowserDirectConnect','loginWindow')
    $menuDeadline = (Get-Date).AddSeconds($TimeoutSeconds)
    do {
        try { $tree = Invoke-ReportBridge $client '/ui/tree' }
        catch {
            if ((Get-Date) -ge $menuDeadline -or !(Get-Owned $run.client)) { throw }
            $tree = $null
        }
        if (@($tree.openWindows | Where-Object { $_ -in $readyWindows }).Count) { break }
        if ((Get-Date) -ge $menuDeadline) { throw 'The game did not reach a menu where Direct Connect can be opened.' }
        Start-Sleep -Milliseconds 500
    } while ($true)
    if ('loginWindow' -in $tree.openWindows) { throw 'Sign into Steam and retry game login first.' }
    if ('mainMenu' -in $tree.openWindows -and 'playGamePaging' -notin $tree.openWindows) { Ui-Post 'click?id=btnPlayGame' }
    $tree = Invoke-ReportBridge $client '/ui/tree'
    if ('serverBrowser' -notin $tree.openWindows -and 'serverBrowserDirectConnect' -notin $tree.openWindows) {
        Ui-Post 'click?path=playGamePaging/playGamePaging/selector/rect/rect/tabButtons/serverBrowser'
    }
    $tree = Invoke-ReportBridge $client '/ui/tree'
    if ('serverBrowserDirectConnect' -notin $tree.openWindows) { Ui-Post 'click?id=btnDirectConnect' }
    Ui-Post 'type?id=txtIp&value=127.0.0.1'
    Ui-Post "type?id=txtPort&value=$($run.gamePort)"
    $beforeJoin = (Invoke-ReportBridge $client '/ping').lastLogSeq
    Ui-Post 'click?id=btnConnect'
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $retryAt = (Get-Date).AddSeconds(10)
    $retriedConnect = $false
    $submittedPassword = $false
    do {
        $tree = Invoke-ReportBridge $client '/ui/tree'
        $nativeStart = Invoke-ReportBridge $client "/log?since=$beforeJoin&contains=StartGame&limit=8"
        if ($nativeStart.entries.msg -match '(^|\s)StartGame\s*$') {
            if ($NativeSpawn) {
                # The actual clickable child receives the native press. The generic
                # auto-respawn path can repeatedly aim at its non-clickable parent.
                $button = if ($NativeSpawn -eq 'FirstEntry') { 'btnSpawnFirstTime' } else { 'btnSpawn' }
                $path = "spawnselection/spawnselection/buttons/content/$button/clickable"
                $spawnDeadline = (Get-Date).AddSeconds($TimeoutSeconds)
                $spawnSubmitted = $false
                $spawnAlreadyAdvancing = $false
                do {
                    $tree = Invoke-ReportBridge $client '/ui/tree'
                    if (!$spawnSubmitted -and !$spawnAlreadyAdvancing -and 'spawnselection' -in $tree.openWindows -and
                        @($tree.nodes | Where-Object { $_.path -ceq $path -and $_.clickable }).Count -eq 1) {
                        $xml = Invoke-ReportBridge $client "/log?since=$beforeJoin&contains=XML%20loader&limit=40"
                        if ($xml.entries.msg -match "Loading and parsing '(recipes|loot)' failed") {
                            throw 'Native recipes or loot failed to load before spawn.'
                        }
                        try {
                            Ui-Post ('click?path=' + [uri]::EscapeDataString($path))
                            Write-Output "Submitted the native $NativeSpawn spawn control."
                            $spawnSubmitted = $true
                        } catch {
                            $refusal = $_.ErrorDetails.Message | ConvertFrom-Json -ErrorAction SilentlyContinue
                            $status = [int]$_.Exception.Response.StatusCode
                            if ($status -eq 404 -and $refusal.ok -eq $false -and $refusal.error -ceq 'no visible UI node matches  selector') {
                                # Auto-respawn can close the native screen before this click resolves a target.
                                $tree = Invoke-ReportBridge $client '/ui/tree'
                                $freshState = (Invoke-ReportBridge $client '/ping').state
                                $spawnAlreadyAdvancing = 'spawnselection' -notin $tree.openWindows -and
                                    $freshState -in 'loading','spawning','ingame'
                                Write-Output ([ordered]@{
                                    event='NATIVE_SPAWN_RESOLVE_MISS'; spawn=$NativeSpawn; path=$path
                                    status=$status; error=$refusal.error; freshState=$freshState
                                    freshWindows=@($tree.openWindows); advancing=$spawnAlreadyAdvancing
                                } | ConvertTo-Json -Compress)
                                if (!$spawnAlreadyAdvancing) { throw }
                            } elseif ($status -ne 409 -or $refusal.ok -ne $false -or
                                      $refusal.error -cne 'Selected UI node changed before the click. Select it again.') {
                                throw
                            }
                        }
                    }
                    if ((Invoke-ReportBridge $client '/ping').state -eq 'ingame' -or
                        @($tree.openWindows | Where-Object { $_ -in 'rebirthChooseSurvivor','rebirthSurvivorCreator' }).Count) { return }
                    if ('messageBox' -in $tree.openWindows) { throw 'Native spawn opened a message box.' }
                    Start-Sleep -Milliseconds 400
                } while ((Get-Date) -lt $spawnDeadline)
                if ($spawnAlreadyAdvancing) { throw "Native $NativeSpawn spawn did not finish after the control disappeared." }
                if ($spawnSubmitted) { throw "Native $NativeSpawn spawn did not complete after submission." }
                throw "Native $NativeSpawn spawn control did not become available."
            }
            Write-Output 'Native authentication completed and the world is loading. Complete Survivor creation and first spawn before recording the roster.'
            return
        }
        if ('messageBox' -in $tree.openWindows) {
            throw 'Native Direct Connect opened a message box. Inspect it before retrying.'
        }
        $rejection = Invoke-ReportBridge $client "/log?since=$beforeJoin&contains=Reject%20cause%3A%20RateLimit&limit=1"
        if ($rejection.entries.Count) {
            throw 'Native Direct Connect was rate-limited. Let the connection attempt finish before retrying.'
        }
        if ('serverPassword' -in $tree.openWindows) {
            if ($submittedPassword) { throw 'Native Direct Connect requested the fixture password again after submission.' }
            [xml]$cfg = Get-Content -LiteralPath (Join-Path $RunRoot 'serverconfig.xml') -Raw
            $password = $cfg.SelectSingleNode("/ServerSettings/property[@name='ServerPassword']").GetAttribute('value')
            Ui-Post ('type?id=txtPassword&value=' + [uri]::EscapeDataString($password))
            # The rejected no-password attempt counts toward the native 500 ms per-IP limit.
            Start-Sleep -Milliseconds 550
            Ui-Post 'click?id=btnSubmit'
            $submittedPassword = $true
        } elseif (!$submittedPassword -and !$retriedConnect -and (Get-Date) -ge $retryAt -and
            'serverBrowserDirectConnect' -in $tree.openWindows) {
            Ui-Post 'click?id=btnConnect'
            $retriedConnect = $true
        }
        Start-Sleep -Milliseconds 250
    } while ((Get-Date) -lt $deadline)
    throw 'Native Direct Connect did not authenticate and start loading within the join timeout.'
}
if ($Mode -in @('StopClient','Stop')) {
    $sides = if ($Mode -eq 'StopClient') { @('client') } else { @('client','server') }
    foreach ($side in $sides) {
        $owned = Get-Owned $run.$side
        if (!$owned) { continue }
        if (!$run.$side.session) { throw "PID $($owned.Id) is owned by this run but has no ready Bridge. Stop this exact PID manually." }
        $s = Get-Content -LiteralPath $run.$side.session -Raw | ConvertFrom-Json
        if ($s.pid -ne $owned.Id -or ([datetime]$s.processStartUtc).ToUniversalTime().Ticks -ne $owned.StartTime.ToUniversalTime().Ticks) { throw 'Bridge identity mismatch.' }
        $null = Invoke-RestMethod -Method Post -Uri "http://127.0.0.1:$($s.port)/quit" -Headers @{'X-Bridge-Token'=$s.token} -TimeoutSec 8
        if (!$owned.WaitForExit(15000)) { throw "PID $($owned.Id) did not stop." }
    }
    return
}
if ((Get-FileHash -LiteralPath $dll).Hash -ne $run.dllHash) { throw 'Mod DLL changed since Prepare. Make a new run for this build.' }
$games = @(Get-Process -Name 7DaysToDie,7DaysToDieServer -ErrorAction SilentlyContinue)
$server = Get-Owned $run.server
if ($Mode -eq 'Start' -and $games.Count) { throw 'An existing game process is running.' }
if ($Mode -eq 'Client' -and (!$server -or @($games | Where-Object Id -ne $server.Id).Count)) { throw 'Client requires the isolated dedicated server and no other game process.' }
if ($Mode -eq 'Solo' -and @($games | Where-Object { !$server -or $_.Id -ne $server.Id }).Count) { throw 'Close the existing game client first.' }
$env:SteamAppId = '251570'
$env:SteamGameId = '251570'
if ($Mode -eq 'Start') {
    foreach ($port in @($run.gamePort,($run.gamePort+1),($run.gamePort+2),$run.serverBridgePort)) { Assert-PortFree $port }
    $p = Start-Process -FilePath $exe -WorkingDirectory $game -WindowStyle Hidden -PassThru -ArgumentList @(
        '-quit','-batchmode','-nographics',(Quote-Arg logfile (Join-Path $RunRoot 'server.log')),
        (Quote-Arg configfile (Join-Path $RunRoot 'serverconfig.xml')),"-rebirthbridge=$($run.serverBridgePort)",'-dedicated')
    $run.server = [pscustomobject]@{pid=$p.Id;started=$p.StartTime.ToUniversalTime().ToString('o');session=$null}
    Save-Run
    $run.server.session = Bridge-Ping $p $run.serverBridgePort 'server'
    Save-Run
    $save = Join-Path $RunRoot "server-data/Saves/$($run.world)/$($run.gameName)"
    [xml]$saved = Get-Content -LiteralPath (Join-Path $save 'RebirthSandboxOptions.xml') -Raw
    $savedCode = $saved.DocumentElement.GetAttribute('code')
    $savedOptions = for ($i=3; $i+2 -lt $savedCode.Length; $i+=3) { $savedCode.Substring($i,3) }
    if ($savedOptions -cnotcontains 'CTB') { throw 'The local server did not persist Purge. Stop the run and check the REBIRTH DLL and export.' }
} else {
    Assert-PortFree $run.clientBridgePort
    $dataLeaf = if ($FreshClientData) { 'client-cold-' + [Guid]::NewGuid().ToString('N').Substring(0,10) } else { 'client-data' }
    $dataPath = Join-Path $RunRoot $dataLeaf
    if ($FreshClientData -and (Test-Path -LiteralPath $dataPath)) { throw 'Fresh client data directory already exists.' }
    $clientArgs = @('-noeac',"-rebirthbridge=$($run.clientBridgePort)",'-SkipSpawnButton=false','-SkipNewsScreen=true','-skipintro',
        '-screen-fullscreen','0','-screen-width','1280','-screen-height','720',
        (Quote-Arg UserDataFolder $dataPath),(Quote-Arg logfile (Join-Path $RunRoot ($dataLeaf + '.log'))))
    if ($Mode -eq 'Solo') {
        $continueExisting = Test-Path -LiteralPath (Join-Path $dataPath "Saves/$($run.world)/$($run.gameName)/main.ttw")
        $clientArgs += @('-LoadSaveGame=true',(Quote-Arg GameWorld $run.world),(Quote-Arg GameName $run.gameName),'-GameMode=GameModeSurvival')
    }
    $p = Start-Process -FilePath $exe -WorkingDirectory $game -PassThru -ArgumentList $clientArgs
    $run.client = [pscustomobject]@{pid=$p.Id;started=$p.StartTime.ToUniversalTime().ToString('o');session=$null;dataPath=$dataPath;freshClientData=[bool]$FreshClientData}
    Save-Run
    $run.client.session = Bridge-Ping $p $run.clientBridgePort ''
}
Save-Run
if ($Mode -eq 'Solo' -and $continueExisting) { Wait-SoloSaveStart $run }
$run | ConvertTo-Json -Depth 8
