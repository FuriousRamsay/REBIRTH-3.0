param()
$ErrorActionPreference='Stop'
$root=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
[xml]$native=Get-Content (Join-Path $root '../../Data/Config/loot.xml') -Raw
[xml]$patch=Get-Content (Join-Path $root 'Config/_Rebirth/toolbox_loot.xml') -Raw
function Assert($condition,$reason){if(!$condition){throw $reason}}
$before=@{}
foreach($name in @('groupToolbox','groupRollingToolbox')){$node=$native.SelectSingleNode("/lootcontainers/lootgroup[@name='$name']");Assert ($null -ne $node) "native group $name missing";Assert ($node.count -eq 'all') 'count all required';$before[$name]=@($node.SelectNodes('item')).Count}
foreach($op in $patch.configs.ChildNodes){if($op.NodeType -ne [Xml.XmlNodeType]::Element){continue};Assert ($op.Name -eq 'append') 'unsupported test operation';$targets=@($native.SelectNodes($op.xpath));Assert ($targets.Count -eq 1) "expected one target $($op.xpath)";foreach($item in $op.ChildNodes){if($item.NodeType -eq [Xml.XmlNodeType]::Element){[void]$targets[0].AppendChild($native.ImportNode($item,$true))}}}
[xml]$items=Get-Content (Join-Path $root '../../Data/Config/items.xml') -Raw
[xml]$legacy=Get-Content (Join-Path $root 'Config/_Survivor/starter_legacy_items.xml') -Raw
$group=$native.SelectSingleNode("/lootcontainers/lootgroup[@name='rebirthToolboxImportantTools']")
Assert ($group.count -eq '1' -and $group.item.Count -eq 4) 'single weighted tool reward'
foreach($item in $group.item){$id=$item.name;Assert (($null -ne $items.SelectSingleNode("/items/item[@name='$id']")) -or ($null -ne $legacy.SelectSingleNode("/configs/append/item[@name='$id']"))) "missing item $id";Assert ($item.count -eq '1') 'single item count'}
foreach($name in @('groupToolbox','groupRollingToolbox')){$node=$native.SelectSingleNode("/lootcontainers/lootgroup[@name='$name']");Assert (@($node.SelectNodes('item')).Count -eq ($before[$name]+1)) 'native contents preserved';$roll=$node.SelectSingleNode("item[@group='rebirthToolboxImportantTools']");Assert ($roll.force_prob -eq 'true') 'independent roll';$expected=if($name -eq 'groupToolbox'){'0.20'}else{'0.35'};Assert ($roll.prob -eq $expected) 'roll probability'}
foreach($name in @('toolBox','rollingToolBox')){Assert ($null -ne $native.SelectSingleNode("/lootcontainers/lootcontainer[@name='$name']/item")) 'native container root missing'}
[xml]$entry=Get-Content (Join-Path $root 'Config/loot.xml') -Raw
Assert ($null -ne $entry.SelectSingleNode('/configs/conditional/if[@cond="character_progression(''Rebirth'')"]/include[@filename="_Rebirth/loot.xml"]')) 'REBIRTH conditional include missing'
[xml]$rebirth=Get-Content (Join-Path $root 'Config/_Rebirth/loot.xml') -Raw
Assert (@($rebirth.SelectNodes('/configs/include[@filename="toolbox_loot.xml"]')).Count -eq 1) 'toolbox include exactly once'
foreach($file in @('Config/_Survivor/loot.xml','Config/_Rebirth/progression_loot.xml')){[xml]$doc=Get-Content (Join-Path $root $file) -Raw;Assert (@($doc.SelectNodes('//item[@name="rebirthManualForgeTools"]')).Count -eq 0) 'retired manual still distributed'}
'PASS installed XML + scoped append assertions: supported IDs, native contents, count-all independent rolls, one-item weighted group, REBIRTH gate, retired manual routes. Not a full native config merge or runtime loot sample.'