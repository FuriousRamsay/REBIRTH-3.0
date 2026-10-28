param([ValidateRange(8,120)][int]$Seconds=20,[string]$ArgsFile,[switch]$AuditWindows,[switch]$GraphicsTiming,[switch]$AuditLightIndex,[switch]$SampleTimeline,[switch]$AuditCollisions,[switch]$RepeatCamp,[switch]$KeepOnFailure,[switch]$ReuseProfiler,[switch]$AuditPoiLookup,[switch]$PrepareOnly,[switch]$PrepareWorldOnly,[switch]$FunctionsOnly)

$ErrorActionPreference='Stop'
$profilerRoot=$PSScriptRoot
$modRoot=Split-Path (Split-Path $profilerRoot -Parent) -Parent
$out=Join-Path $profilerRoot 'out'
$bridge=Join-Path $modRoot 'Tools/GameBridge/gamebridge.ps1'
$launchArgsFile=if($ArgsFile){(Resolve-Path -LiteralPath $ArgsFile).Path}else{Join-Path $out 'capt_bridge.args'}
$exe=Join-Path $profilerRoot 'bin/RebirthProfiler.exe'
$profiler=$null
$game=$null
$session=$null
$runFailed=$false
$prepared=$false
$previousTimeline=$env:PROFILER_TIMELINE

function Bridge([string[]]$arguments)
{
    $sessionPath=Join-Path $session.outputDir 'session.json'
    $raw=& powershell -ExecutionPolicy Bypass -File $bridge @arguments -SessionPath $sessionPath
    if($LASTEXITCODE -ne 0){throw "Game Bridge failed: $($arguments -join ' '); response: $($raw -join ' ')"}
    $result=$raw|ConvertFrom-Json
    if(!$result.ok){throw "Game Bridge refused: $($arguments -join ' '); response: $($raw -join ' ')"}
    return $result
}

function WaitForWorld
{
    for($i=0;$i -lt 36;$i++)
    {
        if($game.HasExited)
        {
            $launchLog=Join-Path $out 'capt_launch.log'
            $failure=if(Test-Path -LiteralPath $launchLog){
                Get-Content -LiteralPath $launchLog -Tail 100 |
                    Where-Object {$_ -match ' ERR |Exception:|Access to the path'} |
                    Select-Object -Last 1
            }
            throw "Capt game exited before world load (code $($game.ExitCode)): $failure"
        }
        Start-Sleep -Seconds 5
        if($game.HasExited){continue}
        $pointer=Join-Path $modRoot 'Tools/GameBridge/out/session.json'
        if(!(Test-Path -LiteralPath $pointer)){continue}
        $candidate=Get-Content -LiteralPath $pointer -Raw|ConvertFrom-Json
        if($candidate.pid -ne $game.Id){continue}
        $script:session=$candidate
        $status=Bridge @('status')
        if($status.state -eq 'ingame'){return}
    }
    SaveStartupHangEvidence
    throw 'Capt did not reach the world within three minutes.'
}

function SaveStartupHangEvidence
{
    # Preserve evidence before existing finally cleanup ends the stalled game.
    # Never run on a successful launch, and never block cleanup indefinitely.
    $collector=$null
    try
    {
        if(!$game -or $game.HasExited){return}
        $folder=Join-Path $out ('startup_hang_'+$game.Id+'_'+(Get-Date -Format 'yyyyMMdd_HHmmss'))
        New-Item -ItemType Directory -Path $folder -ErrorAction Stop|Out-Null
        $helper=Join-Path $profilerRoot 'capture-startup-hang.ps1'
        if(!(Test-Path -LiteralPath $helper)){throw 'Startup snapshot helper is missing.'}
        $hostExe=(Get-Process -Id $PID).Path
        $arguments='-NoProfile -ExecutionPolicy Bypass -File "'+$helper+'" -GameProcessId '+$game.Id+' -ExpectedExecutable "'+$game.Path+'" -OutputDirectory "'+$folder+'"'
        $collector=Start-Process -FilePath $hostExe -ArgumentList $arguments -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $folder 'collector.stdout.txt') -RedirectStandardError (Join-Path $folder 'collector.stderr.txt')
        if(!$collector.WaitForExit(20000))
        {
            # This handle belongs only to the diagnostic child started above.
            $collector.Kill()
            'Collector exceeded20seconds; partial evidence retained.'|Set-Content -LiteralPath (Join-Path $folder 'collector-timeout.txt')
        }
        Write-Output ('Startup hang evidence: '+$folder)
    }
    catch {Write-Warning ('Startup hang evidence collection failed: '+$_.Exception.Message)}
    finally {if($collector){$collector.Dispose()}}
}

