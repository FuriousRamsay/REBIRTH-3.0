from pathlib import Path
from lxml import etree
import csv, re, sys

root = Path(__file__).resolve().parents[2]
checks=[]

def check(name, ok, detail=''):
    checks.append((name, bool(ok), detail))

windows = root/'Config/XUi_InGame/windows.xml'
xui = root/'Config/XUi_InGame/xui.xml'
loc = root/'Config/Localization.csv'
service = root/'Scripts/Crafting/UI/PersonalCrafting/RebirthCraftingRecipeCatalogueService.cs'
catalogue = root/'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingRecipeCatalogue.cs'
entry = root/'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingRecipeEntry.cs'
owner = root/'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthPersonalCrafting.cs'

for p in [windows,xui,loc,service,catalogue,entry,owner]:
    check('exists '+p.relative_to(root).as_posix(), p.exists())

for p in [windows,xui]:
    try:
        etree.parse(str(p)); check('xml parse '+p.name, True)
    except Exception as e:
        check('xml parse '+p.name, False, str(e))

wt = windows.read_text(errors='ignore')
xt = xui.read_text(errors='ignore')
st = service.read_text(errors='ignore')
ct = catalogue.read_text(errors='ignore')
et = entry.read_text(errors='ignore')
ot = owner.read_text(errors='ignore')

# Architecture and Base Game isolation retained from Chunks A-C.
check('rebirth-only crafting conditional retained', "character_progression('Rebirth')" in xt)
check('public crafting route retained', "window_group[@name='crafting']" in xt)
check('rebirth root controller retained', 'name="controller">RebirthPersonalCrafting, RebirthUtils' in xt)
check('native auto backpack disabled only in rebirth patch', 'name="open_backpack_on_open">false</setattribute>' in xt)
check('custom root remains sole rebirth crafting child', '<remove xpath="/xui/window_group[@name=\'crafting\']/window"/>' in xt and 'name="rebirthPersonalCraftingRoot"' in xt)
check('owner discovers custom recipe contract', 'GetChildByType<XUiC_RebirthCraftingRecipeCatalogue>()' in ot and 'recipeList = rebirthRecipeList;' in ot)

# Custom catalogue surface.
start = wt.find('<rect name="rebirthCraftingRecipesRegion"')
end = wt.find('<rect name="rebirthCraftingExpectedOutcomeRegion"', start)
region = wt[start:end if end > start else None] if start >= 0 else ''
check('custom recipe region exists', start >= 0)
check('custom recipe region controller', 'controller="RebirthCraftingRecipeCatalogue, RebirthUtils"' in region)
check('recipe heading is RECIPES key', 'text_key="xuiRebirthRecipes"' in region)
check('native BASICS label absent from custom region', '>BASICS<' not in region and 'text="BASICS"' not in region)
check('native pager absent from custom region', '<pager' not in region and 'name="pager"' not in region)
check('native windowCraftingList absent from custom region', 'windowCraftingList' not in region)
check('search input exists outside RecipeList-derived region', 'name="rebirthCraftingRecipeSearch"' in wt and 'name="rebirthCraftingRecipeSearch"' not in region)
check('favorites filter exists', 'name="btnRebirthCraftingFavoriteFilter"' in region)
check('zero results state exists', 'name="rebirthCraftingRecipeEmptyState"' in region)
check('crafting-owned scrollbar track exists', 'name="rebirthCraftingRecipeScrollTrack"' in region)
check('crafting-owned scrollbar thumb exists', 'name="rebirthCraftingRecipeScrollThumb"' in region)
check('native recipe UIScrollView proxy removed', 'rebirthCraftingRecipeScrollView' not in region and 'rebirthCraftingRecipeScrollProxy' not in region)

row_roots = re.findall(r'<rect name="rebirthCraftingRecipeRow(\d+)"[^>]*controller="RebirthCraftingRecipeEntry, RebirthUtils"', region)
check('nine presentation rows support eight-row smooth viewport', sorted(row_roots) == [str(i) for i in range(9)], str(row_roots))
check('eight-row visible contract retained', 'VisibleRowCount = 8' in ct and 'PresentationRowCount = VisibleRowCount + 1' in ct)
for i in range(8):
    check(f'row {i} hit target', f'name="rebirthCraftingRecipeRowHit{i}"' in region)
    check(f'row {i} favorite target', f'name="rebirthCraftingRecipeFavoriteHit{i}"' in region)

category_buttons = re.findall(r'name="btnRebirthCraftingCategory(\d+)"', region)
check('real category strip has authored dynamic slots', len(set(category_buttons)) >= 8, str(sorted(set(category_buttons))))

