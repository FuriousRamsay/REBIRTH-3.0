param([Parameter(Mandatory=$true)][string]$Stage,[int]$Count=20,[string]$Prefix='stash')
$ErrorActionPreference='Stop';$h=Join-Path $PSScriptRoot 'Invoke-PurgeRegression.ps1'
function B($label,$a){$r=& $h -Stage $Stage -Label ($Prefix+'-'+$label) -ArgumentsJson (ConvertTo-Json -InputObject @($a) -Compress);return (($r -join "`n")|ConvertFrom-Json)}
$ui=B 'stash-before-ui' @('uitree');$empty=@($ui.nodes|Where-Object {$_.ctrl -eq 'XUiC_RebirthContextSlot' -and $_.path -match '/contextContainerGridSlot\d+$' -and -not $_.item});$sources=@($ui.nodes|Where-Object {$_.ctrl -eq 'XUiC_RebirthContextSlot' -and $_.path -match '/contextBackpackGridSlot\d+$' -and $_.item})
$n=[Math]::Min($Count,[Math]::Min($empty.Count,$sources.Count));if($n -lt 1){throw 'No actual empty container slots'}
$transfers=@()
for($i=0;$i -lt $n;$i++){
 $src=$sources[$i];$dst=$empty[$i]
  $drag=B "stash-$i-drag" @('drag',"from.path=$($src.path)","to.path=$($dst.path)")
 if($drag.stillHeld){$null=B "stash-$i-return-remainder" @('click',"path=$($src.path)")}
 $transfers+=@{item=$src.item.name;count=$src.item.count;from=$src.path;to=$dst.path}
}
$after=B 'stash-after-ui' @('uitree')
foreach($t in $transfers){$target=@($after.nodes|Where-Object {$_.path -eq $t.to});$source=@($after.nodes|Where-Object {$_.path -eq $t.from});$remaining=0;if($source.Count -eq 1 -and $source[0].item){if($source[0].item.name -ne $t.item){throw "Unexpected source replacement"};$remaining=[int]$source[0].item.count};if($target.Count -ne 1 -or $target[0].item.name -ne $t.item -or ([int]$target[0].item.count+$remaining) -ne $t.count){throw "Native stash conservation mismatch: $($t.item)"}}
$transfers|ConvertTo-Json -Depth 5|Set-Content (Join-Path $Stage ($Prefix+'-transfers.json'))
$null=B 'stash-close' @('key','name=Escape')
@{movedOrPartiallyMoved=$n;conservedCounts=$true}|ConvertTo-Json


