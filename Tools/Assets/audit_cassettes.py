"""Read-only cassette consumer audit; does not infer usefulness from item registration."""
from pathlib import Path
import json,xml.etree.ElementTree as E
root=Path(__file__).resolve().parents[2]
c=root/'Config/_Survivor'
audio=E.parse(c/'audiobooks.xml').findall('.//audiobook')
lit={x.get('id'):x for x in E.parse(c/'literature.xml').findall('.//item')}
know={x.get('id'):x for x in E.parse(c/'progression.xml').findall('.//knowledge')}
recipes=E.parse(c/'recipe_knowledge.xml').findall('.//recipe')
scripts={str(p.relative_to(root)):p.read_text(encoding='utf-8-sig') for p in (root/'Scripts').rglob('*.cs')}
done=json.loads((root/'_Documentation/Art/LiteratureConcepts/Production_Completed.json').read_text())
rows=[]
for a in audio:
 d=lit.get(a.get('source_literature_id')); k=d.get('knowledge','') if d is not None else ''
 refs=[]
 if k:
  for p,t in scripts.items():
   for n,line in enumerate(t.splitlines(),1):
    if '"'+k+'"' in line: refs.append(f'{p}:{n}: {line.strip()}')
 asset=root/'UIAtlases/ItemIconAtlas'/ (a.get('icon_key')+'.png')
 rows.append(dict(a.attrib,knowledge=k,kind=d.get('kind') if d is not None else None,recipes=[r.get('name') for r in recipes if k and k in r.get('knowledge','').split(',')],code_refs=refs,status=know[k].get('explorer_status','') if k in know else '',bytes=asset.stat().st_size if asset.exists() else 0,production=done.get(a.get('item_id'),{})))
out=root/'_Documentation/Icon_Restoration/cassette_consumer_audit_20260930.json'
out.write_text(json.dumps(rows,indent=2)+'\n')
for r in rows:
 if r['subtype'] not in ('primer','field_notes'):
  print(r['item_id'],r['knowledge'],'recipes='+str(len(r['recipes'])),'bytes='+str(r['bytes']))
  for s in r['code_refs']:print(' ',s)
