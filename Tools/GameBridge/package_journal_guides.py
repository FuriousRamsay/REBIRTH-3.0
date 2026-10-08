"""Package only files touched by the journal task, preserving their relative paths."""
from pathlib import Path
import zipfile,hashlib,json
R=Path(__file__).resolve().parents[2]
files=[
'Scripts/UI/RebirthJournalGuideService.cs','Scripts/UI/XUiC_RebirthJournal.cs','Scripts/UI/RebirthScreenLayout.cs',
'Scripts/Metabolism/UI/XUiC_RebirthMetabolismUi.cs','Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingTopTabs.cs',
'Scripts/GameBridge/RebirthGameBridgeHandlers.cs','Config/Localization.csv','Config/XUi_InGame/windows.xml','Config/XUi_InGame/journal_guides.xml',
'Resources/Journal/entries.xml','RebirthUtils.dll','RebirthUtils.pdb',
'Tools/GameBridge/build_journal_content.py','Tools/GameBridge/build_journal_ui.py','Tools/GameBridge/audit_journal_guides.py',
'Tools/GameBridge/test_journal_state.ps1','Tools/GameBridge/tests/journal_guides.json','Tools/GameBridge/package_journal_guides.py',
'_Documentation/Codex_Changes/CUMULATIVE_LOG.txt']
files+= [str(p.relative_to(R)).replace('\\','/') for folder in ['Resources/Journal/Images','_Documentation/JournalGuides'] for p in (R/folder).rglob('*') if p.is_file()]
files=sorted(set(files));assert all((R/f).is_file() for f in files)
manifest={f:hashlib.sha256((R/f).read_bytes()).hexdigest() for f in files}
out=R/'Tools/GameBridge/out/delivery';out.mkdir(parents=True,exist_ok=True)
p=out/'REBIRTH_20261002_journal_guides.zip'
with zipfile.ZipFile(p,'w',zipfile.ZIP_DEFLATED) as z:
    for f in files:z.write(R/f,f)
    z.writestr('JOURNAL_FILE_SHA256.json',json.dumps(manifest,indent=2))
with zipfile.ZipFile(p) as z:
    assert z.testzip() is None
    for f,h in manifest.items():assert hashlib.sha256(z.read(f)).hexdigest()==h
h=hashlib.sha256(p.read_bytes()).hexdigest();p.with_suffix('.zip.sha256').write_text(h+'  '+p.name+'\n')
print(json.dumps({'archive':str(p),'files':len(files),'sha256':h,'verified':True}))
