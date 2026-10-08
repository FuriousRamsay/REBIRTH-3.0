from pathlib import Path
from lxml import etree as E
r=E.parse("Config/XUi_InGame/windows.xml")
m=r.xpath('//*[@name="rebirthModifyEditor"]')[0]
slots=m.findall('.//item_stack')
assert len(slots)==100
for i,s in enumerate(slots):
 assert s.get('repeat_i')==str(i)
 assert s.getparent().get('pos')==f'{i%10*62},0'
 assert s.getparent().getparent().get('pos')==f'0,{-(i//10)*62}'
assert len(m.xpath('.//*[@name="editorBagModify"]'))==1
assert m.xpath('.//*[@name="editorBagDurability"]')
assert m.xpath('.//*[@name="listViewport"]')[0].get('clippingsize')=='620,589'
s=Path('Scripts/Survivor/UI/XUiC_RebirthEditorBackpack.cs').read_text()
assert '.OnPressed +=' in s and 'action.OnActivated()' in s
assert 'selected.InfoWindow = xui.GetChildByType<XUiC_ItemInfoWindow>()' in s
assert 'GetStatItemValueTextWithCompareInfo' in s and 'XUiM_ItemStack.CanCompare' in s
assert 'AttributeLock ? new UnityEngine.Color32(31,31,36,255)' in s
s=Path('Scripts/Survivor/HudTracking/RebirthCompactBuffLayout.cs').read_text()
assert 'index % 2 * 122' in s and 'index / 2 * 28' in s
assert 'tween.enabled = false' in s and 'if (!RebirthCompactBuffLayout.Active) return true' in s
print('PASS: native grid mapping, single native Modify action, quality icon, comparison formatter, encumbered palette and Rebirth-only two-column buff layout')
