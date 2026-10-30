"""Classify every inherited-only legacy player definition; no runtime changes."""
import csv,json,re
from pathlib import Path
from lxml import etree as E
import research_legacy as research
OUT=research.OUT

def value(blocks,name,key,seen=None):
 seen=set() if seen is None else seen
 if name in seen:raise ValueError('cycle '+name)
 seen.add(name);b=blocks.get(name)
 if b is None:return None,seen
 values=b.xpath('./property[@name=$key]/@value',key=key)
 if not values:values=b.xpath('./property[@class="CompositeFeatures"]/property[@class="TEFeatureStorage"]/property[@name=$key]/@value',key=key)
 if values:return values[-1],seen
 parents=b.xpath('./property[@name="Extends"]/@value')
 return value(blocks,parents[-1],key,seen) if parents else (None,seen)

def has_storage(blocks,name,seen=None):
 seen=set() if seen is None else seen
 if name in seen:return False
 seen.add(name);block=blocks.get(name)
 if block is None:return False
 if block.xpath('./property[@class="CompositeFeatures"]/property[@class="TEFeatureStorage"]'):return True
 parent=block.xpath('./property[@name="Extends"]/@value')
 return bool(parent) and has_storage(blocks,parent[-1],seen)

_conditional_cache={}
def conditional_records(tree,files):
 key=(id(tree),tuple(f['path'] for f in files))
 if key in _conditional_cache:return _conditional_cache[key]
 records=[]
 for file in files:
  path=Path(file['path'])
  for conditional in E.parse(str(path)).xpath('//conditional'):
   for op in conditional.iterdescendants():
    if not isinstance(op.tag,str):continue
    xpath=op.get('xpath')
    if not xpath:
     if op.tag=='property' and op.get('name') in ('LootList','Extends','ContainerSize','RebirthContainerSize','LootContainerSize'):
      ancestors=set(op.xpath('ancestor::block[1]/@name'))
      parent_paths=op.xpath('ancestor::*[@xpath][1]/@xpath')
      if parent_paths:
       try:
        for target in tree.xpath(parent_paths[0]):
         node=target if isinstance(target,E._Element) else target.getparent()
         ancestors.update(node.xpath('ancestor-or-self::block[1]/@name'))
       except E.XPathError:ancestors=None
      records.append((str(path)+': conditional capacity/inheritance property',ancestors,set()))
     continue
    try:targets=tree.xpath(xpath)
    except E.XPathError:
     records.append((str(path)+': unsupported conditional xpath '+xpath,None,None));continue
    added={b.get('name') for b in op.xpath('./block')}
    affected_blocks=set(added);affected_loot={b.get('name') for b in op.xpath('./lootcontainer')}
    for target in targets:
     node=target if isinstance(target,E._Element) else target.getparent()
     blocks=node.xpath('ancestor-or-self::block[1]/@name')
     containers=node.xpath('ancestor-or-self::lootcontainer[1]/@name')
     affected_blocks.update(blocks);affected_loot.update(containers)
    records.append((str(path)+': '+xpath,affected_blocks,affected_loot))
 _conditional_cache[key]=records
 return records

def influences(tree,files,names,loot):
 return [label for label,blocks,containers in conditional_records(tree,files) if blocks is None or blocks&names or loot in containers]

