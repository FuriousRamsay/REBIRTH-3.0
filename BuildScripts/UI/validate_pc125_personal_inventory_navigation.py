#!/usr/bin/env python3
from pathlib import Path
import sys, re, xml.etree.ElementTree as ET

root = Path(sys.argv[1]) if len(sys.argv) > 1 else Path(__file__).resolve().parents[2]
win = (root/'Config/XUi_InGame/windows.xml').read_text(encoding='utf-8')
main = (root/'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthPersonalCrafting.cs').read_text(encoding='utf-8')
tabs = (root/'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingTopTabs.cs').read_text(encoding='utf-8')
nav = (root/'Scripts/Crafting/UI/PersonalCrafting/RebirthCraftingNavigationService.cs').read_text(encoding='utf-8')
layout = (root/'Scripts/Crafting/UI/PersonalCrafting/RebirthPersonalCraftingLayoutService.cs').read_text(encoding='utf-8')
ctx = (root/'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingItemContext.cs').read_text(encoding='utf-8')
audit = (root/'Scripts/Crafting/UI/PersonalCrafting/RebirthPersonalCraftingLayoutAudit.cs').read_text(encoding='utf-8')
checks=[]
def ck(name, cond): checks.append((name,bool(cond)))

def balanced(s):
    # sufficient static sanity for these source-only project changes
    return s.count('{') == s.count('}')

for f in ['Config/XUi_InGame/windows.xml','Config/XUi_InGame/templates.xml']:
    try: ET.parse(root/f); ok=True
    except Exception: ok=False
    ck('XML parses: '+Path(f).name, ok)

ck('Inventory tab is authored', 'name="rebirthCraftingTabInventory"' in win)
ck('Inventory tab is before Crafting in authored top navigation', win.find('name="rebirthCraftingTabInventory"') < win.find('name="rebirthCraftingTabCrafting"'))
ck('Inventory tab uses backpack icon', 'rebirthCraftingTabInventoryIcon' in win and 'sprite="ui_game_symbol_backpack"' in win)
ck('Inventory tab uses existing Inventory localization', 'rebirthCraftingTabInventoryLabel' in win and 'text_key="xuiRebirthInventory"' in win)
ck('Inventory tab navigates right to Crafting', 'btnRebirthCraftingTabInventory' in win and 'nav_right="btnRebirthCraftingTabCrafting"' in win)
ck('Crafting tab navigates left to Inventory', 'btnRebirthCraftingTabCrafting' in win and 'nav_left="btnRebirthCraftingTabInventory"' in win)

ck('Top-tabs controller now owns eight tabs', 'new TabVisual[8]' in tabs)
ck('Top-tabs controller wires Inventory first', 'tabs[0] = Wire("Inventory", RebirthCraftingNavigationService.Destination.Inventory, true);' in tabs)
ck('Top-tabs active destination is dynamic', 'SetActiveDestination(RebirthCraftingNavigationService.Destination destination)' in tabs)
ck('Navigation service exposes Inventory destination', 'Inventory = 0' in nav)
ck('Inventory navigation is an internal surface switch', 'owner.ShowInventorySurface();' in nav)
ck('Crafting navigation is an internal surface switch', 'owner.ShowCraftingSurface();' in nav)
ck('Inventory/Crafting internal switches do not close and reopen group', 'case Destination.Inventory:' in nav and 'case Destination.Crafting:' in nav and 'windowManager.Close("crafting")' not in nav.split('case Destination.Inventory:')[1].split('case Destination.Character:')[0])

ck('Personal Crafting has explicit Inventory/Crafting surface modes', 'public enum SurfaceMode' in main and 'Inventory = 0' in main and 'Crafting = 1' in main)
ck('Public crafting group opens in Inventory mode', re.search(r'public override void OnOpen\(\).*?surfaceMode = SurfaceMode\.Inventory;.*?base\.OnOpen\(\);', main, re.S) is not None)
ck('Controller navigation begins on Inventory tab', '"btnRebirthCraftingTabInventory"' in main and '? "btnRebirthCraftingTabInventory"' in main)
ck('Inventory and Crafting tabs switch layout without closing', 'private void SetSurfaceMode(SurfaceMode mode)' in main and 'layoutService?.Apply(true);' in main)
ck('Layout audit permits deliberate mode geometry changes', 'sameSurface = hasAuditSurfaceBaseline && lastAuditInventoryMode == IsInventoryOnlyMode' in main)