function EnergyMode
{
    return (Bridge @('energy','entity=210')).mode
}

function WaitForMode([string]$expected)
{
    for($i=0;$i -lt 20;$i++)
    {
        if((EnergyMode) -eq $expected){return}
        Start-Sleep -Seconds 2
    }
    throw "Camper did not enter $expected mode within 40 seconds."
}

function OpenEnergy
{
    $window='rebirthCamperEnergyManagement'
    if((Bridge @('uitree')).openWindows.Contains($window)){return}
    # Crosshair detection extends farther than native activation. A saved
    # camera facing the camper is not proof that the player can interact.
    [void](Bridge @('lookat','entity=210'))
    $aim=Bridge @('target')
    # lookat already verifies the native hit root resolves to entity 210.
    # The descriptive target JSON can omit entity for a child collider; it
    # must not override that stronger native identity proof.
    if($aim.target.distance -gt 2.5) {
        $hit=$aim.target.point;$pos=$aim.position
        $dx=$pos[0]-$hit[0];$dz=$pos[2]-$hit[2];$length=[Math]::Sqrt($dx*$dx+$dz*$dz)
        if($length -le 0.01){throw 'Cannot approach camper from this vertical target.'}
        $x=([double]($hit[0]+$dx/$length*1.75)).ToString('R',[Globalization.CultureInfo]::InvariantCulture)
        $z=([double]($hit[2]+$dz/$length*1.75)).ToString('R',[Globalization.CultureInfo]::InvariantCulture)
        [void](Bridge @('walkto',('x='+$x),('z='+$z)))
    }
    try { [void](Bridge @('radial','select',('command='+$window),'entity=210',('expectWindow='+$window))) }
    catch {
        $failure=$_.Exception.Message
        $diagnostic=[ordered]@{error=$failure;utc=[DateTime]::UtcNow.ToString('o')}
        foreach($probe in @('target','radial','guard','uitree')) {
            try {$diagnostic[$probe]=Bridge @($probe)}
            catch {$diagnostic[$probe]=@{error=$_.Exception.Message}}
        }
        $diagnostic|ConvertTo-Json -Depth 12|Set-Content -LiteralPath (Join-Path $out ('capt_radial_failure_'+(Get-Date -Format 'yyyyMMdd_HHmmss')+'.json'))
        # A target can leave the crosshair during the held Activate gesture.
        # Retry that refused gesture once; never bypass the native target proof.
        if($failure -notmatch 'Radial selection aborted: owned action item proof refused'){throw}
        if((Bridge @('uitree')).openWindows.Contains($window)){return}
        [void](Bridge @('radial','select',('command='+$window),'entity=210',('expectWindow='+$window)))
    }
}

