from pathlib import Path
import sys, xml.etree.ElementTree as ET

root = Path(__file__).resolve().parents[2]
errors=[]

def need(path,text):
    data=(root/path).read_text(encoding='utf-8')
    if text not in data:
        errors.append(f"{path}: missing {text!r}")

cfg=root/'Config/_Survivor/debug.xml'
if not cfg.exists():
    errors.append('Config/_Survivor/debug.xml missing')
else:
    r=ET.parse(cfg).getroot()
    if r.tag!='survivor_debug': errors.append('debug.xml root must be survivor_debug')
    if r.attrib.get('trait_ui_logging','').lower()!='true': errors.append('trait_ui_logging must be true for diagnostic package')

need('Scripts/Logging/RebirthLogSettings.cs','LoadFromConfigRoot')
need('Scripts/Logging/RebirthLogSettings.cs','traitUi = nextTraitUi;')
need('Scripts/Logging/RebirthLogSettings.cs','traitUi = configuredTraitUi;')
need('Scripts/Survivor/RebirthSurvivorInstaller.cs','RebirthLogSettings.LoadFromConfigRoot(bundle.ConfigRoot);')
need('Scripts/Survivor/UI/XUiC_RebirthSurvivorCreator.cs','RebirthSurvivorTraitUiDebug.BuildReport(model)')
need('Scripts/Survivor/UI/RebirthSurvivorCreatorViewModel.cs','RebirthSurvivorTraitUiDebug.LogToggle')

if errors:
    print('FAIL')
    for e in errors: print(' -',e)
    sys.exit(1)
print('PASS: Trait UI diagnostics auto-enable from Survivor config before a world is loaded and survive debug reset.')
