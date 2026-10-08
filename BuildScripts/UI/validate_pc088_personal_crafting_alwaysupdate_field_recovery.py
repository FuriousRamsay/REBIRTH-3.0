from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[2]
SRC = ROOT / 'Scripts' / 'Crafting' / 'UI' / 'PersonalCrafting'
checks=[]

def check(name, ok):
    checks.append((name, bool(ok)))
    print(('PASS' if ok else 'FAIL') + ' - ' + name)

def text(name):
    p=SRC/name
    check('exists ' + str(p.relative_to(ROOT)), p.is_file())
    return p.read_text(encoding='utf-8') if p.is_file() else ''

cat=text('XUiC_RebirthCraftingRecipeCatalogue.cs')
root=text('XUiC_RebirthPersonalCrafting.cs')
combined=cat+'\n'+root

check('recipe catalogue assigns inherited AlwaysUpdate member in Init',
      'public override void Init()' in cat and 'AlwaysUpdate = true;' in cat)
check('personal crafting assigns inherited AlwaysUpdate member in Init',
      'public override void Init()' in root and 'AlwaysUpdate = true;' in root)
check('recipe catalogue no longer declares AlwaysUpdate override',
      'override bool AlwaysUpdate' not in cat and 'AlwaysUpdate()' not in cat)
check('personal crafting no longer declares AlwaysUpdate override',
      'override bool AlwaysUpdate' not in root and 'AlwaysUpdate()' not in root)
check('exactly two AlwaysUpdate assignments exist', combined.count('AlwaysUpdate = true;') == 2)
check('catalogue Update override retained', 'public override void Update(float dt)' in cat)
check('personal crafting Update override retained', 'public override void Update(float dt)' in root)
check('catalogue braces balanced', cat.count('{') == cat.count('}'))
check('catalogue parentheses balanced', cat.count('(') == cat.count(')'))
check('personal crafting braces balanced', root.count('{') == root.count('}'))
check('personal crafting parentheses balanced', root.count('(') == root.count(')'))

for bad in [
    'public override bool AlwaysUpdate => true;',
    'public override bool AlwaysUpdate()',
    'protected override bool AlwaysUpdate',
]:
    check('forbidden AlwaysUpdate declaration absent: ' + bad, bad not in combined)

passed=sum(1 for _,ok in checks if ok)
failed=len(checks)-passed
print(f'\nPC088 AlwaysUpdate field recovery validation: {passed} PASS / {failed} FAIL')
sys.exit(1 if failed else 0)
