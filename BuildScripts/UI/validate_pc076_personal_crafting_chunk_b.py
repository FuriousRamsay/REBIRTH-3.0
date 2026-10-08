from pathlib import Path
from lxml import etree
import re, sys

root=Path(__file__).resolve().parents[2]
checks=[]
def ok(name, cond, detail=''):
    checks.append((name,bool(cond),detail))

xui=root/'Config/XUi_InGame/xui.xml'
windows=root/'Config/XUi_InGame/windows.xml'
loc=root/'Config/Localization.csv'
scripts=[
 root/'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthPersonalCrafting.cs',
 root/'Scripts/Crafting/UI/PersonalCrafting/RebirthPersonalCraftingState.cs',
 root/'Scripts/Crafting/UI/PersonalCrafting/RebirthPersonalCraftingLayoutService.cs',
 root/'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthPersonalCraftingSurface.cs',
]
for p in [xui,windows,loc,*scripts]: ok('exists '+str(p.relative_to(root)),p.exists())

for p in [xui,windows]:
    try: etree.parse(str(p)); ok('xml parse '+p.name,True)
    except Exception as e: ok('xml parse '+p.name,False,str(e))

xt=etree.parse(str(xui))
# The replacement operations must live under the Rebirth conditional, never globally.
ops=xt.xpath("//if[@cond=\"character_progression('Rebirth')\"]//*[self::setattribute or self::remove or self::append]")
text='\n'.join(etree.tostring(n,encoding='unicode') for n in ops)
ok('Rebirth crafting controller replacement', "name=\"controller\">RebirthPersonalCrafting, RebirthUtils" in text)
ok('Rebirth disables automatic native backpack', "name=\"open_backpack_on_open\">false" in text)
ok('Rebirth removes all native crafting child windows', "xpath=\"/xui/window_group[@name='crafting']/window\"" in text)
ok('Rebirth appends one custom root', 'window name="rebirthPersonalCraftingRoot"' in text)
# Ensure there is no crafting-group replacement outside a conditional.
outside=xt.xpath("/configs/*[not(self::conditional)]//*[contains(@xpath, \"window_group[@name='crafting']\")]")
ok('no unconditional personal crafting group mutation',len(outside)==0,str(len(outside)))

wt=etree.parse(str(windows))
ok('custom root definition exists',bool(wt.xpath("//window[@name='rebirthPersonalCraftingRoot']")))
for zid in ['rebirthCraftingTopZone','rebirthCraftingLeftZone','rebirthCraftingCenterZone','rebirthCraftingRightZone','rebirthCraftingLayoutDebug']:
    ok('zone '+zid,bool(wt.xpath(f"//*[@name='{zid}']")))
# No native windows are nested in the new custom root.
rootnodes=wt.xpath("//window[@name='rebirthPersonalCraftingRoot']")
if rootnodes:
    rs=etree.tostring(rootnodes[0],encoding='unicode')
    for banned in ['windowCraftingList','craftingInfoPanel','windowBackpack','itemInfoPanel','emptyInfoPanel','windowNonPagingHeader','rebirthCraftingQueueNonStation']:
        ok('custom root excludes '+banned,banned not in rs)

# C# architectural markers and simple lexical balance.
source='\n'.join(p.read_text(encoding='utf-8') for p in scripts)
ok('root subclasses native crafting contract','XUiC_RebirthPersonalCrafting : XUiC_CraftingWindowGroup' in source)
ok('root intentionally bypasses native Init','public override void Init()' in source and 'children[i].Init();' in source)
ok('layout uses XUi logical screen size','GetXUiScreenSize()' in source)
ok('paging header is hidden not removed','pagingHeader.ViewComponent.IsVisible = false' in source and 'RestoreNativePagingHeader' in source)
ok('layout debug gated by bool','LayoutDebugEnabled' in source and 'IsVisible = RebirthPersonalCraftingState.LayoutDebugEnabled' in source)
for p in scripts:
    s=p.read_text(encoding='utf-8')
    ok('brace balance '+p.name,s.count('{')==s.count('}'),f"{s.count('{')}/{s.count('}')}")

# Localization uniqueness for the keys introduced in this chunk.
lines=loc.read_text(encoding='utf-8-sig').splitlines()
keys=[ln.split(',',1)[0] for ln in lines[1:] if ',' in ln]
for key in ['xuiRebirthPersonalCrafting','xuiRebirthCraftingTabsComing','xuiRebirthSelectedRecipe','xuiRebirthRequirements','xuiRebirthInventory']:
    ok('localization '+key,keys.count(key)==1,str(keys.count(key)))

failed=[c for c in checks if not c[1]]
for name,passed,detail in checks:
    print(('PASS' if passed else 'FAIL')+' - '+name+((' :: '+detail) if detail else ''))
print(f"SUMMARY {len(checks)-len(failed)} PASS / {len(failed)} FAIL")
if failed: sys.exit(1)
