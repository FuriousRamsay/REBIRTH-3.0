from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[2]
SRC = ROOT / 'Scripts' / 'Crafting' / 'UI' / 'PersonalCrafting'

checks = []

def check(name, ok):
    checks.append((name, bool(ok)))
    print(('PASS' if ok else 'FAIL') + ' - ' + name)

def text(name):
    p = SRC / name
    check('exists ' + str(p.relative_to(ROOT)), p.is_file())
    return p.read_text(encoding='utf-8') if p.is_file() else ''

slot = text('XUiC_RebirthCraftingInventorySlot.cs')
inv = text('XUiC_RebirthCraftingInventory.cs')
ctx = text('XUiC_RebirthCraftingItemContext.cs')
cat = text('XUiC_RebirthCraftingRecipeCatalogue.cs')
entry = text('XUiC_RebirthCraftingRecipeEntry.cs')
root = text('XUiC_RebirthPersonalCrafting.cs')

check('item-info override matches b259 public accessibility',
      'public override void updateItemInfoWindow(XUiC_ItemStack itemStack)' in slot)
check('nonexistent OnCursorSelected override removed',
      'override void OnCursorSelected' not in slot and 'base.OnCursorSelected' not in slot)
check('valid click-completion hook retains viewport visibility',
      'public override void HandleClickComplete()' in slot and 'scroll.EnsureSlotVisible(SlotNumber)' in slot)
check('valid click-completion hook retains custom item context selection',
      'context.SelectSlot(this)' in slot)
check('backpack SetStacks override matches b259 public accessibility',
      'public override void SetStacks(ItemStack[] stackList)' in inv)
check('item-context binding override matches b259 public accessibility',
      'public override bool GetBindingValueInternal(ref string value, string bindingName)' in ctx)
check('recipe entry SelectedChanged override matches b259 public accessibility',
      'public override void SelectedChanged(bool isSelected)' in entry)
check('recipe catalogue AlwaysUpdate uses inherited V3.2 field',
      'AlwaysUpdate = true;' in cat and 'override bool AlwaysUpdate' not in cat and 'AlwaysUpdate()' not in cat)
check('personal crafting AlwaysUpdate uses inherited V3.2 field',
      'AlwaysUpdate = true;' in root and 'override bool AlwaysUpdate' not in root and 'AlwaysUpdate()' not in root)

for name, content in [
    ('slot', slot), ('inventory', inv), ('item context', ctx),
    ('recipe catalogue', cat), ('recipe entry', entry), ('personal crafting', root)
]:
    check(name + ' braces balanced', content.count('{') == content.count('}'))
    check(name + ' parentheses balanced', content.count('(') == content.count(')'))

for bad in [
    'protected override void updateItemInfoWindow',
    'protected override void SetStacks',
    'protected override bool GetBindingValueInternal',
    'protected override void SelectedChanged',
    'public override bool AlwaysUpdate()',
    'override void OnCursorSelected',
]:
    combined = '\n'.join([slot, inv, ctx, cat, entry, root])
    check('forbidden legacy signature absent: ' + bad, bad not in combined)

passed = sum(1 for _, ok in checks if ok)
failed = len(checks) - passed
print(f'\nPC087 b259 compile-signature recovery validation: {passed} PASS / {failed} FAIL')
sys.exit(1 if failed else 0)
