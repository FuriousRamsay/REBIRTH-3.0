"""PC185: resolve the installed patches and validate list capacity and panel geometry."""
from pathlib import Path
exec(Path('BuildScripts/validate_pc181_ui.py').read_text(encoding='utf-8'))
t=resolve('Config/XUi_InGame/templates.xml','../../Data/Config/XUi_InGame/templates.xml')
for name,content,controller,height in [('mapTracking','waypointList','RebirthWaypointList',378),('mapInvites','invitesList','RebirthInviteList',168)]:
 win=w.xpath('/windows/window[@name="'+name+'"]')[0]
 assert not win.xpath('.//pager'),name
 host=win.xpath('.//*[@name="rebirthMapListScroll"]')[0]
 viewport=host.xpath('./panel[@name="listViewport"]')[0]
 pool=viewport.xpath('./rect[@name="'+content+'"]')[0]
 assert pool.get('controller')==controller+', RebirthUtils'
 assert len(pool)==16 and not pool.xpath('.//grid')
 assert host.xpath('./rect[@name="listNativeScrollbar"]/defaultscrollbar')
 assert int(viewport.get('height'))==height
 for i,row in enumerate(pool):
  assert row.get('pos')=='0,'+str(-36*i)
  assert int(row.get('height'))==34 and int(row.get('width'))==394
  assert row.xpath('./sprite[@name="background"]')
  assert row.xpath('./label[@name="Name"]') and row.xpath('./label[@name="Distance"]')
 for toolbar in win.xpath('.//rect[@name="toolbar" or @name="searchControls"]'):assert int(toolbar.get('width'))==418
 # Long collections remain reachable through the fixed pool, including the final row.
 for count in [0,1,5,16,17,100,500]:
  content_height=max(0,(count-1)*36+34) if count else 0
  max_offset=max(0,content_height-height)
  first=max(0,min(int(max_offset//36)-1,count-16))
  if count and max_offset:assert first+16>=count
for name in ['challenge_list_entry','weather_challenge_list_entry']:
 root=t.xpath('/templates/'+name+'/rect')[0]
 assert int(root.get('width'))==764
 reward=root.xpath('./label[@text="{groupreward}"]')[0]
 assert int(reward.get('pos').split(',')[0])+int(reward.get('width'))<=764
 assert int(reward.get('font_size'))==22
assert w.xpath('//grid[@controller="RebirthChallengeGroupList, RebirthUtils"]')
players=w.xpath('/windows/window[@name="players"]')[0]
roster=players.xpath('.//rect[@controller="RebirthPlayersList, RebirthUtils"]')[0]
detail=players.xpath('.//rect[@name="rebirthPlayerDetailsPanel"]')[0]
assert int(roster.get('width'))==1310 and int(detail.get('width'))==478
assert 1310<int(detail.get('pos').split(',')[0])
assert detail.xpath('.//texture[@name="rebirthSelectedPlayerModel"]')
assert not detail.xpath('.//*[@name="rebirthPlayerDetailsZombieKillsValue" or @name="rebirthPlayerDetailsLevelValue"]')
assert len(detail.xpath('.//label[starts-with(@name,"rebirthProfileSkillName")]'))==4
assert all(int(label.get('font_size'))>=18 for label in t.xpath('/templates/rebirth_players_header//label'))
assert journal.xpath('.//*[@name="journalBodyViewport"]')[0].get('height')=='800'
print('PASS: unpaged compact waypoint pools, last-row reachability, challenge titles, wider roster and model/profile panel, full-height Journal reader.')
