#!/usr/bin/env python3
from pathlib import Path
import sys
import xml.etree.ElementTree as ET

root = Path(sys.argv[1] if len(sys.argv) > 1 else '.').resolve()
paths = {
    'xml': root/'Config/XUi_InGame/windows.xml',
    'debug': root/'Config/_Survivor/debug.xml',
    'logs': root/'Scripts/Logging/RebirthLogSettings.cs',
    'owner': root/'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthPersonalCrafting.cs',
    'layout': root/'Scripts/Crafting/UI/PersonalCrafting/RebirthPersonalCraftingLayoutService.cs',
    'catalogue': root/'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingRecipeCatalogue.cs',
    'entry': root/'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingRecipeEntry.cs',
    'inventory': root/'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingInventory.cs',
    'invscroll': root/'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingInventoryScroll.cs',
    'invbridge': root/'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingInventoryBridge.cs',
}
pass_count = fail_count = 0

def chk(cond, label):
    global pass_count, fail_count
    if cond:
        pass_count += 1; print('PASS - ' + label)
    else:
        fail_count += 1; print('FAIL - ' + label)

for key,p in paths.items():
    chk(p.exists(), f'exists {p.relative_to(root)}')

for key in ('xml','debug'):
    try:
        ET.parse(paths[key]); chk(True, f'{paths[key].name} parses')
    except Exception as e:
        chk(False, f'{paths[key].name} parses: {e}')

src = {k:p.read_text(encoding='utf-8-sig') for k,p in paths.items() if p.exists()}
xml = src['xml']

# Dedicated release-runtime diagnostic gate.
chk('crafting_ui_logging="true"' in src['debug'], 'Crafting UI runtime diagnostics enabled in debug.xml for PC093 diagnostic pass')
chk('private static bool craftingUi;' in src['logs'], 'log settings owns crafting UI flag')
chk('CraftingUiLoggingEnabled' in src['logs'], 'log settings exposes crafting UI flag')
chk('ReadBool(root, "crafting_ui_logging"' in src['logs'], 'log settings reads crafting_ui_logging')
chk('[REBIRTH Crafting Trace] startup logging=True' in src['logs'], 'crafting diagnostic gate announces itself at startup')

# Recipe-scroll evidence must report live input, rebuilds and offsets.
for token in ['[REBIRTH Crafting RecipeScroll] wheelIgnored',
              '[REBIRTH Crafting RecipeScroll] searchChanged',
              '[REBIRTH Crafting RecipeScroll] rebuild reason=',
              '[REBIRTH Crafting RecipeScroll] snapshot instance=',
              'inputOffsetPx=']:
    chk(token in src['catalogue'], f'recipe trace contains {token}')
chk('public void HandleWheel(float delta)' in src['catalogue'], 'one-argument wheel contract retained')
chk('public void HandleWheel(float delta, string source)' in src['catalogue'], 'diagnostic wheel overload records source')
chk('resetScrollOnNextRebuild = userEdit;' in src['catalogue'], 'programmatic search maintenance cannot reset recipe scroll')
chk('pendingRebuildReason = userEdit ? "search-change-user" : "search-change-code";' in src['catalogue'], 'search reset reason distinguishes user/code changes')
chk('targetScrollOffsetPixels = Mathf.Clamp(authoritativeScrollTargetPixels, 0f, MaxPixelOffset);' in src['catalogue'], 'background rebuild preserves player scroll target')
chk('FinishRebuild(false, "availability")' in src['catalogue'], 'inventory availability refresh preserves recipe offset')
chk('FinishRebuild(false, "external-dataset")' in src['catalogue'], 'external/native dataset refresh does not recenter selection')
chk('WheelStepRows = 0.62f' in src['catalogue'] and 'Mathf.MoveTowards(scrollOffsetPixels, targetScrollOffsetPixels' in src['catalogue'], 'recipe scrolling remains smooth fractional movement')
chk('public const int VisibleRowCount = 10;' in src['catalogue'], 'recipe catalogue remains ten visible rows')

# Inventory runtime diagnosis and explicit Bag -> presentation projection.
for token in ['[REBIRTH Crafting InventoryTrace] reason=',
              '[REBIRTH Crafting InventoryTrace] geometry reason=',
              '[REBIRTH Crafting InventoryTrace] scroll source=',
              'sourceNonEmpty=', 'controllerNonEmpty=', 'controllerVisible=', 'sample=[']:
    chk(token in (src['inventory'] + src['invscroll']), f'inventory trace contains {token}')
chk('slot.ItemStack = desired;' in src['inventory'], 'authoritative Bag stack explicitly projected into presentation slot')
chk('slot.SlotChangedEvent -= handleSlotChangedDelegate;' in src['inventory'], 'projection detaches backend slot-change event')
chk('slot.SlotChangedEvent += handleSlotChangedDelegate;' in src['inventory'], 'authoritative slot event restored after projection')
chk('GetChildrenByType<XUiC_ItemStack>()' in src['inventory'], 'repeated ItemStack controller recovery retained')
chk('bag.SetSlot(slotNumber' in src['inventory'], 'inventory backend writes remain one authoritative Bag slot')
chk('base.HandleSlotChangedEvent' in src['inventory'] and 'never call base.HandleSlotChangedEvent' in src['inventory'], 'authored wide presenter cannot resize Bag')
chk('name="inventory" rows="9" cols="12"' in xml, 'inventory authors 12 columns with overflow presentation rows')
chk('VisibleRows = RebirthCraftingInventoryBridge.VisibleRows' in src['invscroll'], 'inventory viewport still uses bridge four-row visibility contract')
chk('public const int VisibleRows = 4;' in (root/'Scripts/Crafting/UI/PersonalCrafting/RebirthCraftingInventoryBridge.cs').read_text(encoding='utf-8-sig') if (root/'Scripts/Crafting/UI/PersonalCrafting/RebirthCraftingInventoryBridge.cs').exists() else False, 'inventory bridge remains four visible rows')

