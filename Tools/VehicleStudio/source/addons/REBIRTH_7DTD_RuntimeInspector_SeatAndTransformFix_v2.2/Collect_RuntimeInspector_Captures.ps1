# Packages only structurally COMPLETE v2.2 inspector JSONs from the isolated dev mod.
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
    Write-Host "[1/3] Validating $($files.Count) capture JSON files (older inspector versions skipped)..."
    foreach($item in $files){
        if($item.Length -gt 32MB){Write-Warning "SKIP $($item.Name): >32MB";$skipped++;continue}
        try {
            $obj=Get-Content -LiteralPath $item.FullName -Raw | ConvertFrom-Json
            if($obj.schemaVersion -ne 1 -or $obj.captureType -notin @('snapshot','motion')){throw 'unexpected schema/capture type'}
            if($obj.serializer -ne 'RebirthCaptureFields/1'){throw 'missing nested-record serializer marker'}
            if($obj.inspectorVersion -ne '0.7.0-v2.2'){throw 'previous inspector version; requires v2.2 seat/transform diagnostics'}
            if($null -eq $obj.vehicle -or $null -eq $obj.xmlConfig){throw 'vehicle or XML reference missing'}
            $nodes=@($obj.hierarchy); $frames=@($obj.frames); $checks=@($obj.transformChecks)
            if($nodes.Count -lt 1 -or $frames.Count -lt 1){throw 'hierarchy/frames missing or empty'}
            if($obj.captureType -eq 'motion' -and $frames.Count -lt 2){throw 'motion recording has fewer than 2 frames'}
            if($null -eq $frames[0].vehiclePose -or $null -eq $frames[0].trackedNodes -or $null -eq $frames[0].occupantCandidates){throw 'first frame missing pose or tracked-node/occupant arrays'}
            if($obj.hierarchyNodeCount -ne $nodes.Count -or $obj.frameCount -ne $frames.Count -or $obj.transformCheckCount -ne $checks.Count){throw 'declared counts do not match content'}
            if($null -eq $nodes[0].pose -or $null -eq $nodes[0].components){throw 'hierarchy node is missing nested pose/components'}
            if($null -eq $obj.vehicle.entityId){throw 'vehicle record has no entity ID'}
            $requiredMissing=@($checks | Where-Object {$_.found -eq $false -and $_.classification -eq 'required'}).Count
            $conditionalMissing=@($checks | Where-Object {$_.found -eq $false -and $_.classification -eq 'conditional_upgrade'}).Count
            if($null -eq $obj.unresolvedRequiredTransformCount -or $obj.unresolvedRequiredTransformCount -ne $requiredMissing){throw 'required transform classification count mismatch'}
            if($null -eq $obj.unresolvedConditionalUpgradeTransformCount -or $obj.unresolvedConditionalUpgradeTransformCount -ne $conditionalMissing){throw 'conditional upgrade classification count mismatch'}
            if(@($checks | Where-Object {$_.classification -notin @('required','conditional_upgrade')}).Count -gt 0){throw 'unclassified transform reference'}
            $sha=(Get-FileHash -LiteralPath $item.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
            Copy-Item -LiteralPath $item.FullName -Destination (Join-Path $tmp $item.Name)
            $accepted.Add([pscustomobject]@{name=$item.Name;sha256=$sha;captureType=$obj.captureType;scenarioLabel=$obj.scenarioLabel;hierarchyNodes=$nodes.Count;frames=$frames.Count;boneSamples=$obj.occupantBoneSamples;knownSeatSamples=$obj.knownSeatSamples;requiredMissing=$requiredMissing;conditionalUpgradesUnresolved=$conditionalMissing})
            Write-Host "  PASS $($obj.scenarioLabel): nodes=$($nodes.Count) frames=$($frames.Count) bones=$($obj.occupantBoneSamples) confirmedSeat=$($obj.knownSeatSamples) requiredMissing=$requiredMissing optionalUpgradeUnresolved=$conditionalMissing"
            if($obj.knownSeatSamples -eq 0){Write-Host '    NOTE: seat still unverified. Use vinspect seatdiag while seated to locate game slot members.'}
        }catch{
            Write-Warning "SKIP $($item.Name): $($_.Exception.Message)"
            $skipped++
        }
    }
    if($accepted.Count -eq 0){throw 'No complete captures; the older files contain only metadata or the new export is still incomplete. Do not repeat 4 scenarios until serialization is verified.'}
    Write-Host "[2/3] Writing manifest for $($accepted.Count) validated captures..."
    [pscustomobject]@{collector='REBIRTH runtime captures v2.2';schemaVersion=1;validatedCaptureCount=$accepted.Count;skippedCount=$skipped;files=@($accepted.ToArray())} | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $tmp 'manifest.json') -Encoding UTF8
    Write-Host '[3/3] Creating validated report ZIP...'
    if(Test-Path -LiteralPath $dest){throw "Destination already exists: $dest"}
    Compress-Archive -Path (Join-Path $tmp '*') -DestinationPath $dest
    Write-Host "STATUS: CAPTURES_VALIDATED ($($accepted.Count) included, $skipped skipped)"
    Write-Host "Upload ZIP: $dest"
    Write-Host 'Reminder: only direct runtime seat-slot evidence can confirm seat numbers; hand IK fit still requires in-game testing.'
}finally{if(Test-Path -LiteralPath $tmp){Remove-Item -LiteralPath $tmp -Recurse -Force}}
