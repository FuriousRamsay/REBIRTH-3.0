[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$Workspace,
    [Parameter(Mandatory=$true)][string]$ClientStage,
    [string]$ServerStage,
    [Parameter(Mandatory=$true)][string]$SaveDirectory,
    [Parameter(Mandatory=$true)][ValidateSet('Rebirth','BaseGame')][string]$Progression,
    [Parameter(Mandatory=$true)][ValidatePattern('^[A-Za-z0-9_-]{1,36}$')][string]$Label,
    [ValidateSet('Navezgane','Pregen06k01','Pregen06k02','Pregen08k01','Pregen08k02','West Xuyofu Territory')][string]$World='Navezgane',
    [ValidateSet('bob','hugh','jen','joel','rekt')][string]$Trader='bob',
    [ValidatePattern('^[A-Za-z0-9_.:-]+$')][string]$ExpectedBlock,
    [switch]$CheckCraftingSearch,
    [string]$SearchText='Combine Station'
)
$ErrorActionPreference='Stop'
$helper=Join-Path $Workspace 'Tools/GameBridge/Invoke-PurgeRegression.ps1'
$preflight=Join-Path $PSScriptRoot 'Get-P29CombinePreflight.ps1'
$fixture=& $preflight -SaveDirectory $SaveDirectory -Progression $Progression -World $World -RequirePurge | ConvertFrom-Json
if ($fixture.status -ne 'PREFLIGHT' -or !$fixture.purge) { throw 'Actual prepared mode and authority config required.' }
if (!$ExpectedBlock) { $ExpectedBlock=$fixture.expectedFreshNativeParent }
function Bridge([string]$stage,[string]$step,[string[]]$request) {
    $raw=& $helper -Stage $stage -Label ("p29-{0}-{1}" -f $Label,$step) -Direct -ArgumentsJson (ConvertTo-Json -InputObject $request -Compress)
    $response=($raw -join [Environment]::NewLine) | ConvertFrom-Json
    if ($response.ok -eq $false -or $response.hadError) { throw "Bridge refused $step." }
    $response
}
function AssertBlock($actual,[int[]]$pos,[bool]$authoritative) {
    if ($actual.authoritative -ne $authoritative -or $actual.block -cne $ExpectedBlock -or
        $actual.x -ne $pos[0] -or $actual.y -ne $pos[1] -or $actual.z -ne $pos[2]) {
        throw "Exact loaded Combine fixture at $($pos -join ',') expected $ExpectedBlock but observed $($actual.block)."
    }
}
$start=Bridge $ClientStage 'client-start' @('state','-Sections','player')
if ($start.state -ne 'ingame' -or !$start.guard.enabled -or $start.player.dead) {
    throw 'Prepared live actor and Guard required.'
}
$owner=(Bridge $ClientStage 'owner-topology' @('call','GET','/auditstate','domain=topology')).domains.topology
if (!$owner.localActor -or !$owner.ownerKey -or !$owner.worldKey -or ($Progression -eq 'Rebirth' -and !$owner.creationId)) {
    throw 'Exact current native local actor required.'
}
$id=[int]$owner.entityId
$authorityStage=$ClientStage
if ($ServerStage) {
    $authorityStage=$ServerStage
    $authority=(Bridge $ServerStage 'server-topology' @('call','GET','/auditstate','domain=topology',"player=$id")).domains.topology
    if (!$owner.remote -or !$owner.connected -or !$authority.dedicated -or !$authority.authoritativePeer -or $authority.remote -or
        $owner.worldKey -ne $authority.worldKey -or $owner.ownerKey -ne $authority.ownerKey -or
        $owner.creationId -ne $authority.creationId -or $authority.entityId -ne $id) { throw 'Native authenticated peer mismatch.' }
} else {
    if ($owner.remote -or $owner.dedicated) { throw 'Solo mode requires a local authority.' }
    $authority=$owner
}
if (!$authority.server -or $authority.gameName -ne $fixture.saveName) { throw 'Current authority does not match the prepared save.' }
$native=Bridge $authorityStage 'native-prefab' @('console',"rbpurgetestobserve player $id")
$lines=@($native.results | ForEach-Object { $_.output } | Where-Object { $_ -is [string] -and $_.StartsWith('{') })
if ($lines.Count -ne 1) { throw 'Exact native Purge observation missing.' }
$live=$lines[0] | ConvertFrom-Json
$prefab=$live.scannerNativePoi
if ($live.purgeEnabled -ne $true -or $live.saveName -ne $fixture.saveName -or $live.player -ne $id -or $prefab.prefab -cne "trader_$Trader") {
    throw 'Actual owning actor must be inside the requested native trader in the same Purge save.'
}
$target=@($fixture.targets | Where-Object {
    $_.prefab -ceq $prefab.prefab -and ($_.origin -join ',') -eq ($prefab.origin -join ',') -and
    ($_.size -join ',') -eq ($prefab.size -join ',')
})
if ($target.Count -ne 1) { throw 'Actual native trader origin or bounds did not match one installed authored target.' }
$position=[int[]]$target[0].parent
$blockQuery=@('call','GET','/blockdamage',"x=$($position[0])","y=$($position[1])","z=$($position[2])")
$hostBlock=Bridge $authorityStage 'block-authority' $blockQuery
AssertBlock $hostBlock $position $true
$clientBlock=$null
if ($ServerStage) {
    $clientBlock=Bridge $ClientStage 'block-client' $blockQuery
    AssertBlock $clientBlock $position $false
}
$searchCount=$null
if ($CheckCraftingSearch) {
    $tree=Bridge $ClientStage 'craft-search' @('uitree')
    $search=@($tree.nodes | Where-Object { $_.id -eq 'rebirthCraftingRecipeSearch' -and $_.input -eq $SearchText })
    if ($search.Count -ne 1) { throw 'Open the actual Crafting search and enter the exact requested visible search text first.' }
    $matches=@($tree.nodes | Where-Object {
        $_.recipe -ceq 'combineStation' -and $_.path -match '/rebirthCraftingRecipeRow[0-9]+(?:$|/)'
    })
    $searchCount=$matches.Count
    if ($searchCount -ne $fixture.effectiveRecipeCount) { throw 'The visible exact native recipe did not match the authority recipe gate.' }
}
$errors=Bridge $ClientStage 'errors' @('errors','-Since',([string]$start.lastLogSeq))
if ($errors.count -ne 0) { throw 'New client errors during Combine observation.' }
[pscustomobject]@{
    status='OBSERVED';player=$id;dedicated=[bool]$ServerStage;mode=$fixture.playerProgression;purge=$live.purgeEnabled
    worldKey=$owner.worldKey;save=$fixture.saveName;prefab=$target[0];effectiveRecipeCount=$fixture.effectiveRecipeCount
    effectiveRecipeSha256=$fixture.effectiveRecipeSha256;snapshotSha256=$fixture.snapshotSha256
    authorityBlock=$hostBlock;clientBlock=$clientBlock;visibleRecipeCount=$searchCount;playerPlacedSameCellQualified=$false
} | ConvertTo-Json -Depth 11
