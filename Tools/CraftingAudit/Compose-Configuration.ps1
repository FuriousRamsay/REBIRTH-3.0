param([ValidateSet('blocks','recipes','items','item_modifiers','loot','traders')][string]$Kind='recipes',[bool]$Rebirth=$true,[bool]$RequireTimedReading=$true)
$ErrorActionPreference='Stop'
$root=(Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$config=Join-Path $root 'Config'
$doc=[xml](Get-Content "$root/../../Data/Config/$Kind.xml" -Raw)
$events=[Collections.Generic.List[object]]::new()
function ApplyChildren($parent,$file){
 foreach($op in $parent.ChildNodes){
  if($op.NodeType -ne 'Element'){continue}
  if($op.LocalName -eq 'include'){
   $path=Join-Path $config $op.GetAttribute('filename')
   if(!(Test-Path -LiteralPath $path)){$path=Join-Path (Split-Path $file) $op.GetAttribute('filename')}
   $included=[xml](Get-Content -LiteralPath $path -Raw);ApplyChildren $included.DocumentElement $path;continue
  }
  if($op.LocalName -eq 'conditional'){
   $branch=$op.SelectSingleNode('if');$condition=$branch.GetAttribute('cond')
   if($condition -eq "character_progression('Rebirth')"){$take=$Rebirth}
   elseif($condition -eq "isoption('RequireTimedReading', 'bool', 'false')"){$take=!$RequireTimedReading}
   elseif($condition.StartsWith("xpath('") -and $condition.EndsWith("') != null")){
    $query=$condition.Substring(7,$condition.Length-17)
    $take=$null -ne $doc.SelectSingleNode($query)
   }else{throw "Unhandled condition: $condition"}
   if(!$take){$branch=$op.SelectSingleNode('else')}
   if($branch){ApplyChildren $branch $file};continue
  }
  $xpath=$op.GetAttribute('xpath');$nodes=@($doc.SelectNodes($xpath))
  $events.Add([pscustomobject]@{Source=$file;Operation=$op.LocalName;XPath=$xpath;Matches=$nodes.Count})
  foreach($n in $nodes){switch($op.LocalName){
   'remove' {if($n -is [Xml.XmlAttribute]){$null=$n.OwnerElement.RemoveAttributeNode($n)}else{$null=$n.ParentNode.RemoveChild($n)}}
   'append' {foreach($child in $op.ChildNodes){$null=$n.AppendChild($doc.ImportNode($child,$true))}}
   'set' {$n.InnerText=$op.InnerText}
   'setattribute' {$n.SetAttribute($op.GetAttribute('name'),$op.InnerText)}
   'removeattribute' {if($n -is [Xml.XmlAttribute]){$null=$n.OwnerElement.RemoveAttributeNode($n)}else{$n.RemoveAttribute($op.GetAttribute('name'))}}
   'insertBefore' {foreach($child in $op.ChildNodes){$null=$n.ParentNode.InsertBefore($doc.ImportNode($child,$true),$n)}}
   'insertAfter' {$anchor=$n;foreach($child in $op.ChildNodes){$anchor=$n.ParentNode.InsertAfter($doc.ImportNode($child,$true),$anchor)}}
   default {throw "Unhandled operation: $($op.LocalName)"}
  }}
 }
}
$patch=[xml](Get-Content "$config/$Kind.xml" -Raw);ApplyChildren $patch.DocumentElement "$config/$Kind.xml"
$out=Join-Path $root '_Documentation/CraftingAudit_20261010'
$mode=if($Rebirth){'rebirth'}else{'base'}
$doc.Save("$out/EFFECTIVE_$($Kind)_$mode.xml")
$events|Export-Csv "$out/PATCH_TRACE_$($Kind)_$mode.csv" -NoTypeInformation
"Applied $($events.Count) operations for $Kind/$mode; zero-target operations: $(@($events|Where-Object Matches -eq 0).Count). Includes native data and this mod only; external mods not composed."


