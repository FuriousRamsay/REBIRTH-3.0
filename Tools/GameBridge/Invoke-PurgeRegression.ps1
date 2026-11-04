param([Parameter(Mandatory=$true)][string]$Stage,[Parameter(Mandatory=$true)][string]$Label,[Parameter(Mandatory=$true)][string]$ArgumentsJson,[switch]$Direct)
$ErrorActionPreference='Stop'
$control=Get-Content -LiteralPath (Join-Path $Stage 'control.private.json') -Raw|ConvertFrom-Json
$env:REBIRTH_BRIDGE_RUN_TOKEN=$control.runToken
$argList=@(ConvertFrom-Json $ArgumentsJson)
if($Direct){
 $session=Get-Content -LiteralPath $control.session -Raw|ConvertFrom-Json
 if($session.pid -ne $control.pid -or -not(Get-Process -Id $control.pid -ErrorAction SilentlyContinue)){throw 'Pinned Bridge process is absent or mismatched'}
 $headers=@{'X-Bridge-Token'=$session.token;'X-Bridge-Run'=$control.runToken}
 $method='POST';$path='/';$query=@{};$body=$null;$command=[string]$argList[0];$start=1
 $routes=@{status=@('GET','/ping');surroundings=@('GET','/surroundings');uitree=@('GET','/ui/tree');key=@('POST','/key');select=@('POST','/select');press=@('POST','/press');give=@('POST','/give');walkto=@('POST','/walkto');activate=@('POST','/activate');click=@('POST','/ui/click');hover=@('POST','/ui/hover');drag=@('POST','/ui/drag');restore=@('POST','/restore');blockdamage=@('GET','/blockdamage');setblock=@('POST','/setblock');entities=@('GET','/entities');fight=@('POST','/fight');pois=@('GET','/pois');lookat=@('POST','/lookat')}
 switch($command){
  'console'{$path='/console';$body=($argList|Select-Object -Skip 1)-join "`n";$start=$argList.Count}
  'state'{$method='GET';$path='/state';$query.sections=($argList|Select-Object -Skip 2)-join ',';$start=$argList.Count}
  'screenshot'{$path='/screenshot';$query.name=$argList[1];$start=$argList.Count}
  'errors'{$method='GET';$path='/log';$query.since=$argList[2];$query.level='error';$query.limit=1000;$start=$argList.Count}
  'call'{$method=[string]$argList[1];$path=[string]$argList[2];$start=3}
  default{if(-not $routes.ContainsKey($command)){throw "Unmapped direct Bridge command: $command"};$method=$routes[$command][0];$path=$routes[$command][1]}
 }
 for($i=$start;$i -lt $argList.Count;$i++){$parts=([string]$argList[$i]).Split('=',2);if($parts.Count -ne 2){throw 'Direct Bridge requires key=value arguments'};$query[$parts[0]]=$parts[1]}
 $pairs=@(foreach($key in $query.Keys){[uri]::EscapeDataString([string]$key)+'='+[uri]::EscapeDataString([string]$query[$key])})
 $uri="http://127.0.0.1:$($session.port)$path";if($pairs.Count){$uri+='?'+($pairs -join '&')}
 try{
  $opts=@{Method=$method;Uri=$uri;Headers=$headers;TimeoutSec=300;ContentType='text/plain; charset=utf-8'};if($null -ne $body){$opts.Body=[Text.Encoding]::UTF8.GetBytes($body)}
  $parsed=Invoke-RestMethod @opts
  $result=$parsed|ConvertTo-Json -Depth 100 -Compress
  $result|Set-Content -LiteralPath (Join-Path $Stage ($Label+'.json'))
 }catch{if($_.ErrorDetails.Message){$_.ErrorDetails.Message|Set-Content -LiteralPath (Join-Path $Stage ($Label+'.json'))};throw}
 if($parsed.ok -eq $false){throw "Bridge refused request; inspect saved $Label"}
 $result;return
}
$result=& powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'gamebridge.ps1') @argList -SessionPath $control.session
$exitCode=$LASTEXITCODE
$result|Set-Content -LiteralPath (Join-Path $Stage ($Label+'.json'))
if($exitCode){throw "Bridge exit $exitCode; inspect saved $Label before any retry."}
$parsed=($result -join "`n")|ConvertFrom-Json
if($parsed.PSObject.Properties['ok'] -and -not $parsed.ok){throw "Bridge refused request; inspect saved $Label."}
$result
