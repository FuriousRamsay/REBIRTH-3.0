#!/usr/bin/env python3
from pathlib import Path
import re
import sys
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]
WINDOWS = ROOT / "Config" / "XUi_InGame" / "windows.xml"
TEMPLATES = ROOT / "Config" / "XUi_InGame" / "templates.xml"
QUEUE = ROOT / "Scripts" / "Crafting" / "UI" / "PersonalCrafting" / "XUiC_RebirthCraftingQueue.cs"
ENTRY = ROOT / "Scripts" / "Crafting" / "UI" / "PersonalCrafting" / "XUiC_RebirthCraftingQueueEntry.cs"
INV = ROOT / "Scripts" / "Crafting" / "UI" / "PersonalCrafting" / "XUiC_RebirthCraftingInventoryScroll.cs"
OUTCOME = ROOT / "Scripts" / "Crafting" / "UI" / "PersonalCrafting" / "XUiC_RebirthCraftingOutcome.cs"
OWNER = ROOT / "Scripts" / "Crafting" / "UI" / "PersonalCrafting" / "XUiC_RebirthPersonalCrafting.cs"

checks = []
def check(name, condition, detail=""):
    checks.append((name, bool(condition), detail))

sources = {p: p.read_text(encoding="utf-8") for p in (WINDOWS,TEMPLATES,QUEUE,ENTRY,INV,OUTCOME,OWNER)}
windows=sources[WINDOWS]
templates=sources[TEMPLATES]
queue=sources[QUEUE]
entry=sources[ENTRY]
inv=sources[INV]
outcome=sources[OUTCOME]
owner=sources[OWNER]

for p in (WINDOWS,TEMPLATES):
    try:
        ET.parse(p)
        check(f"XML parses: {p.name}", True)
    except Exception as exc:
        check(f"XML parses: {p.name}", False, str(exc))

# User-facing polish.
action_block = re.search(r'<rect name="rebirthCraftingItemActionList".*?</rect>\s*</rect>', windows, re.S)
action = action_block.group(0) if action_block else ""
check("Action list found", bool(action))
check("All five action captions lowered to y=-8", len(re.findall(r'<label[^>]*name="name"[^>]*pos="0,-8"', action)) == 5)
check("All five keyboard shortcuts lowered to y=-8", len(re.findall(r'<label[^>]*name="keyboardButton"[^>]*pos="57,-8"', action)) == 5)
check("Requirements count label physically removed", 'name="rebirthCraftingRequirementCount"' not in windows)
check("Result icon frame physically removed", 'name="rebirthCraftingOutcomeResultIconFrame"' not in windows)
check("Outcome code can tolerate missing frame", 'SetControllerVisible(GetChildById("rebirthCraftingOutcomeResultIconFrame"), false);' in outcome)

# Native cancel/refund path: the real child named background is the stock X icon button.
queue_template = re.search(r'<rebirth_personal_crafting_queue_entry>(.*?)</rebirth_personal_crafting_queue_entry>', templates, re.S)
chunk = queue_template.group(1) if queue_template else ""
check("Queue template found", bool(chunk))
check("Native background is the stock X icon button",
      re.search(r'<button[^>]*name="background"[^>]*sprite="ui_game_symbol_x"', chunk) is not None)
check("Native cancel compatibility child remains sprite",
      re.search(r'<sprite[^>]*name="cancel"[^>]*color="0,0,0,0"', chunk) is not None)
check("No proxy cancel button remains", 'rebirthQueueCancelButton' not in chunk and 'rebirthQueueCancelButton' not in entry)
check("No literal X label remains", 'rebirthQueueCancelLabel' not in chunk and 'rebirthQueueCancelLabel' not in entry)
check("Cancel icon has hover enlargement", re.search(r'name="background"[^>]*hoverscale="1\.22"', chunk) is not None)
check("Entry preserves base Init native event wiring", 'base.Init();' in entry)
check("Entry explicitly preserves background input flags",
      'nativeBackground.ViewComponent.EventOnPress = true;' in entry and
      'nativeBackground.ViewComponent.EventOnHover = true;' in entry)
check("Entry never implements manual refund/cancel transaction", 'HandleOnPress(sender' not in entry and 'PlayerInventory.AddItem' not in entry)

# Queue smooth clipping + performance culling.
check("50 native queue entry children remain authored",
      windows.count('<rebirth_personal_crafting_queue_entry name="rebirthCraftingQueueSlot') == 50)
check("Queue runtime SoftClip remains", 'UIDrawCall.Clipping.SoftClip' in queue and 'rowsClipPanel.baseClipRegion' in queue)
check("Queue culls only fully offscreen cards",
      'bool intersectsViewport = bottom > 0.01f && top < viewportHeight - 0.01f;' in queue)
