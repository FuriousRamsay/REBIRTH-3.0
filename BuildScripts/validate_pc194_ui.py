from pathlib import Path
import re
exec(Path('BuildScripts/validate_pc193_ui.py').read_text())
latest=E.parse('Config/XUi_InGame/windows.xml').xpath('./conditional')[-1]
for op in latest.iter():
    assert not re.search(r'/window\[\d+\]',op.get('xpath','')), 'Window order differs with active conditionals'
heading=w.xpath("//*[@name='survivorProgressionSkillsPanel']/label[@text='LEVEL / NEXT']")
assert len(heading)==1 and 'text_key' not in heading[0].attrib
for name in ('rebirthModifyEditor',):
    node=w.xpath(f"/windows/window[@name='{name}']//*[@name='editorBagLock']")[0]
    x,y=map(int,node.get('pos').split(','))
    assert x+int(node.get('width'))==14+10*62
    assert -y+int(node.get('height'))==282-6
for node in w.xpath("//*[@name='characterBagLock']"):
    x,y=map(int,node.get('pos').split(','))
    assert x+int(node.get('width'))==14+5*76
    assert -y+int(node.get('height'))==416-6
for name in ('windowQuestDescription','windowQuestObjectives','windowQuestRewards','windowChallengeEntryDescription'):
    node=w.xpath(f"/windows/window[@name='{name}']")[0]
    assert node.xpath('./sprite[@depth="0"]')[0].get('color')=='24,24,29,250'
    assert node.xpath('./sprite[@depth="0"]')[0].get('fillcenter')=='true'
for field in ('Status','Tier','Distance','Tracking'):
    card=w.xpath(f"//*[@name='questSummary{field}']/..")[0]
    assert card.xpath('./sprite[@color="24,24,29,255" and @depth="1"]')
    assert all(label.get('depth')=='3' for label in card.findall('label'))
assert w.xpath('//rect[@controller="InGameTimeControls"]')[0].get('height')=='272'
assert w.xpath('//*[@name="rebirthDebugRows"]')
print('PASS: toolbar edges and six-unit gaps; one translucent detail fill; opaque quest cards below labels; taller clock.')
