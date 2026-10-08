from lxml import etree as E
r=E.parse('Config/XUi_InGame/windows.xml'); x=E.parse('Config/XUi_InGame/xui.xml'); native=E.parse('../../Data/Config/XUi_InGame/xui.xml')
def win(name):return r.xpath('//window[@name="'+name+'"]')[-1]
for name in ['windowQuestList','windowQuestSharedList','windowQuestDescription','windowQuestObjectives','windowQuestRewards']:
 w=win(name);assert w.get('anchor')=='Center' and 'panel' not in w.attrib
 assert len(w.xpath('./sprite[@name="rebirthQuestFrame"]'))==1
for name,ids in [('windowQuestList',['trackBtn','showOnMapBtn','questRemoveBtn','questShareBtn','searchInput','pager','questList']),('windowQuestSharedList',['acceptBtn','showOnMapBtn','questRemoveBtn','searchInput','pager','questList'])]:
 for id in ids:assert len(win(name).xpath('.//*[@name="'+id+'"]'))==1,(name,id)
assert -928+520+8==-400 and -400+788+8==396 and 396+532==928
assert 435-280-8==147 and 147-525==-378
for auto in [False,True]:
 rows=15 if auto else 9;height=813 if auto else 535
 assert 43+49+rows*46<=height
assert 43+49+3*46<=270
assert 46+12+10*34<525 and 46+372+88<525
for b in ['commonrewards','chosenrewards','chainrewards']:
 assert win('windowQuestRewards').xpath('.//label[@text="{'+b+'}"]')
registration=x.xpath('//append[window[@name="rebirthQuestChrome"]]')[-1];assert len(native.xpath(registration.get('xpath')))==1
print('Quest layouts, list capacity, actions, rewards and navigation registration passed.')
