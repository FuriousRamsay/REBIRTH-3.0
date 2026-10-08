from pathlib import Path
import json, csv, re, hashlib, shutil, argparse
from lxml import etree as ET
ROOT=Path(__file__).resolve().parents[2]
LEGACY=Path('C:/Program Files (x86)/Steam/steamapps/common/7 Days To Die 2.6/Mods/zzz_REBIRTH__Core')
OUT=ROOT/'_Documentation/Icon_Restoration'
TYPES={'items':'item','item_modifiers':'item_modifier','blocks':'block'}
parser=ET.XMLParser(remove_comments=True)
def parse(p):return ET.parse(str(p),parser)
def images(folder):
 d={}
 for p in folder.rglob('*.png'):d.setdefault(p.stem,[]).append(p)
 return d
def source_definitions(root):
 records={};files=set()
 def walk(p,stack=()):
  p=p.resolve()
  if p in stack:return
  files.add(p)
  doc=parse(p)
  for n in doc.getroot().iter():
   if n.tag=='include':
    inc=p.parent/n.get('filename','')
    if inc.exists():walk(inc,stack+(p,))
   if n.tag in TYPES.values() and n.get('name'):
    records[(n.tag,n.get('name'))]=(n,str(p.relative_to(root)),n.sourceline)
 for typ in TYPES:
  p=root/'Config'/f'{typ}.xml'
  if p.exists():walk(p)
 return records,files
# Resolve icon-relevant patches against native + custom declarations. Both option branches
# are inventoried; no game is launched and this is not a full runtime XML interpreter.
def assemble(root, native):
 docs={};records,files=source_definitions(root)
 for typ,tag in TYPES.items():
  p=native/f'{typ}.xml'
  docs[typ]=parse(p) if p.exists() else ET.ElementTree(ET.Element(typ))
 for (tag,name),(node,src,line) in records.items():
  typ=next(t for t,v in TYPES.items() if v==tag);doc=docs[typ]
  for old in doc.xpath(f'/{typ}/{tag}[@name=$name]',name=name):old.getparent().remove(old)
  doc.getroot().append(ET.fromstring(ET.tostring(node)))
 # Gather only root property mutations, preserving include traversal order.
 seen=set();patches=[]
 def walk(p):
  p=p.resolve()
  if p in seen:return
  seen.add(p)
  def visit(n):
   if n.tag=='include':
    inc=p.parent/n.get('filename','')
    if inc.exists():walk(inc)
    return
   if n.tag in ('append','set','setattribute','remove') and n.get('xpath'):
    xp=n.get('xpath'); vals=n.findall('property')
    if ('CustomIcon' in xp or "[@name='Extends']" in xp or any(x.get('name') in ('CustomIcon','Extends') for x in vals)):
     patches.append((n,p))
   for c in n:visit(c)
  visit(parse(p).getroot())
 for typ in TYPES:walk(root/'Config'/f'{typ}.xml')
 errors=[]
 for n,p in patches:
  xp=n.get('xpath');typ=xp.split('/')[1] if xp.startswith('/') else ''
  if typ not in docs:continue
  try:matches=docs[typ].xpath(xp)
  except ET.XPathError as e:errors.append({'file':str(p),'xpath':xp,'error':str(e)});continue
  for m in matches:
   if n.tag=='append' and isinstance(m,ET._Element):
    for prop in n.findall('property'):
     if prop.get('name') in ('CustomIcon','Extends'):
      for old in m.findall(f"property[@name='{prop.get('name')}']"):m.remove(old)
      m.append(ET.fromstring(ET.tostring(prop)))
   elif n.tag=='setattribute' and isinstance(m,ET._Element):m.set(n.get('name'),n.text or '')
   elif n.tag=='set':
    if isinstance(m,ET._Element):m.text=n.text or ''
    elif getattr(m,'is_attribute',False):m.getparent().set(m.attrname,n.text or '')
   elif n.tag=='remove' and isinstance(m,ET._Element):m.getparent().remove(m)
 lookup={}
 for typ,doc in docs.items():
  for n in doc.getroot():
   if n.get('name'):lookup[(TYPES[typ],n.get('name'))]=n
 def icon(key,trail=()):
  if key in trail:return ('','inheritance cycle')
  n=lookup.get(key)
  if n is None:return ('','unresolved parent '+key[1])
  prop=n.find("property[@name='CustomIcon']")
  if prop is not None and prop.get('value'):return(prop.get('value'),'explicit/inherited CustomIcon')
  ext=n.find("property[@name='Extends']")
  if ext is not None and 'CustomIcon' not in ext.get('param1','').split(','):
   parent=(key[0],ext.get('value','')); parent=parent if parent in lookup else ('item',parent[1])
   val,reason=icon(parent,trail+(key,))
   if reason=='explicit/inherited CustomIcon':return val,reason
   if reason.startswith('unresolved'):return key[1],reason
  return key[1],'definition name'
 return records,files,icon,errors

