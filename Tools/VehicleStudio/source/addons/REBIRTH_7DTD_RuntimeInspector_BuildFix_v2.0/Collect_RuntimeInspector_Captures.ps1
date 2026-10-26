# Only bundles inspector JSON captures from our isolated dev mod. No player logs or saves.
[CmdletBinding()]
param(
  [string]$ModletPath=(Join-Path $env:APPDATA '7DaysToDie\Mods\REBIRTH_VehicleInspector_Dev'),
  [string]$DestinationDirectory=(Join-Path $env:USERPROFILE 'Downloads')
)
$ErrorActionPreference='Stop'; Set-StrictMode -Version Latest
$folder=Join-Path $ModletPath 'Exports'
if(-not (Test-Path -LiteralPath $folder -PathType Container)) {throw "Export folder missing: $folder"}
$files=@(Get-ChildItem -LiteralPath $folder -File -Filter '*.json' | Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 32)
if($files.Count -lt 1) {throw 'No capture JSONs yet. Run vinspect snap/record in a local test world.'}
$batch=[guid]::NewGuid().ToString('N')
$dest=Join-Path $DestinationDirectory ('REBIRTH_7DTD_RuntimeCaptures_'+$batch+'.zip')
$tmp=Join-Path ([IO.Path]::GetTempPath()) ('rebirth_vinspect_'+$batch)
$null=New-Item -ItemType Directory -Force -Path $tmp
try {
    $accepted=New-Object 'System.Collections.Generic.List[object]'
    foreach($item in $files) {
        if($item.Length -gt 32MB) {Write-Warning "Skipping oversized capture: $($item.Name)";continue}
        try {$obj=Get-Content -LiteralPath $item.FullName -Raw | ConvertFrom-Json} catch {Write-Warning "Skipping invalid JSON: $($item.Name)";continue}
        if($obj.schemaVersion -ne 1 -or $obj.captureType -notin @('snapshot','motion')) {Write-Warning "Skipping unexpected capture type: $($item.Name)";continue}
        Copy-Item -LiteralPath $item.FullName -Destination (Join-Path $tmp $item.Name)
        $accepted.Add([pscustomobject]@{name=$item.Name;sha256=(Get-FileHash $item.FullName -Algorithm SHA256).Hash.ToLowerInvariant();captureType=$obj.captureType;scenarioLabel=$obj.scenarioLabel})
    }
    if($accepted.Count -eq 0){throw 'No valid capture files matched the expected v1 schema.'}
    [pscustomobject]@{collector='REBIRTH runtime captures';schemaVersion=1;capturedCount=$accepted.Count;files=@($accepted.ToArray())} | ConvertTo-Json -Depth 7 | Set-Content -LiteralPath (Join-Path $tmp 'manifest.json') -Encoding UTF8
    Compress-Archive -Path (Join-Path $tmp '*') -DestinationPath $dest
    Write-Host "Captured files: $($accepted.Count)"
    Write-Host "Upload ZIP: $dest"
} finally {if(Test-Path -LiteralPath $tmp){Remove-Item -LiteralPath $tmp -Recurse -Force}}
