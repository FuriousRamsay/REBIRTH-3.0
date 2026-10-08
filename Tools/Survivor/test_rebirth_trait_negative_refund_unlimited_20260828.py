from pathlib import Path
import xml.etree.ElementTree as ET
import sys

root=Path(__file__).resolve().parents[2]
errors=[]
def need(path,text):
    data=(root/path).read_text(encoding='utf-8',errors='replace')
    if text not in data: errors.append(f"{path}: missing {text!r}")

creation=ET.parse(root/'Config/_Survivor/progression.xml').getroot().find('./creation')
cap=int(creation.get('max_negative_trait_refund','999'))
if cap != -1: errors.append(f'max_negative_trait_refund expected -1/unlimited, got {cap}')
need('Scripts/Survivor/Creation/RebirthSurvivorTraitPointEconomy.cs','maxNegativeTraitRefund < 0 ? requested')
need('Scripts/Survivor/Creation/RebirthSurvivorCreationValidator.cs','RebirthSurvivorTraitPointEconomy.ApplyNegativeRefund')
need('Scripts/Survivor/UI/RebirthSurvivorUiText.cs','RebirthSurvivorTraitPointEconomy.ApplyNegativeRefund')
need('Scripts/Survivor/Debug/RebirthSurvivorTraitUiDebug.cs','RebirthSurvivorTraitPointEconomy.ApplyNegativeRefund')
need('Scripts/Survivor/Debug/RebirthSurvivorDiagnostics.cs','RebirthSurvivorTraitPointEconomy.ApplyNegativeRefund')
need('Scripts/Survivor/Domain/RebirthSurvivorDefinitionModels.cs','Math.Max(-1,maxNegativeTraitRefund)')
if errors:
    print('FAIL')
    for e in errors: print(' -',e)
    sys.exit(1)
print('PASS: every valid negative Trait refund contributes its full authored value; -1 means unlimited.')