function Capture([string]$label,[string]$profile)
{
    New-Item -ItemType File -Path ($profile+'.reset') -Force|Out-Null
    Start-Sleep -Seconds 5
    $phaseStartUtc=[DateTime]::UtcNow
    [void](Bridge @('call','POST','/frames','action=start',('graphics='+[int][bool]$GraphicsTiming)))
    Start-Sleep -Seconds $Seconds
    $phaseEndUtc=[DateTime]::UtcNow
    $frames=Bridge @('call','POST','/frames','action=stop')
    # The periodic report may predate a late hitch by several seconds. Request
    # a completed snapshot covering the entire measured phase before copying it.
    New-Item -ItemType File -Path ($profile+'.dump') -Force|Out-Null
    $dumpDeadline=[DateTime]::UtcNow.AddSeconds(10)
    while((Get-Item -LiteralPath $profile).LastWriteTimeUtc -lt $phaseEndUtc){
        if([DateTime]::UtcNow -ge $dumpDeadline){throw 'Profiler did not publish the end-of-phase snapshot.'}
        Start-Sleep -Milliseconds 100
    }
    $target=Join-Path $out ("capt_"+$label+"_"+(Get-Date -Format 'yyyyMMdd_HHmmss')+'.json')
    Copy-Item -LiteralPath $profile -Destination $target -Force
    $sample=Get-Content -LiteralPath $target -Raw|ConvertFrom-Json
    New-Item -ItemType File -Path (Join-Path $out 'metrics.request') -Force|Out-Null
    Start-Sleep -Seconds 2
    $metrics=Get-Content -LiteralPath (Join-Path $out 'metrics.json') -Raw|ConvertFrom-Json
    $startTick=$phaseStartUtc.ToFileTimeUtc();$endTick=$phaseEndUtc.ToFileTimeUtc()
    $presentGaps=New-Object System.Collections.Generic.List[object]
    $previousTick=0L
    foreach($tickValue in $metrics.presentTicks)
    {
        $tick=[long]$tickValue
        if($tick -lt $startTick -or $tick -gt $endTick){continue}
        if($previousTick -ne 0L -and $tick-$previousTick -gt 10000L)
        { $presentGaps.Add([pscustomobject]@{ms=($tick-$previousTick)/10000.0;utc=[DateTime]::FromFileTimeUtc($tick).ToString('o')}) }
        $previousTick=$tick
    }
    $dxgiWorstFrames=@($presentGaps|Sort-Object ms -Descending|Select-Object -First 8)
    $gameState=Bridge @('state','-Sections','performance')
    # Outside the measured interval: distinguish an idle camp from actual powered loads.
    $energyState=Bridge @('energy','entity=210')
    $collisionAudit=if($AuditCollisions){Bridge @('energy','entity=210','auditCollisions=1')}else{$null}
    $measurement=[pscustomobject]@{
        label=$label; profile=$target; wallMs=$sample.wallMs; samples=$sample.total
        energyMode=$energyState.mode; energyDemandWatts=$energyState.demandWatts
        energySuppliedWatts=$energyState.suppliedWatts; energyAvailableWatts=$energyState.availableWatts
        poiRequestedOn=$energyState.poi.requestedOn; poiDemandWatts=$energyState.poi.activeMeteredDemand
        externalRelayLinks=$energyState.externalRelayLinkCount
        energySources=@($energyState.sources|Select-Object kind,enabled,status,availableWatts)
        fpsAvailable=$metrics.hasFps; fps=if($metrics.hasFps){$metrics.fps}else{$null}
        gameFps=$gameState.performance.fps
        gameWidth=$gameState.performance.width; gameHeight=$gameState.performance.height
        gameFrames=$frames.frames; gameFrameCaptureFull=$frames.full
        gameAvgMs=$frames.avgMs; gameP95Ms=$frames.p95Ms; gameP99Ms=$frames.p99Ms
        gameWorstMs=$frames.worstMs; gameOver33Ms=$frames.over33Ms
        gameOver50Ms=$frames.over50Ms; gameOver100Ms=$frames.over100Ms
        gameWorstFrames=$frames.worstFrames
        dxgiPresentGaps=$presentGaps.Count; dxgiWorstFrames=$dxgiWorstFrames
        energyRefreshCount=$frames.energyRefreshCount; energyRefreshTop=$frames.energyRefreshTop
        uiUpdates=$frames.uiUpdates
        graphicsTimingEnabled=$frames.graphicsTimingEnabled; graphicsFrames=$frames.graphicsFrames
        cpuMainMsLatestAvg=$frames.cpuMainMsLatestAvg; cpuRenderMsLatestAvg=$frames.cpuRenderMsLatestAvg
        gpuFrameMsLatestAvg=$frames.gpuFrameMsLatestAvg
        uiPanelCount=$frames.uiPanelCount; uiWidgetCount=$frames.uiWidgetCount
        uiVisibleWidgetCount=$frames.uiVisibleWidgetCount; uiDrawCallCount=$frames.uiDrawCallCount
        uiPanels=$frames.uiPanels
        camperActiveRendererCount=$frames.camperActiveRendererCount
        camperActiveMaterialSlots=$frames.camperActiveMaterialSlots
        camperRendererGroups=$frames.camperRendererGroups
        frameError=$metrics.frameError; eventsSeen=$metrics.eventsSeen; gameVramMb=$metrics.gpuVramMb
        collisionAudit=$collisionAudit
        gameVramAvg12sMb=$metrics.gpuVramAvg12sMb; gameVramPeak12sMb=$metrics.gpuVramPeak12sMb
        gpu3dAvg12s=$metrics.gpu3dAvg12s; gpu3dP95_12s=$metrics.gpu3dP95_12s
        processCoresAvg12s=$metrics.processCoresAvg12s; osSamples12s=$metrics.osSamples12s
    }
    $measurement|ConvertTo-Json -Depth 5|Set-Content -LiteralPath ($target+'.metrics.json')
    return $measurement
}

