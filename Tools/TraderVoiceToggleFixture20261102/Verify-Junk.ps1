param([Parameter(Mandatory=$true)][string]$NativeLootDump,[Parameter(Mandatory=$true)][string]$Report,[switch]$NativeFinal)
$ErrorActionPreference='Stop'
$mod=(Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
[xml]$loot=Get-Content -LiteralPath $NativeLootDump
[xml]$registry=Get-Content -LiteralPath (Join-Path $mod 'Config/_Survivor/literature.xml')
[xml]$distribution=Get-Content -LiteralPath (Join-Path $mod 'Config/_Survivor/literature_distribution.xml')
$reading=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
foreach($n in $registry.SelectNodes('/survivor_literature/item')){$null=$reading.Add($n.id)}
foreach($n in $distribution.SelectNodes('//tier/item')){$null=$reading.Add($n.id)}
[xml]$audio=Get-Content -LiteralPath (Join-Path $mod 'Config/_Survivor/audiobooks.xml')
foreach($n in $audio.SelectNodes('//audiobook[@item_id]')){$null=$reading.Add($n.item_id)}
$groups=@{};foreach($n in $loot.SelectNodes('/lootcontainers/lootgroup')){if($groups.ContainsKey($n.name)){throw "Duplicate group $($n.name)"};$groups[$n.name]=$n}
$roots=@($loot.SelectNodes('/lootcontainers/*')|Where-Object {$_.name -match '^groupJunk|Junk$|^junk|^dumpster$'})
function Find-Reading($root){
    $hits=[Collections.Generic.List[object]]::new();$queue=[Collections.Generic.Queue[object]]::new()
    $queue.Enqueue(@{node=$root;path=@($root.name);seen=@{}})
    while($queue.Count){$entry=$queue.Dequeue();foreach($item in $entry.node.SelectNodes('./item')){
        if($item.HasAttribute('name') -and $reading.Contains($item.name)){$hits.Add([pscustomobject]@{item=$item.name;path=(@($entry.path)+$item.name) -join ' -> '})}
        if($item.HasAttribute('group')){
            $name=$item.group;if($entry.seen.ContainsKey($name)){continue}
            if(-not $groups.ContainsKey($name)){throw "Unresolved group $name"}
            $seen=$entry.seen.Clone();$seen[$name]=$true
            $queue.Enqueue(@{node=$groups[$name];path=@($entry.path)+$name;seen=$seen})
        }
    }}
    return $hits.ToArray()
}
$before=@(foreach($root in $roots){Find-Reading $root})
if(-not $NativeFinal -and $before.Count -eq 0){throw 'Baseline did not reproduce junk literature route.'}
[xml]$source=Get-Content -LiteralPath (Join-Path $mod 'Config/_Survivor/loot.xml')
if($source.SelectNodes("/configs/append[@xpath=`"/lootcontainers/lootgroup[@name='groupJunk']`"]").Count){throw 'Source still appends learning materials to junk.'}
if($distribution.survivor_literature_distribution.policy.junk_loot -ne 'false'){throw 'Metadata still authorizes junk literature.'}
$edge=$groups['groupJunk'].SelectSingleNode("item[@group='rebirthLiteratureDistribution']")
if(-not $NativeFinal){
if($null -eq $edge){throw 'Expected exact before-change runtime edge absent.'}
$null=$edge.ParentNode.RemoveChild($edge)
[xml]$cleanup=Get-Content -LiteralPath (Join-Path $mod 'Config/_Rebirth/loot_skill_tag_cleanup.xml')
foreach($operation in $cleanup.configs.remove){foreach($node in @($loot.SelectNodes($operation.xpath))){$null=$node.ParentNode.RemoveChild($node)}}
}
$after=@(foreach($root in $roots){Find-Reading $root})
$bookcase=@(Find-Reading $groups['groupBookcase01'])
if($after.Count){throw ('REBIRTH reading remains reachable from junk: '+$after[0].path)}
if($bookcase.Count -eq 0){throw 'Designated bookcase route lost.'}
[pscustomobject]@{nativeDump=$NativeLootDump;dumpSHA256=(Get-FileHash -LiteralPath $NativeLootDump).Hash;registeredReading=$reading.Count;junkRoots=$roots.Count;beforePaths=$before.Count;afterPaths=$after.Count;bookcasePaths=$bookcase.Count;beforeExamples=@($before|Select-Object -First 5);nativeFinal=[bool]$NativeFinal;scope=$(if($NativeFinal){'Actual regenerated native loot dump, no transformations applied. Conservative graph includes all registered physical/audio reading paths.'}else{'Conservative prior native dump with exact source removal operations applied; future runtime regeneration not verified. Conditional cleanup leaves BaseGame native book routes intact.'})}|ConvertTo-Json -Depth 5|Set-Content -LiteralPath $Report
Get-Content -LiteralPath $Report -Raw
