param(
    [Parameter(Mandatory=$true)][ValidateRange(1,2147483647)][int]$Entity,
    [ValidateSet('gpu','cpu')][string]$Pose='gpu',
    [string]$OutputDirectory
)
$ErrorActionPreference='Stop'
if([string]::IsNullOrWhiteSpace($OutputDirectory)){$OutputDirectory=Join-Path $PSScriptRoot 'out/RuntimeFaceReviews'}
$bridge=Join-Path $PSScriptRoot '../GameBridge/gamebridge.ps1'
function Invoke-Studio([string]$Method,[string]$Route,[string[]]$Query) {
    $raw=& powershell -NoProfile -ExecutionPolicy Bypass -File $bridge call $Method $Route @Query
    if($LASTEXITCODE -ne 0){throw "Bridge request failed: $Method $Route"}
    $value=($raw -join "`n")|ConvertFrom-Json
    if($value.ok -ne $true){throw "Bridge returned failure: $Method $Route"}
    return $value
}
$folder=Join-Path $OutputDirectory ((Get-Date).ToUniversalTime().ToString('yyyyMMdd_HHmmss_fff')+'_entity'+$Entity)
New-Item -ItemType Directory -Path $folder -Force|Out-Null
$before=Invoke-Studio GET '/entitystudio/textures' @("entity=$Entity")
$before|ConvertTo-Json -Depth 50|Set-Content -LiteralPath (Join-Path $folder 'textures_before.json')
if($before.mipLeaseActive){throw 'This entity already has a capture lease; let it complete first.'}
$leased=$false
try {
    $ready=Invoke-Studio POST '/entitystudio/textureprepare' @("entity=$Entity");$leased=$true
    $deadline=[DateTime]::UtcNow.AddSeconds(15)
    while(-not $ready.requestedLevelsLoaded -and [DateTime]::UtcNow -lt $deadline) {
        Start-Sleep -Milliseconds 250
        $ready=Invoke-Studio GET '/entitystudio/textures' @("entity=$Entity")
    }
    $ready|ConvertTo-Json -Depth 50|Set-Content -LiteralPath (Join-Path $folder 'textures_prepared.json')
    if(-not $ready.mipLeaseActive -or -not $ready.requestedLevelsLoaded){throw 'Mip-zero request did not finish within its lease.'}
    if(@($ready.textures|Where-Object {$_.streaming -and ($_.requestedMipmapLevel -ne 0 -or $_.loadedMipmapLevel -ne 0)}).Count){throw 'A streaming texture did not reach mip zero.'}
    $render=Invoke-Studio POST '/entitystudio/render' @("entity=$Entity","pose=$Pose",'focus=face')
    $render|ConvertTo-Json -Depth 50|Set-Content -LiteralPath (Join-Path $folder 'render.json')
    if($render.focus -ne 'face' -or -not(Test-Path -LiteralPath $render.path)){throw 'Face render file or framing evidence missing.'}
    $after=Invoke-Studio GET '/entitystudio/textures' @("entity=$Entity")
    $after|ConvertTo-Json -Depth 50|Set-Content -LiteralPath (Join-Path $folder 'textures_after.json')
    if($after.mipLeaseActive){throw 'Face capture did not release its texture lease.'}
    foreach($prior in $before.textures) {
        $restored=@($after.textures|Where-Object instanceId -eq $prior.instanceId)
        if($restored.Count -ne 1 -or $restored[0].requestedMipmapLevel -ne $prior.requestedMipmapLevel){throw "Texture request was not restored: $($prior.name)"}
    }
    [pscustomobject]@{ok=$true;entity=$Entity;path=$render.path;evidence=$folder;focus=$render.focus;pose=$render.poseSource}|ConvertTo-Json -Compress
}
finally {
    if($leased){try{$null=Invoke-Studio POST '/entitystudio/textureprepare' @("entity=$Entity",'release=1')}catch{Write-Warning 'Explicit texture lease cleanup failed; bridge expiry remains the fallback.'}}
}
