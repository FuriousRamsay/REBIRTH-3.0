from pathlib import Path
import sys, xml.etree.ElementTree as ET

root = Path(__file__).resolve().parents[2]
errors=[]

cfg=root/'Config/_Survivor/debug.xml'
required = {
    'automatic_logging':'false',
    'trait_ui_logging':'false',
    'character_progression_ui_logging':'false',
    'harmony_patch_logging':'false',
    'runtime_install_logging':'false',
    'ui_route_logging':'false',
    'player_profile_bridge_logging':'false',
    'player_profile_portrait_logging':'false',
    'exit_trace_logging':'false',
    'progression_explorer_logging':'false',
}
if not cfg.exists():
    errors.append('Config/_Survivor/debug.xml missing')
else:
    r=ET.parse(cfg).getroot()
    if r.tag!='survivor_debug': errors.append('debug.xml root must be survivor_debug')
    for k,v in required.items():
        if r.attrib.get(k,'').lower()!=v:
            errors.append(f'debug.xml {k} must be {v}')

settings=(root/'Scripts/Logging/RebirthLogSettings.cs').read_text(encoding='utf-8')
for token in [
    'automatic_logging', 'harmony_patch_logging', 'runtime_install_logging',
    'ui_route_logging', 'player_profile_bridge_logging',
    'player_profile_portrait_logging', 'exit_trace_logging',
    'progression_explorer_logging', 'trait_ui_logging', 'character_progression_ui_logging',
    'TraceHarmonyPatch', 'TraceRuntimeInstall', 'TraceUiRoute',
    'TracePlayerProfileBridge', 'TracePlayerProfilePortrait', 'TraceExit',
    'TraceProgressionExplorer'
]:
    if token not in settings: errors.append(f'RebirthLogSettings.cs missing {token!r}')

checks = {
    'Scripts/Patching/Core/RebirthHarmonyBootstrap.cs':'RebirthLogSettings.TraceHarmonyPatch',
    'Scripts/Survivor/RebirthSurvivorInstaller.cs':'RebirthLogSettings.TraceRuntimeInstall',
    'Scripts/Survivor/UI/RebirthNativePlayerProfileBridge.cs':'RebirthLogSettings.TracePlayerProfileBridge',
    'Scripts/Survivor/UI/XUiC_RebirthSurvivorCreator.cs':'RebirthLogSettings.TraceUiRoute',
    'Scripts/Survivor/UI/XUiC_RebirthSurvivorProfileManager.cs':'RebirthLogSettings.TraceUiRoute',
    'Scripts/Survivor/UI/RebirthSurvivorFirstEntryUiService.cs':'RebirthLogSettings.TraceExit',
    'Scripts/Survivor/UI/RebirthSurvivorUiInstaller.cs':'RebirthLogSettings.TraceProgressionExplorer',
}
for path, token in checks.items():
    data=(root/path).read_text(encoding='utf-8')
    if token not in data: errors.append(f'{path}: missing {token}')

# No INFO-level direct calls for the high-volume prefixes should remain outside the centralized
# gate. Warnings/errors intentionally remain direct.
for p in (root/'Scripts').rglob('*.cs'):
    if p.name == 'RebirthLogSettings.cs':
        continue
    data=p.read_text(encoding='utf-8')
    forbidden=[
        'Log.Out("[REBIRTH Harmony][PatchClass]',
        'Log.Out("[REBIRTH Survivor][UiRoute]',
        'Log.Out("[REBIRTH Survivor][PlayerProfileBridge]',
        'Log.Out("[REBIRTH Survivor][PlayerProfilePortrait]',
        'Log.Out("[REBIRTH Survivor][ExitTrace]',
        'Log.Out("[REBIRTH Progression Explorer][PE-06]',
    ]
    for marker in forbidden:
        if marker in data:
            errors.append(f'{p.relative_to(root)}: ungated automatic INFO log remains: {marker}')

if errors:
    print('FAIL')
    for e in errors: print(' -',e)
    sys.exit(1)
print('PASS: noisy automatic REBIRTH INFO traces are config-gated; focused UI traces default off and warnings/errors stay direct.')
