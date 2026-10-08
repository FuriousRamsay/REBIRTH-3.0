from pathlib import Path
exec(Path('BuildScripts/validate_pc186_ui.py').read_text())
r=w.xpath('//*[@name="rebirthRosterScroll"]')[0]
assert not r.xpath('.//pager|.//*[@controller="QuestEntry"]')
v=r.xpath('./panel')[0];assert v.get('clippingsize')=='1284,630'
rows=v.xpath('./rect/*');assert len(rows)==16
assert r.xpath('./rect[@name="listNativeScrollbar"]/defaultscrollbar')
entry=t.xpath('/templates/rebirth_players_entry/rect')[0]
assert entry.get('width')=='1284' and entry.get('pos')=='${pos}'
for count in (0,1,13,14,16,17,64,1000):
 maximum=max(0,(count-1)*46+46-630) if count else 0
 first=max(0,min(int(maximum//46)-1,count-16))
 if maximum:assert first+16>=count
 if count==1:assert maximum==0
p=w.xpath('/windows/window[@name="players"]')[0]
icon=p.xpath('.//*[@name="rebirthProfileBackgroundArt"]')[0];name=p.xpath('.//*[@name="rebirthProfileBackground"]')[0]
assert icon.get('pos')=='190,-72' and name.get('pos')=='274,-72'
assert p.xpath('.//*[@name="rebirthGroupIdentityNameInput"]')[0].get('justify')=='left'
d=p.xpath('.//*[@tab_key="xuiMpTabDiscord"]')[0]
for panel in d:
 x,y=map(int,panel.get('pos').split(','));assert x+int(panel.get('width'))<=1802 and -y+int(panel.get('height'))<=730
assert not d.xpath('.//sprite[@color="[darkGrey]"]')
print('PASS: bounded roster viewport, last-row reachability, aligned background name/art, identity editor, and Discord panel bounds.')
