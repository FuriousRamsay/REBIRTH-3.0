param([int]$ServerTimeoutSeconds = 300)

$ErrorActionPreference = 'Stop'
$gameRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')).Path
$exe = Join-Path $gameRoot '7DaysToDie.exe'
$config = (Resolve-Path (Join-Path $PSScriptRoot 'out\dedicated_codextest\serverconfig_shortpath.xml')).Path
$sessionRoot = Join-Path $PSScriptRoot 'out\sessions'
$serverLog = Join-Path $PSScriptRoot 'out\dedicated_codextest\server_startup.log'

if (-not (Test-Path -LiteralPath $exe)) { throw "Game executable missing: $exe" }
if ((Get-Process -Name 7DaysToDie,7DaysToDieServer -ErrorAction SilentlyContinue)) {
    throw 'A game instance is already running. Leave it untouched and wait until it closes.'
}
$settings = Get-Content -LiteralPath $config -Raw
if ($settings -notmatch 'name="GameName" value="CodexTest"' -or
    $settings -notmatch 'name="ServerPort" value="26905"' -or
    $settings -notmatch 'name="UserDataFolder" value="E:/_Haven/shared/CodexDedicatedTest_20261019"') {
    throw 'The isolated CodexTest server configuration is not the expected disposable target.'
}

# The quoted config path and final -dedicated flag match startdedicated.bat.
$env:SteamAppId = '251570'
$env:SteamGameId = '251570'
$serverArgs = @('-quit', '-batchmode', '-nographics',
    ('-logfile="' + $serverLog + '"'), ('-configfile="' + $config + '"'), '-rebirthbridge=8865', '-dedicated')
$server = Start-Process -FilePath $exe -ArgumentList $serverArgs -WorkingDirectory $gameRoot -PassThru -WindowStyle Hidden
$deadline = (Get-Date).AddSeconds($ServerTimeoutSeconds)
$serverSession = $null
while ((Get-Date) -lt $deadline) {
    $server.Refresh()
    if ($server.HasExited) { throw "Dedicated server exited before readiness (code $($server.ExitCode))." }
    $serverSession = Get-ChildItem -LiteralPath $sessionRoot -Filter session.json -Recurse -ErrorAction SilentlyContinue |
        Where-Object { $_.Directory.Name -like "$($server.Id)-*" -and $_.LastWriteTime -ge $server.StartTime } |
        Select-Object -First 1
    if ($serverSession) {
        $session = Get-Content -LiteralPath $serverSession.FullName -Raw | ConvertFrom-Json
        if ($session.pid -eq $server.Id) {
            try {
                $status = Invoke-RestMethod -Uri "http://127.0.0.1:$($session.port)/ping" -Headers @{'X-Bridge-Token' = $session.token} -TimeoutSec 3
                if ($status.state -eq 'server') { break }
            } catch { }
        }
    }
    Start-Sleep -Seconds 2
}
if (-not $status -or $status.state -ne 'server') {
    throw "Dedicated server did not reach Bridge state=server within $ServerTimeoutSeconds seconds; PID $($server.Id) remains available for diagnosis."
}

$client = Start-Process -FilePath $exe -ArgumentList @(
    '-noeac', '-rebirthbridge=8870', '-SkipSpawnButton=false', '-SkipNewsScreen=true', '-skipintro',
    '-screen-fullscreen', '0', '-screen-width', '1280', '-screen-height', '720') -WorkingDirectory $gameRoot -PassThru
[pscustomobject]@{
    serverPid = $server.Id
    clientPid = $client.Id
    serverSession = $serverSession.FullName
    serverPort = 26905
    serverState = $status.state
} | ConvertTo-Json -Compress
