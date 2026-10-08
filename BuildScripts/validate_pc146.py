from lxml import etree as E
from pathlib import Path
r=E.parse("Config/XUi_InGame/windows.xml")
def n(name):return r.xpath('//*[@name=$n]',n=name)[0]
m=n("rebirthModifyEditor");slots=m.findall(".//item_stack")
assert len(slots)==100
for i,s in enumerate(slots):
 assert int(s.get("repeat_i"))==i
 assert s.get("pos") is None # native template does not forward a position argument
 assert s.getparent().get("pos")==f"{i%10*62},0"
 assert s.getparent().getparent().get("pos")==f"0,{-(i//10)*62}"
 assert s.get("controller")=="RebirthEditorBackpackSlot, RebirthUtils"
 assert s.get("cell_size")=="60"
v=m.xpath('.//*[@name="listViewport"]')[0]
assert v.get("clippingsize")=="620,611" and v.get("clippingcenter")=="310,-305.5"
for name in ("characterBagDurability","rebirthCraftingItemDurability"):
 q=n(name)
 assert q.find('sprite[@name="fill"]').get("height")=="10"
 label=q.find('label');assert label.get("pos")=="0,14" and label.get("font_size")=="26"
assert 'editor.IsEditorOpen' in Path("Scripts/Survivor/UI/RebirthItemWindowLifecycle.cs").read_text()
for file in ("XUiC_RebirthSurvivorCharacter.cs","XUiC_RebirthItemEditorHeader.cs"):
 assert 'RebirthWindowHudScope' in Path("Scripts/Survivor/UI",file).read_text()
print("PASS: native template positioning for all 100 slots, real clipping attributes, native quality metrics, editor drag ownership and shared HUD suppression")

