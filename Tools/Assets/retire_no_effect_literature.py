"""Keep save identities; withdraw audited no-effect literature from acquisition and creative UI."""
from pathlib import Path
import json,re,xml.etree.ElementTree as E
root=Path(__file__).resolve().parents[2]
doc=root/'_Documentation/Icon_Restoration'
rows=json.loads((doc/'cassette_consumer_audit_20260930.json').read_text())
retired=[r for r in rows if r['subtype'] not in ('primer','field_notes') and not r['recipes'] and r['knowledge']!='procedure.drone.field_service']
assert len(retired)==20
ids={r[k] for r in retired for k in ('item_id','source_literature_id')}
for name in ('loot.xml','traders.xml','literature_distribution.xml'):
 p=root/'Config/_Survivor'/name
 s=p.read_text(encoding='utf-8-sig')
 for id in ids:
  s=re.sub(r'^\s*<item\s+(?:name|id)="'+re.escape(id)+r'"[^>]*\/>\s*\n','',s,flags=re.M)
 if name=='literature_distribution.xml':
  def recount(m):
   return re.sub(r'count="\d+"', 'count="'+str(len(E.fromstring(m[0]).findall('item'))) +'"', m[0], count=1)
  s=re.sub(r'<tier\b[^>]*>.*?</tier>',recount,s,flags=re.S)
 E.fromstring(s)
 p.write_text(s,encoding='utf-8',newline='\r\n')
p=root/'Config/_Survivor/items.xml'
s=p.read_text(encoding='utf-8-sig')
for id in sorted(ids):
 pattern=r'(<item name="'+re.escape(id)+r'">)(.*?)(</item>)'
 def hide(m):
  body=m[2]
  if 'name="CreativeMode"' not in body:body='\n      <property name="CreativeMode" value="None" />'+body
  return m[1]+body+m[3]
 s,n=re.subn(pattern,hide,s,flags=re.S)
 assert n==1,(id,n)
E.fromstring(s)
p.write_text(s,encoding='utf-8',newline='\r\n')
(doc/'retired_literature_20260930.json').write_text(json.dumps(retired,indent=2)+'\n')
print('Retired 20 cassette/printed pairs from acquisition and creative UI; definitions, registry and knowledge retained.')
