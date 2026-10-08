param([string]$Root = (Resolve-Path "$PSScriptRoot/../..").Path)
$ErrorActionPreference='Stop'
$d=[xml][IO.File]::ReadAllText((Join-Path $Root 'Config/XUi_InGame/quest_turnin_workspace.xml'))
function Assert($ok,$message){if(!$ok){throw $message}}
$r=$d.SelectSingleNode("//window[@name='rebirthQuestTurnInRoot']")
Assert ($r.width -eq '1872' -and $r.height -eq '850') 'Wide root dimensions changed'
$choices=$r.SelectNodes(".//grid[@name='gridOptions']/rect[@controller='QuestTurnInEntry']")
Assert ($choices.Count -eq 7) 'Seven native reward choices required'
foreach($e in $choices){Assert ($e.width -eq '754') 'Choice width mismatch'}
Assert ($r.SelectSingleNode(".//rect[@name='rectAccept']/rect[@name='btnAccept']/button[@name='clickable']") -ne $null) 'Native claim lookup broken'
foreach($id in @('itemActions','vendorItemActions','parts','statButton','descriptionButton','itemPreview')){
 Assert ($r.SelectSingleNode(".//rect[@name='questItemInfo']//*[@name='$id']") -ne $null) "Missing native item-info child $id"
}
Assert ($r.SelectSingleNode(".//rect[@name='questItemInfo']").controller -eq 'RebirthQuestDetails, RebirthUtils') 'Unscoped item details'
Assert ($r.SelectSingleNode(".//grid[@name='inventory']/item_stack").controller -eq 'RebirthQuestInventorySlot, RebirthUtils') 'Unscoped backpack inspection'
Assert (!$d.OuterXml.Contains('$'+'{')) 'Unexpanded template parameter'
$x=[xml][IO.File]::ReadAllText((Join-Path $Root 'Config/XUi_InGame/xui.xml'))
Assert ($x.SelectSingleNode("//setattribute[@xpath=""/xui/window_group[@name='questTurnIn']"" and @name='controller']").InnerText -eq 'RebirthQuestTurnInWorkspace, RebirthUtils') 'Native quest group not routed'
Assert ($x.SelectSingleNode("//setattribute[@xpath=""/xui/window_group[@name='questTurnIn']"" and @name='open_backpack_on_open']").InnerText -eq 'false') 'Separate backpack would overlay the workspace'
$local=[IO.File]::ReadAllText((Join-Path $Root 'Config/Localization.csv'))
foreach($key in @('xuiRebirthRewardDetails','xuiRebirthRewardSelectHelp')){Assert ($local.Contains($key+',')) "Missing localization $key"}
[void]([IO.File]::ReadAllText((Join-Path $Root 'Tools/GameBridge/tests/quest_turnin_workspace.json')) | ConvertFrom-Json)
'PASS: quest turn-in authored structure, native child paths, scoped controllers, localization and scenario JSON. Does not prove runtime rendering or native claims.'