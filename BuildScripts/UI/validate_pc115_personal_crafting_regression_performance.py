#!/usr/bin/env python3
from pathlib import Path
import re, sys, xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]
WINDOWS = ROOT/'Config'/'XUi_InGame'/'windows.xml'
TEMPLATES = ROOT/'Config'/'XUi_InGame'/'templates.xml'
BASE = ROOT/'Scripts'/'Crafting'/'UI'/'PersonalCrafting'
ITEM = BASE/'XUiC_RebirthCraftingItemContext.cs'
INV = BASE/'XUiC_RebirthCraftingInventoryScroll.cs'
QUEUE = BASE/'XUiC_RebirthCraftingQueue.cs'
ENTRY = BASE/'XUiC_RebirthCraftingQueueEntry.cs'
OWNER = BASE/'XUiC_RebirthPersonalCrafting.cs'
OUTCOME = BASE/'XUiC_RebirthCraftingOutcome.cs'

checks=[]
def ck(name, cond, detail=''):
    checks.append((name,bool(cond),detail))

paths=(WINDOWS,TEMPLATES,ITEM,INV,QUEUE,ENTRY,OWNER,OUTCOME)
src={p:p.read_text(encoding='utf-8') for p in paths}
w,t,item,inv,queue,entry,owner,outcome=[src[p] for p in paths]

for p in (WINDOWS,TEMPLATES):
    try:
        ET.parse(p); ck('XML parses: '+p.name,True)
    except Exception as e: ck('XML parses: '+p.name,False,str(e))

# Action optical centering: authored and runtime must agree so native ItemAction refresh cannot undo it.
action_block=re.search(r'<rect name="rebirthCraftingItemActionList".*?</rect>\s*</rect>',w,re.S)
action=action_block.group(0) if action_block else ''
ck('Selected-item action list found',bool(action))
ck('Five action captions authored at y=-10',len(re.findall(r'<label[^>]*name="name"[^>]*pos="0,-10"',action))==5)
ck('Five shortcut labels authored at y=-10',len(re.findall(r'<label[^>]*name="keyboardButton"[^>]*pos="57,-10"',action))==5)
ck('Runtime action optical correction is 5px below geometric center','int labelY = -Math.Max(0, (listHeight - 24) / 2) - 5;' in item)
ck('Runtime item-action geometry writes are change-aware','controller.ViewComponent.Position.x != position.x' in item and 'controller.ViewComponent.Size.x != size.x' in item)
ck('Selected-item region visibility writes are change-aware','private static void SetControllerActive' in item and 'go.activeSelf != visible' in item)
ck('Hidden selected-item sibling does not fight harmless view flags every frame','if (!visible && go != null && !go.activeSelf)' in item)
ck('Per-frame action layout no longer allocates activeEntries List','private void ApplyActionLabelPresentation()' in item and 'List<XUiController> activeEntries' not in item[item.index('private void ApplyActionLabelPresentation()'):item.index('private static string NormalizeShortcut')])

# Existing requested cleanup remains intact.
ck('Requirements 1/6 label remains removed','name="rebirthCraftingRequirementCount"' not in w)
ck('Result icon outline remains removed','name="rebirthCraftingOutcomeResultIconFrame"' not in w)
ck('Outcome tolerates missing result frame','SetControllerVisible(GetChildById("rebirthCraftingOutcomeResultIconFrame"), false);' in outcome)

# Queue cancel remains on the native refund path but is visibly smaller.
qt=re.search(r'<rebirth_personal_crafting_queue_entry>(.*?)</rebirth_personal_crafting_queue_entry>',t,re.S)
chunk=qt.group(1) if qt else ''
ck('Queue template found',bool(chunk))
ck('Native background remains stock X cancel control',re.search(r'<button[^>]*name="background"[^>]*pos="195,-36"[^>]*width="18"[^>]*height="18"[^>]*sprite="ui_game_symbol_x"',chunk,re.S) is not None)
ck('Cancel X hover scale reduced to 1.16','hoverscale="1.16"' in chunk)
ck('Native compatibility cancel sprite matches compact geometry',re.search(r'<sprite[^>]*name="cancel"[^>]*pos="195,-36"[^>]*width="18"[^>]*height="18"[^>]*color="0,0,0,0"',chunk) is not None)
ck('No proxy/manual cancellation path added','rebirthQueueCancelButton' not in entry and 'PlayerInventory.AddItem' not in entry and 'HandleOnPress(sender' not in entry)
ck('Queue-entry native cancel geometry is 18px','const int cancelSize = 18;' in entry and 'const int cancelY = -36;' in entry)
ck('Queue viewport uses updated compact cancel bounds','const float cancelTop = 36f;' in queue and 'const float cancelBottom = 54f;' in queue)