# HUD suppression must survive later same-frame native HUD updates and restore state.
for token in ['HUDLeftStatBars','HUDRightStatBars','windowQuestTracker','windowRecipeTracker','rebirthPartyCompanionHud','xui.BuffPopoutList','xui.QuestTracker']:
    chk(token in src['owner'], f'HUD suppression targets {token}')
chk('hudEnabledBeforeOpen' in src['owner'], 'HUD suppression records enabled state')
chk('controller.ViewComponent.Enabled = false;' in src['owner'], 'HUD suppression disables target views, not only visibility')
chk('MaintainGameplayHudSuppression();' in src['owner'], 'HUD suppression is reasserted while crafting remains open')
chk('[REBIRTH Crafting HUDTrace] capture source=' in src['owner'], 'HUD trace logs target resolution/capture')
chk('[REBIRTH Crafting HUDTrace] REAPPEARED' in src['owner'], 'HUD trace reports native reappearance attempts')
chk('TraceVisibleControllerTree' in src['owner'], 'HUD trace inventories remaining visible toolbelt tree')
chk('hudEnabledBeforeOpen.TryGetValue' in src['owner'], 'HUD enabled state is restored on close')

# Top navigation: the responsive layout service was the runtime override that PC092 missed.
chk(xml.count('Label" depth="7" pos="43,-18"') == 7, 'all seven authored top-tab labels are at final centered y')
chk('"rebirthCraftingTab" + suffix + "Label"), 43, -18' in src['layout'], 'runtime responsive layout also centers top-tab labels')
chk('[REBIRTH Crafting LayoutTrace]' in src['owner'], 'layout trace records actual live icon/label geometry')

# Expected Outcome organization must be responsive, not the prior cramped split.
chk('rebirthCraftingOutcomeMetricsPanel" pos="14,-48" width="352" height="64"' in xml, 'authored Expected Outcome uses compact top metrics row')
chk('rebirthCraftingOutcomeResultsPanel" pos="14,-120" width="352" height="95"' in xml, 'authored Expected Outcome uses full-width result card below metrics')
for token in ['ApplyOutcomeInternalLayout(width, outcomeHeight);','rebirthCraftingOutcomeMetricsBg','rebirthCraftingOutcomeResultsBg','resultHeight = Math.Max(80, height - 128)']:
    chk(token in src['layout'], f'Expected Outcome responsive layout contains {token}')
chk('rebirthCraftingOutcomeResultIcon" depth="5" pos="15,-36" width="42" height="42" atlas="ItemIconAtlas"' in xml, 'Expected Outcome keeps live ItemIconAtlas result icon')

# Recipe state/favorite visual ordering.
chk('stateIcon.Position = new Vector2i(Math.Max(150, width - 47)' in src['entry'], 'recipe state icon is placed to the right of status text at runtime')
chk('statusLabel.Position = new Vector2i(Math.Max(140, width - 112)' in src['entry'], 'recipe status text reserves right-side state icon space')
chk('name="unlocked" depth="5" pos="289,-9"' in xml and 'rebirthCraftingRecipeStatus" depth="5" pos="224,-5" width="62"' in xml, 'authored recipe lock icon sits right of LOCKED status')

# Inventory toolbar parity.
for name in ['btnRebirthCraftingInventoryQuickStack','btnRebirthCraftingInventoryCompanions','btnRebirthCraftingInventoryLock','btnRebirthCraftingInventorySort']:
    chk(name in xml, f'inventory header authors {name}')
chk('ui_game_symbol_quickstack' in xml and 'rb_backpack_companions' in xml, 'inventory header reuses Quick Stack and Companions icon assets')
chk('QuickStackRadialUiService.Open(xui)' in src['invbridge'], 'Crafting Quick Stack button opens real Quick Stack radial')
chk('RebirthCompanionUiService.Open(xui)' in src['invbridge'], 'Crafting Companions button opens real companions UI')
chk('QuickStackRuntimePolicy.Enabled' in src['invbridge'], 'Quick Stack button obeys runtime policy')
chk('btnRebirthCraftingInventoryQuickStack' in src['layout'] and 'btnRebirthCraftingInventoryCompanions' in src['layout'], 'responsive inventory header positions all four controls')

# Action presentation.
for name in ['Craft','Favorite','Track']:
    fragment = f'name="btnRebirthCrafting{name}"'
    i = xml.find(fragment)
    j = xml.find('/>', i)
    tag = xml[i:j] if i >= 0 and j >= 0 else ''
    chk('font_size="17"' in tag, f'{name} action text is slightly larger')
    chk('upper_case="true"' in tag, f'{name} action text is uppercase')
for icon in ['rebirthCraftingCraftIcon','rebirthCraftingFavoriteIcon','rebirthCraftingTrackIcon']:
    chk(icon in xml, f'action icon retained: {icon}')

# Delimiter sanity on changed C# sources.
for key in ['logs','owner','layout','catalogue','entry','inventory','invscroll','invbridge']:
    text = src[key]
    chk(text.count('{') == text.count('}'), f'balanced braces {paths[key].name}')
    chk(text.count('(') == text.count(')'), f'balanced parentheses {paths[key].name}')

print(f'PC093 runtime diagnostics + visual corrections validation: {pass_count} PASS / {fail_count} FAIL')
sys.exit(1 if fail_count else 0)
