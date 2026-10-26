# Packages only structurally COMPLETE v2.1 inspector JSONs from the isolated dev mod.
# It does not alter the game, save files, REBIRTH, or existing exports.
[CmdletBinding()]
param(
  [string]$ModletPath=(Join-Path $env:APPDATA '7DaysToDie\Mods\REBIRTH_VehicleInspector_Dev'),
  [string]$DestinationDirectory=(Join-Path $env:USERPROFILE 'Downloads')
)
$ErrorActionPreference='Stop'; Set-StrictMode -Version Latest
$folder=Join-Path $ModletPath 'Exports'
if(-not (Test-Path -LiteralPath $folder -PathType Container)){throw "Export folder missing: $folder"}
$files=@(Get-ChildItem -LiteralPath $folder -File -Filter '*.json' | Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 64)
if($files.Count -lt 1){throw 'No capture JSONs found. Run vinspect snap/record in a local test world.'}
$batch=[guid]::NewGuid().ToString('N')
$dest=Join-Path $DestinationDirectory ('REBIRTH_7DTD_RuntimeCaptures_'+$batch+'.zip')
$tmp=Join-Path ([IO.Path]::GetTempPath()) ('rebirth_vinspect_'+$batch)
$null=New-Item -ItemType Directory -Force -Path $tmp
$accepted=New-Object 'System.Collections.Generic.List[object]'
$skipped=0
try {
    Write-Host "[1/3] Validating $($files.Count) capture JSON files (metadata-only v2.0 files will be rejected)..."
    foreach($item in $files){
        if($item.Length -gt 32MB){Write-Warning "SKIP $($item.Name): >32MB";$skipped++;continue}
        try {
            $obj=Get-Content -LiteralPath $item.FullName -Raw | ConvertFrom-Json
            if($obj.schemaVersion -ne 1 -or $obj.captureType -notin @('snapshot','motion')){throw 'unexpected schema/capture type'}
            if($obj.serializer -ne 'RebirthCaptureFields/1'){throw 'missing v2.1 nested-record serializer marker (legacy metadata-only export)'}
            if($null -eq $obj.vehicle -or $null -eq $obj.xmlConfig){throw 'vehicle or XML reference missing'}
            $nodes=@($obj.hierarchy); $frames=@($obj.frames); $checks=@($obj.transformChecks)
            if($nodes.Count -lt 1 -or $frames.Count -lt 1){throw 'hierarchy/frames missing or empty'}
            if($obj.captureType -eq 'motion' -and $frames.Count -lt 2){throw 'motion recording has fewer than 2 frames'}
            if($null -eq $frames[0].vehiclePose -or $null -eq $frames[0].trackedNodes -or $null -eq $frames[0].occupantCandidates){throw 'first frame missing pose or tracked-node/occupant arrays'}
            if($obj.hierarchyNodeCount -ne $nodes.Count -or $obj.frameCount -ne $frames.Count -or $obj.transformCheckCount -ne $checks.Count){throw 'declared counts do not match content'}
            if($null -eq $nodes[0].pose -or $null -eq $nodes[0].components){throw 'hierarchy node is missing nested pose/components'}
            if($null -eq $obj.vehicle.entityId){throw 'vehicle record has no entity ID'}
            $sha=(Get-FileHash -LiteralPath $item.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
            Copy-Item -LiteralPath $item.FullName -Destination (Join-Path $tmp $item.Name)
            $accepted.Add([pscustomobject]@{name=$item.Name;sha256=$sha;captureType=$obj.captureType;scenarioLabel=$obj.scenarioLabel;hierarchyNodes=$nodes.Count;frames=$frames.Count;boneSamples=$obj.occupantBoneSamples;knownSeatSamples=$obj.knownSeatSamples})
            Write-Host "  PASS $($obj.scenarioLabel): nodes=$($nodes.Count) frames=$($frames.Count) bones=$($obj.occupantBoneSamples) verifiedSeatSamples=$($obj.knownSeatSamples)"
        }catch{
            Write-Warning "SKIP $($item.Name): $($_.Exception.Message)"
            $skipped++
        }
    }
    if($accepted.Count -eq 0){throw 'No complete captures; the older files contain only metadata or the new export is still incomplete. Do not repeat 4 scenarios until serialization is verified.'}
    Write-Host "[2/3] Writing manifest for $($accepted.Count) validated captures..."
    [pscustomobject]@{collector='REBIRTH runtime captures v2.1';schemaVersion=1;validatedCaptureCount=$accepted.Count;skippedCount=$skipped;files=@($accepted.ToArray())} | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $tmp 'manifest.json') -Encoding UTF8
    Write-Host '[3/3] Creating validated report ZIP...'
    if(Test-Path -LiteralPath $dest){throw "Destination already exists: $dest"}
    Compress-Archive -Path (Join-Path $tmp '*') -DestinationPath $dest
    Write-Host "STATUS: CAPTURES_VALIDATED ($($accepted.Count) included, $skipped skipped)"
    Write-Host "Upload ZIP: $dest"
    Write-Host 'Reminder: structural data does not establish driver attachment or correct IK; check knownSeatSamples/boneSamples.'
}finally{if(Test-Path -LiteralPath $tmp){Remove-Item -LiteralPath $tmp -Recurse -Force}}
