# Read-only authored-target inventory. Metadata is never counted as a native recipe.
$ErrorActionPreference='Stop'
$root=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$ids=@('FuriousRamsayBikeRepairKit','FuriousRamsayGunRepairKit','FuriousRamsayElectricalRepairKit','FuriousRamsayGeneratorBank','FuriousRamsayBatteryBank','toolForgeAnvil','toolForgeBellows')
$found=@{}
foreach($id in $ids){$found[$id]=@{target=$id;items=@();recipes=@()}}
$paths=@(Get-ChildItem (Join-Path $root 'Config') -Filter *.xml -Recurse)
$native=Join-Path $root '../../Data/Config'
foreach($name in @('items.xml','blocks.xml','recipes.xml')){$paths+=Get-Item (Join-Path $native $name)}
foreach($path in $paths){
 [xml]$xml=[IO.File]::ReadAllText($path.FullName)
 foreach($node in $xml.SelectNodes('//item[@name] | //block[@name] | //recipe[@name]')){
  $id=$node.GetAttribute('name');if(!$found.ContainsKey($id)){continue}
  if($node.LocalName -eq 'recipe'){
   # Authored recipe knowledge records have no ingredients and are not craftable definitions.
   if($node.SelectNodes('ingredient').Count -gt 0){$found[$id].recipes+= $path.FullName}
  }elseif($node.SelectNodes('property').Count -gt 0){$found[$id].items+= $path.FullName}
 }
}
$rows=foreach($id in $ids){[pscustomobject]@{target=$id;hasOutput=$found[$id].items.Count -gt 0;hasRecipe=$found[$id].recipes.Count -gt 0;outputSources=$found[$id].items;recipeSources=$found[$id].recipes}}
$rows | ConvertTo-Json -Depth 4