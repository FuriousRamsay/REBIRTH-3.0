from pathlib import Path
exec(Path('BuildScripts/validate_pc189_ui.py').read_text())
for tag in ('discord_friends_list','discord_pending_list','discord_callmembers_list'):
 root=t.xpath('/templates/'+tag+'/rect')[0]
 assert root.get('paging_step_size')=='SingleEntry'
 assert all(p.get('visible')=='false' and p.get('hotkeys_enabled')=='false' for p in root.xpath('.//pager'))
 assert root.xpath('./rect[@controller="RebirthDiscordScrollbar, RebirthUtils"]/defaultscrollbar')
 assert root.xpath('./panel[@clipping="SoftClip"]/grid[@name="list"]')
 for size in (2,4,10):
  for count in (0,1,size,size+1,1000):
   maximum=max(0,count-size)
   assert (maximum>0)==(count>size)
   if count>size:assert maximum+size==count
assert w.xpath('/windows/window[@name="ingameMenu"]//*[@name="btnRebirthResume"]')[0].get('caption')=='Resume'
print('PASS: Discord lists use clipped single-entry scrolling, conditional scrollbar wiring, hidden page controls, and Resume casing.')
