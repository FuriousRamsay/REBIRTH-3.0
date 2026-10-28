[CmdletBinding(SupportsShouldProcess)]
param([string]$PolicyPath='')
$ErrorActionPreference='Stop'
if([string]::IsNullOrWhiteSpace($PolicyPath)){$PolicyPath=Join-Path $PSScriptRoot 'BuildProfiles/BuggyBike.json'}
$policy=Get-Content -LiteralPath $PolicyPath -Raw | ConvertFrom-Json
$modRoot=Split-Path (Split-Path $PSScriptRoot)
if(Get-CimInstance Win32_Process -Filter "name='7DaysToDie.exe' OR name='7DaysToDieServer.exe'"){throw 'Close the game/server before installing bundles.'}
$run=(Get-Content -LiteralPath (Join-Path $policy.output 'latest-verified-run.txt') -Raw).Trim()
$receipt=Get-Content -LiteralPath (Join-Path $run 'verified.txt') -Raw
if($receipt -notmatch 'PASS no-op rebuild same hashes'){throw 'Missing successful verified build receipt'}
$plan=@(foreach($vehicle in $policy.vehicles){
    if([IO.Path]::GetFileName($vehicle.bundle) -ne $vehicle.bundle){throw 'Bundle name must be a filename'}
    $source=Join-Path (Join-Path $policy.output 'Bundles') $vehicle.bundle
    $match=[regex]::Match($receipt,[regex]::Escape($vehicle.name)+' bytes=(\d+) SHA256=([A-F0-9]{64})')
    if(!$match.Success){throw ('Receipt lacks bundle hash: '+$vehicle.name)}
    if((Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash -ne $match.Groups[2].Value){throw ('Built bundle differs from receipt: '+$source)}
    [pscustomobject]@{source=$source;destination=(Join-Path (Join-Path $modRoot 'Resources') $vehicle.bundle);sha256=$match.Groups[2].Value}
})
if($PSCmdlet.ShouldProcess(($plan.destination -join ', '),'Back up and install verified vehicle bundles')){
    $backup=Join-Path $policy.output ('InstallBackups/'+(Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
    New-Item -ItemType Directory -Path $backup -Force | Out-Null
    foreach($entry in $plan){if(Test-Path -LiteralPath $entry.destination){Copy-Item -LiteralPath $entry.destination -Destination $backup}}
    foreach($entry in $plan){
        if(Get-CimInstance Win32_Process -Filter "name='7DaysToDie.exe' OR name='7DaysToDieServer.exe'"){throw 'Game/server started during installation; aborting.'}
        Copy-Item -LiteralPath $entry.source -Destination $entry.destination
        if((Get-FileHash -LiteralPath $entry.destination -Algorithm SHA256).Hash -ne $entry.sha256){throw ('Installed hash mismatch: '+$entry.destination)}
    }
    $plan | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $backup 'installed.json')
    Write-Output ('Verified bundles installed; backup: '+$backup)
    Write-Output 'XML/DLL integration and runtime acceptance remain separate gates. This command never launches the game.'
}
