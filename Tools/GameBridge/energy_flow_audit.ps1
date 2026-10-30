# Test-only observations; no backend, balance, save or guard mutation.
$script:energySupportNames=$null
function GetEnergyQualifiedSupportNames($facts) {
    if($facts.schema -ne 1 -or @($facts.blocks).Count -ne 4 -or @($facts.sourceFiles).Count -lt 2 -or !$facts.gameRoot){throw 'Energy support source qualification unavailable'}
    $currentEntries=@(foreach($mod in Get-ChildItem -LiteralPath (Join-Path $facts.gameRoot 'Mods') -Directory) {
        foreach($kind in 'blocks.xml','materials.xml') {
            $entry=Join-Path $mod.FullName (Join-Path 'Config' $kind)
            if(Test-Path -LiteralPath $entry){(Resolve-Path -LiteralPath $entry).Path}
        }
    })
    if($currentEntries.Count -ne @($facts.patchEntryInventory).Count -or @(Compare-Object @($facts.patchEntryInventory) $currentEntries).Count){throw 'Energy support patch-entry inventory changed'}
    foreach($inputFile in $facts.sourceFiles) {
        if(!(Test-Path -LiteralPath $inputFile.path) -or (Get-FileHash -LiteralPath $inputFile.path).Hash -ne $inputFile.sha256){throw "Energy support source drift: $($inputFile.path)"}
    }
    $names=@($facts.blocks | Where-Object {$_.movementCollision -eq $true -and $_.terrainDecoration -eq $false -and $_.shape -eq 'Terrain'} | ForEach-Object name)
    $expected=@('terrAsphalt','terrForestGround','terrDirt','terrStone')
    if($names.Count -ne 4 -or @(Compare-Object $expected $names).Count){throw 'Energy support type qualification incomplete'}
    return $names
}
function TestEnergySupportBlock([string]$name) {
    if($null -eq $script:energySupportNames) {
        $facts=Get-Content (Join-Path $PSScriptRoot 'energy_support_facts.json') -Raw | ConvertFrom-Json
        $script:energySupportNames=@(GetEnergyQualifiedSupportNames $facts)
    }
    return $script:energySupportNames -ccontains $name
}
function StartEnergyAction([string]$kind,[string]$name,[double]$seconds=0) {
    $s=Step state '-Sections' 'player,stats'
    if(!$s.guard.enabled -or $s.player.dead -or $null -eq $s.lastLogSeq){throw 'Measured Energy actor/guard/log witness unavailable'}
    return @{kind=$kind;name=$name;seconds=$seconds;before=$s;firstStep=$script:step+1;utcStart=[DateTime]::UtcNow.ToString('o')}
}
function CompleteEnergyAction($action,$result) {
    $s=Step state '-Sections' 'player,stats'
    if(!$s.guard.enabled -or $s.player.dead -or $s.player.entityId -ne $action.before.player.entityId){throw 'Measured Energy actor/guard changed'}
    $action.after=$s;$action.result=$result;$action.lastStep=$script:step;$action.utcEnd=[DateTime]::UtcNow.ToString('o')
    $action | ConvertTo-Json -Depth 32 | Set-Content (Join-Path $evidence ("energy-action-$($action.name).json"))
}
function ReadEnergyKillAudit {
    $a=Step call 'GET' '/auditstate' 'domain=soloCombat'
    if($a.authority -ne $true -or $null -eq $a.domains.soloCombat.nativeKills -or $a.domains.soloCombat.available -eq $false){throw 'Existing native owner-kill counter unavailable; do not invent a witness'}
    return $a
}
