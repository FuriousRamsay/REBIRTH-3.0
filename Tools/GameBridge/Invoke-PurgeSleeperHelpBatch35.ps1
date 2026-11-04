param([Parameter(Mandatory=$true)][string]$Stage,[ValidateSet('Force','Start','Status','Stop')][string]$Action='Status',[string]$Label='native-sleeper-help',[switch]$ExpectDenied)
$ErrorActionPreference='Stop';$h=Join-Path $PSScriptRoot 'Invoke-PurgeRegression.ps1'
function B($suffix,$a){$r=& $h -Stage $Stage -Label ($Label+'-'+$suffix) -Direct -ArgumentsJson (ConvertTo-Json -InputObject @($a) -Compress);return (($r -join "`n")|ConvertFrom-Json)}
function Click($suffix,$id){$t=B ($suffix+'-tree') @('uitree');$parent=@($t.nodes|Where-Object id -eq $id);if($parent.Count -ne 1){throw "Expected one observed control $id"};$prefix=$parent[0].path+'/';$n=@($t.nodes|Where-Object {$_.clickable -and ($_.path -eq $parent[0].path -or $_.path.StartsWith($prefix))});if($n.Count -ne 1){throw "Expected one observed clickable for $id"};$null=B ($suffix+'-click') @('click',('path='+$n[0].path))}
function Result($suffix){$t=B ($suffix+'-tree') @('uitree');return (($t.nodes|Where-Object id -eq 'helpResult'|Select-Object -First 1).text)}
$initial=B 'initial-tree' @('uitree');if($initial.openWindows -notcontains 'ingameMenu'){$null=B 'pause' @('key','name=Escape')};Click 'help-open' 'btnRebirthPlayerHelp'
$ready=$false
for($i=0;$i -lt 30;$i++){$t=B ('permission-'+$i) @('uitree');if(@($t.nodes|Where-Object {$_.clickable -and $_.path -match '/helpStatus/'}).Count -eq 1){$ready=$true;break};Start-Sleep -Milliseconds 200}
if($ExpectDenied){
 $access=($t.nodes|Where-Object id -eq 'helpAccess'|Select-Object -First 1).text
 if($ready -or $access -notmatch 'requires permission from this server'){throw 'Expected native remote permission denial not proven'}
 $null=B 'denied-shot' @('screenshot',($Label+'-denied'))
 @{denied=$true;access=$access;scope='Native Help permission reply and hidden action controls; no activation request.'}|ConvertTo-Json|Set-Content (Join-Path $Stage ($Label+'-verdict.json'))
 Click 'close' 'helpClose';return
}
if(-not $ready){throw 'Authenticated Help actions unavailable; retain native permission response'}
Click 'status-before' 'helpStatus';Start-Sleep -Milliseconds 400;$before=Result 'status-before'
if($Action -eq 'Force'){
 Click 'force-first' 'helpForce';$confirmation=Result 'force-confirmation'
 if($confirmation -notmatch 'Click Force again to confirm'){throw 'Native force confirmation missing; no second click'}
 $null=B 'confirmation-shot' @('screenshot',($Label+'-force-confirmation'))
 Click 'force-confirm' 'helpForce'
}elseif($Action -eq 'Stop'){Click 'stop' 'helpStop'}elseif($Action -eq 'Start'){Click 'start' 'helpStart'}
$after=$before
if($Action -ne 'Status'){
 for($i=0;$i -lt 35;$i++){Start-Sleep -Milliseconds 200;$after=Result ('reply-'+$i);if($after -notmatch 'Waiting|pending|Sending' -and $after -ne $confirmation){break}}
 if($Action -in @('Force','Start') -and $after -notmatch 'poisleepers: (started for|already running)'){throw "Native activation not accepted: $after"}
}
$null=B 'result-shot' @('screenshot',($Label+'-result'))
@{before=$before;after=$after;confirmation=$confirmation;action=$Action;scope='Observed native Help clicks and authenticated service reply; no direct handler or synthetic activation.'}|ConvertTo-Json|Set-Content (Join-Path $Stage ($Label+'-verdict.json'))
Click 'close' 'helpClose';$t=B 'closed-tree' @('uitree');if(@($t.nodes|Where-Object {$_.path -like 'ingameMenu/*' -and $_.clickable}).Count){$null=B 'resume' @('key','name=Escape')}
Write-Output $after
