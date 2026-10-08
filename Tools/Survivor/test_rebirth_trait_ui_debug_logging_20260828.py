from pathlib import Path
import sys

root = Path(__file__).resolve().parents[2]
errors = []

def need(path, text):
    data = (root / path).read_text(encoding='utf-8')
    if text not in data:
        errors.append(f"{path}: missing {text!r}")

need('Config/_Survivor/progression.xml', 'max_negative_trait_refund="-1"')
need('Scripts/Survivor/Creation/RebirthSurvivorCreationValidator.cs', 'RebirthSurvivorTraitPointEconomy.ApplyNegativeRefund')
need('Scripts/Survivor/UI/RebirthSurvivorUiText.cs', 'RebirthSurvivorTraitPointEconomy.ApplyNegativeRefund')
need('Scripts/Survivor/Debug/RebirthSurvivorDebug.cs', 'TraitUiLoggingEnabled')
need('Scripts/Survivor/Debug/ConsoleCmdRebirthSurvivor.cs', 'rbsurvivor debug traitui [status|on|off|dump]')
need('Scripts/Survivor/Debug/RebirthSurvivorTraitUiDebug.cs', 'REFUND-CAP')
need('Scripts/Survivor/Debug/RebirthSurvivorTraitUiDebug.cs', 'selectedIds=')
need('Scripts/Survivor/UI/RebirthSurvivorCreatorViewModel.cs', 'RebirthSurvivorTraitUiDebug.LogToggle')
need('Scripts/Survivor/UI/XUiC_RebirthSurvivorCreator.cs', '[REBIRTH Survivor][TraitUI] click side=')
need('Scripts/Survivor/Debug/RebirthSurvivorTraitUiDebug.cs', 'SELECTION-COUNT-MISMATCH')
need('Scripts/Survivor/UI/XUiC_RebirthSurvivorCreator.cs', 'negative-thumb offset=')
need('Scripts/Survivor/UI/XUiC_RebirthSurvivorCreator.cs', 'DragScroll(ref offset, model.GetNegativeTraitCount(), TraitSideRows, TraitTrackHeight, dy, null);')

if errors:
    print('FAIL')
    for e in errors:
        print(' -', e)
    sys.exit(1)
print('PASS: Trait UI debug logging is wired and negative-Trait refunds are uncapped by current authoring.')