# Restore exact proven backpack movement from PC112: grid moves, slots are not toggled mid-interpolation.
ck('Backpack grid still interpolates continuously','inventory.ViewComponent.Position = new Vector2i(baseGridPosition.x, baseGridPosition.y + Mathf.RoundToInt(pixelOffset));' in inv)
ck('PC113/114 per-slot viewport visibility culling removed','ApplySlotViewportVisibility' not in inv)
ck('Inventory scrollbar remains 16/12',re.search(r'name="rebirthCraftingInventoryScrollTrack"[^>]*width="16"',w) is not None and re.search(r'name="rebirthCraftingInventoryScrollThumb"[^>]*width="12"',w) is not None)

# Main sustained-FPS correction: 50 custom queue slots still exist, but only the active recipe stack runs native RecipeStack.Update.
ck('All 50 native queue slots remain authored',w.count('<rebirth_personal_crafting_queue_entry name="rebirthCraftingQueueSlot')==50)
ck('Queue presentation no longer calls allocating GetRuntimeEntries per frame','RebirthCraftingQueueBridge.GetRuntimeEntries(this)' not in queue)
ck('RuntimeCapacity uses cached custom entry array','entries != null ? entries.Length : 0' in queue)
update=re.search(r'public override void Update\(float dt\)(.*?)\n    \}',entry,re.S)
ut=update.group(1) if update else ''
ck('Queue entry native tick is gated by HasRecipe + IsCrafting','bool nativeTick = HasRecipeForPresentation && IsCrafting;' in ut)
ck('Queue entry only calls base.Update under nativeTick',re.search(r'if \(nativeTick\)\s+base\.Update\(dt\);',ut) is not None)
ck('Queue entry has no unconditional base.Update before nativeTick',ut.find('base.Update(dt);')>ut.find('if (nativeTick)') if 'base.Update(dt);' in ut else False)
ck('Queued cards refresh static native icon/count/time without native per-frame tick','RefreshNativeStaticPresentation(recipe);' in entry and 'GetRecipeCount()' in entry and 'GetTotalRecipeCraftingTimeLeft()' in entry)
ck('Queue progress Fill write is change-aware','Mathf.Abs(progressFill.Fill - clamped) > 0.0005f' in entry)
ck('Queue trace reports native tick count',' + " nativeTicks=" + nativeTicks' in queue)
ck('Queue SoftClip remains intact','UIDrawCall.Clipping.SoftClip' in queue and 'rowsClipPanel.baseClipRegion' in queue)
ck('Queue partial-edge rendering remains intact','bool intersectsViewport = bottom > 0.01f && top < viewportHeight - 0.01f;' in queue)

# Stop the HUD watchdog from fighting harmless IsVisible/Enabled writes on already-inactive roots.
ck('HUD watchdog treats inactive zero-scale roots as stable','if (!active && scaleSuppressed)' in owner)
maint=re.search(r'private void MaintainGameplayHudSuppression\(\)(.*?)(?=\n\s*// V3\.2|\n\s*private static FieldInfo)',owner,re.S)
mt=maint.group(1) if maint else ''
ck('HUD watchdog no longer performs dormant reflection each poll','TryGetControllerDormant' not in mt and 'SetControllerDormant' not in mt)
ck('Owner still avoids direct compile-time IsDormant access','.IsDormant' not in owner)

# Scrollbar parity across all three custom scrollbars.
ck('Recipe scrollbar remains 16/12',re.search(r'name="rebirthCraftingRecipeScrollTrack"[^>]*width="16"',w) is not None and re.search(r'name="rebirthCraftingRecipeScrollThumb"[^>]*width="12"',w) is not None)
ck('Queue scrollbar remains 16/12',re.search(r'name="rebirthCraftingQueueScrollTrack"[^>]*width="16"',w) is not None and re.search(r'name="rebirthCraftingQueueScrollThumb"[^>]*width="12"',w) is not None)

# Lightweight structural safety checks for changed C# files.
def brace_balance(s):
    # Sufficient for these files; no interpolated-string brace changes were introduced by PC115.
    return s.count('{')==s.count('}')
for p in (ITEM,INV,QUEUE,ENTRY,OWNER):
    ck('C# braces balanced: '+p.name,brace_balance(src[p]))

failed=[c for c in checks if not c[1]]
for i,(name,ok,detail) in enumerate(checks,1):
    print(f'{i:02d}. {"PASS" if ok else "FAIL"}: {name}' + (f' ({detail})' if detail else ''))
print(f'\nResult: {len(checks)-len(failed)}/{len(checks)} checks passed')
sys.exit(1 if failed else 0)
