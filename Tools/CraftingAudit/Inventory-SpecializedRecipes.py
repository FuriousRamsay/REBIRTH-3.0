from pathlib import Path
import xml.etree.ElementTree as E,csv,collections
root=Path('_Documentation/CraftingAudit_20261010')
known=set()
for kind in ['items','blocks','item_modifiers']:
 known.update(n.get('name') for n in E.parse(root/f'EFFECTIVE_{kind}_rebirth.xml').getroot() if n.get('name'))
areas={'ModifierStation','WorkbenchDistiller001_FR','WorkbenchShredder001_FR','WorkbenchResearchTable001_FR','SonjaOutfitDesigner','SonjaAmmoRecyclerStation'}
legacy=Path('C:/Program Files (x86)/Steam/steamapps/common/7 Days To Die 2.6/Mods/zzz_REBIRTH__Core/Config')
rows=[]
for file in [legacy/'recipes.xml',legacy/'_ztindustrial_recipes.xml',legacy.parent.parent/'zzzzzzz_REBIRTH__Cleanup/Config/recipes.xml']:
 tree=E.parse(file);parents={c:p for p in tree.iter() for c in p}
 for n in tree.iter('recipe'):
  if n.get('craft_area') not in areas:continue
  conditions=[];parent=parents.get(n)
  while parent is not None:
   if parent.tag=='if':conditions.append(parent.get('cond',''))
   if parent.tag=='else':conditions.append('ELSE branch')
   parent=parents.get(parent)
  deps=[n.get('name'),n.get('craft_tool')]+[i.get('name') for i in n.findall('ingredient')]
  missing=sorted({d for d in deps if d and d not in known})
  rows.append({'station':n.get('craft_area'),'recipe':n.get('name'),'conditions':' | '.join(reversed(conditions)),'missing_dependencies':' | '.join(missing),'source':file.name})
with (root/'SPECIALIZED_LEGACY_RECIPE_CANDIDATES.csv').open('w',newline='') as f:
 w=csv.DictWriter(f,fieldnames=['station','recipe','conditions','missing_dependencies','source']);w.writeheader();w.writerows(rows)
for area in sorted(areas):
 group=[r for r in rows if r['station']==area]
 print(area,len(group),'source recipes;',sum(not r['missing_dependencies'] for r in group),'with all dependencies present')
for r in rows:
 if not r['missing_dependencies']:print(r)

# Late routing operations are evidence separately from authored recipe nodes.
route_rows=[]
for file in (legacy.parent.parent).rglob('*recipes*.xml'):
 if '_NotUsed' in str(file) or '_removed' in str(file):continue
 try:tree=E.parse(file)
 except E.ParseError:continue
 parents={c:p for p in tree.iter() for c in p}
 for n in tree.iter('setattribute'):
  if n.get('name')!='craft_area' or (n.text or '').strip() not in areas:continue
  conditions=[];parent=parents.get(n)
  while parent is not None:
   if parent.tag=='if':conditions.append(parent.get('cond',''))
   if parent.tag=='else':conditions.append('ELSE branch')
   parent=parents.get(parent)
  route_rows.append({'station':n.text.strip(),'xpath':n.get('xpath'),'conditions':' | '.join(reversed(conditions)),'source':str(file.relative_to(legacy.parent.parent))})
with (root/'SPECIALIZED_LEGACY_ROUTING.csv').open('w',newline='') as f:
 w=csv.DictWriter(f,fieldnames=['station','xpath','conditions','source']);w.writeheader();w.writerows(route_rows)
print('Later explicit station-routing operations:',len(route_rows))