if __name__=='__main__':
 legacy=research.read if hasattr(research,'read') else lambda p:json.loads(Path(p).read_text(encoding='utf-8-sig'))
 old=legacy(OUT/'legacy-inherited-player-capacities.json');current=legacy(OUT/'current-research-files.json')
 oldblocks=E.parse(str(OUT/'out/legacy-blocks.xml'));oldloot=E.parse(str(OUT/'out/legacy-loot.xml'))
 newblocks=E.parse(str(OUT/'out/current-generated-blocks.xml'));newloot=E.parse(str(OUT/'out/current-loot.xml'))
 nodes={b.get('name'):b for b in newblocks.xpath('/blocks/block')}
 mapping_path=Path('C:/Program Files (x86)/Steam/steamapps/common/7 Days To Die/Mods/zzz_REBIRTH__3_0/Scripts/BlockPickup/RebirthBlockPickupLegacyTargets.cs')
 mappings=dict(re.findall(r'\{\s*"([^"]+)"\s*,\s*"([^"]+)"\s*\}',mapping_path.read_text(encoding='utf-8-sig')))
 sizes={b.get('name'):b.get('size') for b in newloot.xpath('/lootcontainers/lootcontainer')}
 rows=[]
 for row in old['pickedUp']:
  r=dict(row);source=row['block'][:-len('_PickedUp')];generated=source+'RebirthPlayer'
  r.update(currentSource=source,currentGenerated=generated)
  target=generated if generated in nodes else source if mappings.get(source)==source else None
  r['currentRoute']='generated source variant' if target==generated else 'explicit same-source canonical player mapping' if target else 'unresolved'
  if source not in nodes:r['classification']='REMOVED_OR_RENAMED_SOURCE_UNMAPPED'
  elif not has_storage(nodes,source):r['classification']='UNSUPPORTED_NONSTORAGE_REVERSE_PICKUP_ROUTE'
  elif target is None:r['classification']='UNSUPPORTED_CURRENT_SOURCE_NO_GENERATED_VARIANT'
  elif not row['size']:r['classification']='UNRESOLVED_LEGACY_LOOT_SIZE'
  else:
   loot,chain=value(nodes,target,'LootList');configured=None
   for key in ('RebirthContainerSize','ContainerSize','LootContainerSize'):
    configured,_=value(nodes,target,key)
    if configured:break
   size=configured or sizes.get(loot)
   routing={key:value(nodes,source,key)[0] for key in ('RebirthPickupTarget','ReplacementBlockName','RebirthVanillaPickupTarget')}
   routing={key:v for key,v in routing.items() if v}
   r.update(currentLootList=loot,currentSize=size,currentInheritance=sorted(chain),explicitPlayerSize=configured,currentRoutingOverrides=routing,currentLegacyTarget=mappings.get(source))
   if not size:r['classification']='UNRESOLVED_CURRENT_LOOT_SIZE'
   else:
    oldfiles=[f for f in old['files'] if Path(f['path']).name in ('blocks.xml','loot.xml') or '_loot' in Path(f['path']).name]
    currentfiles=current['files']
    influence=influences(oldblocks,oldfiles,set(row['inheritance']),row['effectiveLootList'])+influences(oldloot,oldfiles,set(),row['effectiveLootList'])
    influence+=influences(newblocks,currentfiles,chain,loot)+influences(newloot,currentfiles,set(),loot)
    r['conditionalInfluence']=sorted(set(influence))
    if influence:r['classification']='UNSUPPORTED_CONDITIONAL_INFLUENCE'
    elif row['size']==size:r['classification']='SOURCE_PARITY_MATCH'
    elif any(v.split(',')[0].strip() not in (source,generated) for v in routing.values()):r['classification']='UNSUPPORTED_ALTERNATE_PICKUP_ROUTE'
    else:
     oldn=__import__('math').prod(map(int,row['size'].split(',')));newn=__import__('math').prod(map(int,size.split(',')))
     r['classification']='EVIDENCED_CAPACITY_GAP' if oldn!=newn else 'DIMENSION_ONLY_DIFFERENCE'
  rows.append(r)
 counts={key:sum(r['classification']==key for r in rows) for key in sorted({r['classification'] for r in rows})}
 result={'scope':'ALL250 legacy picked-up definitions,161 direct+89 inherited; actual current generator source executed offline; conditional influence and alternate pickup routes excluded conservatively; native loader/game not executed','counts':counts,'directCount':sum(not r['inheritedOnly'] for r in rows),'inheritedCount':sum(r['inheritedOnly'] for r in rows),'rows':rows}
 (OUT/'inherited-classification.json').write_text(json.dumps(result,indent=2)+'\n')
 (OUT/'all-player-capacity-classification.json').write_text(json.dumps(result,indent=2)+'\n')
 with (OUT/'inherited-unsupported-removed.csv').open('w',newline='',encoding='utf-8') as f:
  fields=['block','currentSource','classification'];w=csv.DictWriter(f,fieldnames=fields);w.writeheader()
  for r in rows:
   if r['classification'].startswith(('UNSUPPORTED','UNRESOLVED','REMOVED')):w.writerow({k:r.get(k) for k in fields})
 with (OUT/'inherited-capacity-gaps.csv').open('w',newline='',encoding='utf-8') as f:
  fields=['block','currentSource','currentGenerated','effectiveLootList','size','currentLootList','currentSize','classification'];w=csv.DictWriter(f,fieldnames=fields);w.writeheader()
  for r in rows:
   if r['classification']=='EVIDENCED_CAPACITY_GAP':w.writerow({k:r.get(k) for k in fields})
 print(json.dumps(counts))
