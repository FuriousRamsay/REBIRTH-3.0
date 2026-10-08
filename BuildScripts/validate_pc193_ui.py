from pathlib import Path
exec(Path('BuildScripts/validate_pc192_ui.py').read_text())
for name in ('rebirthPersonalCraftingRoot','rebirthModifyEditor','rebirthCosmeticsEditor'):
    assert len(w.xpath('/windows/window[@name="'+name+'"]/*[@controller="RebirthCharacterStatsPopup, RebirthUtils"]'))==1
buttons=w.xpath('/windows/window[@name="ingameMenu"]//*[@name="buttons"]')[0]
assert buttons[-1].get('name')=='btnExit'
debug=w.xpath('/windows/window[@name="ingameDebugMenu"]')[0]
original=E.parse('../../Data/Config/XUi_InGame/windows.xml').xpath('/windows/window[@name="ingameDebugMenu"]')[0]
required={c.get('name') for c in original.xpath('.//simplebutton|.//togglebutton|.//combobox|.//button') if c.get('name')}
actual={c.get('name') for c in debug.iter() if isinstance(c.tag,str)}
assert required <= actual, required-actual
assert debug.get('controller')=='RebirthDebugMenu, RebirthUtils'
assert t.xpath('/templates/rebirth_debug_toggle/rect[@controller="ToggleButton"]')
slot=t.xpath('/templates/rebirth_item_stack_toolbar/rect')[0]
for v in slot.xpath('./label[@name="stackValue"]|./*[@name="durability"]'):
    x,y=map(int,v.get('pos').split(',')); width=int(v.get('width')); height=int(v.get('height'))
    assert 0<=x and x+width<=62 and 0<=-y and -y+height<=62
print('PASS: Crafting/Modify popup hosts, equipment preview wiring, bounded toolbelt overlays, Exit last, preserved debug controls.')