ck('Compact Inventory root has dedicated width', 'InventoryRootWidth = 1000' in layout)
ck('Compact Inventory root has dedicated height', 'InventoryRootHeight = 650' in layout)
ck('Compact Inventory hides world-status module', 'SetControllerActive(owner.GetChildById("rebirthCraftingWorldStatus"), !inventoryOnly);' in layout)
ck('Compact Inventory hides left recipe/outcome zone', 'SetControllerActive(owner.GetChildById("rebirthCraftingLeftZone"), false);' in layout)
ck('Compact Inventory center fills body', 'centerWidth = bodyWidth;' in layout and '"rebirthCraftingCenterZone"' in layout)
ck('Queue authority is parked off-screen instead of deactivated', 'OffscreenZoneX = 10000' in layout and 'SetZone("rebirthCraftingRightZone"' in layout and 'SetControllerActive(owner.GetChildById("rebirthCraftingRightZone"), false)' not in layout)
ck('Compact Inventory removes Requirements surface', 'SetControllerActive(owner.GetChildById("rebirthCraftingRequirementsRegion"), false);' in layout)
ck('Compact Inventory places Inventory immediately below details', 'int inventoryY = -(detailsHeight + ZoneGap);' in layout)
ck('Compact Inventory keeps four-row inventory sizing path', 'ApplyInventoryInternalLayout(width, inventoryHeight);' in layout)
ck('Full Crafting still restores three-column layout', 'ApplyLeftInternalLayout(leftWidth, bodyHeight);' in layout and 'ApplyCenterInternalLayout(centerWidth, bodyHeight);' in layout and 'ApplyRightInternalLayout(rightWidth, bodyHeight);' in layout)

ck('Item context understands compact Inventory mode', 'public void ApplySurfaceMode()' in ctx and 'owner.IsInventoryOnlyMode' in ctx)
ck('Inventory idle keeps Selected Item surface instead of recipe surface', 'ShowInventoryIdleContext()' in ctx and 'SetRecipeDetailsVisible(false);' in ctx)
ck('Inventory idle hides action strip until item selection', 'SetControllerVisible(actionList, false);' in ctx)
ck('Compact item context excludes Requirements height', re.search(r'int requirementsHeight = owner != null && owner\.IsInventoryOnlyMode\s*\? 0', ctx) is not None)
ck('Selecting item still reveals close/action context', 'SetControllerVisible(closeButton, true);' in ctx)

ck('Audit has compact Inventory branch', 'if (owner.IsInventoryOnlyMode)' in audit)
ck('Audit requires Requirements hidden in Inventory', 'Requirements must be hidden in compact Inventory' in audit)
ck('Audit verifies queue authority parked off-screen', 'queue authority must be parked off-screen in compact Inventory' in audit)

# Regressions from the immediately preceding accepted surface.
invscroll=(root/'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingInventoryScroll.cs').read_text()
queue=(root/'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingQueue.cs').read_text()
qentry=(root/'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingQueueEntry.cs').read_text()
ck('Backpack smooth interpolation remains', 'Mathf.Lerp(pixelOffset, targetPixelOffset' in invscroll)
ck('No per-slot scroll culling is reintroduced', 'ApplySlotViewportVisibility' not in invscroll)
ck('Queue still uses SoftClip', 'SoftClip' in queue)
ck('PC124 immediate queue placement remains', 'TryUpdatePosition' in queue and 'QueuePlacement' in queue)
ck('Queue native cancellation/refund path remains', 'ForceCancel' in queue or 'HandleOnPress' in qentry)

for name,text in [('main',main),('tabs',tabs),('nav',nav),('layout',layout),('context',ctx),('audit',audit),('queue',queue),('qentry',qentry)]:
    ck('C# braces balanced: '+name, balanced(text))

passed=sum(1 for _,v in checks if v)
for i,(name,v) in enumerate(checks,1): print(f'{i:02d}. {"PASS" if v else "FAIL"}: {name}')
print(f'\nResult: {passed}/{len(checks)} checks passed')
sys.exit(0 if passed==len(checks) else 1)
