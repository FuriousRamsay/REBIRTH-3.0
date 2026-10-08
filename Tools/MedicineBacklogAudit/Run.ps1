$ErrorActionPreference="Stop"
function Apply([xml]$target,[Xml.XmlNode]$node,[string]$file,[string]$mode){
if($node.NodeType -ne [Xml.XmlNodeType]::Element){return}
switch($node.LocalName){
'conditional'{$matched=$false;foreach($part in $node.ChildNodes){if($part.NodeType -ne [Xml.XmlNodeType]::Element){continue};if($part.LocalName -eq 'if'){$condition=$part.GetAttribute('cond');if($condition -eq "character_progression('Rebirth')"){$selected=$mode -eq 'Rebirth'}elseif($condition.StartsWith("xpath('") -and $condition.EndsWith("') != null")){$xp=$condition.Substring(7,$condition.Length-17);$selected=$target.SelectNodes($xp).Count -gt 0}else{throw "Unknown condition $condition"};$matched=$selected}elseif($part.LocalName -eq 'else'){$selected=!$matched}else{throw "Unsupported conditional branch $($part.LocalName)"};if($selected){foreach($child in $part.ChildNodes){Apply $target $child $file $mode}}};return}
'include'{$included=[IO.Path]::GetFullPath((Join-Path (Split-Path $file) $node.GetAttribute('filename')));$script:includes.Add($included);[xml]$patch=[IO.File]::ReadAllText($included);foreach($child in $patch.DocumentElement.ChildNodes){Apply $target $child $included $mode};return}
}
$targets=@($target.SelectNodes($node.GetAttribute('xpath')));$script:operations++;if($targets.Count -eq 0){$script:unmatched++;$script:misses.Add($file+" :: "+$node.GetAttribute("xpath"))}
switch($node.LocalName){
'append'{foreach($t in $targets){foreach($child in $node.ChildNodes){if($child.NodeType -eq [Xml.XmlNodeType]::Element){[void]$t.AppendChild($target.ImportNode($child,$true))}}}}
'remove'{foreach($t in $targets){if($t -is [Xml.XmlAttribute]){$t.OwnerElement.RemoveAttributeNode($t)|Out-Null}else{[void]$t.ParentNode.RemoveChild($t)}}}
'removeattribute'{foreach($t in $targets){if($t -is [Xml.XmlAttribute]){[void]$t.OwnerElement.RemoveAttributeNode($t)}else{$t.RemoveAttribute($node.GetAttribute('name'))}}}
'setattribute'{foreach($t in $targets){$t.SetAttribute($node.GetAttribute('name'),$node.InnerText)}}
'set'{foreach($t in $targets){if($t -is [Xml.XmlAttribute]){$t.Value=$node.InnerText}else{$t.InnerXml=$node.InnerXml}}}
'insertAfter'{foreach($t in $targets){$prev=$t;foreach($child in $node.ChildNodes){if($child.NodeType -eq [Xml.XmlNodeType]::Element){$prev=$t.ParentNode.InsertAfter($target.ImportNode($child,$true),$prev)}}}}
'insertBefore'{foreach($t in $targets){foreach($child in $node.ChildNodes){if($child.NodeType -eq [Xml.XmlNodeType]::Element){[void]$t.ParentNode.InsertBefore($target.ImportNode($child,$true),$t)}}}}
default{throw "Unsupported patch operation $($node.LocalName) in $file"}
}
}
$script:includes=[Collections.Generic.List[string]]::new();$script:misses=[Collections.Generic.List[string]]::new();$script:operations=0;$script:unmatched=0
[xml]$d=[IO.File]::ReadAllText('C:/Program Files (x86)/Steam/steamapps/common/7 Days To Die/Data/Config/items.xml');$f=(Resolve-Path 'Config/items.xml').Path;[xml]$p=[IO.File]::ReadAllText($f);foreach($n in $p.DocumentElement.ChildNodes){Apply $d $n $f 'Rebirth'}
function Prop($item,$path,$seen){if($seen.Contains($item.name)){throw 'inherit cycle'};$seen.Add($item.name)|Out-Null;$v=$item.SelectSingleNode($path);if($v){return $v.value};$extends=$item.SelectSingleNode("property[@name='Extends']");if($extends){$parent=$d.SelectSingleNode("/items/item[@name='$($extends.value)']");if(!$parent){throw 'missing parent'};return Prop $parent $path $seen};return ''}
$rows=@();foreach($i in $d.SelectNodes('/items/item')){$cl=Prop $i "property[@class='Action1']/property[@name='Class']" ([Collections.Generic.List[string]]::new());if($cl -eq 'UseOther'){$rows+= [pscustomobject]@{Item=$i.name;Action1=$cl;Tags=(Prop $i "property[@name='Tags']" ([Collections.Generic.List[string]]::new()));Parent=$i.SelectSingleNode("property[@name='Extends']").value}}}
$rows|ConvertTo-Json|Set-Content "$PSScriptRoot/inventory.json";$rows|Format-Table -AutoSize;"ITEMS=$($d.SelectNodes('/items/item').Count) USEOTHER=$($rows.Count) OPS=$script:operations INCLUDES=$($script:includes.Count) UNMATCHED=$script:unmatched"