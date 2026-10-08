param([string]$NativeQuests = '..\..\Data\Config\quests.xml')
$ErrorActionPreference='Stop'
$root=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
if (-not [IO.Path]::IsPathRooted($NativeQuests)) { $NativeQuests=Join-Path $root $NativeQuests }
[xml]$native=Get-Content -LiteralPath $NativeQuests -Raw
[xml]$patch=Get-Content -LiteralPath (Join-Path $root 'Config/_Rebirth/quests.xml') -Raw
foreach($op in $patch.configs.ChildNodes) {
 if($op.NodeType -ne [Xml.XmlNodeType]::Element){continue}
 $nodes=@($native.SelectNodes($op.GetAttribute('xpath')))
 foreach($node in $nodes) {
  switch($op.LocalName) {
   'remove' { [void]$node.ParentNode.RemoveChild($node) }
   'set' { $node.Value=$op.InnerText }
   'setattribute' { $node.SetAttribute($op.GetAttribute('name'),$op.InnerText) }
   'append' { foreach($child in $op.ChildNodes) { [void]$node.AppendChild($native.ImportNode($child,$true)) } }
   default { throw "Unsupported operation $($op.Name)" }
  }
 }
}
$routes=@(@(2,'Bob','desert','rekt'),@(3,'Hugh','snow','bob'),@(4,'Joel','wasteland','hugh'),@(5,'Jen','burnt','joel'))
$result=foreach($route in $routes) {
 $tier=$route[0]; $who=$route[1]; $biome=$route[2]; $giver=$route[3]
 $quest=$native.SelectSingleNode("/quests/quest[@id='tier${tier}_nexttrader']")
 if($quest.SelectSingleNode("objective[@type='Goto']/property[@name='location_tag']").value -cne "Trader$who") {throw "Wrong tier $tier destination"}
 if($quest.SelectSingleNode("objective[@type='Goto']/property[@name='location_name']").value -cne "trader_$($who.ToLowerInvariant())") {throw "Wrong destination location"}
 if($quest.SelectSingleNode("property[@name='offer_key']").value -cne "quest_next_trader_$biome") {throw "Wrong biome offer"}
 $entries=@($native.SelectNodes("/quests/quest_list/quest[@id='tier${tier}_nexttrader']"))
 if($entries.Count -ne 1 -or $entries[0].ParentNode.id -cne "trader_${giver}_quests") {throw "Wrong introduction giver at tier $tier"}
 [pscustomobject]@{tier=$tier;target=$who;giver=$giver;biome=$biome;pass=$true}
}
$source=Get-Content -LiteralPath (Join-Path $root 'Scripts/TraderJobs/RebirthTraderJobs.cs') -Raw
$start=$source.IndexOf('public static class RebirthTraderQuestOfferAcceptPreflightPatch')
$end=$source.IndexOf('public static class RebirthTraderQuestOfferReturnToListPatch',$start)
$prefix=$source.Substring($start,$end-$start)
$special=$prefix.IndexOf('!string.IsNullOrEmpty(q.QuestClass.QuestType)')
$normal=$prefix.IndexOf('RebirthTraderOfferSnapshotService.Validate')
if($special -lt 0 -or $normal -le $special) {throw 'Special acceptance exemption absent or too late'}
[pscustomobject]@{scope='Applied native XML and source ordering only; no build/game qualification';routes=$result;specialAcceptExemption=$true;timestampUtc=[DateTime]::UtcNow.ToString('o')} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $PSScriptRoot 'SOURCE_CHECK.json')
$result | ConvertTo-Json

