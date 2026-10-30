param([switch]$Run,[switch]$Resume,[switch]$SkipVerifiedWeapons,[switch]$SkipVerifiedProgression,[switch]$LiquidClicks,[switch]$ColliderObserve,[switch]$HoodSurfaceOnly,[switch]$SkipLadder,
    [ValidatePattern('^RebirthBridgeButcher[A-Za-z0-9_-]+$')][string]$TestSave='RebirthBridgeButcher20261029')
$ErrorActionPreference='Stop'
$modRoot=Split-Path (Split-Path $PSScriptRoot)
$bridge=Join-Path $PSScriptRoot 'gamebridge.ps1'
$testProfile='CodexButcherTest20261029'
$world='West Xuyofu Territory'
$savePath=Join-Path (Join-Path (Join-Path $env:APPDATA '7DaysToDie/Saves') $world) $testSave
if($Resume) {
    if(!(Test-Path -LiteralPath $savePath)){throw 'Resume requires the existing disposable Butcher test save'}
} elseif(Test-Path -LiteralPath $savePath){throw 'Fresh Butcher test save already exists; do not overwrite it'}
if($SkipVerifiedWeapons -or $SkipVerifiedProgression) {
    if(!$Resume){throw 'Skipping verified weapon setup requires exact-character Resume'}
    $prior=Join-Path $PSScriptRoot 'out/tests/20261029-031221-251_player_reports_local_weapon_progress/summary.json'
    if(!(Test-Path $prior) -or !(Get-Content $prior -Raw | ConvertFrom-Json).passed){throw 'Prior genuine Butcher weapon evidence missing or failed'}
}
if(Get-Process 7DaysToDie,7DaysToDieServer -ErrorAction SilentlyContinue){throw 'Close the existing game before this prepared batch'}
if(!$Run){Write-Output "Preflight: game closed; resume=$Resume. -Run plays the disposable Butcher batch and quits in finally. Resume never creates or edits a Survivor Profile.";return}
$evidence=Join-Path $modRoot ('_Documentation/PlayerReports_20261029/NativeButcher/'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Force $evidence | Out-Null
$script:step=0
$ownedPid=$null
$energyTraceActive=$false
function Step {
    param([Parameter(ValueFromRemainingArguments=$true)][string[]]$Parts)
    $script:step++
    $raw=& powershell -NoProfile -ExecutionPolicy Bypass -File $bridge @Parts
    $code=$LASTEXITCODE
    $path=Join-Path $evidence ('{0:D3}-{1}.json' -f $script:step,$Parts[0])
    $raw | Set-Content -LiteralPath $path -Encoding UTF8
    if($code -ne 0){throw "Bridge step failed: $($Parts[0]); see $path"}
    $reply=($raw -join "`n") | ConvertFrom-Json
    if($reply.ok -eq $false -or $reply.hadError -eq $true){throw "Bridge refused step: $($Parts[0]); see $path"}
    Write-Host "Step $script:step $($Parts[0])"
    return $reply
}
function CreatorClick([string]$id){$null=Step click "window=rebirthSurvivorCreator" "id=$id"}
function CloseTestCrafting {
    for($attempt=0;$attempt -lt 4;$attempt++) {
        $tree=Step uitree
        if($tree.openWindows -notcontains 'crafting'){return}
        $null=Step key 'name=Escape'
    }
    throw 'Crafting window did not close; do not attempt gameplay input'
}
function FlatDistance($a,$b) {
    return [math]::Sqrt([math]::Pow([double]$a[0]-[double]$b[0],2)+[math]::Pow([double]$a[2]-[double]$b[2],2))
}
function AssertEntityClear($position,$entities,[double]$metres) {
    foreach($entity in @($entities.entities)) {
        if(!$entity.dead -and $entity.position -and (FlatDistance $position $entity.position) -lt $metres) {
            throw "Test area overlaps observed entity $($entity.id) $($entity.class); choose open ground before setup"
        }
    }
}
function AssertGrounded($state,$terrain) {
    if(!$state.guard.enabled -or $state.player.dead -or !$terrain.onGround -or
        [double]$terrain.flatness -gt 0.5) {
        throw 'Player is not grounded on the observed flat terrain; refuse fixture placement'
    }
    $px=[int][math]::Floor($state.player.position[0]);$pz=[int][math]::Floor($state.player.position[2])
    $py=[int][math]::Floor($state.player.position[1])-1
    $support=Step call 'GET' '/blockdamage' "x=$px" "y=$py" "z=$pz"
    if(!$support.authoritative -or !(TestEnergySupportBlock ([string]$support.block))) {
        throw 'No loaded solid world block below feet (vehicle/ladder support is insufficient)'
    }
}
function PrepareEnergyGround {
    CloseTestCrafting
    $null=Step surroundings
    $entities=Step entities 'radius=120' 'limit=1000'
    if(@($entities.entities).Count -ge 1000){throw 'Entity exclusion inventory truncated'}
    $initial=Step state '-Sections' 'player,stats'
    if(!$initial.guard.enabled -or $initial.player.dead){throw 'Guard/actor not ready for grounded approach'}
    $nearEntity=$false
    foreach($entity in @($entities.entities)) {
        if(!$entity.dead -and $entity.position -and (FlatDistance $initial.player.position $entity.position) -lt 24){$nearEntity=$true}
    }
    if($nearEntity) {
        # The terrain-only best spot can be directly under the saved camper.
        # First walk a bounded observed safe egress, then rescan from there.
        $egressTerrain=Step terrain 'distance=36'
        if(!$egressTerrain.onGround){throw 'Wait for a stable supported actor before leaving the camper area'}
        $egress=GetEnergyEgress $initial $egressTerrain $entities
        $walk=Step walkto "x=$($egress[0])" "z=$($egress[2])" 'radius=0.7' 'maxSeconds=60'
        if($walk.result -ne 'arrived'){throw 'Observed egress did not arrive; no fixture edits'}
        $null=Step surroundings
        $entities=Step entities 'radius=120' 'limit=1000'
        if(@($entities.entities).Count -ge 1000){throw 'Entity exclusion inventory truncated'}
    }
    # Broad flatness/clutter is a search hint, not proof of a particular footprint.
    # size still selects native terrain height, independent of camper elevation.
    $ground=Step openground 'radius=60' 'size=4'
    if(!$ground.position -or $ground.flatRange -lt 0 -or $ground.flatRange -gt 1.2 -or
        [string]::IsNullOrEmpty($ground.surface) -or $ground.surface -match 'water'){throw 'No loaded dry terrain search hint'}
    $candidateNumber=0
    foreach($candidate in @(GetEnergyCandidates $ground.position)) {
        $candidateNumber++
        $entities=Step entities 'radius=120' 'limit=1000'
        if(@($entities.entities).Count -ge 1000){throw 'Entity exclusion inventory truncated'}
        $clear=$true
        foreach($entity in @($entities.entities)) {
            if(!$entity.dead -and $entity.position -and (FlatDistance $candidate $entity.position) -lt 24){$clear=$false;break}
        }
        if(!$clear){WriteEnergyRejection $candidateNumber $candidate 'observed entity within24m';continue}
        $walk=Step walkto "x=$($candidate[0])" "z=$($candidate[2])" 'radius=0.7' 'maxSeconds=30'
        if($walk.result -ne 'arrived' -or $walk.remainingDistance -gt 0.8){throw 'Candidate approach failed; stop rather than hide movement failure'}
        $null=Step surroundings
        $state=Step state '-Sections' 'player,stats'
        if((FlatDistance $state.player.position $candidate) -gt 1.5){throw 'Candidate actor position disagrees with arrived movement evidence'}
        $terrain=Step terrain 'distance=30'
        if(!$state.guard.enabled -or $state.player.dead -or !$terrain.onGround){throw 'Candidate actor/guard is not stably supported; stop search movement'}
        $entities=Step entities 'radius=120' 'limit=1000'
        if(@($entities.entities).Count -ge 1000){throw 'Entity exclusion inventory truncated'}
        try {
            AssertEntityClear $state.player.position $entities 24
            AssertGrounded $state $terrain
        } catch {
            if($_.Exception.Message -notmatch '^(Test area overlaps|Player is not grounded|No loaded solid)'){throw}
            WriteEnergyRejection $candidateNumber $candidate $_.Exception.Message;continue
        }
        $footprint=FindEnergyFootprint $state
        if(!$footprint){WriteEnergyRejection $candidateNumber $candidate 'no empty supported3x3 ladder footprint';continue}
        try {$null=GetSafeSprintRoute $state $terrain $entities 30} catch {
            if($_.Exception.Message -notlike 'No flat dry sprint corridor*'){throw}
            WriteEnergyRejection $candidateNumber $candidate $_.Exception.Message;continue
        }
        WriteEnergySelection $candidateNumber $state.player.position $footprint
        $null=Step screenshot 'butcher-energy-ground-before-setup'
        return @{state=$state;footprint=$footprint}
    }
    throw 'All bounded observed candidate positions exhausted without a verified footprint and sprint route; no world edits performed'
}
function GetEnergyCandidates($position) {
    foreach($offset in @(@(0,0),@(6,0),@(-6,0),@(0,6),@(0,-6),@(6,6),@(-6,6),@(6,-6),@(-6,-6),@(30,0),@(-30,0),@(0,30),@(0,-30),@(30,30),@(-30,30),@(30,-30),@(-30,-30))) {
        ,@(([double]$position[0]+$offset[0]),[double]$position[1],([double]$position[2]+$offset[1]))
    }
}
function WriteEnergyRejection([int]$number,$position,[string]$reason) {
    @{candidate=$number;position=$position;reason=$reason;utc=[DateTime]::UtcNow.ToString('o')} | ConvertTo-Json -Depth 4 | Set-Content (Join-Path $evidence ("energy-candidate-$number-rejected.json"))
}
function WriteEnergySelection([int]$number,$position,$footprint) {
    @{candidate=$number;anchor=$position;footprint=$footprint;utc=[DateTime]::UtcNow.ToString('o');nextStep=$script:step+1} | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $evidence 'energy-ground-selected.json')
}
function FindEnergyFootprint($state) {
    $px=[int][math]::Floor($state.player.position[0]);$pz=[int][math]::Floor($state.player.position[2])
    $y=[int][math]::Floor($state.player.position[1])
    foreach($offset in @(@(4,0),@(-4,0),@(0,4),@(0,-4))) {
        $x=$px+$offset[0];$z=$pz+$offset[1];$clear=$true
        # Cheap support/feet checks reject a curb or plant before the upper scan.
        for($fx=$x-1;$fx -le $x+1 -and $clear;$fx++) {
            foreach($fz in @(($z-1),$z,($z+1))) {
                $support=Step call 'GET' '/blockdamage' "x=$fx" "y=$($y-1)" "z=$fz"
                $feet=Step call 'GET' '/blockdamage' "x=$fx" "y=$y" "z=$fz"
                if(!$support.authoritative -or !$feet.authoritative){throw 'Footprint authority unavailable'}
                if(!(TestEnergySupportBlock ([string]$support.block)) -or $feet.block -ne 'air'){$clear=$false;break}
            }
        }
        if(!$clear){continue}
        for($fx=$x-1;$fx -le $x+1 -and $clear;$fx++) {
            foreach($fz in @(($z-1),$z,($z+1))) {
                for($fy=$y+1;$fy -le $y+6;$fy++) {
                    $space=Step call 'GET' '/blockdamage' "x=$fx" "y=$fy" "z=$fz"
                    if(!$space.authoritative){throw 'Footprint authority unavailable'}
                    if($space.block -ne 'air'){$clear=$false;break}
                }
                if(!$clear){break}
            }
        }
        if($clear){return @{x=$x;y=$y;z=$z;verified=$true}}
    }
    return $null
}
function GetEnergyEgress($state,$terrain,$entities) {
    for($i=0;$i -lt @($terrain.directions).Count;$i++) {
        $direction=$terrain.directions[$i]
        if(!$direction.safe -or $direction.water -or $direction.safeMetres -lt 36 -or $direction.maxDrop -gt 2 -or $direction.maxRise -gt 1.1){continue}
        $yaw=([double]$state.player.rotation[1]+45*$i)*[math]::PI/180
        $candidate=@(([double]$state.player.position[0]+36*[math]::Sin($yaw)),[double]$state.player.position[1],([double]$state.player.position[2]+36*[math]::Cos($yaw)))
        $clear=$true
        foreach($entity in @($entities.entities)) {
            if(!$entity.dead -and $entity.position -and (FlatDistance $candidate $entity.position) -lt 24){$clear=$false;break}
        }
        if($clear){return $candidate}
    }
    throw 'No observed safe egress away from camper; stop before movement or fixture edits'
}
function GetSafeSprintRoute($state,$terrain,$entities,[double]$distance) {
    for($i=0;$i -lt @($terrain.directions).Count;$i++) {
        $direction=$terrain.directions[$i]
        if(!$direction.safe -or $direction.water -or [double]$direction.safeMetres -lt $distance -or
            [double]$direction.maxDrop -gt 0.5 -or [double]$direction.maxRise -gt 0.5){continue}
        $yaw=([double]$state.player.rotation[1]+45*$i)*[math]::PI/180
        $dx=[math]::Sin($yaw);$dz=[math]::Cos($yaw);$blocked=$false
        foreach($entity in @($entities.entities)) {
            if($entity.dead -or !$entity.position){continue}
            $ex=[double]$entity.position[0]-[double]$state.player.position[0]
            $ez=[double]$entity.position[2]-[double]$state.player.position[2]
            $along=[math]::Max(0,[math]::Min($distance,$ex*$dx+$ez*$dz))
            if([math]::Sqrt([math]::Pow($ex-$along*$dx,2)+[math]::Pow($ez-$along*$dz,2)) -lt 12){$blocked=$true;break}
        }
        if(!$blocked){return @{x=[double]$state.player.position[0]+$dx*$distance;z=[double]$state.player.position[2]+$dz*$distance}}
    }
    throw 'No flat dry sprint corridor clear of observed entities; do not run blindly'
}
function EnergyEvidence([string]$phase) {
    @{phase=$phase;utc=[DateTime]::UtcNow.ToString('o');nextStep=$script:step+1} | ConvertTo-Json | Set-Content (Join-Path $evidence ("energy-phase-$phase.json"))
    $null=Step console 'rbmet rates' 'rbsurvivor character condition'
    $null=Step state '-Sections' 'player,stats'
}
function SelectCreatorRow([string]$label,[string]$labelPrefix,[string]$buttonPrefix,[string]$scrollId) {
    $direction=-1
    $previous=''
    $reversed=$false
    for($attempt=0;$attempt -lt 60;$attempt++) {
        $tree=Step uitree 'window=rebirthSurvivorCreator'
        $rows=@($tree.nodes | Where-Object {$_.id -match ('^'+$labelPrefix+'\d+$')})
        $match=$rows | Where-Object {$_.text -eq $label} | Select-Object -First 1
        if($match){$index=[regex]::Match($match.id,'\d+$').Value;CreatorClick ($buttonPrefix+$index);return}
        $signature=($rows.text -join '|')
        if($signature -eq $previous) {
            if($reversed){throw "Creator row not found: $label"}
            $direction=1;$reversed=$true
        }
        $previous=$signature
        $null=Step scroll 'window=rebirthSurvivorCreator' "id=$scrollId" "delta=$direction"
    }
    throw "Creator row search exceeded its bound: $label"
}
. (Join-Path $PSScriptRoot 'energy_flow_audit.ps1')
$energyFlowCollected=$false
try {
    $launchArgs=@{Command='launch';NoBuild=$true;NoWait=$true;World=$world;Save=$testSave}
    if(!$Resume){$launchArgs.NewSave=$true}
    # Keep the launcher in this live driver process. Per-command child shells
    # remain useful for JSON capture, but must not own the long-lived game.
    & $bridge @launchArgs *> (Join-Path $evidence 'launch.log')
    $launchSucceeded=$?
    if(!$launchSucceeded){throw 'Fresh native launch failed'}
    $owner=Get-Content (Join-Path $PSScriptRoot 'out/launch.json') -Raw | ConvertFrom-Json
    if($owner.save -ne $testSave -or $owner.world -ne $world){throw 'Unexpected launcher-owned save'}
    $ownedPid=$owner.pid
    $deadline=(Get-Date).AddSeconds(180)
    $session=$null
    while((Get-Date) -lt $deadline) {
        if(!(Get-Process -Id $ownedPid -ErrorAction SilentlyContinue)){throw 'Owned game exited before bridge startup'}
        $discovery=Join-Path $PSScriptRoot 'out/session.json'
        if(Test-Path -LiteralPath $discovery) {
            $candidate=Get-Content -LiteralPath $discovery -Raw | ConvertFrom-Json
            if($candidate.pid -eq $ownedPid){$session=$candidate;break}
        }
        Start-Sleep -Seconds 2
    }
    if(!$session){throw 'Bridge startup exceeded bound'}
    $env:REBIRTH_BRIDGE_SESSION=Join-Path $session.outputDir 'session.json'
    $lease=Invoke-RestMethod -Method Post -Uri ('http://127.0.0.1:'+$session.port+'/batch?action=acquire&run=fresh-butcher-20261029') -Headers @{'X-Bridge-Token'=$session.token} -TimeoutSec 30
    if(!$lease.runToken){throw 'Exclusive test lease missing'}
    $env:REBIRTH_BRIDGE_RUN_TOKEN=$lease.runToken
    if(!$Resume) {
    $ready=$false
    while((Get-Date) -lt $deadline) {
        $tree=Step uitree
        if($tree.openWindows -contains 'rebirthSurvivorProfiles'){$ready=$true;break}
        $null=Step status
        Start-Sleep -Seconds 2
    }
    if(!$ready){throw 'Expected fresh Survivor Profiles menu not reached'}
    $validation=Step console 'rbsurvivor create validate background.butcher diet.unrestricted trait.fast_reader,trait.quick_healer,trait.disease_resistant'
    $validationText=($validation.results | ForEach-Object {($_.output -join ' ')+' '+($_.log.msg -join ' ')}) -join ' '
    if($validationText -notmatch 'valid=True' -or $validationText -notmatch 'points=0'){throw 'Prepared Butcher control build did not validate'}
    $null=Step type 'window=rebirthSurvivorProfiles' 'id=profileSearchInput' "value=$testProfile"
    $tree=Step uitree 'window=rebirthSurvivorProfiles'
    if($tree.nodes | Where-Object {$_.text -eq $testProfile}){throw 'Test profile name already exists; no existing profile may be edited'}
    $null=Step click 'window=rebirthSurvivorProfiles' 'id=btnCreateProfile'
    CreatorClick 'btnCreatorNext'
    SelectCreatorRow 'Butcher' 'backgroundName' 'btnBackground' 'backgroundNativeScrollView'
    CreatorClick 'btnCreatorNext'
    SelectCreatorRow 'Unrestricted' 'dietName' 'btnDiet' 'dietNativeScrollView'
    CreatorClick 'btnCreatorNext'
    foreach($trait in 'Disease Resistant','Fast Reader','Quick Healer') {SelectCreatorRow $trait 'positiveTraitName' 'btnPositiveTrait' 'positiveTraitNativeScrollView'}
    CreatorClick 'btnCreatorNext'
    $null=Step type 'window=rebirthSurvivorCreator' 'id=survivorProfileName' "value=$testProfile"
    $review=Step uitree 'window=rebirthSurvivorCreator'
    if(($review.nodes.text -join ' ') -notmatch 'Butcher'){throw 'Review does not show Butcher'}
    $null=Step screenshot 'fresh-butcher-review'
    CreatorClick 'btnCreatorSave'
    $manager=Step uitree 'window=rebirthSurvivorProfiles'
    if(($manager.nodes.text -join ' ') -notmatch [regex]::Escape($testProfile)){throw 'New test profile is not selected/visible'}
    $null=Step click 'window=rebirthSurvivorProfiles' 'id=btnSelectProfileForWorld'
    }
    $deadline=(Get-Date).AddSeconds(360)
    $inGame=$false
    $eosRetryAt=$null
    while((Get-Date) -lt $deadline) {
        $status=Step status
        if($status.state -eq 'ingame'){$inGame=$true;break}
        $tree=Step uitree
        if($tree.openWindows -contains 'loginWindow') {
            $retry=@($tree.nodes | Where-Object {$_.path -eq 'loginWindow/loginWindow/content/rect/btnRetry' -and $_.clickable})
            if($retry.Count -ne 1){throw 'Unexpected login prompt; refuse unobserved account input'}
            if(!$eosRetryAt) {
                $null=Step click "path=$($retry[0].path)"
                $eosRetryAt=Get-Date
            } elseif(((Get-Date)-$eosRetryAt).TotalSeconds -ge 30) {
                throw 'Epic Online Services login remains unavailable after one observed Retry; no offline identity switch attempted'
            }
        }
        $spawn=$tree.nodes | Where-Object {$_.id -in 'btnSpawnFirstTime','btnSpawn'} | Select-Object -First 1
        if($spawn){$null=Step click "id=$($spawn.id)"}
        Start-Sleep -Seconds 2
    }
    if(!$inGame){throw 'Fresh Butcher did not spawn'}
    $null=Step surroundings
    $null=Step cleararea 'radius=40'
    $null=Step restore
    $null=Step godmode 'on=0'
    $identity=Step console 'settime 1 9 0' 'rbsurvivor character show' 'rbsurvivor character condition' 'rbskill audit'
    if(($identity.results.output -join ' ') -notmatch 'background=background.butcher'){throw 'Spawned character is not the fresh Butcher'}
    if($Resume -and ($identity.results.output -join ' ') -notmatch 'creationId=ed378cb18a7a4303997605d286eef68e'){throw 'Resume character identity differs from the recorded disposable Butcher; refuse further actions'}
    if($ColliderObserve -or $HoodSurfaceOnly) {
        # The copied fixture retains its saved camper entity172. These existing
        # diagnostics observe its current mode/support before test movement.
        $null=Step entities 'radius=60' 'contains=camper'
        $camperObservation=Step call 'GET' '/camper' 'entity=172'
        $camperObservationPath=Join-Path $evidence ('{0:D3}-call.json' -f $script:step)
        $null=Step console 'camperdiag 172'
        $null=Step screenshot 'camper-support-before-tests'
    }
    if($HoodSurfaceOnly) {
        $python='C:/Users/Etienne/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe'
        $cases=Join-Path $evidence 'hood-cases'
        & $python (Join-Path $PSScriptRoot 'prepare_hood_regression_20261029.py') $camperObservationPath $cases
        if($LASTEXITCODE){throw 'Observed hood scenario preparation failed'}
        $mode=[string]$camperObservation.presentation.mode
        if(!$camperObservation.presentation.bodyInstalled){$mode='bare'}
        if($mode -notin 'Travel','Camp','bare'){throw 'Known supported mode required for hood regression'}
        $scenario=Join-Path $cases ('camper_hood_regression_'+$mode.ToLowerInvariant()+'_20261029.json')
        $result=& powershell -NoProfile -ExecutionPolicy Bypass -File $bridge test $scenario
        $code=$LASTEXITCODE
        $result | Set-Content (Join-Path $evidence 'hood-surface.log')
        if($code -ne 0){throw 'Hood surface scenario failed; review actual movement and diagnostic evidence'}
        $null=Step call 'GET' '/camper' 'entity=172'
        $null=Step console 'camperdiag 172'
        Write-Output "Native hood surface $mode passed; evidence=$evidence"
        return
    }
    $null=Step terrain 'distance=30'
    if(!$SkipVerifiedWeapons) {
    if(!$SkipVerifiedProgression) {
    $result=& powershell -NoProfile -ExecutionPolicy Bypass -File $bridge test (Join-Path $PSScriptRoot 'tests/player_reports_local_weapon_progress.json')
    $result | Set-Content (Join-Path $evidence 'local-weapons.log') -Encoding UTF8
    if($LASTEXITCODE -ne 0){throw 'Fresh Butcher weapon test failed'}
    } else {
        $null=Step give 'item=gunRifleT1HuntingRifle' 'quality=2' 'toolbelt=1'
    }
    # Capture the newly integrated shared hover/compare UI with observed slots.
    $null=Step give 'item=meleeWpnSledgeT1IronSledgehammer' 'quality=5' 'toolbelt=2'
    $null=Step give 'item=meleeWpnSledgeT1IronSledgehammer' 'quality=2' 'toolbelt=3'
    $null=Step key 'name=Tab'
    $inventory=Step uitree
    $high=$inventory.nodes | Where-Object {$_.item.name -eq 'meleeWpnSledgeT1IronSledgehammer' -and $_.item.quality -eq 5} | Select-Object -First 1
    $low=$inventory.nodes | Where-Object {$_.item.name -eq 'meleeWpnSledgeT1IronSledgehammer' -and $_.item.quality -eq 2} | Select-Object -First 1
    if(!$high -or !$low){throw 'Observed sledgehammer UI fixture slots missing'}
    $null=Step hover "path=$($high.path)"
    $single=Step uitree
    if(($single.nodes.text -join ' ') -notmatch 'Wear \(block damage\)'){throw 'Complete melee wear row not displayed'}
    $null=Step screenshot 'butcher-sledge-quality5-single'
    $null=Step click "path=$($low.path)"
    $null=Step hover "path=$($high.path)"
    $comparison=Step uitree
    if(($comparison.nodes.text -join ' ') -notmatch '\+120'){throw 'Higher-durability hovered item must show favorable +120'}
    $null=Step screenshot 'butcher-sledge-comparison'
    $null=Step click "path=$($high.path)"
    $null=Step hover "path=$($low.path)"
    $reverse=Step uitree
    if(($reverse.nodes.text -join ' ') -notmatch '-120'){throw 'Lower-durability hovered item must show -120'}
    $null=Step screenshot 'butcher-sledge-comparison-reverse'
    $null=Step click "path=$($low.path)"
    $inventory=Step uitree
    $rifle=$inventory.nodes | Where-Object {$_.item.name -eq 'gunRifleT1HuntingRifle'} | Select-Object -First 1
    if(!$rifle){throw 'Observed rifle UI fixture missing'}
    $null=Step hover "path=$($rifle.path)"
    $ranged=Step uitree
    if(($ranged.nodes.text -join ' ') -notmatch 'Reload time'){throw 'Reload time not displayed'}
    if(($ranged.nodes.text -join ' ') -match '\bEntityDamage\b'){throw 'Raw untranslated EntityDamage label remains'}
    $null=Step screenshot 'butcher-rifle-reload'
    $null=Step key 'name=Escape'
    CloseTestCrafting
    }
    if($LiquidClicks) {
        $python='C:/Users/Etienne/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe'
        $liquid=Join-Path $PSScriptRoot 'tests/player_report_liquids.py'
        foreach($drink in 'drinkJarBoiledWater','drinkJarCoffee') {
            $null=Step give "item=$drink" 'count=1' 'toolbelt=1'
            $null=Step give "item=$drink" 'count=1' 'toolbelt=2'
            $null=Step give "item=$drink" 'count=2' 'toolbelt=3'
            $null=Step key 'name=Tab'
            $tree=Step uitree
            $controlPath='toolbelt/windowToolbelt/toolbelt/inventory/3'
            $control=@($tree.nodes | Where-Object path -eq $controlPath)
            if($control.Count -ne 1){throw 'Control destination slot not observed'}
            if($control[0].item) {
                # Preserve existing fixture gear through ordinary native movement;
                # never clear a slot by deleting its contents for the test.
                $empty=$tree.nodes | Where-Object {
                    $_.ctrl -eq 'XUiC_RebirthCraftingInventorySlot' -and !$_.item -and $_.clickable -and
                    $_.rect.Count -eq 4 -and $_.rect[0] -ge 0 -and $_.rect[1] -ge 0 -and
                    $_.rect[0]+$_.rect[2] -le $tree.screen[0] -and $_.rect[1]+$_.rect[3] -le $tree.screen[1]
                } | Select-Object -First 1
                if(!$empty){throw 'No observed empty backpack slot to preserve control-slot contents'}
                $null=Step drag "from.path=$controlPath" "to.path=$($empty.path)" 'strict=1' 'trace=1'
                $tree=Step uitree
                if(($tree.nodes | Where-Object path -eq $controlPath).item){throw 'Control slot did not become empty'}
            }
            CloseTestCrafting
            $null=Step select 'slot=2'
            $log=& $python $liquid click-pair --bridge $bridge --confirm-disposable $testSave --out (Join-Path $evidence $drink) --item $drink
            $code=$LASTEXITCODE
            $log | Set-Content (Join-Path $evidence ($drink+'-clicks.log'))
            if($code -ne 0){throw "Liquid click regression failed: $drink"}
        }
        $null=Step console 'rbmet autosip on'
    }
    # Prepare the ladder fixture before the measured flow. World edits here
    # only set up this disposable situation; climbing/combat use real input.
    $preparation=PrepareEnergyGround
    $state=$preparation.state
    $anchor=@($state.player.position)
    if(!$preparation.footprint.verified){throw 'Candidate did not establish an empty supported footprint'}
    $x=[int]$preparation.footprint.x;$y=[int]$preparation.footprint.y;$z=[int]$preparation.footprint.z
    if(!$SkipLadder) {
    for($level=0;$level -lt 6;$level++) {
        $null=Step setblock "x=$x" "y=$($y+$level)" "z=$($z-1)" 'name=concreteShapes:cube'
        $null=Step setblock "x=$x" "y=$($y+$level)" "z=$z" 'name=ladderWood'
        # Default rotation0 has its observed rung plane at the South cell edge.
        # Read back actual world cells; setblock replies only echo the request.
        $backing=Step call 'GET' '/blockdamage' "x=$x" "y=$($y+$level)" "z=$($z-1)"
        $rungs=Step call 'GET' '/blockdamage' "x=$x" "y=$($y+$level)" "z=$z"
        if(!$backing.authoritative -or !$rungs.authoritative -or $backing.block -ne 'concreteShapes:cube' -or $rungs.block -ne 'ladderWood'){throw 'Actual ladder/backing cell readback failed'}
    }
    }
    $null=Step cleararea 'radius=40'
    $null=Step restore
    $null=Step give 'item=ItemsWeaponsCleaver001_FR' 'quality=2' 'toolbelt=1'
    $null=Step select 'slot=2'
    $held=Step select 'slot=1'
    if($held.actualHand -ne 'ItemsWeaponsCleaver001_FR'){throw 'Measured combat weapon is not actually held'}
    $null=Step console 'rbskill audit'
    $null=Step console 'rbmet energytest 100 77 77 600'
    $energyTraceActive=$true
    EnergyEvidence 'start'
    $null=Step call 'GET' '/auditstate' 'domain=topology'
    $null=Step call 'GET' '/auditstate' 'domain=nativeInventory'
    foreach($seconds in 3,3,4) {
        $state=Step state '-Sections' 'player,stats'
        $distance=$seconds*7+2
        $terrain=Step terrain "distance=$($distance+1)"
        $entities=Step entities 'radius=80' 'limit=1000'
        if(@($entities.entities).Count -ge 1000){throw 'Sprint entity inventory truncated'}
        if(!$state.guard.enabled -or !$terrain.onGround){throw 'Guard or grounded sprint readiness lost'}
        $route=GetSafeSprintRoute $state $terrain $entities $distance
        $null=Step lookat "x=$($route.x)" "y=$([double]$state.player.position[1]+1.6)" "z=$($route.z)"
        $aligned=Step terrain "distance=$($distance+1)"
        $ahead=$aligned.directions[0]
        if(!$aligned.onGround -or !$ahead.safe -or $ahead.water -or $ahead.safeMetres -lt $distance -or $ahead.maxDrop -gt 0.5 -or $ahead.maxRise -gt 0.5){throw 'Aligned sprint corridor failed recheck'}
        $action=StartEnergyAction 'sprint' ("sprint-$script:step") $seconds
        $movement=Step move 'forward=1' 'run=1' "seconds=$seconds"
        CompleteEnergyAction $action $movement
        if(!$movement.onGround -or [double]$movement.travelled -lt $seconds*2){throw 'Sprint did not establish grounded forward travel'}
        EnergyEvidence ("sprint-$script:step")
    }
    $return=Step walkto "x=$($anchor[0])" "z=$($anchor[2])" 'radius=0.7' 'maxSeconds=60'
    if($return.result -ne 'arrived'){throw 'Could not return to verified ground anchor'}
    EnergyEvidence 'return-walk'
    if(!$SkipLadder) {
    # Approach the open North side, entering the ladder column before its
    # South-edge rung plane. Cardinal input prevents lateral wall escape.
    $approach=Step walkto "x=$($x+0.5)" "z=$($z+1.5)" 'radius=0.15' 'maxSeconds=30'
    if($approach.result -ne 'arrived' -or $approach.remainingDistance -gt 0.2){throw 'Precise ladder approach failed'}
    $beforeClimb=Step state '-Sections' 'player,stats'
    if(!$beforeClimb.guard.enabled -or $beforeClimb.player.dead -or
        [math]::Abs([double]$beforeClimb.player.position[0]-($x+0.5)) -gt 0.2 -or
        [math]::Abs([double]$beforeClimb.player.position[2]-($z+1.5)) -gt 0.2){throw 'Actor not centred on observed ladder approach'}
    $null=Step lookat "x=$x" "y=$($y+1)" "z=$z" 'block=1'
    $ladderTarget=Step target
    if($ladderTarget.target.block.name -ne 'ladderWood' -or
        $ladderTarget.target.block.position[0] -ne $x -or $ladderTarget.target.block.position[2] -ne $z -or
        $ladderTarget.target.block.face -ne 'North'){throw 'Exact ladder open-side target witness missing'}
    $aligned=Step lookat 'yaw=180' 'pitch=60'
    if([math]::Abs([double]$aligned.rotation[1]-180) -gt 2 -or [double]$aligned.rotation[0] -lt 45){throw 'Cardinal upward ladder alignment failed'}
    if($beforeClimb.player.flying -ne $false){throw 'Ordinary ladder test requires an observed native fly=false'}
    $action=StartEnergyAction 'ascent' 'ladder-ascent'
    $afterClimb=$beforeClimb
    $attachedObserved=$false
    # Stop partway up the six-rung fixture. A long blind hold can crest the
    # top, walk off the backing and fall before its final snapshot.
    for($climbStep=0;$climbStep -lt 12;$climbStep++) {
        $ascent=Step move 'forward=1' 'seconds=0.5'
        $afterClimb=Step state '-Sections' 'player,stats'
        if($afterClimb.player.flying -ne $false -or $afterClimb.player.dead -or
            $afterClimb.stats.health.value -lt $beforeClimb.stats.health.value){throw 'Ladder state changed or actor fell/hurt'}
        if($afterClimb.player.onLadder -eq $true){$attachedObserved=$true}
        if($afterClimb.player.position[1]-$beforeClimb.player.position[1] -ge 3){break}
    }
    CompleteEnergyAction $action $ascent
    if(!$attachedObserved -or $afterClimb.player.onLadder -ne $true){throw 'Native ladder attachment not established at the bounded stop'}
    if($afterClimb.player.position[1]-$beforeClimb.player.position[1] -lt 3){throw 'Actual ladder ascent not established'}
    EnergyEvidence 'ladder-ascent'
    $null=Step lookat 'yaw=180' 'pitch=-60'
    $previousY=[double]$afterClimb.player.position[1]
    $afterDescent=$afterClimb
    $action=StartEnergyAction 'descent' 'ladder-descent'
    for($descentStep=0;$descentStep -lt 8 -and $previousY -gt $y+0.5;$descentStep++) {
        $null=Step move 'forward=1' 'seconds=1'
        $afterDescent=Step state '-Sections' 'player,stats'
        $drop=$previousY-[double]$afterDescent.player.position[1]
        if($drop -lt -0.75 -or $drop -gt 2.5 -or $afterDescent.stats.health.value -lt $afterClimb.stats.health.value){throw 'Controlled ladder descent not established; possible fall, stop test'}
        $previousY=[double]$afterDescent.player.position[1]
    }
    if($afterDescent.player.position[1] -gt $y+0.5){throw 'Safe ladder descent not established'}
    CompleteEnergyAction $action $afterDescent
    EnergyEvidence 'ladder-descent'
    } else { Write-Output 'Ladder deliberately omitted for independent combat diagnosis; full owner-flow acceptance remains incomplete.' }
    # Keep melee well away from the new six-block wall as well as the camper.
    $awayX=[double]$anchor[0]-($x+0.5);$awayZ=[double]$anchor[2]-($z+0.5)
    $awayLength=[math]::Sqrt($awayX*$awayX+$awayZ*$awayZ)
    if($awayLength -lt 2){throw 'Ladder footprint is too close to the ground anchor'}
    if($SkipLadder){$combatAnchor=@($anchor)} else {$combatAnchor=@(([double]$anchor[0]+8*$awayX/$awayLength),[double]$anchor[1],([double]$anchor[2]+8*$awayZ/$awayLength))}
    $return=Step walkto "x=$($combatAnchor[0])" "z=$($combatAnchor[2])" 'radius=0.7' 'maxSeconds=30'
    if($return.result -ne 'arrived'){throw 'Could not return to combat anchor'}
    $state=Step state '-Sections' 'player,stats'
    $terrain=Step terrain 'distance=15'
    AssertGrounded $state $terrain
    foreach($fight in 1..3) {
        if($fight -gt 1) {
            $return=Step walkto "x=$($combatAnchor[0])" "z=$($combatAnchor[2])" 'radius=0.7' 'maxSeconds=30'
            if($return.result -ne 'arrived'){throw 'Could not return to verified combat anchor between fights'}
        }
        $state=Step state '-Sections' 'player,stats'
        $terrain=Step terrain 'distance=15'
        AssertGrounded $state $terrain
        $null=Step surroundings
        $held=Step select 'slot=1'
        if($held.actualHand -ne 'ItemsWeaponsCleaver001_FR'){throw 'Cleaver not actually held before measured fight'}
        $action=StartEnergyAction 'fight' ("fight-$fight")
        $action.actualHandBefore=$held.actualHand
        $action.killBefore=ReadEnergyKillAudit
        $spawned=Step spawn 'entity=zombieArlene' 'distance=6'
        if($spawned.id -le 0 -or $spawned.class -ne 'zombieArlene'){throw 'Exact spawned combat target unavailable'}
        $action.spawned=$spawned
        $combat=Step fight "entity=$($spawned.id)" 'single=1' 'radius=20' 'maxSeconds=60'
        $action.killAfter=ReadEnergyKillAudit
        CompleteEnergyAction $action $combat
        if($combat.incidents.Count -or @($combat.kills).Count -ne 1 -or $combat.kills -notcontains 'zombieArlene' -or $combat.swingsRegistered -le 0 -or $combat.hits -le 0){throw 'Measured cleaver kill not established; inspect trace'}
        EnergyEvidence "fight-$fight"
    }
    $null=Step console 'rbmet rates' 'rbsurvivor character condition' 'rbmet trace stop' 'rbmet autosip on'
    $null=Step log '-Contains' 'Metabolism Trace' '-Limit' '1000'
    $nativeErrors=Step errors '-Since' '0'
    if(@($nativeErrors.entries).Count){throw 'Native errors occurred during the owned session; inspect captured error records'}
    $energyTraceActive=$false
    $energyFlowCollected=$true
    Write-Output "Butcher inputs collected; Energy accounting and owner-flow qualification pending offline review; evidence=$evidence"
} finally {
    # End the test before offline analysis, including menu/creator failures.
    if($ownedPid -and (Get-Process -Id $ownedPid -ErrorAction SilentlyContinue)) {
        try { $null=Step errors '-Since' '0' }
        catch { Write-Warning "Final native error observation unavailable: $_" }
        if($energyTraceActive) {
            try {
                EnergyEvidence 'interrupted'
                $null=Step console 'rbmet trace stop' 'rbmet autosip on'
                $null=Step log '-Contains' 'Metabolism Trace' '-Limit' '1000'
            } catch { Write-Warning "Interrupted Energy evidence collection failed: $_" }
        }
        & powershell -NoProfile -ExecutionPolicy Bypass -File $bridge quit | Set-Content (Join-Path $evidence 'quit.log')
        if(Get-Process -Id $ownedPid -ErrorAction SilentlyContinue){throw "Owned test process $ownedPid did not exit"}
        @{ownedPid=$ownedPid;exited=$true;utc=[DateTime]::UtcNow.ToString('o')} | ConvertTo-Json | Set-Content (Join-Path $evidence 'owned-process-exited.json')
    }
}

if($energyFlowCollected) {
    $python='C:/Users/Etienne/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe'
    & $python (Join-Path $PSScriptRoot 'energy_flow_audit.py') $evidence
    if($LASTEXITCODE){throw 'Energy flow qualification incomplete/failed; see energy-audit.json; game already closed'}
}
