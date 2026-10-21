param(
    [Parameter(Mandatory=$true)][ValidateSet('capture','verify')][string]$Phase,
    [int]$Entity=718,
    [switch]$IdleContents
)
$ErrorActionPreference='Stop'
$bridge=Join-Path $PSScriptRoot 'gamebridge.ps1'
$baseline=Join-Path $PSScriptRoot 'out/camper_persistence_baseline.json'
if($IdleContents){$baseline=Join-Path $PSScriptRoot 'out/camper_idle_contents_baseline.json'}
function Read-Bridge([string[]]$Arguments) {
    $lines=& powershell -NoProfile -ExecutionPolicy Bypass -File $bridge @Arguments
    if($LASTEXITCODE -ne 0){throw 'Bridge observation failed'}
    $reply=($lines -join "`n") | ConvertFrom-Json
    if($reply.ok -ne $true){throw 'Bridge observation was not acknowledged'}
    return $reply
}
$state=Read-Bridge @('state','sections=world')
if($state.world.gameName -ne 'CodexTest'){throw 'Disposable CodexTest required'}
$current=Read-Bridge @('camper',"entity=$Entity")
if($current.pending -ne $false -or $current.capacity -ne $current.cargoSlots){throw 'Capture/verification requires settled custody and matching cargo dimensions'}
if($IdleContents) {
    if($current.inventorySchema -ne 1){throw 'Install the detailed inventory observation build first'}
    if(@($current.stations | Where-Object {$_.burning -or @($_.queue).Count -gt 0}).Count -gt 0){throw 'Idle contents comparison requires all stations stopped with empty queues'}
}
if($Phase -eq 'capture') {
    if($current.stationCount -lt 1){throw 'Install at least one station before capturing persistence'}
    $record=[ordered]@{version=2;idleContents=[bool]$IdleContents;entity=$Entity;vehicleIdentity=$current.vehicleIdentity;cargo=@($current.cargo);gameName=$state.world.gameName;capturedUtc=[DateTime]::UtcNow.ToString('o');capacity=$current.capacity;storage=@($current.storage);stations=@($current.stations)}
    $record | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $baseline -Encoding UTF8
    [ordered]@{ok=$true;phase=$Phase;stationCount=$current.stationCount;baseline=$baseline} | ConvertTo-Json -Compress
    exit 0
}
$saved=Get-Content -LiteralPath $baseline -Raw | ConvertFrom-Json
if($saved.version -ne 2 -or $saved.entity -ne $Entity -or $saved.gameName -ne 'CodexTest' -or [bool]$saved.idleContents -ne [bool]$IdleContents){throw 'Baseline scope differs; capture with the intended comparison mode'}
if($IdleContents) {
    if($saved.vehicleIdentity -ne $current.vehicleIdentity){throw 'Vehicle identity changed'}
    if(($saved.cargo | ConvertTo-Json -Depth 12 -Compress) -cne ($current.cargo | ConvertTo-Json -Depth 12 -Compress)){throw 'Cargo slots, counts or native item values changed'}
}
if(@($saved.stations).Count -ne $current.stationCount -or @($saved.storage).Count -ne $current.storageCount -or $saved.capacity -ne $current.capacity){throw 'Installed counts/capacity changed across reload'}
foreach($expected in $saved.stations) {
    $found=@($current.stations | Where-Object {$_.slot -eq $expected.slot -and $_.id -eq $expected.id})
    if($found.Count -ne 1){throw "Station identity changed in slot $($expected.slot)"}
    if($IdleContents) {
        foreach($field in @('tools','fuel','input','output')) {
            if(($expected.$field | ConvertTo-Json -Depth 12 -Compress) -cne ($found[0].$field | ConvertTo-Json -Depth 12 -Compress)){throw "Station $($expected.slot) $field contents changed"}
        }
        if([Math]::Abs($expected.fuelSeconds-$found[0].fuelSeconds) -gt 0.01){throw "Idle station $($expected.slot) fuel time changed"}
    }
    if($expected.fuelSeconds -gt 0 -and ($found[0].fuelSeconds -le 0 -or $found[0].fuelSeconds -gt $expected.fuelSeconds+1)){throw "Fuel disappeared or increased in slot $($expected.slot)"}
    # Queue progression/output custody needs its own gameplay assertions. This
    # helper verifies installation identity and retained fuel, not completion.
}
foreach($expected in $saved.storage) {
    if(@($current.storage | Where-Object {$_.slot -eq $expected.slot -and $_.item -eq $expected.item}).Count -ne 1){throw "Container changed in slot $($expected.slot)"}
}
[ordered]@{ok=$true;phase=$Phase;stationCount=$current.stationCount;storageCount=$current.storageCount;pending=$current.pending;stations=@($current.stations)} | ConvertTo-Json -Depth 8 -Compress
