$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$legacy = 'C:\Program Files (x86)\Steam\steamapps\common\7 Days To Die 2.6'
[xml]$old = Get-Content -LiteralPath "$legacy\Data\Config\loot.xml" -Raw
[xml]$patch = Get-Content -LiteralPath "$legacy\Mods\zzz_REBIRTH__Core\Config\loot.xml" -Raw
[xml]$current = Get-Content -LiteralPath "$root\..\..\Data\Config\loot.xml" -Raw
$included = @{}
function Add-CurrentDeclarations([string]$path) {
    $path = [IO.Path]::GetFullPath($path)
    if ($included.ContainsKey($path)) { return }
    $included[$path] = $true
    [xml]$document = Get-Content -LiteralPath $path -Raw
    foreach ($node in $document.SelectNodes('//lootcontainer')) {
        if (!$current.SelectSingleNode("/lootcontainers/lootcontainer[@name='$($node.name)']")) {
            $null = $current.DocumentElement.AppendChild($current.ImportNode($node, $true))
        }
    }
    foreach ($include in $document.SelectNodes('//include[@filename]')) {
        Add-CurrentDeclarations (Join-Path (Split-Path -Parent $path) $include.filename)
    }
}
Add-CurrentDeclarations "$root\Config\loot.xml"
$touched = @{}
$missingSelectors = @()
foreach ($operation in $patch.DocumentElement.ChildNodes) {
    if ($operation.LocalName -in 'append','insertBefore','insertAfter') {
        foreach ($container in $operation.SelectNodes('./lootcontainer')) {
            $null = $old.DocumentElement.AppendChild($old.ImportNode($container, $true))
        }
    }
    if ($operation.LocalName -ne 'setattribute' -or $operation.name -ne 'size') { continue }
    $targets = @($old.SelectNodes($operation.xpath))
    if (!$targets.Count) { $missingSelectors += $operation.xpath }
    foreach ($target in $targets) {
        $target.SetAttribute('size', $operation.InnerText.Trim())
        $touched[$target.GetAttribute('name')] = $true
    }
}
$rows = foreach ($name in ($touched.Keys | Sort-Object)) {
    $oldNode = $old.SelectSingleNode("/lootcontainers/lootcontainer[@name='$name']")
    $newNode = $current.SelectSingleNode("/lootcontainers/lootcontainer[@name='$name']")
    [pscustomobject]@{
        Name = $name
        LegacySize = $oldNode.GetAttribute('size')
        CurrentNativeSize = $(if ($newNode) { $newNode.GetAttribute('size') } else { '' })
        Status = $(if ($name -eq 'playerBackpack') { 'Separate player inventory system' }
            elseif (!$newNode) { 'Missing current native name: review mapping' }
            elseif ($oldNode.GetAttribute('size') -eq $newNode.GetAttribute('size')) { 'Already equal' }
            else { 'Named parity patch required' })
    }
}
# Verified same-block replacements: old/current palletBrownBoxes*Closed use
# FuriousRamsayBrownBox/rebirthBrownPalletLoot; decorative wineBarrel* uses
# FuriousRamsay865/rebirthWineBarrelJunk. Other renamed lists require evidence first.
foreach ($pair in @(
    @{ Old='FuriousRamsayBrownBox'; New='rebirthBrownPalletLoot' },
    @{ Old='FuriousRamsay865'; New='rebirthWineBarrelJunk' }
)) {
    $oldNode = $old.SelectSingleNode("/lootcontainers/lootcontainer[@name='$($pair.Old)']")
    $newNode = $current.SelectSingleNode("/lootcontainers/lootcontainer[@name='$($pair.New)']")
    if (!$oldNode -or !$newNode) { throw "Missing verified replacement $($pair.New)" }
    $rows += [pscustomobject]@{Name=$pair.New;LegacySize=$oldNode.GetAttribute('size');CurrentNativeSize=$newNode.GetAttribute('size');Status="Verified block mapping from $($pair.Old)"}
}
$out = Join-Path $root '_Documentation\PlayerReports_20261029'
$rows | Export-Csv -LiteralPath "$out\loot-sizes-26-audit.csv" -NoTypeInformation
[IO.File]::WriteAllText("$out\loot-sizes-26-unmatched-selectors.txt", ($missingSelectors -join "`r`n"))
$lines = @('<configs>', '  <!-- Owner-requested 2.6 capacity parity. Named containers only; player inventory is separate. -->')
foreach ($row in $rows | Where-Object { $_.CurrentNativeSize -and $_.Name -ne 'playerBackpack' }) {
    $lines += "  <setattribute xpath=`"/lootcontainers/lootcontainer[@name='$($row.Name)']`" name=`"size`">$($row.LegacySize)</setattribute>"
}
$lines += '</configs>'
[IO.File]::WriteAllLines("$out\loot_sizes_26.xml", $lines)
# Legacy picked-up storage used *_player and custom-named loot lists. Current generated
# player blocks copy an explicit size from the same source block instead.
[xml]$oldBlocks=Get-Content -LiteralPath "$legacy\Mods\zzz_REBIRTH__Core\Config\blocks.xml" -Raw
[xml]$newBlocks=Get-Content -LiteralPath "$root\..\..\Data\Config\blocks.xml" -Raw
$includedBlocks=@{}
function Add-CurrentBlockDeclarations([string]$path) {
    $path=[IO.Path]::GetFullPath($path)
    if($includedBlocks.ContainsKey($path)){return}
    $includedBlocks[$path]=$true
    [xml]$document=Get-Content -LiteralPath $path -Raw
    # A placeholder's named reference is not a block declaration.
    foreach($node in $document.SelectNodes('//block[@name and property]')) {
        if(!$newBlocks.SelectSingleNode("/blocks/block[@name='$($node.name)']")) {
            $null=$newBlocks.DocumentElement.AppendChild($newBlocks.ImportNode($node,$true))
        }
    }
    foreach($include in $document.SelectNodes('//include[@filename]')) {
        Add-CurrentBlockDeclarations (Join-Path (Split-Path -Parent $path) $include.filename)
    }
}
Add-CurrentBlockDeclarations "$root\Config\blocks.xml"
$playerRows=foreach($row in $rows) {
    foreach($block in $oldBlocks.SelectNodes("//block[property[@name='LootList' and @value='$($row.Name)']]")) {
        # Identify the actual picked-up definition, not a naming convention for
        # its loot list (store shelves and brown pallets use custom list names).
        if($block.name -notlike '*_PickedUp'){continue}
        $source=$block.SelectSingleNode("property[@name='Extends']").value
        $exists=$source -and $newBlocks.SelectSingleNode("/blocks/block[@name='$source']")
        [pscustomobject]@{LegacyList=$row.Name;LegacyBlock=$block.name;CurrentSourceBlock=$source;Size=$row.LegacySize;Status=$(if($exists){'Exact inherited source exists'}else{'Missing source: no guessed mapping'})}
    }
}
# The old picked-up ice machine inherited its natural source's custom LootList.
# It therefore has no direct LootList node for the scan above. Preserve that
# positively verified inheritance chain rather than guessing from its name.
$icePicked=$oldBlocks.SelectSingleNode("//block[@name='cntIceMachine_PickedUp']")
$iceSourceLoot=$oldBlocks.SelectSingleNode("/Config/append[@xpath=""/blocks/block[@name='cntIceMachine']""]/property[@name='LootList']")
if(!$icePicked -or $icePicked.SelectSingleNode("property[@name='LootList']") -or
   $icePicked.SelectSingleNode("property[@name='Extends']").value -ne 'cntIceMachine' -or
   $iceSourceLoot.value -ne 'FuriousRamsayFreezer' -or
   !$newBlocks.SelectSingleNode("/blocks/block[@name='cntIceMachine']")){throw 'Inherited ice-machine mapping drift'}
