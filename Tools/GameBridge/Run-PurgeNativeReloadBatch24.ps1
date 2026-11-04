param([Parameter(Mandatory=$true)][string]$Stage,[ValidateSet('Pause','Exit','Menu','Continue')][string]$Phase='Pause',[string]$ButtonId)
$ErrorActionPreference='Stop';$h=Join-Path $PSScriptRoot 'Invoke-PurgeRegression.ps1'
function B($label,$a){$r=& $h -Stage $Stage -Label ('world-reload-'+$Phase+'-'+$label) -Direct -ArgumentsJson (ConvertTo-Json -InputObject @($a) -Compress);return (($r -join "`n")|ConvertFrom-Json)}
if($Phase -eq 'Pause'){$null=B 'surroundings' @('surroundings');$null=B 'open' @('key','name=Escape')}
if($Phase -in @('Exit','Continue')){if(-not $ButtonId){throw 'Require exact observed native button ID'};$null=B 'click' @('click',"id=$ButtonId")}
$null=B 'ui' @('uitree');$null=B 'status' @('status')