if($FunctionsOnly){return}
if($PrepareOnly -and !$ReuseProfiler){throw 'PrepareOnly requires the existing profiler.'}
if($PrepareWorldOnly -and !$PrepareOnly){throw 'PrepareWorldOnly requires PrepareOnly.'}
if(Get-Process -Name 7DaysToDie -ErrorAction SilentlyContinue)
{throw 'A game is already running. Reuse that bridge session first.'}
$existingProfilers=@(Get-Process -Name RebirthProfiler -ErrorAction SilentlyContinue)
if($ReuseProfiler) {
    if($existingProfilers.Count -ne 1){throw 'Reuse requires exactly one existing profiler.'}
    if($ArgsFile -or $SampleTimeline){throw 'Reuse keeps the open profiler settings; launch arguments and timeline must already be configured there.'}
    $profiler=$existingProfilers[0]
    if([System.IO.Path]::GetFullPath($profiler.Path) -ne [System.IO.Path]::GetFullPath($exe)){throw 'The open profiler belongs to another build directory.'}
} elseif($existingProfilers.Count -ne 0) {throw 'A profiler is already running. Use -ReuseProfiler.'}
if(!(Test-Path -LiteralPath $exe) -or !(Test-Path -LiteralPath $launchArgsFile))
{throw 'Build the profiler and prepare capt_bridge.args before measuring.'}

