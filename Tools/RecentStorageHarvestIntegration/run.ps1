$ErrorActionPreference='Stop'
$root=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$native='C:/Program Files (x86)/Steam/steamapps/common/7 Days To Die/Data/Config'
$script:checks=0;$script:files=@();$script:ops=@();$script:unknown=@()
function Check($ok,$label){if(-not $ok){throw "FAIL $label"};$script:checks++;"PASS $label"}
function Walk($node,$dir,$doc,$mode){
 foreach($n in $node.ChildNodes){if($n.NodeType -ne 'Element'){continue}
 switch($n.LocalName){
 'include' {$path=[IO.Path]::GetFullPath((Join-Path $dir $n.filename));$script:files+= $path;[xml]$x=Get-Content -LiteralPath $path -Raw;Walk $x.DocumentElement (Split-Path $path) $doc $mode}
 'conditional' {$taken=$false;foreach($c in $n.ChildNodes){if($c.NodeType -ne 'Element'){continue};if($c.LocalName -eq 'if'){$cond=[string]$c.cond;$ok=$false;if($cond -match "^character_progression\('Rebirth'\)$"){$ok=$mode -eq 'Rebirth'}elseif($cond -match "^xpath\('(.+)'\) != null$"){$ok=$doc.SelectNodes($Matches[1]).Count -gt 0}else{$script:unknown+=$cond};if($ok){Walk $c $dir $doc $mode;$taken=$true;break}}elseif($c.LocalName -eq 'else' -and -not $taken){Walk $c $dir $doc $mode}}}
 default {if(-not $n.HasAttribute('xpath')){continue};$matches=@($doc.SelectNodes($n.xpath));$script:ops+=[pscustomobject]@{Kind=$n.LocalName;XPath=[string]$n.xpath;Matches=$matches.Count;Directory=$dir}
 foreach($m in $matches){switch($n.LocalName){
 'remove' {if($m -is [System.Xml.XmlAttribute]){$m.OwnerElement.RemoveAttributeNode($m)|Out-Null}else{$m.ParentNode.RemoveChild($m)|Out-Null}}
 'set' {$m.InnerText=$n.InnerText}
 'setattribute' {$m.SetAttribute($n.GetAttribute('name'),$n.InnerText)}
 'removeattribute' {$m.RemoveAttribute($n.GetAttribute('name'))}
 'append' {foreach($child in $n.ChildNodes){if($child.NodeType -eq 'Element'){$m.AppendChild($doc.ImportNode($child,$true))|Out-Null}}}
 'insertAfter' {$anchor=$m;foreach($child in $n.ChildNodes){if($child.NodeType -eq 'Element'){$copy=$doc.ImportNode($child,$true);$m.ParentNode.InsertAfter($copy,$anchor)|Out-Null;$anchor=$copy}}}
 'insertBefore' {foreach($child in $n.ChildNodes){if($child.NodeType -eq 'Element'){$m.ParentNode.InsertBefore($doc.ImportNode($child,$true),$m)|Out-Null}}}
 default {$script:unknown+="operation:$($n.LocalName)"}
 }}
 }
 }
 }
}
foreach($mode in @('Base','Rebirth')){
 $script:files=@();$script:ops=@();$script:unknown=@()
 [xml]$doc=Get-Content "$native/blocks.xml" -Raw;[xml]$patch=Get-Content "$root/Config/blocks.xml" -Raw;Walk $patch.DocumentElement "$root/Config" $doc $mode
 $farm=@('farmPlotBlock','farmPlotBlockRaised','farmPlotBlockCornerRound','farmPlotBlockPlayer','farmPlotBlockPlayerRaised','farmPlotBlockPlayerCornerRound')
 foreach($name in $farm){$b=$doc.SelectSingleNode("/blocks/block[@name='$name']");Check ($b.SelectNodes('dropextendsoff').Count -eq 1) "$mode $name inheritance cutoff";Check ($b.SelectNodes("drop[@event='Destroy' and @name='farmPlotBlockVariantHelper' and @count='1']").Count -eq 1) "$mode $name one helper";Check ($b.SelectNodes('drop').Count -eq 1) "$mode $name no material/fall drops"}
 $helper=$doc.SelectSingleNode("/blocks/block[@name='farmPlotBlockVariantHelper']/property[@name='PlaceAltBlockValue']")
 Check (($helper.value -split ',').Count -eq 3) "$mode reusable helper three player shape choices"
 foreach($f in @('_Farming/farm_plot_recovery.xml','_LootConversions/blocks.xml','_Crowbar/blocks.xml')){Check (@($script:files|Where-Object {$_ -eq [IO.Path]::GetFullPath("$root/Config/$f")}).Count -eq 1) "$mode include exactly once $f"}
 Check ([array]::IndexOf($script:files,[IO.Path]::GetFullPath("$root/Config/_LootConversions/blocks.xml")) -lt [array]::IndexOf($script:files,[IO.Path]::GetFullPath("$root/Config/_Crowbar/blocks.xml"))) "$mode conversion before cover exclusion"
 foreach($name in @('cntShippingCrateHero','cntShippingCrateShamway')){Check ($doc.SelectNodes("/blocks/block[@name='$name']/property[@name='Tags' and @value='rbCrowbarCover']").Count -eq 1) "$mode root tag once $name"}
 Check ($doc.SelectNodes("/blocks/block[@name='cntShippingCrateConstructionSupplies']/property[@name='Tags' and @value='']").Count -eq 1) "$mode converted construction storage excluded"
 Check ($doc.SelectNodes("/blocks/block[starts-with(@name,'cntLootCrate')]/property[@name='Tags' and contains(@value,'rbCrowbarCover')]").Count -eq 0) "$mode opened loot untagged"
 Check ($doc.SelectNodes("/blocks/block[@name='cntShippingCrateConstructionSupplies']/property[@name='Class' and @value='CompositeTileEntity']").Count -eq 1) "$mode construction conversion retained"
 Check (@($script:files | Group-Object | Where-Object Count -gt 1).Count -eq 0) "$mode no duplicate active block includes"
 $doc.Save("$PSScriptRoot/$mode-blocks-effective.xml")
 $blockFiles=@($script:files);$blockOps=@($script:ops);$blockUnknown=@($script:unknown)
 [xml]$doc=Get-Content "$native/items.xml" -Raw;[xml]$patch=Get-Content "$root/Config/items.xml" -Raw;$script:files=@();$script:ops=@();$script:unknown=@();Walk $patch.DocumentElement "$root/Config" $doc $mode
 $expected=if($mode -eq 'Rebirth'){1}else{0};Check ($doc.SelectNodes("/items/item[@name='ItemsWeaponsCrowbar001_FR']").Count -eq $expected) "$mode one crowbar definition"
 Check ($doc.SelectNodes("/items/item[@name='ItemsWeaponsCrowbar001_FR']/effect_group/passive_effect[@name='BlockDamage' and @tags='rbCrowbarCover']").Count -eq $expected) "$mode one active cover damage effect"
 Check (@($script:files | Group-Object | Where-Object Count -gt 1).Count -eq 0) "$mode no duplicate active item includes"
 [pscustomobject]@{Mode=$mode;BlockIncludes=$blockFiles;BlockOperations=$blockOps;UnknownBlockConditions=$blockUnknown;ItemIncludes=$script:files;ItemOperations=$script:ops;UnknownItemConditions=$script:unknown}|ConvertTo-Json -Depth 6|Set-Content "$PSScriptRoot/$mode-evaluation.json" -Encoding utf8
 "COUNTS $mode blockIncludes=$($blockFiles.Count) blockOps=$($blockOps.Count) itemIncludes=$($script:files.Count) itemOps=$($script:ops.Count) unknown=$($blockUnknown.Count+$script:unknown.Count)"
}
"$script:checks PASS bounded recursive XML evaluation; no runtime/generator execution"

