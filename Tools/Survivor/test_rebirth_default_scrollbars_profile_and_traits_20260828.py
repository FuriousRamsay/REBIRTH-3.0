from pathlib import Path
import sys

root = Path(sys.argv[sys.argv.index('--root') + 1]) if '--root' in sys.argv else Path('.')
xml = (root / 'Config/XUi_Menu/windows.xml').read_text(encoding='utf-8')
creator = (root / 'Scripts/Survivor/UI/XUiC_RebirthSurvivorCreator.cs').read_text(encoding='utf-8')
manager = (root / 'Scripts/Survivor/UI/XUiC_RebirthSurvivorProfileManager.cs').read_text(encoding='utf-8')
errors=[]

def need(text, token, where):
    if token not in text: errors.append(f'{where}: missing {token}')

def forbid(text, token, where):
    if token in text: errors.append(f'{where}: forbidden {token}')

for id_ in ['profileManagerScrollHost','positiveTraitNativeScrollHost','negativeTraitNativeScrollHost']:
    need(xml, f'name="{id_}"', 'windows.xml')
need(xml, '<defaultscrollbar/>', 'windows.xml')
for id_ in ['positiveTraitNativeScrollView','negativeTraitNativeScrollView','positiveTraitNativeScrollProxy','negativeTraitNativeScrollProxy']:
    need(xml, f'name="{id_}"', 'windows.xml')
forbid(xml, 'name="customScrollBarChrome"', 'windows.xml')
forbid(xml, 'name="customScrollBarChromeInner"', 'windows.xml')
for token in ['PollTraitNativeScroll(false);PollTraitNativeScroll(true);','UpdateTraitNativeScroll(false, model.PositiveTraitOffset, positiveTotal);','UpdateTraitNativeScroll(true, model.NegativeTraitOffset, negativeTotal);']:
    need(creator, token, 'creator')
for token in ['profileNativeScrollHost = GetChildById("profileManagerScrollHost")','PollProfileNativeScroll();','UpdateProfileNativeScroll();']:
    need(manager, token, 'profile manager')

if errors:
    print('Default scrollbar audit FAIL')
    for e in errors: print(e)
    raise SystemExit(1)
print('Default scrollbar audit PASS')
