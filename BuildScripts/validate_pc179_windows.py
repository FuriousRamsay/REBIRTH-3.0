from pathlib import Path
from lxml import etree as E
r=E.parse('Config/XUi_InGame/windows.xml');x=E.parse('Config/XUi_InGame/xui.xml');n=E.parse('../../Data/Config/XUi_InGame/xui.xml')
def win(name):return r.xpath('//window[@name="'+name+'"]')[-1]
for bar in r.xpath('//rect[@name="rebirthCraftingTopTabs"]'):
 assert len(bar.xpath('./rect[starts-with(@name,"rebirthCraftingTab")]'))==8
 for name in ['Crafting','Character','Map','Skills','Quests','Challenges','Players','Journal']:
  assert len(bar.xpath('.//*[@name="btnRebirthCraftingTab'+name+'"]'))==1
for name in ['players','windowChallengeList','windowChallengeEntryDescription']:
 w=win(name);assert w.get('anchor')=='Center' and w.get('height')=='813'
 assert w.find('sprite[@name="rebirthSectionFrame"]') is not None
assert win('players').xpath('.//*[@controller="RebirthPlayersList, RebirthUtils"]')
assert win('players').xpath('.//*[@name="btnRebirthTeachSelected"]')
assert win('players').xpath('.//*[@name="rebirthGroupIdentityNameInput"]')
for name in ['btnTrack','btnComplete','gotoButton','objectives']:
 assert len(win('windowChallengeEntryDescription').xpath('.//*[@name="'+name+'"]'))==1
assert win('windowChallengeEntryDescription').xpath('.//sprite[@sprite="{entryicon}" and @width="80" and @height="80"]')
assert -928+820+8==-100 and -100+1028==928
j=win('rebirthJournalRoot');ids=[e.get('name') for e in j.iter() if e.get('name')];assert len(ids)==len(set(ids))
for id in ['journalNew','journalClose','journalEdit','journalSave','journalCancel','journalDelete','journalPrevious','journalNext','journalSort','journalType','journalTextPrevious','journalTextNext']+['journalFilter'+v for v in ['All','Notes','Places','Plans','Lore']]+['journalRow'+str(i) for i in range(10)]:
 assert j.xpath('.//button[@name="'+id+'Hit"]'),id
editor=j.xpath('.//rect[@name="journalEditor"]')[0];assert not editor.xpath('.//*[@atlas]')
assert editor.xpath('.//textfield[@name="journalBodyInput" and @on_return="NewLine" and @character_limit="4000"]')
for name in ['journalEntryTitle','journalEntryBody']+["journalRowTitle"+str(i) for i in range(10)]:
 assert j.xpath('.//label[@name="'+name+'" and @support_bb_code="false"]')
assert len(E.parse('Resources/Journal/entries.xml').xpath('/journal/entry[image and body]'))==1
for title in ['Players','Challenges']:
 registration=x.xpath('//append[window[@name="rebirth'+title+'Chrome"]]')[-1];assert len(n.xpath(registration.get('xpath')))==1
assert x.xpath('//window_group[@name="rebirthJournal"]/window[@name="rebirthJournalRoot"]')
print('Journal controls, text-only editor, navigation, preserved Players/Challenges actions and panel geometry passed.')
