#!/usr/bin/env python3
from pathlib import Path
import sys

root = Path(sys.argv[1]) if len(sys.argv) > 1 else Path.cwd()
xml = (root / "Config/XUi_InGame/windows.xml").read_text(encoding="utf-8")
item = (root / "Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingItemContext.cs").read_text(encoding="utf-8")
recipe = (root / "Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingRecipeEntry.cs").read_text(encoding="utf-8")
checks = {
    "inventory stats lowered for visual centering": 'name="rebirthCraftingInventoryCapacity" depth="4" pos="154,-10"' in xml,
    "craft time label shares value y": 'name="rebirthCraftingSelectedRecipeTimeTitle" depth="4" pos="446,-150"' in xml and 'name="rebirthCraftingSelectedRecipeTimeValue" depth="4" pos="571,-150"' in xml,
    "batch label lowered": 'name="rebirthCraftingBatchTitle" depth="4" pos="446,-199"' in xml,
    "outcome result value enlarged": 'name="rebirthCraftingOutcomeResultsValue" depth="4" pos="82,-32" width="260" height="30" font_size="18"' in xml,
    "outcome explanation enlarged": 'name="rebirthCraftingOutcomeExplanation" depth="4" pos="82,-64" width="260" height="48" font_size="17"' in xml,
    "recipe status xml uses icon baseline": 'name="rebirthCraftingRecipeStatus" depth="5" pos="220,-10" width="68" height="18" font_size="14"' in xml,
    "runtime recipe status shares stateY": 'statusLabel.Position = new Vector2i(Math.Max(136, width - 118), stateY);' in recipe and 'stateIcon.Position = new Vector2i(Math.Max(150, width - 41), stateY);' in recipe,
    "item action caption centered full button": 'SetRect(nameController, 0, -7, cell, 24);' in item,
    "item shortcut separate right label": 'SetRect(keyboardController, cell - 46, -7, 36, 24);' in item,
    "white stock crafting icon": 'name="rebirthCraftingTabCraftingIcon" depth="6" pos="12,-14" width="24" height="24" sprite="ui_game_symbol_hammer" color="245,245,247,255"' in xml,
    "white stock character icon": 'name="rebirthCraftingTabCharacterIcon" depth="6" pos="12,-14" width="24" height="24" sprite="ui_game_symbol_player" color="245,245,247,255"' in xml,
    "white stock map icon": 'name="rebirthCraftingTabMapIcon" depth="6" pos="12,-14" width="24" height="24" sprite="ui_game_symbol_compass" color="245,245,247,255"' in xml,
    "white stock skills icon": 'name="rebirthCraftingTabSkillsIcon" depth="6" pos="12,-14" width="24" height="24" sprite="ui_game_symbol_book" color="245,245,247,255"' in xml,
    "white stock challenges icon": 'name="rebirthCraftingTabChallengesIcon" depth="6" pos="12,-14" width="24" height="24" sprite="ui_game_symbol_trophy" color="245,245,247,255"' in xml,
    "white stock players icon": 'name="rebirthCraftingTabPlayersIcon" depth="6" pos="12,-14" width="24" height="24" sprite="ui_game_symbol_allies" color="245,245,247,255"' in xml,
    "100-slot test override retained": '8/100' not in xml and 'rows="8" cols="13"' in xml,
}
failed=[k for k,v in checks.items() if not v]
for k,v in checks.items(): print(("PASS" if v else "FAIL") + " - " + k)
print(f"RESULT: {len(checks)-len(failed)}/{len(checks)} PASS")
sys.exit(1 if failed else 0)