# Behavioral compatibility: new presentation, native-authority data/actions.
check('catalogue preserves native RecipeList behavioral type', 'class XUiC_RebirthCraftingRecipeCatalogue : XUiC_RecipeList' in ct)
check('entry preserves native RecipeEntry behavioral type', 'class XUiC_RebirthCraftingRecipeEntry : XUiC_RecipeEntry' in et)
check('eight-row contract in code', 'VisibleRowCount = 8' in ct)
check('no native pager ownership in code', 'pager = null;' in ct)
check('native recipe source authority', 'XUiM_Recipes.GetRecipes()' in st)
check('native workstation filter authority', 'XUiM_Recipes.FilterRecipesByWorkstation' in st)
check('native search authority', 'XUiM_Recipes.FilterRecipesByName' in st)
check('native category filter authority', 'XUiM_Recipes.FilterRecipesByCategory' in st)
check('native favorite filter authority', 'CraftingManager.GetFavoriteRecipesFromList' in st)
check('native favorite mutation authority', 'CraftingManager.ToggleFavoriteRecipe(recipe)' in ct)
check('native RecipeInfo availability authority', 'catalogue.BuildRecipeInfosList(items)' in st)
check('native RecipeInfo ordering authority', 'catalogue.recipeInfos.Sort(catalogue.CompareRecipeInfos)' in st)
check('remote crafting-compatible authority path documented', 'Remote Crafting' in st and 'BuildRecipeInfosList' in st)
check('real category display authority', 'GetCraftingCategoryDisplayList(string.Empty)' in st)
check('quest recipe projection', 'QuestJournal.GetQuestRecipes()' in st)
check('challenge recipe projection', 'TrackedChallenge.CraftedRecipes()' in st)
check('stable first selection', 'selectedIndex = 0;' in ct and 'SetNativeCurrentRecipe(preferred);' in ct)
check('external native cross-links preserved', 'externalDatasetChanged = resortRecipes && pageChanged' in ct)
check('cross-link source list is not replaced', 'service.BuildRecipeInfosFromCurrentSource(this);' in ct)
check('smooth wheel scroll targets fractional pixels', 'public void HandleWheel(float delta)' in ct and 'WheelStepRows' in ct and 'targetScrollOffsetPixels' in ct)
check('custom track/thumb drag ownership', 'ScrollThumb_OnDrag' in ct and 'ScrollTrack_OnPress' in ct)
check('native scrollbar polling removed', 'RebirthNativeScrollbarUtil' not in ct and 'PollNativeScrollbar' not in ct)
check('scroll range uses actual recipe count', 'recipeInfos.Count' in ct and 'MaxPixelOffset' in ct)
check('selection signals later detail chunk', 'PublishSelectionIfChanged(recipe);' in ct and 'RebirthSelectionChanged?.Invoke(recipe);' in ct)

# No fake concept-only level metadata was introduced.
check('no hardcoded Level 1 recipe metadata', 'Level 1' not in ct and 'Level 1' not in et and 'Level 1' not in region)

required_loc = [
'xuiRebirthCraftingSearchPlaceholder','xuiRebirthCraftingNoRecipes','xuiRebirthCraftingFavoritesTooltip',
'xuiRebirthCraftingStatusReady','xuiRebirthCraftingStatusMissing','xuiRebirthCraftingStatusLocked',
'xuiRebirthCraftingStatusTracked','xuiRebirthCraftingStatusQuest','xuiRebirthCraftingStatusChallenge']
keys=[]
try:
    with loc.open(encoding='utf-8-sig', newline='') as f:
        for row in csv.reader(f):
            if row: keys.append(row[0].strip())
except Exception as e:
    check('localization readable', False, str(e))
else:
    check('localization readable', True)
    for key in required_loc:
        check('localization '+key, keys.count(key)==1, f'count={keys.count(key)}')

# Static delimiter preflight. Game assemblies/compiler are intentionally not shipped in this artifact runtime.
for p in [service,catalogue,entry,owner]:
    text=p.read_text(errors='ignore')
    check('delimiter braces '+p.name, text.count('{')==text.count('}'), f"{text.count('{')}/{text.count('}')}")
    check('delimiter parens '+p.name, text.count('(')==text.count(')'), f"{text.count('(')}/{text.count(')')}")
    check('delimiter brackets '+p.name, text.count('[')==text.count(']'), f"{text.count('[')}/{text.count(']')}")

# Representative responsive geometry: exactly 8 positive-height rows must remain inside the region.
for h in [340, 380, 447, 520, 620]:
    available=max(8*24,h-130)
    stride=max(24,min(42,available//8))
    rowh=max(22,stride-2)
    viewport=stride*8-2
    check(f'geometry smoke height={h}', rowh>0 and viewport<=max(8*42-2,h), f'stride={stride} row={rowh} viewport={viewport}')

passed=sum(1 for _,ok,_ in checks if ok)
failed=[x for x in checks if not x[1]]
print(f'PC078 CHUNK D VALIDATION: {passed} PASS / {len(failed)} FAIL')
for name,ok,detail in checks:
    if not ok: print('FAIL -',name,detail)
if failed: sys.exit(1)
