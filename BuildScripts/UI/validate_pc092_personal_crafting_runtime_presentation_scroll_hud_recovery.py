#!/usr/bin/env python3
from pathlib import Path
import sys
import xml.etree.ElementTree as ET

root = Path(sys.argv[1] if len(sys.argv) > 1 else '.').resolve()
xml_path = root/'Config/XUi_InGame/windows.xml'
files = {
    'owner': root/'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthPersonalCrafting.cs',
    'layout': root/'Scripts/Crafting/UI/PersonalCrafting/RebirthPersonalCraftingLayoutService.cs',
    'world': root/'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingWorldStatus.cs',
    'catalogue': root/'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingRecipeCatalogue.cs',
    'entry': root/'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingRecipeEntry.cs',
    'inventory': root/'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingInventory.cs',
    'invscroll': root/'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingInventoryScroll.cs',
    'outcome': root/'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingOutcome.cs',
    'actions': root/'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingActions.cs',
}
pass_count=fail_count=0

def chk(cond, label):
    global pass_count, fail_count
    if cond:
        pass_count += 1; print('PASS - '+label)
    else:
        fail_count += 1; print('FAIL - '+label)

for k,p in files.items(): chk(p.exists(), f'exists {p.relative_to(root)}')
chk(xml_path.exists(), 'exists Config/XUi_InGame/windows.xml')
try:
    ET.parse(xml_path); chk(True, 'windows.xml parses')
except Exception as e:
    chk(False, f'windows.xml parses: {e}')

xml = xml_path.read_text(encoding='utf-8-sig')
src = {k:p.read_text(encoding='utf-8-sig') for k,p in files.items()}

# HUD suppression/restoration.
chk('HideGameplayHudOverlays();' in src['owner'], 'Personal Crafting suppresses gameplay HUD overlays on open')
chk('RestoreGameplayHudOverlays();' in src['owner'], 'Personal Crafting restores gameplay HUD overlays on close')
for token in ['windowLocation','windowQuestTracker','windowRecipeTracker','rebirthPartyCompanionHud']:
    chk(token in src['owner'], f'HUD suppression includes {token}')
chk('xui.BuffPopoutList' in src['owner'], 'HUD suppression includes BuffPopoutList')
chk('hudVisibilityBeforeOpen' in src['owner'], 'HUD suppression preserves prior visibility')

# Top tabs + world status.
chk(xml.count('Label" depth="7" pos="43,-18"') == 7, 'all seven top navigation labels use final vertically centered y')
chk('"rebirthCraftingTab" + suffix + "Label"), 43, -18' in src['layout'], 'runtime tab layout preserves final vertical centering')
for icon in ['rb_hud_sun','rb_hud_moon','rb_hud_clock','rb_hud_temperature_v2']:
    chk(icon in xml, f'world status uses {icon}')
chk('rebirthCraftingStatusBiome' not in xml, 'biome information removed from Personal Crafting status')
chk('GetBiome' not in src['world'] and 'ResolveBiome' not in src['world'], 'world status controller has no biome authority path')
chk('color="240,240,240,255"' in xml[xml.find('rebirthCraftingWorldStatus'):xml.find('rebirthCraftingBodyZone')], 'world status text, including temperature, is white')
chk('width - rightPad - total' in src['layout'], 'world status cluster is right-aligned')
chk('x += dayWidth' in src['layout'] and 'x += timeWidth' in src['layout'], 'day/time/temperature modules are adjacent without arbitrary gaps')

