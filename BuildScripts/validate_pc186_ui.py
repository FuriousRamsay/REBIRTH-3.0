"""PC186: validate resolved menu actions, compact rows and group picker integration."""
from pathlib import Path
exec(Path('BuildScripts/validate_pc185_ui.py').read_text())
x=resolve('Config/XUi_InGame/xui.xml','../../Data/Config/XUi_InGame/xui.xml')
quest=w.xpath('/windows/window[@name="windowQuestList"]//*[@name="listContent"]')[0]
assert len(quest)==22
for i,row in enumerate(quest):
 assert row.get('height')=='34' and row.get('pos')=='0,'+str(-36*i)
 for label in row.findall('label'):
  assert label.get('pivot')=='left' and label.get('pos').endswith(',-17')
  assert int(label.get('height'))<=34
picker=w.xpath('/windows/window[@name="rebirthGroupColorPickerWindow"]')[0]
assert picker.get('controller')=='RebirthGroupColorPicker, RebirthUtils'
for name in ['targetNameColorPicker','btnApplyTargetNameColor','btnCancelTargetNameColor']:assert picker.xpath('.//*[@name="'+name+'"]')
assert x.xpath('/xui/window_group[@name="rebirthGroupColorPicker"]/window[@name="rebirthGroupColorPickerWindow"]')
editor=w.xpath('//rect[@name="rebirthGroupIdentityPanel"]')[0]
for child in editor:
 if not child.get('pos'):continue
 a,b=map(int,child.get('pos').split(','))
 assert a+int(child.get('width'))<=442
 assert -b+int(child.get('height'))<=102
menu=w.xpath('/windows/window[@name="ingameMenu"]')[0]
native=E.parse('../../Data/Config/XUi_InGame/windows.xml').xpath('/windows/window[@name="ingameMenu"]')[0]
old={b.get('name') for b in native.xpath('./grid/*')}
new={b.get('name') for b in menu.xpath('./grid/*')}
assert new==old|{'btnRebirthResume'}
assert menu.get('controller')=='RebirthPauseMenu, RebirthUtils'
assert t.xpath('/templates/rebirth_pause_button/rect[@controller="SimpleButton"]/button[@name="clickable"]')
for b in menu.xpath('./grid/*'):assert b.get('height')=='46'
print('PASS: compact centered quest rows, bounded identity editor, picker window/controller wiring, all native ESC actions retained.')
