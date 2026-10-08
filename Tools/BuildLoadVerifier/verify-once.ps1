$ErrorActionPreference='Stop'
$root='C:\Program Files (x86)\Steam\steamapps\common\7 Days To Die\Mods\zzz_REBIRTH__3_0'
Set-Location -LiteralPath $root
$base=Join-Path $root 'Tools\BuildLoadVerifier'
$statePath=Join-Path $base 'STATE.json'
$hold=Get-Content (Join-Path $base 'LEAD_WRITER_HOLD.json') -Raw|ConvertFrom-Json
if($hold.state -ne 'GRANTED'){throw 'No granted hold'}
function Write-DurableJson([string]$path,$value){
 $tmp=$path+'.'+[guid]::NewGuid().ToString('N')+'.tmp';$stream=$null
 try {
  $bytes=[Text.Encoding]::UTF8.GetBytes(($value|ConvertTo-Json -Depth 12))
  $stream=[IO.File]::Open($tmp,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::None)
  $stream.Write($bytes,0,$bytes.Length);$stream.Flush($true);$stream.Dispose();$stream=$null
  if([IO.File]::Exists($path)){[IO.File]::Replace($tmp,$path,$path+'.previous')}else{[IO.File]::Move($tmp,$path)}
 }finally{if($stream){$stream.Dispose()};if([IO.File]::Exists($tmp)){[IO.File]::Delete($tmp)}}
}
function Discover-Owned {
 if($script:owned -or -not $script:launchAt -or -not $script:launchHelperId){return}; $script:ownershipRecoveryUnresolved=$true
 $candidates=@(Get-CimInstance Win32_Process -Filter "Name='7DaysToDie.exe'" | Where-Object {
  $_.ParentProcessId -eq $script:launchHelperId -and $_.CreationDate.ToUniversalTime() -ge $script:launchAt -and
  $_.ExecutablePath -eq $script:expectedGamePath -and $_.CommandLine -match '(?:^|\s)"?-GameName=CodexTest"?(?=\s|$)' -and $_.CommandLine -match '(?:^|\s)"?-rebirthbridge=8765"?(?=\s|$)' -and $_.CommandLine -match '(?:^|\s)"?-GameWorld=West Xuyofu Territory"(?=\s|$)'
 })
 if($candidates.Count -gt 1){$script:ownershipAmbiguous=$true; throw 'Ambiguous ownership; will not close candidates'}
 if($candidates.Count -eq 1){
  $script:owned=Get-Process -Id $candidates[0].ProcessId
  $script:ownedStart=$script:owned.StartTime.ToUniversalTime();$script:ownedPath=$script:owned.Path
  $result.ownedProcess=@{pid=$script:owned.Id;startUtc=$script:ownedStart.ToString('o');path=$script:ownedPath;parentPid=$script:launchHelperId;commandLine=$candidates[0].CommandLine}
  $result.cleanupExited=$false
  $result.ownedProcess|ConvertTo-Json|Set-Content (Join-Path $run 'OWNED_PROCESS.json')
 }
 $script:ownershipRecoveryUnresolved=$false
}
function Is-OwnedAlive {
 if(-not $script:owned){return $false}
 $p=Get-Process -Id $script:owned.Id -ErrorAction SilentlyContinue
 return ($p -and $p.StartTime.ToUniversalTime() -eq $script:ownedStart -and $p.Path -eq $script:ownedPath)
}
$run=$null; $started=$null; $owned=$null; $launchAt=$null; $launchHelperId=$null; $launchHelperUnresolved=$false; $ownershipAmbiguous=$false; $ownershipRecoveryUnresolved=$false; $before=$null; $paths=@(); $result=[ordered]@{status='FAIL';errors=@();gameplay='NOT RUN';cleanupExited=$true}
try {
if(Test-Path $statePath){
 $previous=Get-Content $statePath -Raw|ConvertFrom-Json
 $previousStart=[datetimeoffset]::MinValue
 if(-not $previous -or -not $previous.lastStartedUtc -or $previous.lastStartedUtc -notmatch 'Z$' -or -not [datetimeoffset]::TryParse($previous.lastStartedUtc,[ref]$previousStart)){throw 'Invalid interval state: start timestamp required; fail closed'}
 if(([datetimeoffset]::UtcNow-$previousStart).TotalMinutes -lt 30){throw 'Minimum 30 minute interval not met'}
}
function Coordination {
 $note=Get-Content 'E:\_Haven\shared\coordination\HAV3N_to_codex.txt' -Raw
 if($note -match '(?m)^STATUS: (QUIET|CLOSE_GAME)' -and $note -match '(?m)^UNTIL: (.+)$'){$until=[datetime]::Parse($Matches[1]);$local=[TimeZoneInfo]::ConvertTimeBySystemTimeZoneId([datetime]::UtcNow,'Eastern Standard Time');if($until -gt $local){throw 'Active coordination restriction'}}
}
Coordination
if(Get-Process -Name 7DaysToDie -ErrorAction SilentlyContinue){throw 'Existing game: do not build or launch'}
$cfg=Get-Content Tools/GameBridge/gamebridge.config.json -Raw|ConvertFrom-Json
if($cfg.save -ne 'CodexTest' -or $cfg.world -ne 'West Xuyofu Territory' -or $cfg.baselineSave -eq $cfg.save){throw 'Disposable configuration mismatch'}
$started=[datetime]::UtcNow
$run=Join-Path $base $started.ToString('yyyyMMddTHHmmssZ');New-Item $run -ItemType Directory -Force|Out-Null
Write-DurableJson $statePath @{lastStartedUtc=$started.ToString('o');run=$run;state='RUNNING'}
$result=[ordered]@{startedUtc=$started.ToString('o');run=$run;target='7DTD 3.3 b18';save=$cfg.save;status='FAIL';gameplay='NOT RUN';ownedProcess=$null;buildExit=$null;stableIngame=$false;errors=@();cleanupExited=$false}
$owned=$null
$log=Join-Path $env:USERPROFILE 'AppData\LocalLow\The Fun Pimps\7 Days To Die\Player.log'
$baseline=if(Test-Path $log){Get-Item $log}else{$null}
if($baseline){Copy-Item $log (Join-Path $run 'Player.before.log');@{bytes=$baseline.Length;lastWriteUtc=$baseline.LastWriteTimeUtc.ToString('o');capturedUtc=$started.ToString('o')}|ConvertTo-Json|Set-Content (Join-Path $run 'LOG_BASELINE.json')}
$pins=Get-Content $hold.sourceManifest -Raw|ConvertFrom-Json
$paths=@($pins.path)+@('RebirthUtils.csproj','Tools/GameBridge/gamebridge.config.json')+@(Get-ChildItem Config -File -Recurse|ForEach-Object{$_.FullName.Substring($root.Length+1)})
function Hashes { @($paths|Sort-Object -Unique|ForEach-Object{@{path=$_;sha256=(Get-FileHash -LiteralPath (Join-Path $root $_) -Algorithm SHA256).Hash}}) }
$before=Hashes;$before|ConvertTo-Json -Depth 5|Set-Content (Join-Path $run 'SOURCE_BEFORE.json')
foreach($pin in $pins){if((Get-FileHash -LiteralPath $pin.path).Hash -ne $pin.sha256){throw "Lead cohort drift: $($pin.path)"}}
$result.dllBefore=(Get-FileHash RebirthUtils.dll).Hash
function Child([string]$exe,[string]$arguments,[string]$label,[int]$seconds){
 $out=Join-Path $run ($label+'.out.txt');$err=Join-Path $run ($label+'.err.txt');$child=$null;$done=$false
 try {
  $child=Start-Process -FilePath $exe -ArgumentList $arguments -WindowStyle Hidden -PassThru -RedirectStandardOutput $out -RedirectStandardError $err
  if($label -eq 'launch'){$script:launchHelperId=$child.Id;$script:launchHelperUnresolved=$true}
  $null=$child.Handle
  if($label -eq 'launch'){@{launchAtUtc=$launchAt.ToString('o');helperPid=$child.Id;expectedPath=$expectedGamePath;save=$cfg.save;port=$cfg.port}|ConvertTo-Json|Set-Content (Join-Path $run 'LAUNCH_INTENT.json')}
  $childDeadline=[datetime]::UtcNow.AddSeconds($seconds)
  $lastProgress=[datetime]::MinValue
  while(-not $child.HasExited){
   $remaining=($childDeadline-[datetime]::UtcNow).TotalMilliseconds
   if($remaining -le 0){throw "$label watchdog timeout"}
   $null=$child.WaitForExit([int][Math]::Min(5000,$remaining))
   if($label -eq 'quit' -and ([datetime]::UtcNow-$lastProgress).TotalSeconds -ge 15){
    $lastProgress=[datetime]::UtcNow
    "$($lastProgress.ToString('o')) graceful-quit ownedAlive=$(Is-OwnedAlive) remainingSeconds=$([int][Math]::Max(0,($childDeadline-$lastProgress).TotalSeconds))"|Add-Content (Join-Path $run 'SHUTDOWN_PROGRESS.txt')
   }
  }
  $child.Refresh();if($null -eq $child.ExitCode){throw "$label did not capture an exit code"}
  $done=$true
  return @{exit=$child.ExitCode;output=(Get-Content $out -Raw -ErrorAction SilentlyContinue);error=(Get-Content $err -Raw -ErrorAction SilentlyContinue)}
 } finally {
  if($child){
   try {
    if(-not $done -and -not $child.HasExited){$child.Kill()}
    if(-not $child.WaitForExit(5000)){throw "$label helper failed to exit"}
    if($label -eq 'launch'){$script:launchHelperUnresolved=$false}
   }catch{if($label -eq 'launch'){$script:launchHelperUnresolved=$true};$result.errors+="$label helper cleanup: $($_.Exception.Message)";$result.status='FAIL'}
  }
 }
}
 Coordination
 $build=Child 'dotnet' 'build RebirthUtils.csproj -c Debug --nologo -v:q -p:UseSharedCompilation=false' 'build' 180
 $result.buildExit=$build.exit;if($build.exit -ne 0){throw 'Build failed'}
 $result.dllAfter=(Get-FileHash RebirthUtils.dll).Hash
 Coordination
 if(Get-Process -Name 7DaysToDie -ErrorAction SilentlyContinue){throw 'Game appeared before launch'}
 $expectedGamePath=[IO.Path]::GetFullPath((Join-Path $root '..\..\7DaysToDie.exe')); $launchAt=[datetime]::UtcNow
 $launch=Child 'powershell' '-NoProfile -ExecutionPolicy Bypass -File Tools\GameBridge\gamebridge.ps1 launch -NoBuild -NoWait' 'launch' 40
 Discover-Owned
 if(-not $owned){throw 'Cannot identify unique positively owned game'}
 Get-CimInstance Win32_Process|Where-Object{$_.ProcessId -eq $owned.Id -or $_.ParentProcessId -eq $owned.Id}|Select-Object ProcessId,ParentProcessId,Name,CreationDate,ExecutablePath,CommandLine|ConvertTo-Json -Depth 5|Set-Content (Join-Path $run 'OWNED_TREE.json')
 if($launch.exit -ne 0){throw 'Bridge launch failed'}
 $deadline=$launchAt.AddMinutes(8);$stable=0;$index=0
 while([datetime]::UtcNow -lt $deadline){
  Coordination
  if(-not(Get-Process -Id $owned.Id -ErrorAction SilentlyContinue)){throw 'Owned game crashed/exited during load'}
  $index++;$status=Child 'powershell' '-NoProfile -ExecutionPolicy Bypass -File Tools\GameBridge\gamebridge.ps1 status' ('status'+$index) 15
  $gameState='bridge-unavailable';if($status.exit -eq 0){try{$gameState=($status.output|ConvertFrom-Json).state}catch{}}
  "$( [datetime]::UtcNow.ToString('o')) state=$gameState"|Add-Content (Join-Path $run 'PROGRESS.txt')
  if($gameState -eq 'ingame'){$stable++}else{$stable=0}
  if($stable -ge 2){$result.stableIngame=$true;break}
  Start-Sleep -Seconds 15
 }
 if(-not $result.stableIngame){throw 'Eight minute load deadline reached'}
 $bridgeErrors=Child 'powershell' '-NoProfile -ExecutionPolicy Bypass -File Tools\GameBridge\gamebridge.ps1 errors -Since 0 -Limit 10000' 'bridge_errors' 15
 if($bridgeErrors.exit -ne 0){throw 'Bridge error inspection failed'}
 $parsedErrors=$bridgeErrors.output|ConvertFrom-Json
 if(-not $parsedErrors -or $null -eq $parsedErrors.entries -or $null -eq $parsedErrors.count -or $null -eq $parsedErrors.lastLogSeq -or -not $parsedErrors.gameLog){throw 'Malformed bridge error response'}
 $count=$parsedErrors.count
 if(($count -isnot [int] -and $count -isnot [long]) -or $count -lt 0 -or $count -ne @($parsedErrors.entries).Count){throw 'Invalid bridge error count'}
 if(($parsedErrors.lastLogSeq -isnot [int] -and $parsedErrors.lastLogSeq -isnot [long]) -or $parsedErrors.lastLogSeq -lt 0){throw 'Invalid bridge log sequence'}
 $result.bridgeErrors=@($parsedErrors.entries); if($parsedErrors.count -gt 0 -or @($parsedErrors.entries).Count -gt 0){throw 'Bridge reported load errors'}
 $result.status='PASS'
} catch {$result.status='FAIL';$result.errors+= $_.Exception.Message} finally {
 # Each evidence/finalization task is isolated so failure cannot skip cleanup or release.
 try { Discover-Owned } catch {$result.status='FAIL';$result.errors+='Ownership recovery: '+$_.Exception.Message}
 try {
  if(Is-OwnedAlive){
   try {
    $session=Get-Content Tools/GameBridge/out/session.json -Raw|ConvertFrom-Json
    if($session.pid -eq $owned.Id -and @(Get-Process -Name 7DaysToDie).Count -eq 1){$null=Child 'powershell' '-NoProfile -ExecutionPolicy Bypass -File Tools\GameBridge\gamebridge.ps1 quit' 'quit' 135}
   } catch {$result.quitNote=$_.Exception.Message}
   try {if(Is-OwnedAlive){$current=Get-Process -Id $owned.Id;$null=$current.CloseMainWindow();$null=$current.WaitForExit(10000)}}catch{$result.closeWindowNote=$_.Exception.Message}
   if(Is-OwnedAlive){Stop-Process -Id $owned.Id -Force;$result.forcedTermination=$true;for($i=0;$i -lt 10 -and (Is-OwnedAlive);$i++){Start-Sleep -Milliseconds 500}}
  }
  $result.cleanupExited=-not (Is-OwnedAlive)
  if(-not $result.cleanupExited){throw 'Owned game did not exit'}
 } catch {$result.status='FAIL';$result.errors+='Cleanup: '+$_.Exception.Message}
 try {
  if($owned -and (Test-Path $log)){
   Copy-Item $log (Join-Path $run 'Player.after.log')
   $afterBytes=[IO.File]::ReadAllBytes($log);$beforeBytes=if(Test-Path (Join-Path $run 'Player.before.log')){[IO.File]::ReadAllBytes((Join-Path $run 'Player.before.log'))}else{@()}
   $prefix=$afterBytes.Length -ge $beforeBytes.Length
   if($prefix){for($i=0;$i -lt $beforeBytes.Length;$i++){if($beforeBytes[$i] -ne $afterBytes[$i]){$prefix=$false;break}}}
   $offset=if($prefix){$beforeBytes.Length}else{0}
   $new=[Text.Encoding]::UTF8.GetString($afterBytes,$offset,$afterBytes.Length-$offset)
   $result.logMode=if($prefix){'Appended bytes after recorded baseline'}else{'Replaced/truncated Unity startup log; full file is new segment'}
   $new|Set-Content (Join-Path $run 'Player.new.log')
   $identity=[regex]::Match($new,'(?m)INF Version: V ([0-9.]+) \(b([0-9]+)\)')
   if(-not $identity.Success){throw 'Fresh startup runtime version identity missing'}
   $result.observedTarget='7DTD '+$identity.Groups[1].Value+' b'+$identity.Groups[2].Value
   $result.versionMatch=($identity.Groups[1].Value -eq '3.3.0' -and $identity.Groups[2].Value -eq '18')
   if(-not $result.versionMatch){throw ('Runtime version mismatch: '+$result.observedTarget+' expected 3.3 b18')}
   $matches=@($new -split '\r?\n'|Where-Object{$_ -match '\bERR\b|\bEXC\b|\b\w*Exception\b|\[Error\]'})
   $matches|Set-Content (Join-Path $run 'LOAD_ERRORS.txt');$result.logErrors=$matches
   if($matches.Count){$result.status='FAIL'}
   if(-not $new){throw 'No new startup log segment'}
  }else{$result.logMode='No positively owned launch; load not checked'}
 } catch {$result.status='FAIL';$result.errors+='Log evidence: '+$_.Exception.Message}
 try {
  if($before){$after=Hashes;$after|ConvertTo-Json -Depth 5|Set-Content (Join-Path $run 'SOURCE_AFTER.json');$drift=@(Compare-Object ($before|ForEach-Object{$_.path+' '+$_.sha256}) ($after|ForEach-Object{$_.path+' '+$_.sha256}));$result.sourceDrift=$drift.Count;if($drift.Count){$result.status='FAIL'}}
 } catch {$result.status='FAIL';$result.errors+='Hash evidence: '+$_.Exception.Message}
 $result.finishedUtc=[datetime]::UtcNow.ToString('o')
 try {if($run){$result|ConvertTo-Json -Depth 10|Set-Content (Join-Path $run 'RECEIPT.json')}}catch{[Console]::Error.WriteLine('Receipt write failed: '+$_.Exception.Message)}
 try {if($started){Write-DurableJson $statePath @{lastStartedUtc=$started.ToString('o');run=$run;state=$result.status;finishedUtc=$result.finishedUtc}}}catch{[Console]::Error.WriteLine('State write failed: '+$_.Exception.Message)}
 try {$result|ConvertTo-Json -Depth 10|Set-Content 'E:\_Haven\shared\REBIRTHDashboard\updates\build-load-verifier.json'}catch{[Console]::Error.WriteLine('Dashboard write failed: '+$_.Exception.Message)}
 try {
  $holdPath=Join-Path $base 'LEAD_WRITER_HOLD.json';$holdStream=$null
  try {
   # FileShare.None prevents a competing writer opening/replacing this grant during comparison and flush.
   $holdStream=[IO.File]::Open($holdPath,[IO.FileMode]::Open,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)
   $reader=New-Object IO.StreamReader($holdStream,[Text.Encoding]::UTF8,$true,1024,$true)
   $live=$reader.ReadToEnd()|ConvertFrom-Json;$reader.Dispose()
   if($live.state -eq 'GRANTED' -and $live.owner -eq $hold.owner -and $live.grantedUtc -eq $hold.grantedUtc){
    if(-not $ownershipAmbiguous -and -not $ownershipRecoveryUnresolved -and -not $launchHelperUnresolved -and -not (Is-OwnedAlive)){
     $live.state='RELEASED';$live|Add-Member releasedUtc ([datetime]::UtcNow.ToString('o')) -Force
     $live|Add-Member receiptReference $(if($run){Join-Path $run 'RECEIPT.json'}else{'No test start; preflight aborted'}) -Force
     $bytes=[Text.Encoding]::UTF8.GetBytes(($live|ConvertTo-Json -Depth 10));$holdStream.Position=0;$holdStream.SetLength(0);$holdStream.Write($bytes,0,$bytes.Length);$holdStream.Flush($true)
    }else{[Console]::Error.WriteLine('Hold retained: unresolved helper/game ownership or exit')}
   }
  }finally{if($holdStream){$holdStream.Dispose()}}
 }catch{[Console]::Error.WriteLine('Hold release failed: '+$_.Exception.Message)}
}