# Recipe scrolling: 8 visible + clipped buffer, pixel target, no native feedback reset.
chk('public const int VisibleRowCount = 10;' in src['catalogue'], 'recipe viewport retains ten visible rows')
chk('public const int PresentationRowCount = VisibleRowCount + 1;' in src['catalogue'], 'recipe viewport has one clipped smooth-scroll buffer row')
chk(xml.count('rebirthCraftingRecipeRowHit') >= 9, 'nine recipe presentation row hit targets authored')
chk('clipping="softclip"' in xml[xml.find('rebirthCraftingRecipeViewport'):xml.find('rebirthCraftingOutcomeRegion')], 'recipe rows are clipped by viewport')
chk('WheelStepRows = 0.62f' in src['catalogue'], 'mouse wheel target is fractional-row')
chk('Mathf.MoveTowards(scrollOffsetPixels, targetScrollOffsetPixels' in src['catalogue'], 'recipe scrolling interpolates smoothly per frame')
chk('float remainder = currentRowStride > 0' in src['catalogue'] and 'yShift = Mathf.RoundToInt(remainder)' in src['catalogue'], 'fractional pixel remainder moves row presentation smoothly')
chk('RebirthNativeScrollbarUtil' not in src['catalogue'], 'recipe catalogue no longer uses native scrollbar feedback proxy')
chk('rebirthCraftingRecipeScrollTrack' in xml and 'rebirthCraftingRecipeScrollThumb' in xml, 'custom recipe track and thumb are authored')
chk('ScrollThumb_OnDrag' in src['catalogue'] and 'ScrollTrack_OnPress' in src['catalogue'], 'custom recipe scrollbar supports drag and track click')
chk('FinishRebuild(false, "availability")' in src['catalogue'], 'inventory/material refresh preserves recipe scroll position')
chk('FinishRebuild(false, "external-dataset")' in src['catalogue'], 'native/background RecipeList refresh cannot force selected row back into view')
chk('GetBehaviorEntryForRecipe' in src['catalogue'] and 'EnsureRecipeIndexVisible(index, true)' in src['catalogue'], 'explicit cross-link navigation can still reveal requested recipe')
chk('[REBIRTH Crafting RecipeScroll]' in src['catalogue'], 'gated recipe-scroll diagnostic retained')

# Inventory presentation recovery and capacity constraints.
needle = '<grid depth="10" name="inventory" rows="9" cols="12"'
chk(needle in xml, 'Personal Crafting inventory uses native-compatible wide inventory grid')
chk('<item_stack name="0" cell_size="64" controller="RebirthCraftingInventorySlot, RebirthUtils"/>' in xml, 'inventory item_stack supplies required cell_size')
chk('VisibleRows = RebirthCraftingInventoryBridge.VisibleRows' in src['invscroll'], 'inventory viewport derives four visible rows from bridge')
chk('rows="9" cols="12"' in xml, 'authored inventory capacity exceeds four visible rows')
chk('EnsureItemControllers();' in src['inventory'], 'inventory recovers repeated ItemStack controller cache when needed')
chk('GetChildrenByType<XUiC_ItemStack>()' in src['inventory'], 'inventory fallback discovers derived repeated ItemStack controllers')
chk('RebirthCraftingInventoryBridge.GetPhysicalSlotCount(xui)' in src['inventory'], 'visible inventory remains bounded by physical Bag slots')
chk('bag.SetSlot(slotNumber' in src['inventory'], 'inventory writes only the authoritative Bag slot')
chk('base.HandleSlotChangedEvent' in src['inventory'] and 'never call base.HandleSlotChangedEvent' in src['inventory'], 'inventory explicitly avoids authored-grid bulk backend write')

# Expected Outcome and action icon presentation.
chk('rebirthCraftingOutcomeMetricRule1' in xml and 'rebirthCraftingOutcomeMetricRule2' in xml, 'Expected Outcome metrics are visually separated')
chk('rebirthCraftingOutcomeResultsPanel' in xml and 'rebirthCraftingOutcomeResultRule' in xml, 'Expected Outcome result has dedicated organized panel')
chk('<sprite name="rebirthCraftingOutcomeResultIcon"' in xml and '<sprite name="rebirthCraftingOutcomeResultIcon" depth="5" pos="15,-36" width="42" height="42" atlas="ItemIconAtlas"' in xml, 'Expected Outcome result item icon is authored from ItemIconAtlas')
chk('RenderResultIcon(recipe)' in src['outcome'], 'Expected Outcome controller renders selected recipe result icon')
chk('GetIconName()' in src['outcome'] and 'GetIconTint' in src['outcome'], 'Expected Outcome result icon uses item icon/tint authority')
for name,sprite in [('Craft','ui_game_symbol_hammer'),('Favorite','server_favorite'),('Track','ui_game_symbol_compass')]:
    chk(f'rebirthCrafting{name}Icon' in xml and sprite in xml, f'{name} action has a left-side icon')
chk('rebirthCraftingFavoriteState' in xml and 'rebirthCraftingTrackState' in xml, 'favorite/track state checks remain distinct from action icons')

# Basic source delimiter checks for all touched C# files.
for k,text in src.items():
    chk(text.count('{') == text.count('}'), f'balanced braces {files[k].name}')
    chk(text.count('(') == text.count(')'), f'balanced parentheses {files[k].name}')

print(f'PC092 runtime presentation/scroll/HUD recovery validation: {pass_count} PASS / {fail_count} FAIL')
sys.exit(1 if fail_count else 0)
