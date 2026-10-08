from pathlib import Path
import sys
import xml.etree.ElementTree as ET

root = Path(sys.argv[1] if len(sys.argv) > 1 else '.').resolve()
win = root / 'Config' / 'XUi_InGame' / 'windows.xml'
text = win.read_text(encoding='utf-8')
checks=[]
def check(name, ok):
    checks.append((name, bool(ok)))

check('PC045 nested recipe scroll host removed', 'name="rebirthRecipeScrollHost"' not in text)
check('PC045 custom recipe smooth-scroll XML binding removed', 'controller="RebirthRecipeSmoothScroll, RebirthUtils"' not in text)
check('native RecipeList grid restored', '<grid name="recipes" depth="2" rows="8" cols="1" pos="3,-98" width="396" height="368" cell_width="396" cell_height="46" controller="RecipeList"' in text)
check('RecipeList grid remains direct child of content block', 'XUiC_RecipeList.Init resolves its CraftingListInfo siblings through this hierarchy' in text)
check('crafting external scrollbar bridge restored', 'name="rebirthCraftingPagerScroll" controller="RebirthPagerScrollbar, RebirthUtils" pos="405,-98" width="20" height="368"' in text)
check('native crafting pager remains hidden', "window[@name='windowCraftingList']/rect[@name='content']/rect[@name='searchControls']/rect/pager[@name='pager']\" name=\"pos\">-5000,-5000" in text)
check('recipe row width leaves scrollbar gutter', 'width="396" height="368" cell_width="396"' in text)
check('expected outcome panel preserved', 'name="rebirthExpectedOutcome" controller="RebirthCraftOutcomePanel, RebirthUtils"' in text)

# All XML mod files should remain parseable.
xml_files=list((root/'Config').rglob('*.xml'))
parse_errors=[]
for p in xml_files:
    try:
        ET.parse(p)
    except Exception as e:
        parse_errors.append((p,e))
check(f'all Config XML parses ({len(xml_files)} files)', not parse_errors)

failed=[name for name,ok in checks if not ok]
for name,ok in checks:
    print(('PASS' if ok else 'FAIL') + ' - ' + name)
if parse_errors:
    for p,e in parse_errors[:20]:
        print(f'XML ERROR - {p}: {e}')
print(f'RESULT: {len(checks)-len(failed)}/{len(checks)} passed')
if failed:
    sys.exit(1)
