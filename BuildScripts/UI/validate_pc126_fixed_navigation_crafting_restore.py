#!/usr/bin/env python3
from pathlib import Path
import re, sys, xml.etree.ElementTree as ET

ROOT = Path(sys.argv[1]) if len(sys.argv) > 1 else Path(__file__).resolve().parents[2]
win = (ROOT/'Config/XUi_InGame/windows.xml').read_text(encoding='utf-8')
layout = (ROOT/'Scripts/Crafting/UI/PersonalCrafting/RebirthPersonalCraftingLayoutService.cs').read_text(encoding='utf-8')
audit = (ROOT/'Scripts/Crafting/UI/PersonalCrafting/RebirthPersonalCraftingLayoutAudit.cs').read_text(encoding='utf-8')
main = (ROOT/'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthPersonalCrafting.cs').read_text(encoding='utf-8')
tabs = (ROOT/'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingTopTabs.cs').read_text(encoding='utf-8')
ctx = (ROOT/'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingItemContext.cs').read_text(encoding='utf-8')
actions = (ROOT/'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingActions.cs').read_text(encoding='utf-8')
invscroll = (ROOT/'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingInventoryScroll.cs').read_text(encoding='utf-8')
queue = (ROOT/'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingQueue.cs').read_text(encoding='utf-8')
qentry = (ROOT/'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingQueueEntry.cs').read_text(encoding='utf-8')

checks=[]
def ck(name, cond): checks.append((name,bool(cond)))
def balanced(s): return s.count('{') == s.count('}')

for f in ['Config/XUi_InGame/windows.xml','Config/XUi_InGame/templates.xml']:
    try: ET.parse(ROOT/f); ok=True
    except Exception: ok=False
    ck('XML parses: '+Path(f).name, ok)

# Fixed navigation shell: Inventory/Crafting must not resize top/root.
ck('Inventory no longer has a separate root width', 'InventoryRootWidth' not in layout)
ck('Inventory no longer has a separate root height', 'InventoryRootHeight' not in layout)
ck('Root width is always based on FixedRootWidth', 'int rootWidth = Math.Min(FixedRootWidth' in layout)
ck('Root height is always based on FixedRootHeight', 'int rootHeight = Math.Min(FixedRootHeight' in layout)
ck('Top layout has no inventoryOnly argument', 'ApplyTopLayout(int width)' in layout and 'ApplyTopLayout(int width, bool inventoryOnly)' not in layout)
ck('World status remains visible on both surfaces', 'SetControllerActive(owner.GetChildById("rebirthCraftingWorldStatus"), true);' in layout)
ck('Inventory does not hide the world status', 'SetControllerActive(owner.GetChildById("rebirthCraftingWorldStatus"), !inventoryOnly);' not in layout)
ck('Inventory only hides the large root shell, not the nav', 'SetControllerActive(owner.GetChildById("rebirthCraftingRootBackground"), !inventoryOnly);' in layout and 'SetControllerActive(owner.GetChildById("rebirthCraftingRootFrame"), !inventoryOnly);' in layout)

# Compact Inventory is an independent centered panel beneath the fixed nav.
ck('Compact Inventory has independent panel width', 'InventoryPanelWidth = 1000' in layout)
ck('Compact Inventory has independent panel height', 'InventoryPanelHeight = 572' in layout)
ck('Inventory panel is centered inside full body', 'int centerX = Math.Max(0, (bodyWidth - centerWidth) / 2);' in layout)
# PC129 supersedes the original compact-panel y=0 with a bottom-aligned centerY.
ck('Inventory center stays horizontally centered and uses bottom-aligned y', 'SetZone("rebirthCraftingCenterZone", "rebirthCraftingCenterBackground", "rebirthCraftingCenterFrame", centerX, centerY, centerWidth, centerHeight);' in layout and 'int centerY = -Math.Max(0, bodyHeight - centerHeight);' in layout)
ck('Inventory left zone remains hidden', 'SetControllerActive(owner.GetChildById("rebirthCraftingLeftZone"), false);' in layout)
ck('Inventory queue authority remains parked offscreen', 'SetZone("rebirthCraftingRightZone", "rebirthCraftingRightBackground", "rebirthCraftingRightFrame", OffscreenZoneX' in layout)
ck('Inventory Requirements remain absent', 'SetControllerActive(owner.GetChildById("rebirthCraftingRequirementsRegion"), false);' in layout)

# Crafting must still use the approved three-column PC124 formulas.
ck('Crafting left column keeps 25 percent allocation', 'Math.Round(availableWidth * 0.25f)' in layout)
ck('Crafting right column keeps 27 percent allocation', 'Math.Round(availableWidth * 0.27f)' in layout)
ck('Crafting center receives exact remaining width', 'availableWidth - leftWidth - rightWidth' in layout)
ck('Crafting zones retain 10px gaps', 'private const int ZoneGap = 10;' in layout)
ck('Crafting restores all three visible zones', all(x in layout for x in [
    'SetControllerActive(owner.GetChildById("rebirthCraftingLeftZone"), true);',
    'SetControllerActive(owner.GetChildById("rebirthCraftingCenterZone"), true);',
    'SetControllerActive(owner.GetChildById("rebirthCraftingRightZone"), true);']))
ck('Crafting restores full left/center/right internal layouts', all(x in layout for x in [
    'ApplyLeftInternalLayout(leftWidth, bodyHeight);',
    'ApplyCenterInternalLayout(centerWidth, bodyHeight);',
    'ApplyRightInternalLayout(rightWidth, bodyHeight);']))
