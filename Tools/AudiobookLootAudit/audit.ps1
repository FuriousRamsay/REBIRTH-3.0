$ErrorActionPreference='Stop'
$taskRoot=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$taskBase=Join-Path (Split-Path (Split-Path $taskRoot -Parent) -Parent) 'Data/Config/loot.xml'
$taskAudioGroups=@('rebirthAudiobookCassettes','rebirthAudiobookDiscoveryCassettes')
function Apply-AudioRoutes([xml]$document,[string]$path,[bool]$timed) {
 [xml]$patch=[IO.File]::ReadAllText($path)
 foreach($node in $patch.DocumentElement.ChildNodes){
  if($node.NodeType -ne [Xml.XmlNodeType]::Element){continue}
  if($node.LocalName -eq 'include') {Apply-AudioRoutes $document ([IO.Path]::GetFullPath((Join-Path (Split-Path $path -Parent) $node.GetAttribute('filename')))) $timed;continue}
  if($node.LocalName -eq 'conditional') {
   foreach($branch in $node.ChildNodes){
    if($branch.NodeType -ne [Xml.XmlNodeType]::Element){continue}
    if($branch.GetAttribute('cond') -eq "isoption('RequireTimedReading', 'bool', 'false')") {
     if(!$timed){foreach($remove in $branch.SelectNodes('remove')){foreach($match in @($document.SelectNodes($remove.GetAttribute('xpath')))){$null=$match.ParentNode.RemoveChild($match)}}}
    } elseif($branch.OuterXml -match 'rebirthAudiobook'){throw "Unmodelled audiobook condition in $path"}
   };continue
  }
  if($node.LocalName -eq 'if'){throw "Unsupported direct if patch in $path"}
  if($node.LocalName -eq 'insertBefore') {
   foreach($group in $node.SelectNodes('lootgroup')){
    if($taskAudioGroups -notcontains $group.GetAttribute('name')){continue}
    $target=$document.SelectSingleNode($node.GetAttribute('xpath'));if(!$target){throw "Missing actual group insertion target in $path"}
    $null=$target.ParentNode.InsertBefore($document.ImportNode($group,$true),$target)
   };continue
  }
  if($node.LocalName -eq 'append'){
   foreach($item in $node.SelectNodes('item')){
    if($taskAudioGroups -notcontains $item.GetAttribute('group')){continue}
    $targets=@($document.SelectNodes($node.GetAttribute('xpath')));if(!$targets.Count){throw "Missing actual audio route target in $path"}
    foreach($target in $targets){$null=$target.AppendChild($document.ImportNode($item,$true))}
   };continue
  }
  if($node.LocalName -eq 'remove' -and $node.GetAttribute('xpath') -match 'rebirthAudiobook') {
   foreach($match in @($document.SelectNodes($node.GetAttribute('xpath')))){$null=$match.ParentNode.RemoveChild($match)}
  }
 }
}
foreach($timed in @($true,$false)) {
 [xml]$taskMerged=[IO.File]::ReadAllText($taskBase)
 Apply-AudioRoutes $taskMerged (Join-Path $taskRoot 'Config/_Rebirth/loot.xml') $timed
 $theory=@($taskMerged.SelectNodes("/lootcontainers//item[@group='rebirthAudiobookCassettes']")).Count
 $discovery=@($taskMerged.SelectNodes("/lootcontainers//item[@group='rebirthAudiobookDiscoveryCassettes']")).Count
 if($discovery -ne 0 -or ($timed -and $theory -le 0) -or (!$timed -and $theory -ne 0)){throw "Wrong final routes timed=$timed theory=$theory discovery=$discovery"}
 foreach($group in @('groupBackpacks01','groupNightstand')){
  $count=@($taskMerged.SelectNodes("/lootcontainers/lootgroup[@name='$group']/item[@group='rebirthAudiobookCassettes']")).Count
  if(($timed -and $count -lt 1) -or (!$timed -and $count -ne 0)){throw "Late route policy failed $group timed=$timed"}
 }
 Write-Output "PASS timed=$timed theoryRoutes=$theory discoveryRoutes=$discovery including late backpack/nightstand routes"
}
[xml]$taskWrapper=[IO.File]::ReadAllText((Join-Path $taskRoot 'Config/_Rebirth/loot.xml'))
if($taskWrapper.DocumentElement.LastChild.LocalName -ne 'conditional'){throw 'Audio removal must follow every included distribution'}
[xml]$taskTrader=[IO.File]::ReadAllText((Join-Path $taskRoot 'Config/_Survivor/traders.xml'))
if($taskTrader.SelectNodes("//item[@group='rebirthAudioTraderDiscovery']").Count){throw 'Obsolete empty trader group reference'}
if(!$taskTrader.SelectSingleNode("//trader_item_group[@name='rebirthAudioTrader']/item[@group='rebirthAudioTraderTheory']") -or !$taskTrader.SelectSingleNode("//trader_item_group[@name='rebirthAudioTrader']/item[@group='rebirthAudioTraderMusic']")){throw 'Valid trader audio routes removed'}
Write-Output 'PASS XML audio route audit. Uses actual base groups/include order and selected audio append/remove operations; native patcher/loot selection not executed.'