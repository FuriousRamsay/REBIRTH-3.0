from pathlib import Path
from lxml import etree
import re, sys

root = Path(__file__).resolve().parents[2]
windows = root / 'Config/XUi_InGame/windows.xml'
catalogue = root / 'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingRecipeCatalogue.cs'
owner = root / 'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthPersonalCrafting.cs'
bridge = root / 'Scripts/Crafting/UI/PersonalCrafting/RebirthCraftingInventoryBridge.cs'
scroll = root / 'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingInventoryScroll.cs'

checks = []
def ck(name, ok, detail=''):
    checks.append((name, bool(ok), detail))

for p in [windows, catalogue, owner, bridge, scroll]:
    ck('exists ' + p.relative_to(root).as_posix(), p.exists())

try:
    tree = etree.parse(str(windows))
    ck('windows.xml parses', True)
except Exception as e:
    tree = None
    ck('windows.xml parses', False, str(e))

wt = windows.read_text(encoding='utf-8', errors='ignore')
ct = catalogue.read_text(encoding='utf-8', errors='ignore')
ot = owner.read_text(encoding='utf-8', errors='ignore')
bt = bridge.read_text(encoding='utf-8', errors='ignore')
st = scroll.read_text(encoding='utf-8', errors='ignore')

if tree is not None:
    search_nodes = tree.xpath('//*[@name="rebirthCraftingRecipeSearch"]')
    placeholder_nodes = tree.xpath('//*[@name="rebirthCraftingRecipeSearchPlaceholder"]')
    region_nodes = tree.xpath('//*[@name="rebirthCraftingRecipesRegion"]')
    ck('exactly one recipe search field', len(search_nodes) == 1, f'count={len(search_nodes)}')
    ck('exactly one recipe search placeholder', len(placeholder_nodes) == 1, f'count={len(placeholder_nodes)}')
    ck('exactly one recipe catalogue region', len(region_nodes) == 1, f'count={len(region_nodes)}')

    if search_nodes and placeholder_nodes and region_nodes:
        search = search_nodes[0]
        placeholder = placeholder_nodes[0]
        region = region_nodes[0]
        parent = region.getparent()
        ck('search is sibling of RecipeList-derived region', search.getparent() is parent)
        ck('placeholder is sibling of RecipeList-derived region', placeholder.getparent() is parent)
        ck('search is not descendant of RecipeList-derived region', region not in search.iterancestors())
        ck('placeholder is not descendant of RecipeList-derived region', region not in placeholder.iterancestors())
        children = list(parent) if parent is not None else []
        ck('search is authored before catalogue region', search in children and region in children and children.index(search) < children.index(region))
        ck('placeholder is authored before catalogue region', placeholder in children and region in children and children.index(placeholder) < children.index(region))
        ck('catalogue controller retained', region.get('controller') == 'RebirthCraftingRecipeCatalogue, RebirthUtils', region.get('controller'))
        ck('search native semantic flag retained', search.get('search_field') == 'true')
        ck('search clear button retained', search.get('clear_button') == 'true')
        ck('search virtual keyboard prompt retained', bool(search.get('virtual_keyboard_prompt')))
        ck('search geometry retained', search.get('pos') == '50,-82' and search.get('width') == '318' and search.get('height') == '34', str(search.attrib))
        ck('search navigation retained', search.get('nav_left') == 'btnRebirthCraftingFavoriteFilter' and search.get('nav_down') == 'rebirthCraftingRecipeRowHit0')

        recipe_rows = region.xpath('.//*[starts-with(@name,"rebirthCraftingRecipeRow") and @controller="RebirthCraftingRecipeEntry, RebirthUtils"]')
        ck('eight visible recipe rows plus one clipped smooth buffer retained', len(recipe_rows) == 9, f'count={len(recipe_rows)}')

