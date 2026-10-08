$ErrorActionPreference='Stop'
$root='C:\Program Files (x86)\Steam\steamapps\common\7 Days To Die\Mods\zzz_REBIRTH__3_0'
Set-Location -LiteralPath $root
$base=Join-Path $root 'Tools\BuildLoadVerifier'
$statePath=Join-Path $base 'STATE.json'
$hold=Get-Content (Join-Path $base 'LEAD_WRITER_HOLD.json') -Raw|ConvertFrom-Json
if($hold.state -ne 'GRANTED'){throw 'No granted hold'}
if(Test-Path $statePath){$previous=Get-Content $statePath -Raw|ConvertFrom-Json;if($previous.lastStartedUtc -and ([datetime]::UtcNow-[datetime]$previous.lastStartedUtc).TotalMinutes -lt 30){throw 'Minimum 30 minute interval not met'}}
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
@{lastStartedUtc=$started.ToString('o');run=$run;state='RUNNING'}|ConvertTo-Json|Set-Content $statePath
$result=[ordered]@{startedUtc=$started.ToString('o');run=$run;target='7DTD 3.2 b10';save=$cfg.save;status='FAIL';gameplay='NOT RUN';ownedProcess=$null;buildExit=$null;stableIngame=$false;errors=@();cleanupExited=$false}
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
 $out=Join-Path $run ($label+'.out.txt');$err=Join-Path $run ($label+'.err.txt')
 $child=Start-Process -FilePath $exe -ArgumentList $arguments -WindowStyle Hidden -PassThru -RedirectStandardOutput $out -RedirectStandardError $err
  $null=$child.Handle; if(-not $child.WaitForExit($seconds*1000)){Stop-Process -Id $child.Id -Force;throw "$label watchdog timeout"}
 $child.Refresh();return @{exit=$child.ExitCode;output=(Get-Content $out -Raw -ErrorAction SilentlyContinue);error=(Get-Content $err -Raw -ErrorAction SilentlyContinue)}
}
try {
 Coordination
 $build=Child 'dotnet' 'build RebirthUtils.csproj -c Debug --nologo -v:q -p:UseSharedCompilation=false' 'build' 180
 $result.buildExit=$build.exit;if($build.exit -ne 0){throw 'Build failed'}
 $result.dllAfter=(Get-FileHash RebirthUtils.dll).Hash
 Coordination
 if(Get-Process -Name 7DaysToDie -ErrorAction SilentlyContinue){throw 'Game appeared before launch'}
 $launchAt=[datetime]::UtcNow
 $launch=Child 'powershell' '-NoProfile -ExecutionPolicy Bypass -File Tools\GameBridge\gamebridge.ps1 launch -NoBuild -NoWait' 'launch' 40
 $candidates=@(Get-CimInstance Win32_Process -Filter "Name='7DaysToDie.exe'"|Where-Object{$_.CreationDate.ToUniversalTime() -ge $launchAt.AddSeconds(-1) -and $_.CommandLine -like '*-GameName=CodexTest*' -and $_.CommandLine -like '*-rebirthbridge=8765*'})
 if($candidates.Count -ne 1){throw 'Cannot identify unique owned game'}
 $owned=Get-Process -Id $candidates[0].ProcessId
 $ownedStart=$owned.StartTime.ToUniversalTime();$ownedPath=$owned.Path
 $result.ownedProcess=@{pid=$owned.Id;startUtc=$ownedStart.ToString('o');path=$ownedPath;parentPid=$candidates[0].ParentProcessId;commandLine=$candidates[0].CommandLine}
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
 $result.status='PASS'
} catch {$result.errors+= $_.Exception.Message} finally {
 if($owned){
  $current=Get-Process -Id $owned.Id -ErrorAction SilentlyContinue
  if($current -and $current.StartTime.ToUniversalTime() -eq $ownedStart -and $current.Path -eq $ownedPath){
   try{$session=Get-Content Tools/GameBridge/out/session.json -Raw|ConvertFrom-Json;if($session.pid -eq $owned.Id -and @(Get-Process -Name 7DaysToDie).Count -eq 1){$null=Child 'powershell' '-NoProfile -ExecutionPolicy Bypass -File Tools\GameBridge\gamebridge.ps1 quit' 'quit' 25}}catch{$result.quitNote=$_.Exception.Message}
   $current=Get-Process -Id $owned.Id -ErrorAction SilentlyContinue
   if($current -and $current.StartTime.ToUniversalTime() -eq $ownedStart){$null=$current.CloseMainWindow();if(-not $current.WaitForExit(10000)){ $current=Get-Process -Id $owned.Id -ErrorAction SilentlyContinue;if($current -and $current.StartTime.ToUniversalTime() -eq $ownedStart -and $current.Path -eq $ownedPath){Stop-Process -Id $current.Id -Force;$result.forcedTermination=$true}}}
  }
  $result.cleanupExited=-not [bool](Get-Process -Id $owned.Id -ErrorAction SilentlyContinue)
 }
 if($owned -and (Test-Path $log)){Copy-Item $log (Join-Path $run 'Player.after.log');$new=Get-Content $log -Raw;$result.logMode='new Unity launch log (normally truncated on startup)';$new|Set-Content (Join-Path $run 'Player.new.log');$matches=@(Select-String -Path (Join-Path $run 'Player.new.log') -Pattern '\bERR\b|\bEXC\b|Exception|\[Error\]'|ForEach-Object{$_.Line});$matches|Set-Content (Join-Path $run 'LOAD_ERRORS.txt');$result.logErrors=$matches;if($matches.Count){$result.status='FAIL'}}
 $after=Hashes;$after|ConvertTo-Json -Depth 5|Set-Content (Join-Path $run 'SOURCE_AFTER.json');$drift=@(Compare-Object ($before|ForEach-Object{$_.path+' '+$_.sha256}) ($after|ForEach-Object{$_.path+' '+$_.sha256}));$result.sourceDrift=$drift.Count;if($drift.Count){$result.status='FAIL'}
 if($owned -and -not $result.cleanupExited){$result.status='FAIL'}
 $result.finishedUtc=[datetime]::UtcNow.ToString('o');$result|ConvertTo-Json -Depth 8|Set-Content (Join-Path $run 'RECEIPT.json')
 @{lastStartedUtc=$started.ToString('o');run=$run;state=$result.status;finishedUtc=$result.finishedUtc}|ConvertTo-Json|Set-Content $statePath
 $result|ConvertTo-Json -Depth 8|Set-Content 'E:\_Haven\shared\REBIRTHDashboard\updates\build-load-verifier.json'
}