$iceSize=($rows | Where-Object Name -eq 'FuriousRamsayFreezer').LegacySize
if($iceSize -ne '6,7'){throw 'Inherited ice-machine final legacy size drift'}
$playerRows+= [pscustomobject]@{LegacyList='FuriousRamsayFreezer';LegacyBlock='cntIceMachine_PickedUp';CurrentSourceBlock='cntIceMachine';Size=$iceSize;Status='Exact inherited source exists'}
$playerRows | Export-Csv -LiteralPath "$out\loot-player-size-mapping.csv" -NoTypeInformation
$playerLines=@('<configs>','  <!-- 2.6 picked-up container capacity on the exact inherited source; copied by the current player-block generator. -->')
foreach($group in $playerRows | Where-Object Status -eq 'Exact inherited source exists' | Group-Object CurrentSourceBlock) {
    $sizes=@($group.Group.Size | Sort-Object -Unique)
    if($sizes.Count -ne 1){throw "Conflicting legacy player sizes for $($group.Name)"}
    $playerLines += "  <append xpath=`"/blocks/block[@name='$($group.Name)']`"><property name=`"RebirthContainerSize`" value=`"$($sizes[0])`"/></append>"
}
$playerLines += '</configs>'
[IO.File]::WriteAllLines("$out\loot_player_sizes_26.xml",$playerLines)
# A renamed natural list must be traced through the exact old block operation,
# not inferred from similar names. The wall-safe override applied only to the
# locked block; changing the shared current smallSafes table would affect others.
$safeOperation=$oldBlocks.SelectSingleNode("/Config/setattribute[@xpath=""/blocks/block[@name='cntWallSafe']/property[@name='LootList']"" and @name='value']")
$safeLegacy=$rows | Where-Object Name -eq 'FuriousRamsayGunSafeSmall'
$safeCurrent=$newBlocks.SelectSingleNode("/blocks/block[@name='cntWallSafe']/property[@class='CompositeFeatures']/property[@class='TEFeatureStorage']/property[@name='LootList']")
if(!$safeOperation -or $safeOperation.InnerText.Trim() -ne 'FuriousRamsayGunSafeSmall' -or
   !$safeLegacy -or $safeCurrent.value -ne 'smallSafes') {throw 'Wall-safe exact-block mapping drift'}
@('<configs>',
  '  <!-- Exact 2.6 locked-wall-safe mapping; preserve shared loot rolls and other safe sizes. -->',
  "  <append xpath=""/blocks/block[@name='cntWallSafe']""><property name=""RebirthLootContainerSize"" value=""$($safeLegacy.LegacySize)""/></append>",
  '</configs>') | Set-Content -LiteralPath "$out\loot_wall_safe_26.xml"
$naturalMappings=@(
 [pscustomobject]@{LegacyList='FuriousRamsayFreezer';Block='cntIceMachine';CurrentList='shamwayFreezer';Size='6,7';Mechanism='Existing block-specific natural grid override'},
 [pscustomobject]@{LegacyList='FuriousRamsayGunSafeSmall';Block='cntWallSafe';CurrentList='smallSafes';Size=$safeLegacy.LegacySize;Mechanism='Staged exact locked-block override; native acceptance pending'}
)
$naturalMappings | Export-Csv -LiteralPath "$out\loot-natural-block-mappings.csv" -NoTypeInformation
$rows | Group-Object Status | Select-Object Name,Count | Format-Table -AutoSize
$playerRows | Group-Object Status | Select-Object Name,Count | Format-Table -AutoSize
