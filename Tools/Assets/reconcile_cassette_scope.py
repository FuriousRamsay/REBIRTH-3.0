"""Restrict new cassette acquisition to Fundamentals/Field Notes; preserve saved IDs."""
from pathlib import Path
import collections, hashlib, json, re, xml.etree.ElementTree as E
root=Path(__file__).resolve().parents[2]; doc=root/'_Documentation/Icon_Restoration'; config=root/'Config/_Survivor'
audio=E.parse(config/'audiobooks.xml').findall('.//audiobook')
retired=[dict(e.attrib) for e in audio if e.get('subtype') not in ('primer','field_notes')]
ids={r['item_id'] for r in retired}; active={e.get('item_id') for e in audio}-ids
assert len(ids)==68 and len(active)==84
def definitions():
 return {e.get('name'):E.tostring(e) for e in E.parse(config/'items.xml').findall('.//item')}
before=definitions(); printed={r['source_literature_id'] for r in retired}
def noncassette_entries(name):
 return [E.tostring(e) for e in E.parse(config/name).findall('.//item') if e.get('name',e.get('id')) not in ids]
entry_before={n:noncassette_entries(n) for n in ('loot.xml','traders.xml','literature_distribution.xml')}
for name in entry_before:
 p=config/name;s=p.read_text(encoding='utf-8-sig')
 for id in ids:s=re.sub(r'^[ \t]*<item\s+(?:name|id)="'+re.escape(id)+r'"[^>]*/>[ \t]*\n','',s,flags=re.M)
 if name=='literature_distribution.xml':
  s=re.sub(r'<tier\b[^>]*>.*?</tier>',lambda m:re.sub(r'count="\d+"','count="'+str(len(E.fromstring(m[0]).findall('item')))+'"',m[0],count=1),s,flags=re.S)
 E.fromstring(s);p.write_text(s,encoding='utf-8',newline='\r\n')
p=config/'items.xml';s=p.read_text(encoding='utf-8-sig')
for id in sorted(ids):
 def hide(m):
  body=m[2]
  if 'name="CreativeMode"' not in body:body='\n      <property name="CreativeMode" value="None" />'+body
  return m[1]+body+m[3]
 s,n=re.subn(r'(<item name="'+re.escape(id)+r'">)(.*?)(</item>)',hide,s,flags=re.S);assert n==1,id
E.fromstring(s);p.write_text(s,encoding='utf-8',newline='\r\n')
p=config/'audiobooks.xml';s=p.read_text()
s=s.replace('current_audio_scope="theory_plus_narratable_discovery"','current_audio_scope="fundamentals_and_field_notes"').replace('discovery_scope="manuals_guides_recipes_formulas_cookbooks_audio_enabled_visual_schematics_print_only"','discovery_scope="print_only_legacy_cassette_ids_retained_for_saves"')
p.write_text(s,encoding='utf-8',newline='\r\n')
after=definitions();assert set(before)==set(after)
assert all(before[id]==after[id] for id in before if id not in ids),'Unrelated item changed'
assert all(entry_before[n]==noncassette_entries(n) for n in entry_before),'Printed or unrelated acquisition changed'
items={e.get('name'):e for e in E.parse(config/'items.xml').findall('.//item')}
for id in ids:
 assert items[id].find("property[@name='CreativeMode']").get('value')=='None'
 for n in entry_before:assert all(e.get('name',e.get('id'))!=id for e in E.parse(config/n).findall('.//item'))
for id in active:
 assert items[id].find("property[@name='CreativeMode']") is None
 for n in ('loot.xml','traders.xml'):assert any(e.get('name')==id for e in E.parse(config/n).findall('.//item')),(id,n)
assert [dict(e.attrib) for e in E.parse(config/'audiobooks.xml').findall('.//audiobook')]==[dict(e.attrib) for e in audio]
report={'active':84,'withdrawn':68,'withdrawn_by_type':dict(collections.Counter(r['subtype'] for r in retired)),'printed_definitions_and_acquisition_unchanged':True,'all_saved_ids_and_registry_entries_preserved':True,'in_game_tested':False,'retired':retired}
(doc/'cassette_scope_20260930.json').write_text(json.dumps(report,indent=2)+'\n')
print({k:v for k,v in report.items() if k!='retired'})
