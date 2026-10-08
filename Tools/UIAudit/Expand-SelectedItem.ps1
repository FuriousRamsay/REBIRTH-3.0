$ErrorActionPreference='Stop'
$p=Join-Path $PSScriptRoot '../../Config/XUi_InGame/windows.xml'
$s=[IO.File]::ReadAllText($p)
function EditWindow([string]$name,[scriptblock]$edit){
 $start=$script:s.IndexOf('<window name="'+$name+'"'); if($start -lt 0){throw "Missing $name"}
 $end=$script:s.IndexOf('</window>',$start)+9
 $doc=[xml]$script:s.Substring($start,$end-$start); & $edit $doc.DocumentElement $doc
 $settings=[Xml.XmlWriterSettings]::new();$settings.OmitXmlDeclaration=$true;$settings.Indent=$true
 $out=[IO.StringWriter]::new();$writer=[Xml.XmlWriter]::Create($out,$settings);$doc.DocumentElement.WriteTo($writer);$writer.Close()
 $script:s=$script:s.Substring(0,$start)+$out.ToString()+$script:s.Substring($end)
}
EditWindow 'rebirthContextWorkspace' {
 param($w,$d)
 foreach($name in @('contextInspectPanel','contextInspectPanelBg','contextInspectPanelFrame')){$w.SelectSingleNode('.//*[@name="'+$name+'"]').SetAttribute('height','421')}
 foreach($name in @('contextBackpackPanel','contextBackpackPanelBg','contextBackpackPanelFrame')){$w.SelectSingleNode('.//*[@name="'+$name+'"]').SetAttribute('height','380')}
 $w.SelectSingleNode('.//*[@name="contextBackpackPanel"]').SetAttribute('pos','934,-433')
 foreach($n in $w.SelectNodes('.//*[@name="contextBackpackGrid"]//*|.//*[@name="contextBackpackGrid"]')){if($n.GetAttribute('height') -eq '320'){$n.SetAttribute('height','292')};if($n.HasAttribute('clippingsize')){$n.SetAttribute('clippingsize','878,292');$n.SetAttribute('clippingcenter','439,-146')}}
 $w.SelectSingleNode('.//*[@name="contextSelectedName"]').SetAttribute('font_size','28')
 foreach($name in @('contextSelectedSummary','contextSelectedDescription')){$w.SelectSingleNode('.//*[@name="'+$name+'"]').SetAttribute('font_size','24')}
 $w.SelectSingleNode('.//*[@name="contextSelectedDescription"]').SetAttribute('height','132')
 foreach($n in $w.SelectNodes('.//label[starts-with(@name,"contextStat")]')){$n.SetAttribute('font_size','24');$n.SetAttribute('height','29');$v=$n.GetAttribute('pos').Split(',');$i=[int]([regex]::Match($n.GetAttribute('name'),'\d+$').Value);$n.SetAttribute('pos',$v[0]+','+(-64-30*$i))}
 $w.SelectSingleNode('.//*[@name="contextActions"]').SetAttribute('pos','16,-286')
 foreach($n in $w.SelectNodes('.//rect[starts-with(@name,"contextAction") and @controller]')){$n.SetAttribute('height','40');$v=$n.GetAttribute('pos').Split(',');$n.SetAttribute('pos',$v[0]+','+([int]$v[1]/37*44));foreach($c in $n.SelectNodes('.//*')){if($c.GetAttribute('height') -eq '34'){$c.SetAttribute('height','40')};if($c.LocalName -eq 'label'){$c.SetAttribute('font_size','22');$c.SetAttribute('height','28');$pos=$c.GetAttribute('pos').Split(',');$c.SetAttribute('pos',$pos[0]+',-7')}}}
 $cap=$w.SelectSingleNode('.//*[@name="contextBackpackCapacity"]');$cap.SetAttribute('pos','14,-350')
}
# Header quotes are authored on the same owner as the selected-item content.
$d=[xml]$s
$headers=$d.SelectNodes('//label[@text="SELECTED ITEM" or @text_key="xuiRebirthSelectedItem" or @name="contextInspectTitle"]')
foreach($header in $headers){
 $parent=$header.ParentNode;$width=[int]$parent.GetAttribute('width');if($width -le 0){continue}
 $header.SetAttribute('width',[string]([Math]::Max(180,$width-390)))
 $label=$d.CreateElement('label');$label.SetAttribute('name','rebirthSelectedHeaderSalePrice');$label.SetAttribute('controller','RebirthSelectedItemSalePrice, RebirthUtils');$label.SetAttribute('pos',([string]($width-374))+',-8');$label.SetAttribute('width','360');$label.SetAttribute('height','32');$label.SetAttribute('font_size','24');$label.SetAttribute('justify','right');$label.SetAttribute('color','240,240,244,255');$label.SetAttribute('depth','12');$label.SetAttribute('overflow','shrinkcontent');[void]$parent.AppendChild($label)
}
# Save just changed window fragments to retain unrelated formatting.
foreach($name in @('rebirthContextWorkspace','rebirthSurvivorCharacterWindow','rebirthPersonalCraftingRoot','rebirthCreativeRoot','rebirthModifyEditor')){
 $node=$d.SelectSingleNode('//window[@name="'+$name+'"]');if(!$node){continue}
 $start=$s.IndexOf('<window name="'+$name+'"');$end=$s.IndexOf('</window>',$start)+9
 $settings=[Xml.XmlWriterSettings]::new();$settings.OmitXmlDeclaration=$true;$settings.Indent=$true;$out=[IO.StringWriter]::new();$writer=[Xml.XmlWriter]::Create($out,$settings);$node.WriteTo($writer);$writer.Close();$s=$s.Substring(0,$start)+$out.ToString()+$s.Substring($end)
}
foreach($c in @('194,26,31','210,30,35','220,45,55','230,65,65','209,73,63')){$s=$s.Replace($c,'181,140,255')}
[IO.File]::WriteAllText($p,$s)
