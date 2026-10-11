$path='Config/XUi_InGame/windows.xml'
$source=[IO.File]::ReadAllText($path)
$old=[regex]::Match($source,'(?s)<window name="rebirthMusicLibraryRoot".*?</window>').Value
$volume=[regex]::Match($old,'(?s)<rect name="musicVolumeControls".*?</rect><label name="musicVolumeValue".*?</rect>').Value
$volume=$volume.Replace('pos="28,-694"','pos="20,-590"').Replace('width="950"','width="620"').Replace('pos="260,0"','pos="110,0"').Replace('pos="575,0"','pos="430,0"')
$b=[Text.StringBuilder]::new()
[void]$b.AppendLine('<window name="rebirthMusicLibraryRoot" depth="80" width="1872" height="830" anchor="Center" pos="-936,440" controller="RebirthMusicLibrary, RebirthUtils" cursor_area="true">')
function Panel($name,$x,$y,$w,$h,$key){[void]$b.AppendLine("<rect name=`"$name`" pos=`"$x,$y`" width=`"$w`" height=`"$h`"><sprite width=`"$w`" height=`"$h`" sprite=`"menu_empty`" color=`"12,12,16,225`"/><sprite width=`"$w`" height=`"$h`" sprite=`"menu_empty2px`" type=`"sliced`" fillcenter=`"false`" color=`"181,140,255,255`"/><label pos=`"14,-10`" width=`"$($w-28)`" height=`"30`" font_size=`"24`" color=`"181,140,255,255`" text_key=`"$key`"/><sprite pos=`"0,-46`" width=`"$w`" height=`"2`" sprite=`"menu_empty`" color=`"181,140,255,255`"/>")}
function Button($name,$x,$y,$w,$key){[void]$b.AppendLine("<simplebutton name=`"$name`" pos=`"$x,$y`" width=`"$w`" height=`"36`" caption_key=`"$key`"/>")}
Panel 'libraryPanel' 0 0 720 830 'xuiRebirthMusicLibrary'
Button 'musicTab' 16 -62 330 'xuiRebirthWalkmanMusic'
Button 'audioTab' 358 -62 330 'xuiRebirthAudiobooks'
[void]$b.AppendLine('<label name="libraryCapacity" pos="500,-108" width="188" height="28" justify="right" font_size="20"/>')
for($i=0;$i -lt 24;$i++){
$x=16+($i%6)*110;$y=-150-[Math]::Floor($i/6)*158
[void]$b.AppendLine("<rect pos=`"$x,$y`" width=`"104`" height=`"150`"><sprite width=`"104`" height=`"150`" sprite=`"menu_empty2px`" color=`"50,50,58,255`"/><sprite name=`"libraryIcon$i`" pos=`"4,-4`" width=`"96`" height=`"96`" atlas=`"ItemIconAtlas`" sprite=`"rb_music_cassette`" visible=`"false`"/><label name=`"libraryTitle$i`" pos=`"4,-100`" width=`"96`" height=`"46`" font_size=`"17`" justify=`"center`" overflow=`"shrinkcontent`"/><sprite name=`"librarySelection$i`" width=`"104`" height=`"150`" sprite=`"menu_empty2px`" type=`"sliced`" fillcenter=`"false`" color=`"220,195,120,255`" visible=`"false`" depth=`"4`"/><button name=`"library$i`" width=`"104`" height=`"150`" sprite=`"menu_empty`" defaultcolor=`"0,0,0,1`" hovercolor=`"181,140,255,30`" on_drag=`"true`" on_scroll=`"true`" depth=`"5`"/></rect>")}
[void]$b.AppendLine('<rect name="libraryScrollbar" controller="RebirthDiscordScrollbar, RebirthUtils" pos="692,-150" width="20" height="624"><defaultscrollbar/></rect></rect>')
Panel 'playbackPanel' 734 0 558 590 'xuiRebirthWalkmanNowPlaying'
[void]$b.AppendLine('<sprite name="playingPreview" pos="95,-68" width="360" height="250" atlas="ItemIconAtlas" sprite="rb_music_cassette"/><label name="nowPlaying" pos="20,-330" width="518" height="80" font_size="28" justify="center" overflow="shrinkcontent"/>')
Button 'musicPrevious' 20 -426 160 'xuiRebirthWalkmanPrevious'
Button 'musicPause' 194 -426 160 'xuiRebirthMusicPlayPause'
Button 'musicNext' 368 -426 160 'xuiRebirthMusicNext'
Button 'musicShuffle' 20 -480 250 'xuiRebirthMusicToggle'
Button 'musicResume' 280 -480 248 'xuiRebirthWalkmanResume'
# Keep the existing volume controller and its input implementation.
[void]$b.AppendLine(($volume.Replace('pos="20,-590"','pos="20,-538"').Replace('pos="0,-42"','pos="0,-1000"')))
[void]$b.AppendLine('</rect>')
Panel 'inspectionPanel' 1306 0 566 590 'xuiRebirthWalkmanSelected'
[void]$b.AppendLine('<sprite name="cassettePreview" pos="20,-68" width="150" height="150" atlas="ItemIconAtlas" sprite="rb_music_cassette" visible="false"/><label name="cassetteTitle" pos="186,-70" width="354" height="144" font_size="26" overflow="shrinkcontent"/><rect pos="20,-230" width="526" height="240" controller="RebirthReadableText, RebirthUtils" on_scroll="true"><defaultscrollbar/><scrollview name="readableTextViewport" width="504" height="240" clippingsoftness="0,4"><label name="cassetteDescription" width="496" height="240" font_size="20" overflow="resizeheight" support_bb_code="true"/></scrollview></rect>')
Button 'cassetteInsert' 20 -496 252 'xuiRebirthWalkmanInsertW'
Button 'cassetteRemove' 20 -496 252 'xuiRebirthWalkmanRemoveW'
Button 'musicPlay' 286 -496 252 'xuiRebirthMusicPlay'
[void]$b.AppendLine('</rect>')
Panel 'cassetteBackpackPanel' 734 -604 1138 226 'xuiBackpack'
[void]$b.AppendLine('<label name="cassetteBagCount" pos="942,-12" width="104" height="28" justify="right" font_size="20"/><button name="cassetteSort" sprite="rb_backpack_sort" atlas="RebirthUiIcons" tooltip_key="lblSortContainer" pos="1070,-10" width="28" height="28" style="icon32px, press, hover"/>')
for($i=0;$i -lt 36;$i++){
$x=14+($i%12)*90;$y=-54-[Math]::Floor($i/12)*55
[void]$b.AppendLine("<rect pos=`"$x,$y`" width=`"86`" height=`"52`"><sprite width=`"86`" height=`"52`" sprite=`"menu_empty2px`" color=`"50,50,58,255`"/><sprite name=`"cassetteBagIcon$i`" pos=`"19,-2`" width=`"48`" height=`"48`" atlas=`"ItemIconAtlas`" sprite=`"rb_music_cassette`" visible=`"false`"/><label name=`"cassetteBagCount$i`" pos=`"56,-25`" width=`"26`" height=`"24`" font_size=`"20`" justify=`"right`"/><sprite name=`"cassetteBagSelection$i`" width=`"86`" height=`"52`" sprite=`"menu_empty2px`" type=`"sliced`" fillcenter=`"false`" color=`"220,195,120,255`" visible=`"false`" depth=`"4`"/><button name=`"cassetteBag$i`" width=`"86`" height=`"52`" sprite=`"menu_empty`" defaultcolor=`"0,0,0,1`" hovercolor=`"181,140,255,30`" on_drag=`"true`" on_scroll=`"true`" depth=`"5`"/></rect>")}
[void]$b.AppendLine('<rect name="cassetteBagScrollbar" controller="RebirthDiscordScrollbar, RebirthUtils" pos="1108,-54" width="20" height="162"><defaultscrollbar/></rect></rect>')
[void]$b.AppendLine('<simplebutton name="musicClose" pos="1686,56" width="176" height="42" caption_key="xuiBack"/></window>')
$source=$source.Replace($old,$b.ToString())
[xml]$check=$source
[IO.File]::WriteAllText($path,$source)
