param([Parameter(Mandatory=$true)][string]$Comparison,[double]$MinimumMs=33)
$ErrorActionPreference='Stop'
# Use external Present gaps: the Unity observation can be recorded after the
# long frame, so its endpoint is not always the exact interval to inspect.
$phases=Get-Content -LiteralPath $Comparison -Raw|ConvertFrom-Json
$results=@(foreach($phase in $phases){
    $profile=Get-Content -LiteralPath $phase.profile -Raw|ConvertFrom-Json
    if(!$profile.timeline.enabled){continue}
    $stacks=@{}
    foreach($stack in $profile.stacks){if($stack.key){$stacks[$stack.key]=$stack}}
    foreach($gap in $phase.dxgiWorstFrames|Where-Object {$_.ms -ge $MinimumMs}){
        $end=([DateTime]::Parse($gap.utc).ToUniversalTime()).ToFileTimeUtc()
        $start=$end-[long]($gap.ms*10000)
        $samples=@($profile.timeline.samples|Where-Object {[long]$_[0] -ge $start -and [long]$_[0] -le $end})
        $groups=@($samples|Group-Object {$_[1]}|Sort-Object Count -Descending)
        $gc=@($profile.gc.events|Where-Object {[long]$_[0] -le $end -and ([long]$_[0]+[long]$_[1]*10) -ge $start})
        [pscustomobject]@{
            phase=$phase.label;endUtc=$gap.utc;gapMs=$gap.ms;samples=$samples.Count
            gc=$gc
            runtimeSuspensions=@(foreach($pause in $profile.runtimeSuspensions.events){
                if(!$pause){continue}
                $pauseStart=[long]$pause[0];$pauseEnd=$pauseStart+[long]$pause[1]*10
                if($pauseStart -le $end -and $pauseEnd -ge $start){
                    [pscustomobject]@{
                        startUtc=[DateTime]::FromFileTimeUtc($pauseStart).ToString('o')
                        durationMs=[double]$pause[1]/1000
                        overlapMs=([Math]::Min($pauseEnd,$end)-[Math]::Max($pauseStart,$start))/10000.0
                    }
                }
            })
            collectionCount=$profile.heap.collections
            timedCollectionCount=$profile.gc.count
            gcTimingIncomplete=($profile.heap.collections -gt $profile.gc.count)
            gcCallbackRegistered=$profile.gc.callbackRegistered
            gcCallbacks=$profile.gc.callbacks
            stacks=@(foreach($group in $groups|Select-Object -First 8){
                $stack=$stacks[$group.Name]
                [pscustomobject]@{samples=$group.Count;key=$group.Name;frames=if($stack){
                    @($stack.f|ForEach-Object {$profile.methods[[int]$_].n})
                }elseif($group.Name -eq '0000000000000000'){@('No managed stack recovered')}
                else{@('Stack unavailable in snapshot')}}
            })
        }
    }
})
ConvertTo-Json -InputObject $results -Depth 7
