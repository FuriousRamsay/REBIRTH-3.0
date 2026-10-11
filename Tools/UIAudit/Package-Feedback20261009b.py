from pathlib import Path
import hashlib,zipfile,json
root=Path.cwd();out=root/'_Documentation/UiFeedback_20261009b';before=out/'before'
changed=[p.relative_to(before).as_posix() for p in before.rglob('*') if p.is_file() and (root/p.relative_to(before)).is_file() and p.read_bytes()!=(root/p.relative_to(before)).read_bytes()]
changed+=['Scripts/Crafting/Cooking/XUiC_RebirthCookingWorkspace.MillingLayout.cs','Tools/GameBridge/tests/ui_feedback_20261009b.json','RebirthUtils.dll','RebirthUtils.pdb']
changed += [p.relative_to(root).as_posix() for p in (root/'Tools/UIAudit').glob('*Feedback*20261009b*')]
changed += [p.relative_to(root).as_posix() for p in (root/'Tools/UIAudit').glob('*Milling*20261009b*')]
changed=sorted(set(p for p in changed if (root/p).is_file()))
report='''# Latest UI feedback — 2026-10-09

Delivery: changed-files package, installed Debug build in the mod root. No game launch or input performed.

| Request | Implementation | Validation / status |
|---|---|---|
| Sell equipped backpack sale stash | RebirthStashBatchPrice.Price now reads SandboxOptionManager TraderSellPrices; obsolete EnumGamePrefs 130 was OptionsBloom | Production pricing/planner fixture passes with old preference zero, bonus, override, condition, bundles, rounding, shared stock limit and retained remainder. Trader interaction pending. |
| Modify backpack 11 columns | EditorBackpack.SetStacks and windows.xml: 169 stable cell indices, 69px cells / 71px pitch, 779px viewport with separate scrollbar | XML parses, cell identities verified, compiled. Visual/drag/filter test pending. |
| Non-cooking station crafting arrangement | Shared station_templates.xml: item/description/stats center, two-column requirements below, center backpack with 11 columns, existing tools/fuel above right-side output and queue | All shared template variants retain native crafting controllers. XML parses and compiles. Workbench, chemistry, forge, tools-station visuals pending. |
| Mortar arrangement, categories and no herbs | CookingWorkspace and MillingLayout: same three-column arrangement; classified skill labels, item description/attributes, two-column ingredient rows; herb slots hidden; reference preparation only with references | Keeps existing milling processing, ingredient returns and queue. Compiled; live processing/visual validation pending. |

Cooking stations keep their existing wide 22-column backpack and herbs. The narrower non-cooking center backpack uses 11 columns to match the newly requested personal-crafting arrangement. Storage capacity and item indices are unchanged.

Build: SUCCESS, 0 errors, 21 existing warnings. See BUILD.log. Pricing: SALE_TEST.txt. XML: XML_CHECK.txt. Test checklist: Tools/GameBridge/tests/ui_feedback_20261009b.json (not executed).

The DLL is cumulative and also contains the Purge source changes already present in this working tree. Purge remains unfinished and disabled; this build is not a Purge completion claim. No saved-game schema was changed by this UI pass.

## REBIRTH FEATURE COMPLETION CONFIRMATION

Feature: latest sale / Modify / non-cooking station feedback
Source cumulative project: current zzz_REBIRTH__3_0 workspace
Target project/version: installed game assemblies referenced by RebirthUtils.csproj
Design requirements reviewed: PASS
Relevant existing documentation reviewed: PASS
Accepted architecture preserved: PASS
Code requirements: PASS
XML requirements: PASS
UI requirements: FAIL — visual and interaction qualification pending
Localization requirements: PASS — existing keys and skill names reused
Images/assets requirements: N/A
Serialization/persistence requirements: N/A — existing storage untouched
Networking/synchronization requirements: PASS — existing sale transaction retained
Documentation requirements: PASS
Packaging requirements: PASS
Compilation: Performed
In-game validation: Not performed
COMPLETION VERDICT: NOT COMPLETE
Basis for verdict: implemented and built for testing; the reported station presentation and sale interaction still require in-game confirmation. Do not close them solely on compilation.
'''
(out/'REPORT.md').write_text(report,encoding='utf-8')
changed += ['_Documentation/UiFeedback_20261009b/'+n for n in ['REPORT.md','BUILD.log','SALE_TEST.txt','XML_CHECK.txt','NATIVE_SELL_PRICE.txt'] if (out/n).exists()]
hashes={p:hashlib.sha256((root/p).read_bytes()).hexdigest() for p in changed}
(out/'MANIFEST.json').write_text(json.dumps(hashes,indent=2));changed+=['_Documentation/UiFeedback_20261009b/MANIFEST.json']
archive=out/'UI_Feedback_20261009b_changed.zip'
with zipfile.ZipFile(archive,'w',zipfile.ZIP_DEFLATED) as z:
 for p in changed:z.write(root/p,p)
with zipfile.ZipFile(archive) as z:
 assert set(z.namelist())==set(changed)
 for p,h in hashes.items():assert hashlib.sha256(z.read(p)).hexdigest()==h
(out/'PACKAGE_SHA256.txt').write_text(hashlib.sha256(archive.read_bytes()).hexdigest()+'  '+archive.name+'\n')
print('Verified package: '+str(archive));print(str(len(changed))+' files; all archived hashes match source.')
