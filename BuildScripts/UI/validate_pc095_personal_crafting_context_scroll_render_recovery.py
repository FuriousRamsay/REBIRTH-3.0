#!/usr/bin/env python3
from pathlib import Path
import argparse, re, sys
from lxml import etree

ap=argparse.ArgumentParser()
ap.add_argument('--project-root', default=None)
args=ap.parse_args()
root=Path(args.project_root).resolve() if args.project_root else Path(__file__).resolve().parents[2]

files={
 'xml': root/'Config/XUi_InGame/windows.xml',
 'cat': root/'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingRecipeCatalogue.cs',
 'inv': root/'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingInventory.cs',
 'ctx': root/'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingItemContext.cs',
 'out': root/'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingOutcome.cs',
 'layout': root/'Scripts/Crafting/UI/PersonalCrafting/RebirthPersonalCraftingLayoutService.cs',
 'owner': root/'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthPersonalCrafting.cs',
}
for k,p in files.items():
    if not p.exists():
        print('FAIL missing',p); sys.exit(2)
text={k:p.read_text(encoding='utf-8') for k,p in files.items()}
checks=[]
def check(name, ok): checks.append((name,bool(ok)))

# Scroll authority recovery grounded in PC093 runtime trace.
check('independent authoritative recipe scroll target', 'authoritativeScrollTargetPixels' in text['cat'])
check('post-child scroll authority repair method', 'ReassertAuthoritativeScrollAfterChildren' in text['cat'])
check('post-child authority runs after child updates', text['cat'].find('UpdateControllerTree(dt);', text['cat'].find('public override void Update')) < text['cat'].find('ReassertAuthoritativeScrollAfterChildren', text['cat'].find('public override void Update')))
check('wheel updates scroll authority', re.search(r'SetScrollTarget\(authoritativeScrollTargetPixels \+ direction \* step, false, true\)', text['cat']) is not None)
check('transient zero range does not erase authority', 'Do not erase the independent user authority' in text['cat'])
check('scroll trace includes authority', ' authority=' in text['cat'])

# Inventory data existed at runtime; force repeated cells to present it.
check('inventory slot root explicitly activated', 'UiTransform.gameObject.SetActive(authoritative)' in text['inv'])
check('inventory authoritative stack refreshes bindings', re.search(r'slot\.ItemStack = desired;[\s\S]{0,600}?slot\.RefreshBindings\(\);', text['inv']) is not None)
check('inventory hidden stack refreshes bindings', re.search(r'ItemStack\.Empty\.Clone\(\);[\s\S]{0,180}?slot\.RefreshBindings\(\);', text['inv']) is not None)

# Integrated item context, not floating inventory popup.
check('item context targets top detail region', 'detailsRegion = owner != null ? owner.GetChildById("rebirthCraftingDetailsRegion")' in text['ctx'])
check('item context no longer targets inventory region', 'inventoryRegion' not in text['ctx'])
check('item context subtree deactivates when hidden', 'UiTransform.gameObject.SetActive(active)' in text['ctx'])
check('item context no scroll-offscreen autoclose', 'IsSlotVisible' not in text['ctx'])
check('item context uses selected-recipe outer geometry', 'full replacement for the Selected Recipe detail surface' in text['ctx'])

# Outcome controller itself must agree with responsive layout service.
check('outcome controller horizontal metrics', 'Three compact metrics across one row' in text['out'])
check('outcome controller full-width result', 'Result gets the full width beneath the metric strip' in text['out'])
check('old outcome vertical leftWidth removed', 'leftWidth' not in text['out'])
check('layout service horizontal metrics', 'rebirthCraftingOutcomeMetricsPanel' in text['layout'] and 'rebirthCraftingOutcomeResultsPanel' in text['layout'])

# Runtime HUD suppression stronger than IsVisible alone and no per-frame spam.
check('HUD captures original scale', 'hudScaleBeforeOpen' in text['owner'])
check('HUD suppression applies zero scale', 'localScale = Vector3.zero' in text['owner'])
check('HUD restore restores saved scale', 'hudScaleBeforeOpen.TryGetValue' in text['owner'])
check('HUD reappearance trace is throttled aggregate', 'reappliedSinceLastTrace=' in text['owner'])
check('old per-controller REAPPEARED spam absent', 'HUDTrace] REAPPEARED' not in text['owner'])

# XML structure and visual alignment.
parser=etree.XMLParser(remove_comments=False)
tree=etree.parse(str(files['xml']), parser)
def one(name):
    xs=tree.xpath(f'//*[@name="{name}"]')
    return xs[0] if len(xs)==1 else None
ctx=one('rebirthCraftingItemContext'); invreg=one('rebirthCraftingInventoryRegion'); center=one('rebirthCraftingCenterZone')
check('one item context authored', ctx is not None)
check('item context is direct center-zone child', ctx is not None and ctx.getparent() is center)
check('item context not inside inventory region', ctx is not None and ctx.getparent() is not invreg)
check('item context overlays selected detail geometry', ctx is not None and ctx.get('pos')=='0,0' and ctx.get('width')=='730' and ctx.get('height')=='232')
viewport=one('rebirthCraftingInventoryViewport')
check('inventory viewport explicit foreground depth', viewport is not None and int(viewport.get('depth','0'))>=20)
invgrid=viewport.xpath('.//grid[@name="inventory"]')[0] if viewport is not None and viewport.xpath('.//grid[@name="inventory"]') else None
item_stack=invgrid.xpath('./item_stack[@name="0"]')[0] if invgrid is not None and invgrid.xpath('./item_stack[@name="0"]') else None
check('inventory item stack uses stock-compatible size contract', item_stack is not None and item_stack.get('cell_size') is None)
check('inventory remains 8 columns', invgrid is not None and invgrid.get('cols')=='8')
# 4 visible rows are provided by viewport/cell geometry (210 / responsive ~69); authored grid retains 13 presentation rows.
check('inventory physical presentation rows retained', invgrid is not None and int(invgrid.get('rows','0'))>=13)
for n in ['Crafting','Character','Map','Skills','Quests','Challenges','Players']:
    el=one(f'rebirthCraftingTab{n}Label')
    check(f'{n} tab label lowered for icon vertical centering', el is not None and el.get('pos')=='43,-8' and el.get('height')=='34')
check('layout service uses same tab label baseline', 'SetRect(owner.GetChildById("rebirthCraftingTab" + suffix + "Label"), 43, -8, Math.Max(1, tabWidth - 49), 34)' in text['layout'])

passed=sum(ok for _,ok in checks); failed=len(checks)-passed
for name,ok in checks: print(('PASS' if ok else 'FAIL')+': '+name)
print(f'RESULT: {passed} PASS / {failed} FAIL')
sys.exit(0 if failed==0 else 1)
