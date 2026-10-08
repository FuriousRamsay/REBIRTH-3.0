# Keeps the player out of the "Ready to spawn" / death screen: whenever the game has been in state dead/spawning for more than a
# few seconds, press the real respawn button (id btnNearBackpack, falling back to the first spawn button on the screen).
# Starts automatically with `gamebridge.ps1 launch` and ends when the game process does.
param([int]$Port = 8765, [int]$AfterSeconds = 6)
$gb = Join-Path $PSScriptRoot 'gamebridge.ps1'
$since = $null
while ($true) {
    if (-not (Get-Process 7DaysToDie -ErrorAction SilentlyContinue)) { Start-Sleep 5; if (-not (Get-Process 7DaysToDie -ErrorAction SilentlyContinue)) { break } }
    $s = $null
    try { $s = (& powershell -ExecutionPolicy Bypass -File $gb status 2>$null | Out-String | ConvertFrom-Json).state } catch { }
    if ($s -eq 'dead' -or $s -eq 'spawning') {
        if (-not $since) { $since = Get-Date }
        if (((Get-Date) - $since).TotalSeconds -gt $AfterSeconds) {
            $out = & powershell -ExecutionPolicy Bypass -File $gb click id=btnNearBackpack 2>&1 | Out-String
            if ($out -notmatch '"clicked"') { $out = & powershell -ExecutionPolicy Bypass -File $gb click "text=Spawn near" 2>&1 | Out-String }
            if ($out -notmatch '"clicked"') { & powershell -ExecutionPolicy Bypass -File $gb click "text=Spawn" 2>&1 | Out-Null }
            $since = Get-Date
        }
    } else { $since = $null }
    Start-Sleep 4
}
