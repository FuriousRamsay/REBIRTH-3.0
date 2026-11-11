function Get-ReportBridge($receipt) {
    if (!$receipt -or !$receipt.session) { throw 'Bridge session receipt is missing.' }
    $s = Get-Content -LiteralPath $receipt.session -Raw | ConvertFrom-Json
    $p = Get-Process -Id $receipt.pid -ErrorAction SilentlyContinue
    if (!$p -or $p.ProcessName -ne '7DaysToDie' -or $s.pid -ne $receipt.pid -or
        $p.StartTime.ToUniversalTime().Ticks -ne ([datetime]$receipt.started).ToUniversalTime().Ticks -or
        $p.StartTime.ToUniversalTime().Ticks -ne ([datetime]$s.processStartUtc).ToUniversalTime().Ticks) { throw 'Stale Bridge session.' }
    $s
}
function Invoke-ReportBridge($session,[string]$path,[string]$command,[ValidateRange(1,15)][int]$TimeoutSeconds=15) {
    $arguments = @{Uri="http://127.0.0.1:$($session.port)$path";Headers=@{'X-Bridge-Token'=$session.token};TimeoutSec=$TimeoutSeconds}
    if ($command) { $arguments.Method='POST'; $arguments.ContentType='text/plain'; $arguments.Body=$command }
    $response = Invoke-RestMethod @arguments
    if ($response.ok -eq $false -or $response.hadError) { throw "Bridge refused $path." }
    $response
}
function Assert-ReportPurgeRules($session,$run,[int]$entityId) {
    $response = Invoke-ReportBridge $session '/console' "rbpurgetestobserve player $entityId"
    $lines = @($response.results | ForEach-Object { $_.output } | Where-Object { $_ -is [string] -and $_.StartsWith('{') })
    if ($lines.Count -ne 1) { throw 'Could not verify the current Purge rules on the authority.' }
    $purge = $lines[0] | ConvertFrom-Json
    if ($purge.purgeEnabled -ne $true -or $purge.player -ne $entityId -or $purge.saveName -ne $run.gameName) { throw 'This player is not in the Purge test world.' }
}
function Assert-ReportPurgePeer($session,$run,[int]$entityId) {
    $peer = (Invoke-ReportBridge $session "/auditstate?domain=topology&player=$entityId").domains.topology
    if (!$peer.dedicated -or !$peer.server -or !$peer.authoritativePeer -or $peer.remote -or $peer.gameName -ne $run.gameName -or
        !$peer.ownerKey -or !$peer.worldKey -or !$peer.creationId) { throw 'The player must finish first spawn on the isolated dedicated server.' }
    Assert-ReportPurgeRules $session $run $entityId
    return $peer
}