ck('Crafting center keeps 34 percent details allocation', 'Math.Round(height * 0.34f)' in layout)
ck('Crafting center keeps 24 percent Requirements allocation', 'Math.Round(height * 0.24f)' in layout)
ck('Crafting backpack keeps 310 minimum region height', 'const int inventoryMinHeight = 310;' in layout)

# No action strip may bleed into queue after a mode switch.
strip_expr='SetRect(owner.GetChildById("rebirthCraftingActionsStrip"), 8, -198, Math.Max(360, width - 16), 34);'
ck('Recipe action strip is width-bound to Crafting center', layout.count(strip_expr) >= 2)
ck('Selected Item reads the same recipe action strip geometry', 'owner.GetChildById("rebirthCraftingActionsStrip")' in ctx)
ck('Selected Item action list uses strip size', 'recipeActionSize.x' in ctx and 'SetRect(list, listX, recipeActionPos.y, listWidth, listHeight);' in ctx)
ck('Recipe action controller remains responsive to actual strip width', 'int width = Math.Max(320, ViewComponent.Size.x);' in actions and 'ApplyGeometry(false);' in actions)
ck('Layout audit now checks recipe action containment', 'RequireContained("recipeActions/details"' in audit)
ck('Layout audit checks selected-item action containment', 'RequireContained("itemActions/itemContext"' in audit)

# Navigation visual density and old-style separators.
ck('Eight navigation tabs remain authored', 'new TabVisual[8]' in tabs)
ck('Inventory remains first tab', 'tabs[0] = Wire("Inventory"' in tabs)
ck('Authored nav fallback uses full 1856 width', 'name="rebirthCraftingTopZone" pos="8,-8" width="1856"' in win)
ck('Authored tabs fallback uses 1576 width', 'name="rebirthCraftingTopTabs" width="1576"' in win)
ck('World status fallback uses compact 270 width', 'name="rebirthCraftingWorldStatus" pos="1586,0" width="270"' in win)
ck('Tab labels no longer use shrinkcontent', not re.search(r'name="rebirthCraftingTab(?:Inventory|Crafting|Character|Map|Skills|Quests|Challenges|Players)Label"[^>]*overflow="shrinkcontent"', win))
# PC133 shares this navigation on Character too. Validate eight frames PER host,
# rather than treating the second valid navigation instance as duplicate global controls.
nav_hosts = [e for e in ET.fromstring(win).iter() if e.get('name') == 'rebirthCraftingTopTabs']
frames_per_host = [[e for e in host.iter('sprite') if re.fullmatch(r'rebirthCraftingTab(?:Inventory|Crafting|Character|Map|Skills|Quests|Challenges|Players)Frame', e.get('name', ''))] for host in nav_hosts]
ck('All eight tab separator frames are authored per navigation host', bool(frames_per_host) and all(len(frames)==8 for frames in frames_per_host))
ck('All eight tab separator frames remain visible per navigation host', bool(frames_per_host) and all(len(frames)==8 and all(f.get('visible') != 'false' for f in frames) for frames in frames_per_host))
ck('Runtime status reservation is compact enough for PC124-like tab width', 'width * 0.145f' in layout and 'TopStatusMinWidth = 254' in layout and 'TopStatusMaxWidth = 286' in layout)

# Surface routing remains as requested.
ck('Tab/native group still opens on Inventory surface', 'surfaceMode = SurfaceMode.Inventory;' in main)
ck('Crafting tab still switches internally without reopening group', 'public void ShowCraftingSurface()' in main and 'SetSurfaceMode(SurfaceMode.Crafting);' in main)
ck('Inventory tab still switches internally without reopening group', 'public void ShowInventorySurface()' in main and 'SetSurfaceMode(SurfaceMode.Inventory);' in main)

# Audit reflects the new architecture rather than PC125 root resizing.
ck('Audit requires centered compact Inventory', 'compact Inventory panel must be centered beneath fixed navigation' in audit)
ck('Audit requires fixed-nav status in Inventory', 'world status must remain visible because navigation is surface-independent' in audit)
ck('Audit requires transparent large root shell in Inventory', 'full Crafting root shell must remain transparent in compact Inventory' in audit)
ck('Audit labels Inventory as fixed-nav', 'surface=Inventory fixed-nav + centered compact panel' in audit)
ck('Audit still labels Crafting as full three-column', 'surface=Crafting full three-column' in audit)

# Critical accepted regressions stay protected.
ck('Backpack smooth interpolation remains', 'Mathf.Lerp(pixelOffset, targetPixelOffset' in invscroll)
ck('Per-slot backpack scroll culling remains absent', 'ApplySlotViewportVisibility' not in invscroll)
ck('Queue SoftClip remains', 'UIDrawCall.Clipping.SoftClip' in queue)
ck('PC124 immediate queue placement remains', 'TryUpdatePosition' in queue and '[REBIRTH Crafting QueuePlacement]' in queue)
ck('Queue native cancellation/refund remains', 'ForceCancel();' in qentry)
ck('Queue remains single-native-tick', 'bool nativeTick = HasRecipeForPresentation && IsCrafting;' in qentry)

for name,text in [('layout',layout),('audit',audit),('main',main),('tabs',tabs),('context',ctx),('actions',actions),('inventoryScroll',invscroll),('queue',queue),('queueEntry',qentry)]:
    ck('C# braces balanced: '+name, balanced(text))

passed=sum(v for _,v in checks)
for i,(name,v) in enumerate(checks,1): print(f'{i:02d}. {"PASS" if v else "FAIL"}: {name}')
print(f'\nResult: {passed}/{len(checks)} checks passed')
sys.exit(0 if passed==len(checks) else 1)
