param([Parameter(Mandatory=$true)][string]$TargetRoot,[switch]$VerifyOnly)
$ErrorActionPreference='Stop'
$sourceRoot=Split-Path (Split-Path $PSScriptRoot)
$target=(Resolve-Path -LiteralPath $TargetRoot).Path
$manifest=Get-Content -LiteralPath "$PSScriptRoot/source-manifest.json" -Raw | ConvertFrom-Json
foreach($row in $manifest.files){
    $from=Join-Path $sourceRoot $row.path
    $to=Join-Path $target $row.path
    if((Get-FileHash -LiteralPath $from -Algorithm SHA256).Hash -ne $row.proposed){throw "source changed: $($row.path)"}
    if(!(Test-Path -LiteralPath $to)){throw "target missing: $($row.path)"}
    $hash=(Get-FileHash -LiteralPath $to -Algorithm SHA256).Hash
    if($hash -ne $row.baseline -and $hash -ne $row.proposed){throw "target diverged: $($row.path)"}
}
# Complete preflight precedes all copies. No DLL/build/game/save operation.
if(!$VerifyOnly){foreach($row in $manifest.files){Copy-Item -LiteralPath (Join-Path $sourceRoot $row.path) -Destination (Join-Path $target $row.path)}}
Write-Output "PASS all four source guards; verifyOnly=$VerifyOnly target=$target"
