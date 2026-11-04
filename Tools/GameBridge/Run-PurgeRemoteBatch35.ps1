param([ValidateSet('Connect','Baseline','ZeroMap','Fresh','Ranger','Disconnect','Reconnect','Close')][string]$Phase,[ValidateRange(32,99)][int]$Batch=32)
$ErrorActionPreference='Stop';$base="Tools/Profiler/out/purge-b$Batch";$h=Join-Path $PSScriptRoot 'Invoke-PurgeRegression.ps1'
function B($role,$label,$a){$raw=& $h -Stage (Join-Path $base $role) -Label ("b$Batch-"+$label) -Direct -ArgumentsJson (ConvertTo-Json -InputObject @($a) -Compress);return (($raw -join "`n")|ConvertFrom-Json)}
function Projection($label){$r=B client $label @('console','rbpurgetestobserve projection');return ($r.results[0].output[0]|ConvertFrom-Json)}
function Context {if(-not(Test-Path "$base/context.json")){throw 'Baseline context missing'};return (Get-Content "$base/context.json" -Raw|ConvertFrom-Json)}
function ServerO($label){$c=Context;$r=B server $label @('console',"rbpurgetestobserve player $($c.player)");return ($r.results[0].output[0]|ConvertFrom-Json)}
function Ready($label){for($i=0;$i -lt 90;$i++){ $s=B client ($label+'-'+$i) @('state','-Sections','player');if($s.state -eq 'ingame' -and -not $s.player.dead -and $s.player.onGround -and -not $s.player.flying){if(-not $s.guard.enabled -or -not $s.guard.heal -or -not $s.guard.reload){throw 'Guard/heal/reload required'};return $s};Start-Sleep -Seconds 1 };throw 'Native arrival not ready; retain same peer'}
function ClickId($label,$id){$tree=B client ($label+'-tree') @('uitree');$nodes=@($tree.nodes|Where-Object {$_.id -eq $id -and $_.clickable});if($nodes.Count -ne 1){throw "Require one actual observed clickable $id"};$null=B client ($label+'-click') @('click',('path='+$nodes[0].path))}
function Join {
 ClickId 'play-game' 'btnPlayGame';ClickId 'join-tab' 'serverBrowser';ClickId 'direct-open' 'btnDirectConnect'
 $null=B client 'direct-ip' @('call','POST','/ui/type','id=txtIp','value=127.0.0.1');$null=B client 'direct-port' @('call','POST','/ui/type','id=txtPort','value=26903');ClickId 'connect' 'btnConnect'
 $deadline=[datetime]::UtcNow.AddMinutes(15)
 while([datetime]::UtcNow -lt $deadline){$ping=B client 'joining-status' @('status');if($ping.state -eq 'ingame'){return};if($ping.state -eq 'dead'){ $u=B client 'spawn-tree' @('uitree');$n=@($u.nodes|Where-Object {$_.clickable -and $_.text -eq 'Spawn'});if($n.Count -eq 1){$null=B client 'actual-spawn' @('click',('path='+$n[0].path))}};Start-Sleep -Seconds 2};throw 'Join bound expired; inspect existing peers, no new launch'
}
function Enter($label,$c){
 $x=$c.origin[0]-12;$y=$c.origin[1]+10;$z=$c.origin[2]+[int]($c.size[2]/2);$null=B client ($label+'-exterior') @('console',"teleport $x $y $z");$null=Ready ($label+'-landed');$null=B client ($label+'-surroundings') @('surroundings')
 $null=B client ($label+'-walk-in') @('call','POST','/walkto',('x='+($c.origin[0]+3)),"z=$z",'maxSeconds=35');$o=ServerO ($label+'-entered-server');if($o.prefab -ne $c.name){throw 'Native intended POI not entered; no inside teleport fallback'};return $o
}
if($Phase -eq 'Connect' -or $Phase -eq 'Reconnect'){Join;$null=Ready ($Phase+'-arrived');$null=B client ($Phase+'-surroundings') @('surroundings');$null=Projection ($Phase+'-projection');return}
if($Phase -eq 'Baseline'){
 $s=Ready 'baseline-ready';$id=$s.player.entityId
 $client=B client 'client-topology' @('call','GET','/auditstate','domain=topology');$server=B server 'server-topology' @('call','GET','/auditstate','domain=topology',"player=$id")
 $ct=$client.domains.topology;$st=$server.domains.topology
 if(-not $ct.remote -or -not $ct.connected -or -not $st.server -or -not $st.dedicated -or -not $st.authoritativePeer -or $ct.worldKey -ne $st.worldKey -or $ct.ownerKey -ne $st.ownerKey -or $ct.entityId -ne $st.entityId){throw 'Genuine connected dedicated/client identity not proven'}
 @{player=$id;world=$st.worldKey;owner=$st.ownerKey;name=$s.player.name}|ConvertTo-Json|Set-Content "$base/context.json"
 $o=ServerO 'baseline-server';if($o.saveName -ne 'CodexPurgeRemote20261102' -or -not $o.purgeEnabled){throw 'Wrong authoritative disposable world/mode'}
 $p=Projection 'baseline-client';if(-not $p.purgeEnabled){throw 'Client Purge options not replicated'}
 if($s.player.name -notmatch '^[A-Za-z0-9_-]{1,64}$'){throw 'Name requires explicit native identity selector'}
 $null=B server 'isolated-admin-grant' @('console',"admin add $($s.player.name) 0")
 $null=B client 'baseline-godmode-off' @('call','POST','/godmode','on=0');$null=B client 'fixture-restore' @('restore');$null=B server 'daylight' @('console','settime 8 9 0');return
}
if($Phase -eq 'ZeroMap'){
 $o=ServerO 'zero-target-authority';if($o.nativeSupplyCacheCount -ne 0 -or -not $o.supplyReady -or ($o.supply -and ($o.supply.inFlight -ne 0 -or $o.supply.deliveredDrops -ne 0))){throw 'Not an authoritative zero-drop fixture; no mutation to manufacture it'}
 $null=B client 'zero-map-open' @('key','name=M');$tree=$null
 for($i=0;$i -lt 8;$i++){$tree=B client ('zero-map-tree-'+$i) @('uitree');Start-Sleep -Milliseconds 250}
 $null=B client 'zero-map-shot' @('screenshot','b32-remote-zero-supply-map')
 $visible=@($tree.nodes|Where-Object {$_.id -eq 'supplyCycleButton' -or $_.id -eq 'supplyCycleIcon'});@{nativeCacheCount=$o.nativeSupplyCacheCount;supplyReady=$o.supplyReady;visibleControls=$visible;remote=$true;scope='Connected native client; server zero native crate caches, no cache deletion.'}|ConvertTo-Json -Depth 5|Set-Content "$base/zero-supply-map-verdict.json"
 $null=B client 'zero-map-close' @('key','name=M');if($visible.Count -ne 0){throw 'Supply control visible despite authoritative zero native targets'};return
}
if($Phase -in @('Fresh','Ranger')){
 $target=if($Phase -eq 'Fresh'){Get-Content "$base/fresh-candidates.json" -Raw|ConvertFrom-Json|Select-Object -First 1}else{[pscustomobject]@{name='ranger_station_07';origin=@(-1234,58,408);size=@(60,38,60)}}
 $null=B client ($Phase+'-fixture-restore') @('restore');$null=B client ($Phase+'-entry-audio-start') @('console','rbpurgeaudiotrace start 120');$o=Enter $Phase $target;$null=Projection ($Phase+'-before-activation');$null=B client ($Phase+'-notice-shot') @('screenshot',('b32-remote-'+$Phase+'-entry'));$null=B client ($Phase+'-entry-audio-status') @('console','rbpurgeaudiotrace status');$null=B client ($Phase+'-entry-audio-stop') @('console','rbpurgeaudiotrace stop');$context=Context
 $inventory=B client ($Phase+'-inventory') @('state','-Sections','player,inventory')
 if(@($inventory.toolbelt|Where-Object {$_.slot -eq 1 -and $_.name -eq 'gunHandgunPistolAdmin'}).Count -ne 1){$null=B client ($Phase+'-fixture-admin') @('give','item=gunHandgunPistolAdmin','toolbelt=2')}
 $null=B client ($Phase+'-select') @('select','slot=2');$held=B client ($Phase+'-held') @('state','-Sections','player');if($held.player.holding.name -ne 'gunHandgunPistolAdmin'){throw 'Actual held admin fixture required'}
 & "$PSScriptRoot/Invoke-PurgeSleeperHelpBatch35.ps1" -Stage "$base/client" -Action Force -Label ("b$Batch-"+$Phase+"-native-help")
 for($i=0;$i -lt 20;$i++){$o=ServerO ($Phase+'-spawn-'+$i);if($o.savedEvidence.enrolled -gt $o.savedEvidence.deathReceipts){break};Start-Sleep -Seconds 1}
 & "$PSScriptRoot/Run-PurgeActorsBatch22.ps1" -Stage "$base/client" -ObservationStage "$base/server" -PlayerId $context.player -ExpectedPrefab $target.name -RunLabel ('b32-'+$Phase+'-combat') -StageDistance 1 -Passes 4 -RestoreFixtureBeforeFight
 for($i=0;$i -lt 35;$i++){$o=ServerO ($Phase+'-completion-'+$i);$projection=Projection ($Phase+'-projection-'+$i);if($o.record.state -eq 'Cleared'){break};Start-Sleep -Seconds 1}
 $null=B client ($Phase+'-final-shot') @('screenshot',('b32-remote-'+$Phase+'-final'))
 @{server=$o;client=$projection;scope='Actual dedicated authority and connected client; native force and actual staged combat, not two-player collaboration or ordinary all-room traversal.'}|ConvertTo-Json -Depth 20|Set-Content "$base/$Phase-clearance-verdict.json"
 if($o.record.state -ne 'Cleared'){throw 'Remote native target not cleared; exact server blockers retained'};return
}
if($Phase -eq 'Disconnect'){
 $null=B client 'pre-disconnect-surroundings' @('surroundings');$null=B client 'pause-open' @('key','name=Escape');$tree=B client 'pause-tree' @('uitree')
 $path='ingameMenu/ingameMenu/buttons/btnExit/clickable';if(@($tree.nodes|Where-Object {$_.path -eq $path -and $_.clickable}).Count -ne 1){throw 'Exact native Exit button not observed'}
 $null=B client 'native-disconnect' @('click',"path=$path")
 for($i=0;$i -lt 120;$i++){$s=B client ('disconnect-status-'+$i) @('status');if($s.state -eq 'menu'){break};Start-Sleep -Seconds 1}
 if($s.state -ne 'menu'){throw 'Did not reach native menu'};$null=B client 'menu-lifecycle' @('call','GET','/purgelifecycle');$null=B client 'menu-ui' @('uitree');return
}
if($Phase -eq 'Close'){
 $null=B client 'final-godmode-off' @('call','POST','/godmode','on=0');$null=B client 'final-stop' @('call','POST','/stop');$null=B client 'normal-client-quit' @('call','POST','/quit');return
}



