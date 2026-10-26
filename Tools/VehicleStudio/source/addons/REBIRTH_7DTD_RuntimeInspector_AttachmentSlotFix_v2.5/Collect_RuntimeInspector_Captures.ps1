# Packages only structurally COMPLETE v2.5 inspector JSONs and seat diagnostic text files from the isolated dev mod.
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
# Seat diagnostics can be packaged even if no capture JSON exists yet.
$batch=[guid]::NewGuid().ToString('N')
$dest=Join-Path $DestinationDirectory ('REBIRTH_7DTD_RuntimeCaptures_'+$batch+'.zip')
$tmp=Join-Path ([IO.Path]::GetTempPath()) ('rebirth_vinspect_'+$batch)
$null=New-Item -ItemType Directory -Force -Path $tmp
$accepted=New-Object 'System.Collections.Generic.List[object]'
$skipped=0
$oldSkipped=0
$diagnostics=New-Object 'System.Collections.Generic.List[object]'
try {
    Write-Host "[1/4] Validating $($files.Count) capture JSON files (older inspector versions skipped)..."
    foreach($item in $files){
        if($item.Length -gt 32MB){Write-Warning "SKIP $($item.Name): >32MB";$skipped++;continue}
        try {
            $obj=Get-Content -LiteralPath $item.FullName -Raw | ConvertFrom-Json
            if($obj.schemaVersion -ne 1 -or $obj.captureType -notin @('snapshot','motion')){throw 'unexpected schema/capture type'}
            if($null -eq $obj.PSObject.Properties['serializer']){$oldSkipped++;continue}
            if($obj.serializer -ne 'RebirthCaptureFields/1'){throw 'missing nested-record serializer marker'}
            if($obj.inspectorVersion -ne '0.7.0-v2.5'){$oldSkipped++;continue}
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
            $accepted.Add([pscustomobject]@{name=$item.Name;sha256=$sha;captureType=$obj.captureType;scenarioLabel=$obj.scenarioLabel;hierarchyNodes=$nodes.Count;frames=$frames.Count;boneSamples=$obj.occupantBoneSamples;knownSeatSamples=$obj.knownSeatSamples;poseMatchedSeatSamples=@($frames | ForEach-Object {$_.occupantCandidates} | Where-Object {$_.poseMatchStatus -eq 'unique_pose_in_bound_xml_not_authoritative_slot'}).Count;requiredMissing=$requiredMissing;conditionalUpgradesUnresolved=$conditionalMissing})
            Write-Host "  PASS $($obj.scenarioLabel): nodes=$($nodes.Count) frames=$($frames.Count) bones=$($obj.occupantBoneSamples) verifiedSeatSamples=$($obj.knownSeatSamples) requiredMissing=$requiredMissing optionalUpgradeUnresolved=$conditionalMissing"
            if($obj.knownSeatSamples -eq 0){Write-Host '    NOTE: no authoritative seat slot. A unique pose-to-seat match, if reported, remains an INFERENCE.'}
        }catch{
            Write-Warning "SKIP $($item.Name): $($_.Exception.Message)"
            $skipped++
        }
    }
    Write-Host '[2/4] Collecting saved seat diagnostics from the owned development modlet...'
    $diagDir=Join-Path $folder 'Diagnostics'
    if(Test-Path -LiteralPath $diagDir -PathType Container){
        $diagFiles=@(Get-ChildItem -LiteralPath $diagDir -File -Filter 'seatdiag_*.txt' |
            Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 24)
        $diagOut=Join-Path $tmp 'Diagnostics'
        foreach($item in $diagFiles){
            try {
                if($item.Length -le 0 -or $item.Length -gt 256KB){throw 'empty or too large (>256KB)'}
                $headline=Get-Content -LiteralPath $item.FullName -TotalCount 1
                if($headline -ne 'REBIRTH Vehicle Inspector seat diagnostics v2.5'){throw 'missing v2.5 diagnostic signature'}
                if(-not (Test-Path -LiteralPath $diagOut)){ $null=New-Item -ItemType Directory -Path $diagOut }
                Copy-Item -LiteralPath $item.FullName -Destination (Join-Path $diagOut $item.Name)
                $sha=(Get-FileHash -LiteralPath $item.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
                $diagnostics.Add([pscustomobject]@{name=$item.Name;sha256=$sha;bytes=$item.Length})
                Write-Host "  PASS seat diagnostic: $($item.Name) ($($item.Length) bytes)"
            }catch{
                Write-Warning "SKIP diagnostic $($item.Name): $($_.Exception.Message)"
                $skipped++
            }
        }
    }
    if($oldSkipped -gt 0){Write-Host "  NOTE: $oldSkipped old-version capture(s) skipped; no need to delete them."}
    if($accepted.Count -eq 0 -and $diagnostics.Count -eq 0){
        throw 'No valid v2.5 captures or seat diagnostics found. Run vinspect seatdiag while near the 4x4, then collect again.'
    }
    Write-Host "[3/4] Writing manifest: $($accepted.Count) captures and $($diagnostics.Count) seat diagnostics..."
    [pscustomobject]@{collector='REBIRTH runtime captures v2.5';schemaVersion=1;validatedCaptureCount=$accepted.Count;validatedDiagnosticCount=$diagnostics.Count;skippedCount=$skipped;olderCapturesSkipped=$oldSkipped;files=@($accepted.ToArray());diagnostics=@($diagnostics.ToArray())} | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $tmp 'manifest.json') -Encoding UTF8
    Write-Host '[4/4] Creating validated report ZIP...'
    if(Test-Path -LiteralPath $dest){throw "Destination already exists: $dest"}
    Compress-Archive -Path (Join-Path $tmp '*') -DestinationPath $dest
    $status=if($accepted.Count -eq 0){'DIAGNOSTICS_ONLY_VALIDATED'}elseif($diagnostics.Count -eq 0){'CAPTURES_VALIDATED_NO_DIAGNOSTICS'}else{'CAPTURES_AND_DIAGNOSTICS_VALIDATED'}
    if($diagnostics.Count -eq 0){Write-Warning 'No v2.5 seat diagnostics found. Re-enter the game and run vinspect seatdiag or vinspect snap seated.'}
    Write-Host "STATUS: $status ($($accepted.Count) capture(s), $($diagnostics.Count) seat diagnostic(s), $skipped invalid skipped, $oldSkipped old skipped)"
    Write-Host "Upload ZIP: $dest"
    Write-Host 'Seatdiag output is stored in Diagnostics/*.txt INSIDE THIS ZIP. No game console copy needed.'
    Write-Host 'Reminder: only direct runtime seat-slot evidence can confirm seat numbers; hand IK fit still requires in-game testing.' 
}finally{if(Test-Path -LiteralPath $tmp){Remove-Item -LiteralPath $tmp -Recurse -Force}}
