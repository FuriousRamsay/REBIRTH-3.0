"""Create a verified changed-files delivery; excludes unrelated project files."""
from pathlib import Path
import json, hashlib, zipfile
root=Path(__file__).resolve().parents[2];doc=root/'_Documentation/Icon_Restoration'
v=json.loads((doc/'validation_20260930.json').read_text())
files={root/r['path'] for r in v['files']}
files.update(root/'Config/_Survivor'/n for n in ('items.xml','loot.xml','traders.xml','literature_distribution.xml'))
files.update(doc/n for n in ('CLEANUP_20260930.md','validation_20260930.json','cassette_consumer_audit_20260930.json','retired_literature_20260930.json','generation_status_20260930.json','generated_cassette_manifest_20260930.json','support_icon_installation_20260930.json','cassette_art_queue_20260930.json','support_review_20260930.png','cassettes_review_20260930.png'))
files.update(root/'Tools/Assets'/n for n in ('audit_cassettes.py','retire_no_effect_literature.py','install_support_icons.py','install_generated_cassettes.py','validate_icon_cleanup.py','icon_cleanup_contact_sheet.py','package_icon_cleanup.py'))
files.add(doc/'generated_asset_manifest_20260930.json')
files.add(doc/'CASSETTE_DISPOSITION_20260930.md')
files.add(root/'Tools/Assets/write_cassette_disposition.py')
files.add(root/'Tools/GameBridge/tests/icon_cleanup_20260930.json')
manifest={str(p.relative_to(root)).replace('\\','/'):hashlib.sha256(p.read_bytes()).hexdigest() for p in sorted(files)}
out=Path('C:/Users/Etienne/Desktop/__0/_0/__Rebirth/REBIRTH_3_0_ICON_CASSETTE_CLEANUP_20260930.zip')
with zipfile.ZipFile(out,'w',zipfile.ZIP_DEFLATED) as z:
 for rel in manifest:z.write(root/rel,rel)
 z.writestr('_Documentation/Icon_Restoration/DELIVERY_SHA256.json',json.dumps(manifest,indent=2)+'\n')
with zipfile.ZipFile(out) as z:
 assert z.testzip() is None
 for rel,digest in manifest.items():assert hashlib.sha256(z.read(rel)).hexdigest()==digest,rel
report={'package':str(out),'sha256':hashlib.sha256(out.read_bytes()).hexdigest(),'verified_files':len(manifest),'type':'changed-files overlay; current local baseline, not cumulative'}
(doc/'delivery_20260930.json').write_text(json.dumps(report,indent=2)+'\n');print(report)
