from pathlib import Path
import xml.etree.ElementTree as ET
import sys
root=Path(__file__).resolve().parents[2]
errors=[]
def need(path,text):
    data=(root/path).read_text(encoding='utf-8',errors='replace')
    if text not in data: errors.append(f"{path}: missing {text!r}")

xml=ET.parse(root/'Config/_Survivor/debug.xml').getroot()
expected={
 'automatic_logging':'false',
 'trait_ui_logging':'false',
 'character_progression_ui_logging':'false',
 'harmony_patch_logging':'false',
 'runtime_install_logging':'false',
 'ui_route_logging':'true',
 'player_profile_bridge_logging':'true',
 'player_profile_portrait_logging':'true',
 'exit_trace_logging':'false',
 'progression_explorer_logging':'false',
}
for k,v in expected.items():
    if xml.get(k)!=v: errors.append(f'debug.xml {k}: expected {v}, got {xml.get(k)}')
for k in ('pre_spawn_logging','spawn_flow_logging'):
    if xml.get(k)!='true': errors.append(f'debug.xml {k}: expected true during active first-entry investigation, got {xml.get(k)}')
need('Scripts/Logging/RebirthLogSettings.cs','TraceCharacterProgression')
need('Scripts/Logging/RebirthLogSettings.cs','TraceHarmonyPatch')
need('Scripts/Logging/RebirthLogSettings.cs','TraceRuntimeInstall')
need('Scripts/Logging/RebirthLogSettings.cs','TraceUiRoute')
need('Scripts/Logging/RebirthLogSettings.cs','TracePlayerProfileBridge')
need('Scripts/Logging/RebirthLogSettings.cs','TracePlayerProfilePortrait')
need('Scripts/Logging/RebirthLogSettings.cs','TraceExit')
need('Scripts/Logging/RebirthLogSettings.cs','TraceProgressionExplorer')
need('Scripts/Logging/RebirthLogSettings.cs','TracePreSpawn')
need('Scripts/Logging/RebirthLogSettings.cs','TraceSpawnFlow')
need('Scripts/Patching/Core/RebirthHarmonyBootstrap.cs','RebirthLogSettings.TraceHarmonyPatch')
need('Scripts/Survivor/RebirthSurvivorInstaller.cs','RebirthLogSettings.TraceRuntimeInstall')
need('Scripts/Survivor/UI/XUiC_RebirthSurvivorCreator.cs','RebirthLogSettings.TraceUiRoute')
need('Scripts/Survivor/UI/XUiC_RebirthSurvivorCreator.cs','RebirthLogSettings.TracePlayerProfileBridge')
need('Scripts/Survivor/UI/XUiC_RebirthSurvivorCreator.cs','RebirthLogSettings.TracePlayerProfilePortrait')
if errors:
    print('FAIL')
    for e in errors: print(' -',e)
    sys.exit(1)
print('PASS: Main Menu automatic INFO logging categories have independent config toggles; automatic INFO categories stay gated; focused pre-spawn/creator traces are temporarily enabled for acceptance.')