# Runtime ownership correction.
ck('catalogue no longer discovers search as its child', 'GetChildById("rebirthCraftingRecipeSearch")' not in ct)
ck('catalogue exposes sibling search attachment', 'public void AttachSearchInput(XUiC_TextInput input, XUiV_Label placeholder)' in ct)
ck('attachment sets native txtInput bridge', 'txtInput = input;' in ct)
ck('attachment subscribes live change handler', 'searchInput.OnChangeHandler += HandleSearchChanged;' in ct)
ck('attachment subscribes submit handler', 'searchInput.OnSubmitHandler += HandleSearchSubmitted;' in ct)
ck('attachment safely unsubscribes prior change handler', 'searchInput.OnChangeHandler -= HandleSearchChanged;' in ct)
ck('attachment safely unsubscribes prior submit handler', 'searchInput.OnSubmitHandler -= HandleSearchSubmitted;' in ct)

ck('root discovers custom catalogue', 'XUiC_RebirthCraftingRecipeCatalogue rebirthRecipeList = GetChildByType<XUiC_RebirthCraftingRecipeCatalogue>();' in ot)
ck('root preserves native recipeList contract', 'recipeList = rebirthRecipeList;' in ot)
ck('root resolves sibling search', 'GetChildById("rebirthCraftingRecipeSearch") as XUiC_TextInput' in ot)
ck('root attaches sibling search to catalogue', 'rebirthRecipeList?.AttachSearchInput(recipeSearch, recipeSearchPlaceholder);' in ot)

init_pos = ot.find('for (int i = 0; i < children.Count; i++)')
attach_pos = ot.find('rebirthRecipeList?.AttachSearchInput(recipeSearch, recipeSearchPlaceholder);')
ck('search attachment occurs after complete child Init pass', init_pos >= 0 and attach_pos > init_pos, f'init={init_pos} attach={attach_pos}')

# The crash path remains intentionally manual for the custom RecipeList, but TextInput is no longer in that child list.
ck('catalogue manual RecipeList init remains explicit', 'for (int i = 0; i < children.Count; i++)' in ct and 'children[i].Init();' in ct)
ck('V3.2 b10 ownership rationale documented in catalogue', 'V3.2 b10' in ct and 'sibling search field' in ct)
ck('V3.2 b10 ownership rationale documented in owner', 'V3.2 b10' in ot and 'outside the RecipeList controller' in ot)

# Backpack acceptance guard: presentation stays 8 columns x 4 visible rows while physical rows remain larger/scrollable.
ck('crafting backpack uses twelve columns', 'public const int Columns = 12;' in bt)
ck('crafting backpack remains four visible rows', 'public const int VisibleRows = 4;' in bt)
m = re.search(r'public const int AuthoredRows\s*=\s*(\d+)', bt)
authored_rows = int(m.group(1)) if m else 0
ck('authored backpack exceeds four visible rows', authored_rows > 4, f'AuthoredRows={authored_rows}')
ck('scroll controller uses visible-row viewport', 'private const int VisibleRows = RebirthCraftingInventoryBridge.VisibleRows;' in st)
ck('scroll controller calculates overflow rows', 'Math.Max(0, totalRows - VisibleRows)' in st)

# No regression to the exact crashing ownership pattern.
region_start = wt.find('<rect name="rebirthCraftingRecipesRegion"')
region_end = wt.find('</rect>', region_start) if region_start >= 0 else -1
# Use XML ancestry as authority; this textual guard catches accidental direct reinsertion near region start.
near_region = wt[region_start:region_start + 2500] if region_start >= 0 else ''
ck('search field not reinserted near catalogue region header', '<textfield name="rebirthCraftingRecipeSearch"' not in near_region)

passed = sum(ok for _, ok, _ in checks)
failed = [(n,d) for n,ok,d in checks if not ok]
for name, ok, detail in checks:
    print(('PASS' if ok else 'FAIL') + ' - ' + name + ((' :: ' + detail) if detail and not ok else ''))
print(f'PC090 V3.2 text-input init recovery validation: {passed} PASS / {len(failed)} FAIL')
sys.exit(1 if failed else 0)
