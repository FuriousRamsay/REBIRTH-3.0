param(
    [Parameter(Mandatory)] [string] $LogPath
)

$lines = Get-Content -LiteralPath $LogPath -ErrorAction Stop
$discovery = @($lines -match ' INF \[MODS\]     Found ModAPI in assembly RebirthUtils(?: \(in .+\))?, creating instance$')
$completion = @($lines -match " INF \[MODS\]     Initialized code in mod 'zzz_REBIRTH__3_0' in assembly RebirthUtils(?: \(in .+\))?$")

if (-not ($lines -match ' INF \[MODS\] Loading done$')) { throw 'Mod loading did not finish.' }
if ($discovery.Count -ne 1 -or $completion.Count -ne 0) {
    throw "Expected one REBIRTH discovery and zero repeated completion lines. Found $($discovery.Count) and $($completion.Count)."
}

Write-Output 'PASS: one REBIRTH discovery message, zero completion lines, mod loading finished.'