check("Queue visibility follows intersection", 'SetVisible(entry, intersectsViewport);' in queue)
check("Queue edge card can stay visible while cancel hit is gated",
      'entry.SetCancelHitEnabled(intersectsViewport &&' in queue)
check("Queue no longer keeps all active cards visible", 'SetVisible(entry, true);' not in queue)
check("Queue entry still runs native update while hidden",
      'base.Update(dt);' in entry and 'if (ViewComponent == null || !ViewComponent.IsVisible)' in entry)
check("Queue trace still reports visible-card count", '" visible=" + visible' in queue)

# Backpack inventory: preserve partial rows, cull fully offscreen ItemStack visuals.
check("Inventory applies viewport visibility after motion", 'ApplySlotViewportVisibility();' in inv)
check("Inventory uses partial-row intersection",
      'bool intersectsViewport = bottom > 0.01f && top < viewportHeight - 0.01f;' in inv)
check("Inventory culls fully offscreen authoritative slots",
      'slot.ViewComponent.IsVisible = intersectsViewport;' in inv)
check("Inventory trace reports visible controller count", ' + " controllerVisible=" + visibleControllerCount' in inv)

# Remove duplicated periodic work.
check("Outcome only forces requirements when recipe/batch changes",
      'bool selectionChanged = recipe != lastRecipe || batch != lastBatch;' in outcome and
      re.search(r'if \(selectionChanged\)\s+requirements\?\.RefreshNow\(\);', outcome) is not None)
check("Owner HUD roots are made dormant", 'controller.IsDormant = true;' in owner)
check("Owner HUD roots are made inactive", 'UiTransform.gameObject.SetActive(false);' in owner)
check("Owner captures and restores HUD active state",
      'hudActiveBeforeOpen' in owner and 'gameObject.SetActive(active);' in owner)
check("Owner captures and restores HUD dormancy",
      'hudDormantBeforeOpen' in owner and 'pair.Key.IsDormant = dormant;' in owner)
check("HUD suppression watchdog is 4 Hz rather than every frame",
      'nextHudSuppressionCheck = Time.realtimeSinceStartup + 0.25f;' in owner and
      re.search(r'if \(Time\.realtimeSinceStartup >= nextHudSuppressionCheck\).*?MaintainGameplayHudSuppression\(\);', owner, re.S) is not None)
update_body = re.search(r'public override void Update\(float dt\)(.*?)\n    \}\n\n', owner, re.S)
update_text = update_body.group(1) if update_body else ""
check("No unconditional HUD suppression in owner Update",
      'MaintainGameplayHudSuppression();' in update_text and
      update_text.find('if (Time.realtimeSinceStartup >= nextHudSuppressionCheck)') < update_text.find('MaintainGameplayHudSuppression();'))
check("Layout audit no longer has timed idle poll", 'nextLayoutAuditCheck' not in owner)
check("Layout audit runs only when requested", re.search(r'if \(layoutAuditRequested\)\s+RunLayoutAudit\(false\);', owner) is not None)

# Scrollbar parity remains untouched.
check("Recipe scrollbar remains 16/12 authored",
      re.search(r'name="rebirthCraftingRecipeScrollTrack"[^>]*width="16"', windows) is not None and
      re.search(r'name="rebirthCraftingRecipeScrollThumb"[^>]*width="12"', windows) is not None)
check("Inventory scrollbar remains 16/12 authored",
      re.search(r'name="rebirthCraftingInventoryScrollTrack"[^>]*width="16"', windows) is not None and
      re.search(r'name="rebirthCraftingInventoryScrollThumb"[^>]*width="12"', windows) is not None)
check("Queue scrollbar remains 16/12 authored",
      re.search(r'name="rebirthCraftingQueueScrollTrack"[^>]*width="16"', windows) is not None and
      re.search(r'name="rebirthCraftingQueueScrollThumb"[^>]*width="12"', windows) is not None)

def balanced(source):
    # All modified files use balanced block braces; a lightweight structural check is sufficient
    # here and avoids false negatives from C# interpolated strings/char literals.
    return source.count("{") == source.count("}")

for p in (QUEUE,ENTRY,INV,OUTCOME,OWNER):
    check(f"C# braces balanced: {p.name}", balanced(sources[p]))

failed=[x for x in checks if not x[1]]
for i,(name,ok,detail) in enumerate(checks,1):
    suffix=f" ({detail})" if detail else ""
    print(f'{i:02d}. {"PASS" if ok else "FAIL"}: {name}{suffix}')
print(f"\nResult: {len(checks)-len(failed)}/{len(checks)} checks passed")
sys.exit(1 if failed else 0)
