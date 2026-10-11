param()
$ErrorActionPreference='Stop'
$root=(Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
[xml]$legacy=Get-Content "$root/../../../7 Days To Die 2.6/Mods/zzz_REBIRTH__Core/Config/recipes.xml" -Raw
[xml]$current=Get-Content "$root/_Documentation/CraftingAudit_20261010/EFFECTIVE_recipes_rebirth.xml" -Raw
$rows=foreach($op in $legacy.SelectNodes('//setattribute[@name="craft_tool"]')){
 foreach($r in $current.SelectNodes($op.xpath)){
  [pscustomobject]@{Recipe=$r.name;Station=$r.craft_area;CurrentTool=$r.craft_tool;LegacyTool=$op.InnerText;Matches=($r.craft_tool -eq $op.InnerText);XPath=$op.xpath}
 }
}
$rows|Export-Csv "$root/_Documentation/CraftingAudit_20261010/LATE_TOOL_ASSIGNMENTS.csv" -NoTypeInformation
"$($rows.Count) candidate matches, $(@($rows|Where-Object {!$_.Matches}).Count) differences. Review only: legacy condition/order and current station/architecture must be reconciled before migration."