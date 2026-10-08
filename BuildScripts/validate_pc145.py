from lxml import etree as E
from pathlib import Path
r=E.parse("Config/XUi_InGame/windows.xml")
def n(root,name): return root.xpath('.//*[@name=$n]',n=name)[0]
for name in ("rebirthModifyEditor","rebirthCosmeticsEditor"):
 w=n(r,name)
 assert w.get("width")=="1560" and w.get("pos")=="-780,505"
 assert n(w,"editorOpaqueBackground").get("globalopacitymod")=="0"
 assert n(w,"editorBack").get("defaultcolor")=="25,25,31,255"
m=n(r,"rebirthModifyEditor")
slots=m.findall(".//item_stack")
assert len(slots)==100
assert [int(e.get("repeat_i")) for e in slots]==list(range(100))
assert all(e.get("cell_size")=="60" for e in slots)
assert n(m,"listViewport").get("clipping_size")=="620,611"
assert n(m,"editorBagItemIcon").get("atlas")=="ItemIconAtlas"
for name in ("characterBagDurability","rebirthCraftingItemDurability"):
 q=n(r,name)
 assert n(q,"fill").get("height")=="16"
 assert n(q,"qualityNumber").get("justify")=="center"
assert n(r,"qualityLabel").get("justify")=="center"
assert n(r,"recipeQualityFill").get("height")=="16"
s=Path("Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthScrapPreview.cs").read_text()
assert '40 + rows * 48' in s and 'width - 132' in s
assert 'IsEditorOpen == true' in Path("Scripts/Crafting/UI/PersonalCrafting/RebirthPersonalCraftingHudSuppressionInstaller.cs").read_text()
print("PASS: editor bounds, opaque backgrounds, dark Back buttons, 100 native slot mappings, icon details, centered 16px bars, adaptive scrap geometry and editor HUD gate")
