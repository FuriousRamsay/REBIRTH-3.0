from pathlib import Path
import re, subprocess, sys

ROOT = Path(__file__).resolve().parents[2]
base = ROOT / 'BuildScripts/UI/validate_pc121_personal_crafting_highlight_progression_encumbrance.py'
entry = ROOT / 'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingRecipeEntry.cs'

checks=[]

def check(name, cond):
    checks.append((name, bool(cond)))

# Preserve the full PC121 regression suite.
r = subprocess.run([sys.executable, str(base)], cwd=str(ROOT), text=True, capture_output=True)
check('PC121 regression validator still passes', r.returncode == 0 and '150/150 checks passed' in r.stdout)

s = entry.read_text(encoding='utf-8')
check('Recipe ClearData no longer reads unqualified Selected as a bool', 'if (Selected)' not in s)
check('Recipe ClearData no longer assigns unqualified Selected', re.search(r'\bSelected\s*=\s*false\s*;', s) is None)
check('Recipe ClearData clears Rebirth-owned selection first', 'public void ClearData()' in s and 'SetRebirthSelected(false);' in s)
check('Recipe selection remains owned by rebirthSelected', 'private bool rebirthSelected;' in s and 'ApplySelectedVisual(rebirthSelected' in s)
check('No cast-based dependency on XUiC_SelectableEntry.Selected was introduced', '((XUiC_SelectableEntry)this).Selected' not in s)

# Simple source balance gate.
check('Recipe entry C# braces balanced', s.count('{') == s.count('}'))

for i,(name,ok) in enumerate(checks,1):
    print(f'{i:02d}. {"PASS" if ok else "FAIL"}: {name}')

passed=sum(ok for _,ok in checks)
print(f'\nResult: {passed}/{len(checks)} checks passed')
if passed != len(checks):
    if r.returncode != 0:
        print('\n--- PC121 validator output ---')
        print(r.stdout)
        print(r.stderr)
    sys.exit(1)
