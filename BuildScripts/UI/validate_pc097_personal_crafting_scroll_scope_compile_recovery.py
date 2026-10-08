from pathlib import Path
import sys
root = Path(sys.argv[1]) if len(sys.argv) > 1 else Path('.')
p = root / 'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingRecipeCatalogue.cs'
checks = []
def check(name, ok):
    checks.append((name, bool(ok)))
    print(('PASS' if ok else 'FAIL') + ' - ' + name)
check('catalogue source exists', p.exists())
text = p.read_text(encoding='utf-8') if p.exists() else ''
check('track fallback local uses unique fallbackBefore name', 'float fallbackBefore = authoritativeScrollTargetPixels;' in text)
check('track fallback trace uses fallbackBefore', 'TraceScrollChange("track-fallback", fallbackBefore, targetScrollOffsetPixels);' in text)
check('conflicting nested track fallback before declaration removed', 'if (collider == null || camera == null)\n        {\n            float before = authoritativeScrollTargetPixels;' not in text)
passed = sum(ok for _, ok in checks)
failed = len(checks) - passed
print(f'PC097 scroll scope compile recovery validation: {passed} PASS / {failed} FAIL')
sys.exit(1 if failed else 0)
