from pathlib import Path
import sys

root = Path(__file__).resolve().parents[2]
checks = []

def chk(name, ok, detail=""):
    checks.append((name, bool(ok), detail))

def text(rel):
    p = root / rel
    return p.read_text(encoding='utf-8', errors='ignore') if p.exists() else ''

xui = text('Config/XUi_InGame/xui.xml')
windows = text('Config/XUi_InGame/windows.xml')
pres = text('Scripts/Crafting/UI/XUiC_RebirthCraftingPresentation.cs')
auth = text('Scripts/Survivor/Capability/RebirthPersonalCraftAuthorizationService.cs')
remote = text('Scripts/Crafting/RemoteCrafting/RemoteResourceClientTransactions.cs')
backpack = text('Scripts/Survivor/UI/XUiC_RebirthExpandableBackpackScroll.cs')

chk('Rebirth conditional exists in XUi', "character_progression('Rebirth')" in xui)
chk('Public crafting group is still current integration target', "window_group[@name='crafting']" in xui or 'name="crafting"' in xui)
chk('Final Rebirth personal crafting queue evidence exists', 'rebirthCraftingQueueController' in windows)
chk('PC072 native CraftingInfo subclass evidence exists', 'XUiC_RebirthCraftingInfoWindow' in pres and 'XUiC_CraftingInfoWindow' in pres)
chk('Personal craft authorization preserves ItemActionEntryCraft', 'ItemActionEntryCraft' in auth and 'AuthorizeActivation' in auth)
chk('Remote crafting transaction preserves ItemActionEntryCraft', 'ItemActionEntryCraft' in remote and 'OnActivated' in remote)
chk('Generic expandable Backpack is 7-column and cannot own concept layout', 'Columns=7' in backpack.replace(' ', ''))
chk('Rebirth Health vital is present', 'rebirthVitalHealth' in windows)
chk('Rebirth Stamina vital is present', 'rebirthVitalStamina' in windows)
chk('Rebirth bottom HUD/toolbelt source is present', 'name="windowToolbelt"' in windows and 'anchor="CenterBottom"' in windows)

required_docs = [
    '_Documentation/ProjectChanges/REBIRTH_3_0_PC075_PERSONAL_CRAFTING_CHUNK_A_NATIVE_AUTHORITY_AUDIT_ARCHITECTURE_FREEZE_20260903.md',
    '_Documentation/ProjectChanges/REBIRTH_3_0_PC075_PERSONAL_CRAFTING_CHUNK_A_ARCHITECTURE_DECISION_RECORD_20260903.md',
    '_Documentation/ProjectChanges/REBIRTH_3_0_PC075_PERSONAL_CRAFTING_CHUNK_A_API_AUTHORITY_MATRIX_20260903.csv',
]
for rel in required_docs:
    chk(f'Document exists: {Path(rel).name}', (root / rel).exists())

for name, ok, detail in checks:
    print(('PASS' if ok else 'FAIL') + ' — ' + name + (f' — {detail}' if detail else ''))

fails = sum(1 for _, ok, _ in checks if not ok)
print(f'\nPC075 Chunk A: {len(checks)-fails} PASS / {fails} FAIL')
sys.exit(1 if fails else 0)
