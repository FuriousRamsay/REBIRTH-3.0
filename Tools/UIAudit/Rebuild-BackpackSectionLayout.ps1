$ErrorActionPreference='Stop'
$p=Join-Path $PSScriptRoot '../../Config/XUi_InGame/windows.xml';$s=[IO.File]::ReadAllText($p)
foreach($mode in @('theory','sell')){
 $name=if($mode -eq 'theory'){'rebirthBackpackLibraryRoot'}else{'rebirthBackpackSellStashRoot'}
 $start=$s.IndexOf('<window name="'+$name+'"');$end=$s.IndexOf('</window>',$start)+9;$d=[xml]$s.Substring($start,$end-$start);$w=$d.DocumentElement;$old=$w.FirstChild
 function Node($tag,$attrs){$n=$d.CreateElement($tag);foreach($key in $attrs.Keys){$n.SetAttribute($key,[string]$attrs[$key])};return $n}
 $body=Node 'rect' @{name=($mode+'Body');width=1856;height=813}
 $left=Node 'rect' @{name=($mode+'StoragePanel');pos='0,0';width=922;height=813}
 $right=Node 'rect' @{name=($mode+'SelectedPanel');pos='934,0';width=922;height=393}
 $bagPanel=Node 'rect' @{name=($mode+'BackpackPanel');pos='934,-405';width=922;height=408}
 foreach($panel in @($left,$right,$bagPanel)){
  [void]$panel.AppendChild((Node 'sprite' @{width=$panel.GetAttribute('width');height=$panel.GetAttribute('height');sprite='menu_empty';color='16,16,20,250';depth=0;globalopacitymod=0}))
  [void]$panel.AppendChild((Node 'sprite' @{pos='0,-48';width=922;height=2;sprite='menu_empty';color='181,140,255,255';depth=3}))
 }
 $title=if($mode -eq 'theory'){'READING MATERIALS'}else{'FOR SALE'}
 [void]$left.AppendChild((Node 'label' @{pos='14,-10';width=540;height=32;font_size=25;color='181,140,255,255';text=$title;depth=6}))
 [void]$right.AppendChild((Node 'label' @{pos='14,-10';width=480;height=32;font_size=25;color='181,140,255,255';text='SELECTED ITEM';depth=6}))
 [void]$right.AppendChild((Node 'label' @{name='rebirthSelectedHeaderSalePrice';controller='RebirthSelectedItemSalePrice, RebirthUtils';pos='548,-10';width=360;height=32;font_size=24;justify='right';color='240,240,244,255';depth=12;overflow='shrinkcontent'}))
 [void]$bagPanel.AppendChild((Node 'label' @{pos='14,-10';width=450;height=32;font_size=25;color='181,140,255,255';text='BACKPACK';depth=6}))
 foreach($n in @($old.ChildNodes)){
  $id=$n.GetAttribute('name')
  if($id -match ('^'+$mode+'Slot(\d+)$')){
   $i=[int]$Matches[1];$n.SetAttribute('pos',([string](14+($i%11)*80))+','+(-58-[Math]::Floor($i/11)*80));$n.SetAttribute('width','78');$n.SetAttribute('height','78')
   foreach($c in $n.ChildNodes){if($c.LocalName -eq 'sprite' -and $c.GetAttribute('atlas') -eq 'ItemIconAtlas'){$c.SetAttribute('pos','4,-4');$c.SetAttribute('width','70');$c.SetAttribute('height','70')}
    elseif($c.LocalName -eq 'label'){$c.SetAttribute('pos','4,-52');$c.SetAttribute('width','70');$c.SetAttribute('height','24');$c.SetAttribute('font_size','22')}
    elseif($c.LocalName -eq 'button'){$c.SetAttribute('width','78');$c.SetAttribute('height','78')}
    elseif($c.GetAttribute('name') -match 'Selected'){$c.SetAttribute('pos','0,-75');$c.SetAttribute('width','78')}
    else{$c.SetAttribute('width','78');$c.SetAttribute('height','78')}}
   [void]$left.AppendChild($n)
  }
 }
 $scroll=Node 'rect' @{name=($mode+'BagScroll');pos='14,-58';width=894;height=320;controller='RebirthCharacterOverviewList, RebirthUtils'}
 $vp=Node 'panel' @{name='listViewport';width=872;height=320;clipping='SoftClip';clippingsize='872,320';clippingcenter='436,-160';depth=10}
 $content=Node 'rect' @{name='listContent';width=872;height=871}
 for($row=0;$row -lt 13;$row++){
  $wrap=Node 'rect' @{name=($mode+'BagRow'+$row);pos=('0,'+(-67*$row));width=872;height=65}
  for($col=0;$col -lt 13;$col++){
   $i=$row*13+$col;$n=$old.SelectSingleNode('./*[@name="'+$mode+'Available'+$i+'"]');if(!$n){continue}
   $n.SetAttribute('pos',([string]($col*67))+',0');$n.SetAttribute('width','65');$n.SetAttribute('height','65')
   foreach($c in $n.ChildNodes){$c.SetAttribute('width','59');if($c.LocalName -eq 'sprite'){$c.SetAttribute('height','59');$c.SetAttribute('pos','3,-3')}else{$c.SetAttribute('pos','3,-43');$c.SetAttribute('font_size','22');$c.SetAttribute('height','22')}}
   [void]$wrap.AppendChild($n)
  };[void]$content.AppendChild($wrap)
 }
 [void]$vp.AppendChild($content);[void]$scroll.AppendChild($vp)
 [void]$scroll.AppendChild((Node 'button' @{name='listTrack';pos='878,0';width=16;height=320;sprite='menu_empty';defaultcolor='40,40,47,255';on_scroll='true'}))
 [void]$scroll.AppendChild((Node 'button' @{name='listThumb';pos='880,0';width=12;height=100;sprite='menu_empty';defaultcolor='181,140,255,255';hovercolor='181,140,255,255';on_drag='true';on_scroll='true'}))
 [void]$bagPanel.AppendChild($scroll)
 foreach($id in @('Capacity','Status','InspectIcon','InspectName','InspectDescription','Purpose','DragPreview')){
  $n=$old.SelectSingleNode('./*[@name="'+$mode+$id+'"]');if(!$n){continue}
  switch($id){
   'Capacity'{$n.SetAttribute('pos','600,-10');$n.SetAttribute('width','300');$n.SetAttribute('justify','right');[void]$left.AppendChild($n)}
   'Status'{$n.SetAttribute('pos','14,-762');$n.SetAttribute('width','894');$n.SetAttribute('height','40');[void]$left.AppendChild($n)}
   'InspectIcon'{$n.SetAttribute('pos','14,-64');[void]$right.AppendChild($n)}
   'InspectName'{$n.SetAttribute('pos','124,-64');$n.SetAttribute('width','770');$n.SetAttribute('height','40');$n.SetAttribute('font_size','28');[void]$right.AppendChild($n)}
   'InspectDescription'{$n.SetAttribute('pos','124,-114');$n.SetAttribute('width','770');$n.SetAttribute('height','112');$n.SetAttribute('font_size','24');[void]$right.AppendChild($n)}
   'Purpose'{$n.SetAttribute('pos','124,-232');$n.SetAttribute('width','770');$n.SetAttribute('height','58');[void]$right.AppendChild($n)}
   'DragPreview'{[void]$body.AppendChild($n)}
  }
 }
 $a=0;foreach($id in @('Deposit','Withdraw','Study','Finish','Close')){
  $n=$old.SelectSingleNode('./*[@name="'+$mode+$id+'"]');if(!$n){continue}
  $n.SetAttribute('pos',([string](14+($a%3)*298))+','+(-300-[Math]::Floor($a/3)*44));$n.SetAttribute('width','286');$n.SetAttribute('height','38');$n.SetAttribute('font_size','23');$n.SetAttribute('bordercolor','181,140,255,255');[void]$right.AppendChild($n);$a++
 }
 [void]$body.AppendChild($left);[void]$body.AppendChild($right);[void]$body.AppendChild($bagPanel);$w.RemoveAll();$w.SetAttribute('name',$name);$w.SetAttribute('controller',$(if($mode -eq 'theory'){'RebirthBackpackLibrary, RebirthUtils'}else{'RebirthBackpackSellStash, RebirthUtils'}));$w.SetAttribute('depth','80');$w.SetAttribute('width','1856');$w.SetAttribute('height','813');$w.SetAttribute('anchor','Center');$w.SetAttribute('pos','-928,406');$w.SetAttribute('cursor_area','true');[void]$w.AppendChild($body)
 $settings=[Xml.XmlWriterSettings]::new();$settings.OmitXmlDeclaration=$true;$settings.Indent=$true;$out=[IO.StringWriter]::new();$writer=[Xml.XmlWriter]::Create($out,$settings);$w.WriteTo($writer);$writer.Close();$s=$s.Substring(0,$start)+$out.ToString()+$s.Substring($end)
}
[IO.File]::WriteAllText($p,$s)