try
{
    $env:PROFILER_TIMELINE=if($SampleTimeline){'1'}else{'0'}
    $launched=Get-Date
    $quotedArgs='--launch --keep-save "--gameargsfile='+$launchArgsFile+'"'
    if($ReuseProfiler) {
        @{argsFile=$launchArgsFile;freshSave=$false;playMode=$false} |
            ConvertTo-Json -Compress |
            Set-Content -LiteralPath (Join-Path $out 'launch.request')
    } else {
        $profiler=Start-Process -FilePath $exe -ArgumentList $quotedArgs -WindowStyle Hidden -PassThru
    }
    for($i=0;$i -lt 30;$i++)
    {
        Start-Sleep -Seconds 1
        $game=Get-Process -Name 7DaysToDie -ErrorAction SilentlyContinue|Where-Object {$_.StartTime -ge $launched}|Select-Object -First 1
        if($game){break}
        if($profiler.HasExited){throw 'The profiler exited before launching the game.'}
    }
    if(!$game){throw 'The profiler did not launch the game.'}
    WaitForWorld
    if($PrepareWorldOnly){$prepared=$true;Write-Output 'Capt world ready for bridge inspection; existing game and profiler retained.';return}
    if($AuditPoiLookup)
    {
        $audit=Bridge @('energy','entity=210','auditPoiLookup=1')
        $audit | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $out ('capt_poi_lookup_audit_'+(Get-Date -Format 'yyyyMMdd_HHmmss')+'.json'))
        if(!$audit.passed){throw 'Spatial POI lookup differs from original full traversal.'}
    }
    if($AuditLightIndex)
    {
        $audit=Bridge @('energy','entity=210','auditLightIndex=1')
        $audit | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $out ('capt_light_index_audit_'+(Get-Date -Format 'yyyyMMdd_HHmmss')+'.json'))
        if(!$audit.passed){throw 'Sparse light index differs from native prefab enumeration.'}
    }
    $profile=(Get-ChildItem -LiteralPath $out -Filter 'profile_*.json'|Where-Object {$_.LastWriteTime -ge $launched}|Sort-Object LastWriteTime -Descending|Select-Object -First 1).FullName
    if(!$profile){throw 'No native profiler sample appeared.'}
    if((EnergyMode) -ne 'Camp')
    {
        OpenEnergy
        [void](Bridge @('click','text=Enter Camp Mode'))
        [void](Bridge @('key','name=Escape'))
    }
    WaitForMode 'Camp'
    if($PrepareOnly){OpenEnergy;$prepared=$true;Write-Output 'Capt ready for bridge inspection; existing game and profiler retained.';return}
    $results=@()
    $results+=Capture 'camp_closed' $profile
    OpenEnergy
    $results+=Capture 'camp_energy_open' $profile
    if($AuditWindows)
    {
        [void](Bridge @('ui','close','rebirthCamperEnergyManagement'))
        [void](Bridge @('key','name=Tab'))
        $crafting=Bridge @('uitree')
        $craftingWindow=@($crafting.openWindows|Where-Object {$_ -match '(?i)craft'}|Select-Object -First 1)
        if($craftingWindow.Count -eq 0)
        {throw 'Crafting window did not open; refusing to mislabel the profile.'}
        $results+=Capture 'camp_crafting_open' $profile
        [void](Bridge @('click','id=btnRebirthCraftingTabJournal'))
        if(!(Bridge @('uitree')).openWindows.Contains('rebirthJournal'))
        {throw 'Journal tab did not open; refusing to mislabel the profile.'}
        $results+=Capture 'camp_journal_open' $profile
        [void](Bridge @('click','id=journalCloseHit'))
        # Some layouts leave Journal open after the native close-button click.
        # Close that exact group before the next capture; never mislabel a modal.
        if((Bridge @('uitree')).openWindows.Contains('rebirthJournal'))
        {[void](Bridge @('ui','close','rebirthJournal'))}
        [void](Bridge @('ui','close',$craftingWindow[0]))
        [void](Bridge @('radial','select','command=rebirthCamperStorageManagement','entity=210','expectWindow=rebirthCamperStorageManagement'))
        $results+=Capture 'camp_storage_open' $profile
        [void](Bridge @('ui','close','rebirthCamperStorageManagement'))
        OpenEnergy
    }
    if(!(Bridge @('uitree')).openWindows.Contains('rebirthCamperEnergyManagement')){OpenEnergy}
    [void](Bridge @('click','text=Enter Travel Mode'))
    [void](Bridge @('key','name=Escape'))
    WaitForMode 'Travel'
    $results+=Capture 'travel_closed' $profile
    if($RepeatCamp)
    {
        OpenEnergy
        [void](Bridge @('click','text=Enter Camp Mode'))
        [void](Bridge @('key','name=Escape'))
        WaitForMode 'Camp'
        $results+=Capture 'camp_return_closed' $profile
    }
    $summary=Join-Path $out ('capt_comparison_'+(Get-Date -Format 'yyyyMMdd_HHmmss')+'.json')
    $results|ConvertTo-Json -Depth 5|Set-Content -LiteralPath $summary
    $results|Format-Table label,wallMs,samples,gameFps,gameP99Ms,gameWorstMs,gameOver50Ms,gameVramMb -AutoSize
    Write-Output "Saved comparison: $summary"
}
catch { $runFailed=$true; throw }
finally
{
    $env:PROFILER_TIMELINE=$previousTimeline
    if(($prepared -or ($runFailed -and $KeepOnFailure)) -and $game -and !$game.HasExited -and $session -and $session.pid -eq $game.Id) {
        Write-Warning ('Diagnostic session retained: game PID '+$game.Id+', profiler PID '+$profiler.Id+'. Reuse the existing bridge session; do not launch another profiler.')
    } else {
    if($game -and !(Get-Process -Id $game.Id -ErrorAction SilentlyContinue)){ $game=$null }
    if($game -and $session -and $session.pid -eq $game.Id)
    {
        try
        {
            Invoke-WebRequest -UseBasicParsing -Method Post -Uri "http://127.0.0.1:$($session.port)/quit" -Headers @{'X-Bridge-Token'=$session.token} -TimeoutSec 10|Out-Null
            $game.WaitForExit(30000)|Out-Null
        }
        catch {Write-Warning "Clean game quit failed: $($_.Exception.Message)"}
    }
    if($game -and !(Get-Process -Id $game.Id -ErrorAction SilentlyContinue)){ $game=$null }
    if($game){Stop-Process -Id $game.Id -Force -ErrorAction SilentlyContinue}
    if($profiler -and !$ReuseProfiler)
    {
        Stop-Process -Id $profiler.Id -Force -ErrorAction SilentlyContinue
        # A forced exit cannot dispose the ETW session. Remove only sessions
        # created by this profiler process so later captures retain DXGI events.
        $ownedTracePattern='^RebirthBenchLive_'+$profiler.Id+'_[0-9a-f]{32}\s'
        $ownedTraces=logman query -ets | ForEach-Object {
            if($_ -match $ownedTracePattern){($_ -split '\s+')[0]}
        }
        foreach($traceName in $ownedTraces){logman stop $traceName -ets | Out-Null}
    }
    }
}