def main():
 ap=argparse.ArgumentParser();ap.add_argument('--apply',action='store_true');args=ap.parse_args()
 current,files,resolve,errors=assemble(ROOT,ROOT.parent.parent/'Data/Config')
 old,_,oldresolve,olderrors=assemble(LEGACY,LEGACY.parent.parent/'Data/Config')
 target=images(ROOT/'UIAtlases/ItemIconAtlas');other=images(ROOT/'UIAtlases');native=images(ROOT.parent.parent/'Data/ItemIcons');legacy=images(LEGACY/'UIAtlases/ItemIconAtlas')
 external={}
 for mod in ROOT.parent.iterdir():
  if mod.is_dir() and mod!=ROOT:
   for k,v in images(mod/'UIAtlases/ItemIconAtlas').items():external.setdefault(k,[]).extend(v)
 rows=[];plan={};mapping=[]
 for key,(n,src,line) in sorted(current.items()):
  name=key[1];icon,reason=resolve(key);status='missing';legacy_key=key
  if name=='rebirthRageCapsule':legacy_key=('item','FuriousRamsayRageCapsule')
  if icon in target:status='present_mod'
  elif icon in native:status='native_icon'
  elif icon in external:status='dependency_icon'
  elif icon in other:status='wrong_atlas'
  candidate=icon if icon in legacy else ''
  if not candidate and legacy_key in old:
   oldicon,_=oldresolve(legacy_key)
   if oldicon in legacy:candidate=oldicon
  # Native/dependency reuse is not missing. Explicitly restore the identified Rage artwork.
  if (status in ('missing','wrong_atlas') or (name=='rebirthRageCapsule' and icon!=candidate)) and candidate:
   paths=legacy[candidate]
   if len({hashlib.sha256(p.read_bytes()).hexdigest() for p in paths})>1:status='ambiguous_legacy'
   else:
    dest=ROOT/'UIAtlases/ItemIconAtlas'/f'{candidate}.png'
    if not dest.exists():plan[candidate]=(paths[0],dest)
    if icon!=candidate:mapping.append({'type':key[0],'name':name,'icon':candidate,'old_icon':icon,'source':src})
    status='restore_legacy'
  rows.append({'type':key[0],'name':name,'source':src,'line':line,'resolved_icon':icon,'resolution':reason,'status':status,'legacy_icon':candidate})
 OUT.mkdir(exist_ok=True)
 with (OUT/'audit.csv').open('w',newline='',encoding='utf-8') as f:
  w=csv.DictWriter(f,fieldnames=rows[0].keys());w.writeheader();w.writerows(rows)
 manifest=[{'source':str(a),'destination':str(b.relative_to(ROOT)),'sha256':hashlib.sha256(a.read_bytes()).hexdigest()} for a,b in plan.values()]
 (OUT/'copy_plan.json').write_text(json.dumps(manifest,indent=2));(OUT/'mapping_plan.json').write_text(json.dumps(mapping,indent=2))
 from collections import Counter
 summary={'definitions':len(rows),'included_files':len(files),'statuses':dict(Counter(r['status'] for r in rows)),'copies':len(plan),'remappings':len(mapping),'patch_errors':errors,'legacy_patch_errors':olderrors}
 (OUT/'summary.json').write_text(json.dumps(summary,indent=2));print(json.dumps(summary,indent=2))
 if args.apply:
  for a,b in plan.values():
   if b.exists():raise RuntimeError('Refusing to overwrite '+str(b))
   shutil.copy2(a,b)
   assert hashlib.sha256(b.read_bytes()).digest()==hashlib.sha256(a.read_bytes()).digest()
if __name__=='__main__':main()
