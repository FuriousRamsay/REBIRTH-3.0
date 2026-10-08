#!/usr/bin/env python3
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[2]
errors=[]

def text(rel):
    return (ROOT/rel).read_text(encoding='utf-8-sig' if rel.endswith('Localization.csv') else 'utf-8')

ids=text('Scripts/Survivor/Domain/RebirthSurvivorIds.cs')
if not re.search(r'enum\s+RebirthPlayerProgressionMode\s*\{[^}]*BaseGame\s*=\s*0\s*,[^}]*Rebirth\s*=\s*1', ids, re.S):
    errors.append('progression enum must contain BaseGame=0 and Rebirth=1')

ui=text('Scripts/UI/XUiC_RebirthSandboxOptions.cs')
for token in [
    'cbxPlayerProgression.Elements.Add(Localization.Get("xuiRebirthPlayerProgressionBaseGame"));',
    'cbxPlayerProgression.Elements.Add(Localization.Get("xuiRebirthPlayerProgressionRebirth"));',
    'selectedIndex > (int)RebirthPlayerProgressionMode.Rebirth',
]:
    if token not in ui:
        errors.append('sandbox UI missing: '+token)

loc=text('Config/Localization.csv')
for row in ['xuiRebirthPlayerProgressionBaseGame,Base Game','xuiRebirthPlayerProgressionRebirth,Rebirth']:
    if row not in loc:
        errors.append('localization missing '+row)

session=text('Scripts/Options/RebirthSandboxUiSession.cs')
if session.count('RebirthSandboxPersistence.CurrentWorldHasStarted()') < 2:
    errors.append('UI session must classify existing saves using CurrentWorldHasStarted in reload and context checks')
if 'CurrentSaveDirectoryExists()' in session:
    errors.append('UI session must not lock Character Progression merely because a New Game target directory exists')

persist=text('Scripts/Options/RebirthSandboxPersistence.cs')
for token in [
    'public static bool CurrentWorldHasStarted()',
    'File.Exists(Path.Combine(saveDirectory, "main.ttw"))',
    'The New Game UI',
]:
    if token not in persist:
        errors.append('persistence started-world detector missing: '+token)

print('REBIRTH Character Progression New-Game Selectability:', 'PASS' if not errors else 'FAIL')
for e in errors: print('FAIL',e)
print('errors='+str(len(errors)))
raise SystemExit(1 if errors else 0)
