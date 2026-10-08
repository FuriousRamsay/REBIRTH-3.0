from pathlib import Path
import re, sys, xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]
windows = ROOT / 'Config/XUi_InGame/windows.xml'
templates = ROOT / 'Config/XUi_InGame/templates.xml'
actions = ROOT / 'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingActions.cs'
slot = ROOT / 'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingInventorySlot.cs'
context = ROOT / 'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingItemContext.cs'
recipe_entry = ROOT / 'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingRecipeEntry.cs'
scroll = ROOT / 'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingInventoryScroll.cs'
queue = ROOT / 'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingQueue.cs'
queue_entry = ROOT / 'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingQueueEntry.cs'

checks=[]
def check(name, cond): checks.append((name, bool(cond)))

for path in (windows, templates):
    try:
        ET.parse(path)
        check('XML parses: ' + path.name, True)
    except Exception:
        check('XML parses: ' + path.name, False)

w = windows.read_text(encoding='utf-8')
a = actions.read_text(encoding='utf-8')
s = slot.read_text(encoding='utf-8')
c = context.read_text(encoding='utf-8')
r = recipe_entry.read_text(encoding='utf-8')
sc = scroll.read_text(encoding='utf-8')
q = queue.read_text(encoding='utf-8')
qe = queue_entry.read_text(encoding='utf-8')

# Recipe action surface: full rendered bounds, exact native-style hover, and shortcuts inside.
check('PC123 action strip uses wider 714px surface', 'name="rebirthCraftingActionsStrip" pos="8,-198" width="714"' in w)
for name in ('Craft','Favorite','Track','Explorer'):
    check(f'PC123 {name} action is a full-width sprite hit surface', re.search(rf'<sprite name="btnRebirthCrafting{name}"[^>]*width="174"[^>]*on_press="true"[^>]*on_hover="true"', w) is not None)
    check(f'PC123 {name} action is no longer a labeledbutton', f'<labeledbutton name="btnRebirthCrafting{name}"' not in w)
for name in ('Craft','Favorite','Track','Explorer'):
    check(f'PC123 {name} shortcut authored right aligned', re.search(rf'name="rebirthCrafting{name}Shortcut"[^>]*justify="right"', w) is not None)
    check(f'PC123 {name} has separate caption label', f'name="rebirthCrafting{name}Caption"' in w)

check('PC123 runtime buttons use 6px gaps', 'int gap = 6;' in a)
check('PC123 runtime buttons fill four equal responsive columns', '(width - gap * 3) / 4' in a and 'int explorerX = (buttonWidth + gap) * 3;' in a)
check('PC123 shortcut pocket is calculated from button right edge', 'int shortcutX = x + width - shortcutRightMargin - shortcutWidth;' in a)
check('PC123 shortcut pocket has fixed 10px right margin', 'const int shortcutRightMargin = 10;' in a)
check('PC123 caption is bounded before shortcut pocket', 'int captionWidth = Math.Max(34, shortcutX - captionX - 4);' in a)
check('PC123 action hover is wired directly on full sprite', 'controller.OnHover += Action_OnHover;' in a)
check('PC123 action hover uses native ItemActionEntry sprite', 'background.SpriteName = "ui_game_select_row";' in a)
check('PC123 action hover uses native ItemActionEntry white tint', 'background.Color = Color.white;' in a)
check('PC123 normal action visual restores outline sprite', 'background.SpriteName = "menu_empty2px";' in a)
check('PC123 no labeledbutton/XUiV_Button hover shim remains', 'HoverSpriteName' not in a and 'ResolveButton' not in a and 'XUiV_Button' not in a)
check('PC123 disabled action content is still visibly disabled', 'SetActionContentColor("rebirthCraftingCraftIcon"' in a and 'new Color32(120, 120, 126, 255)' in a)
check('PC123 hidden Track also hides its caption', 'SetVisible(trackCaption?.Controller, canTrack);' in a)

# Backpack selection: explicit one-owner border state, not inferred from global XUi selection.
check('PC123 slot stores explicit Rebirth context selection', 'private bool rebirthContextSelected;' in s)
check('PC123 slot exposes explicit selection setter', 'public void SetRebirthContextSelected(bool selected)' in s)
check('PC123 selected border is opaque white', 'selectionBorder.SetColorImmediately(Color.white);' in s)
check('PC123 deselected old slot clears white border immediately', 'selectionBorder.SetColorImmediately(backgroundColor);' in s)
check('PC123 per-frame border reapply is gated by explicit flag', 'if (!rebirthContextSelected)' in s)
check('PC123 selecting a new slot explicitly clears the previous Rebirth border first', 'selectedSlot.SetRebirthContextSelected(false);' in c and 'selectedSlot = slot;' in c and 'selectedSlot.SetRebirthContextSelected(true);' in c)
check('PC123 closing item context explicitly clears the Rebirth border', c.count('selectedSlot.SetRebirthContextSelected(false);') >= 2)

# Preserve the compile recovery and smooth-scrolling/queue architecture.
check('PC122 recipe selection compile recovery remains', 'if (Selected)' not in r and re.search(r'\bSelected\s*=\s*false\s*;', r) is None)
check('Recipe selection still uses Rebirth-owned logical state', 'private bool rebirthSelected;' in r and 'ApplySelectedVisual(rebirthSelected' in r)
check('Backpack smooth interpolation remains', 'Mathf.Lerp(pixelOffset, targetPixelOffset, t)' in sc)
check('Per-slot scrolling visibility cull remains absent', 'ApplySlotViewportVisibility' not in sc and 'controllerVisible' in sc)
check('Queue retains single-native-tick optimization', 'nativeTick' in qe and 'IsCrafting' in qe)
check('Queue retains SoftClip runtime clipping', 'SoftClip' in q)

for path, text in ((actions,a),(slot,s),(context,c),(recipe_entry,r),(scroll,sc),(queue,q),(queue_entry,qe)):
    check('C# braces balanced: ' + path.name, text.count('{') == text.count('}'))

for i,(name,ok) in enumerate(checks,1):
    print(f'{i:02d}. {"PASS" if ok else "FAIL"}: {name}')
passed=sum(ok for _,ok in checks)
print(f'\nResult: {passed}/{len(checks)} checks passed')
if passed != len(checks): sys.exit(1)
