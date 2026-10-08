from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
errors = []

def req(path, text, label):
    data = (ROOT / path).read_text(encoding='utf-8')
    if text not in data:
        errors.append(f'{label}: missing {text!r} in {path}')

req('Config/_Survivor/debug.xml', 'character_progression_ui_logging="false"', 'debug gate defaults off after completed trace')
req('Config/_Survivor/debug.xml', 'trait_ui_logging="false"', 'trait trace disabled')
req('Scripts/Logging/RebirthLogSettings.cs', 'CharacterProgressionUiLoggingEnabled', 'logging property')
req('Scripts/Logging/RebirthLogSettings.cs', 'TraceCharacterProgression', 'trace helper')
req('Scripts/UI/XUiC_RebirthSandboxOptions.cs', 'VALUE-CHANGED begin', 'value-change entry trace')
req('Scripts/UI/XUiC_RebirthSandboxOptions.cs', 'VALUE-CHANGED LOCKED', 'lock-reversion trace')
req('Scripts/UI/XUiC_RebirthSandboxOptions.cs', 'VALUE-CHANGED accepted', 'accepted trace')
req('Scripts/UI/XUiC_RebirthSandboxOptions.cs', 'TracePlayerProgressionState("poll", false)', 'native index watcher')
req('Scripts/UI/XUiC_RebirthSandboxOptions.cs', 'values=" + FormatPlayerProgressionElements()', 'element-list trace')
req('Scripts/Options/RebirthSandboxUiSession.cs', 'CharacterProgressionDebugContext', 'session context summary')
req('Scripts/Options/RebirthSandboxUiSession.cs', 'session-set-code LOCK-OVERRIDE', 'session lock override trace')
req('Scripts/Options/Patching/RebirthSandboxMenuPatchInstaller.cs', 'base UpdateOptionValuesFromGamePrefs postfix BEFORE reload', 'base refresh trace')
req('Scripts/Options/Patching/RebirthSandboxMenuPatchInstaller.cs', 'base SaveGameOptions postfix', 'base save trace')

# Regression: the two actual choices must still be authored in order.
ui = (ROOT / 'Scripts/UI/XUiC_RebirthSandboxOptions.cs').read_text(encoding='utf-8')
base = 'cbxPlayerProgression.Elements.Add(Localization.Get("xuiRebirthPlayerProgressionBaseGame"));'
rebirth = 'cbxPlayerProgression.Elements.Add(Localization.Get("xuiRebirthPlayerProgressionRebirth"));'
if base not in ui or rebirth not in ui or ui.index(base) > ui.index(rebirth):
    errors.append('Character Progression choices are not authored as Base Game then Rebirth')

if errors:
    print('FAIL')
    for e in errors:
        print(' -', e)
    raise SystemExit(1)
print('PASS: Character Progression UI diagnostic trace remains wired for main-menu use and defaults off.')
