[xml]$d=Get-Content Config/XUi_InGame/station_templates.xml -Raw
$n=$d.SelectSingleNode('//rect[@name="stationItemInfo"]')
foreach($label in @($n.SelectNodes('.//label[@name="descriptionText" and @text="{itemdescription}"]'))){
 $parent=$label.ParentNode;$width=440;$height=260
 $reader=$d.CreateElement('rect');$reader.SetAttribute('name','stationItemDescriptionReader');$reader.SetAttribute('width',"$width");$reader.SetAttribute('height',"$height");$reader.SetAttribute('pos',$label.GetAttribute('pos'));$reader.SetAttribute('controller','RebirthReadableText, RebirthUtils');$reader.SetAttribute('on_scroll','true')
 $f=$d.CreateDocumentFragment();$f.InnerXml="<defaultscrollbar pos='418,0' barheight='$height'/><scrollview name='readableTextViewport' width='416' height='$height'/>";[void]$reader.AppendChild($f)
 [void]$parent.ReplaceChild($reader,$label);$label.SetAttribute('pos','0,0');$label.SetAttribute('width','410');$label.SetAttribute('height',"$height");$label.SetAttribute('overflow','resizeheight');[void]$reader.SelectSingleNode('scrollview').AppendChild($label)
}
$d.Save((Join-Path (Get-Location) 'Config/XUi_InGame/station_templates.xml'))
