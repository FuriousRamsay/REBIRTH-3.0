# Collapse the generated station variants into one shared template and real optional native controls.
[xml]$windows=Get-Content Config/XUi_InGame/station_workspace.xml -Raw
$templates=New-Object xml;$templates.LoadXml('<configs><append xpath="/templates"><rebirth_station_core><rect name="stationCore" width="1872" height="813"/></rebirth_station_core></append></configs>')
$core=$templates.SelectSingleNode('//rebirth_station_core/rect')
$basic=$windows.SelectSingleNode('//window[@name="rebirthStationRootBasic"]')
foreach($child in $basic.ChildNodes){[void]$core.AppendChild($templates.ImportNode($child,$true))}
foreach($root in $windows.SelectNodes('//window')){
 $extra=@()
 foreach($n in @($root.SelectNodes('.//rect[@name="windowFuel" or @name="windowToolsForge" or @name="windowForgeInput"]'))){$clone=$n.CloneNode($true);if($n.ParentNode.GetAttribute('name') -eq 'station'){$pos=$n.GetAttribute('pos').Split(',');$clone.SetAttribute('pos',"$([int]$pos[0]+1364),$($pos[1])")};$extra+=$clone}
 foreach($child in @($root.ChildNodes)){[void]$root.RemoveChild($child)}
 [void]$root.AppendChild($windows.CreateElement('rebirth_station_core'))
 foreach($n in $extra){[void]$root.AppendChild($n)}
}
$templates.Save((Join-Path (Get-Location) 'Config/XUi_InGame/station_templates.xml'))
$windows.Save((Join-Path (Get-Location) 'Config/XUi_InGame/station_workspace.xml'))
