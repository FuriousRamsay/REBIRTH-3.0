"""Offline legacy Extends/LootList capacity audit; no native/game modifications."""
import copy,csv,hashlib,json
from pathlib import Path
from lxml import etree as E
BASE=Path('C:/Program Files (x86)/Steam/steamapps/common/7 Days To Die 2.6')
OUT=Path(__file__).resolve().parents[2]/'_Documentation/LootCapacityAcceptance_20261029'

def patched(kind):
 tree=E.parse(str(BASE/'Data/Config'/f'{kind}.xml'));unsupported=[];files=[]
 def apply(path):
  files.append({'path':str(path),'sha256':hashlib.sha256(path.read_bytes()).hexdigest().upper()})
  for op in E.parse(str(path)).getroot():
   if not isinstance(op.tag,str):continue
   if op.tag=='include':apply(path.parent/op.get('filename'));continue
   if op.tag not in ('append','prepend','set','setattribute','remove','removeattribute','insertBefore','insertAfter'):
    unsupported.append({'path':str(path),'operation':op.tag});continue
   nodes=tree.xpath(op.get('xpath'))
   for target in nodes:
    if op.tag=='remove':
     if isinstance(target,E._Element):target.getparent().remove(target)
     elif getattr(target,'is_attribute',False):target.getparent().attrib.pop(target.attrname,None)
    elif op.tag=='removeattribute':
     if getattr(target,'is_attribute',False):target.getparent().attrib.pop(target.attrname,None)
     else:target.attrib.pop(op.get('name'),None)
    elif op.tag=='setattribute':target.set(op.get('name'),''.join(op.itertext()).strip())
    elif op.tag=='set':
     value=''.join(op.itertext()).strip()
     if getattr(target,'is_attribute',False):target.getparent().set(target.attrname,value)
     else:target.text=value
    elif getattr(target,'is_attribute',False):
     value=''.join(op.itertext()).strip()
     if op.tag=='append':target.getparent().set(target.attrname,str(target)+value)
     elif op.tag=='prepend':target.getparent().set(target.attrname,value+str(target))
     else:unsupported.append({'path':str(path),'operation':op.tag+' attribute'})
    else:
     children=[copy.deepcopy(c) for c in op if isinstance(c.tag,str)]
     if op.tag=='append':target.extend(children)
     elif op.tag=='prepend':
      for c in reversed(children):target.insert(0,c)
     elif op.tag=='insertBefore':
      for c in children:target.addprevious(c)
     else:
      for c in reversed(children):target.addnext(c)
 for mod in sorted((BASE/'Mods').iterdir(),key=lambda p:p.name.casefold()):
  path=mod/'Config'/f'{kind}.xml'
  if path.is_file():apply(path)
 return tree,files,unsupported

def effective(blocks,name,field,chain=None):
 chain=[] if chain is None else chain
 if name in chain:raise ValueError('Inheritance cycle '+str(chain+[name]))
 chain=chain+[name];block=blocks.get(name)
 if block is None:return None,chain
 values=block.xpath('./property[@name=$field]/@value',field=field)
 if values:return values[-1],chain
 parent=block.xpath('./property[@name="Extends"]/@value')
 if parent:return effective(blocks,parent[-1],field,chain)
 return None,chain

if __name__=='__main__':
 blocks,bfiles,bunsupported=patched('blocks');loot,lfiles,lunsupported=patched('loot')
 names={b.get('name'):b for b in blocks.xpath('/blocks/block')};lists={x.get('name'):x.get('size') for x in loot.xpath('/lootcontainers/lootcontainer')}
 rows=[]
 for name,block in names.items():
  if not name.endswith('_PickedUp'):continue
  value,chain=effective(names,name,'LootList')
  direct=bool(block.xpath('./property[@name="LootList"]'))
  rows.append({'block':name,'directLootList':direct,'effectiveLootList':value,'inheritance':chain,'size':lists.get(value),'inheritedOnly':not direct and value is not None})
 OUT.mkdir(parents=True,exist_ok=True)
 (OUT/'out').mkdir(exist_ok=True)
 blocks.write(str(OUT/'out/legacy-blocks.xml'),encoding='utf-8')
 loot.write(str(OUT/'out/legacy-loot.xml'),encoding='utf-8')
 (OUT/'legacy-inherited-player-capacities.json').write_text(json.dumps({'scope':'All installed legacy mod blocks/loot patches in casefold directory order; offline reconstruction, not native loader invocation','unsupported':bunsupported+lunsupported,'files':bfiles+lfiles,'pickedUp':rows},indent=2)+'\n')
 with (OUT/'legacy-inherited-only.csv').open('w',newline='',encoding='utf-8') as f:
  writer=csv.DictWriter(f,fieldnames=['block','effectiveLootList','size','inheritance']);writer.writeheader()
  for r in rows:
   if r['inheritedOnly']:writer.writerow({k:r[k] for k in writer.fieldnames})
 print(json.dumps({'pickedUp':len(rows),'inheritedOnly':sum(r['inheritedOnly'] for r in rows),'unsupported':bunsupported+lunsupported}))
 for r in rows:
  if r['block'] in ('cntIceMachine_PickedUp','cntDumpster_PickedUp','cntWallSafeInsecure_PickedUp','cntBookcaseFull_PickedUp'):print(json.dumps(r))



