"""Author/export improvised meal assets and item definitions; no crafting recipes."""
import csv, json, sys, shutil
from pathlib import Path
import xml.etree.ElementTree as ET
from PIL import Image, ImageDraw
import assets

OUT = assets.OUT/'Improvised'
MAN = OUT/'manifest.json'
GROUPS = [
 ('Soup', 'Improvised Stew', 'campfire,WorkbenchGasStove001_FR', 'toolCookingPot', 'foodVegetableStew', [
  ('Thick','thick beige-brown anonymous mush',''),
  ('Brothy','pale cloudy broth with indistinct lumps',''),
  ('Dark','dark brown thick anonymous stew',''),
  ('Coarse','rusty tan rippled anonymous mixture','')]),
 ('Pan', 'Improvised Pan Meal', 'campfire,WorkbenchGasStove001_FR', 'rebirthCookingFryingPan', 'foodSteakAndPotato', [
  ('Golden','golden uneven browned anonymous clumps',''),
  ('Dark','dark coarse seared anonymous crumbles',''),
  ('Soft','pale soft lightly browned anonymous folds',''),
  ('Crisped','medium brown broken crisp anonymous patches','')]),
 ('Baked', 'Improvised Oven Bake', 'WorkbenchIronOven001_FR', 'FuriousRamsayBakingPan', 'foodCornBread', [
  ('Golden','golden cracked lumpy anonymous bake',''),
  ('Dark','dark brown dense cracked anonymous bake',''),
  ('Soft','pale lightly blistered anonymous bake',''),
  ('Crumbled','rusty brown rough crumbly anonymous bake','')]),
]

def init():
 OUT.mkdir(parents=True,exist_ok=True)
 if MAN.exists():return json.loads(MAN.read_text(encoding='utf-8'))
 rows=[]
 for category,title,stations,tool,ref,variants in GROUPS:
  for n,(variant,subject,tags) in enumerate(variants,1):
   ident=f'rebirthImprovised{category}{n:02}'
   prompt=('Create ONE 7 Days to Die food inventory icon. Supplied image is STYLE and CAMERA reference only, not the exact dish to copy. Subject: '+subject+'. Modest improvised survival food, believable and edible, not glamorous and not disgusting. Match native realistic game rendering, worn cookware, restrained detail, neutral light, three-quarter overhead angle. Large clear silhouette filling 90 percent of square without clipping. No loose ingredients outside serving, no cutlery, no labels, no text, no card, no scene, no floor, no glow or exterior shadow. Genuine transparent background: RGBA PNG, alpha zero outside food/container. Do NOT draw black, white or checkerboard background. Single isolated item, never a collage.')
   rows.append(dict(id=ident,category=category,title=title+' — '+variant,variant=variant,tags=tags,stations=stations,tool=tool,subject=subject,prompt=prompt,reference=str(assets.OUT/'OriginalFoodReferences'/(ref+'.png')),master=str(OUT/(ident+'.png')),icon=str(assets.ICONS/(ident+'.png')),status='pending'))
 MAN.write_text(json.dumps(rows,indent=2)+'\n',encoding='utf-8');return rows

def accept(ident,source):
 rows=init();r=next(r for r in rows if r['id']==ident)
 im=Image.open(source)
 if im.mode!='RGBA' or im.getchannel('A').getextrema()[0]!=0:
  raise ValueError('No true transparent alpha: '+ident)
 shutil.copy2(source,r['master']);assets.export(source,r['icon'])
 r.update(status='complete',source=str(source))
 MAN.write_text(json.dumps(rows,indent=2)+'\n',encoding='utf-8')

def build():
 rows=init()
 assert all(r['status']=='complete' for r in rows)
 assert len(rows)==12 and len({r['id'] for r in rows})==12
 for r in rows:
  im=Image.open(r['icon'])
  assert im.mode=='RGBA' and im.size==(160,160) and im.getchannel('A').getextrema()==(0,255),r['id']
 root=ET.Element('configs');body=ET.SubElement(root,'append',xpath='/items')
 for r in rows:
  item=ET.SubElement(body,'item',name=r['id'])
  # Preview fallback only. The future batch producer must resolve actual ingredient stats.
  props=dict(Tags='food,foodSkill,rebirthSurvivorFood,rebirthImprovisedFood',HoldType='31',DisplayType='foodWater',
   Meshfile='@:Other/Items/Food/'+('foodPotPrefab.prefab' if r['category']=='Soup' else 'parcelGenericPrefab.prefab'),
   DropMeshfile='@:Other/Items/Misc/sack_droppedPrefab.prefab',Material='Morganic',Stacknumber='1',EconomicValue='0',
   CustomIcon=r['id'],Group='Food/Cooking',DescriptionKey=r['id']+'Desc',CreativeMode='Player',
   RebirthMetabolismType='Food',RebirthNutritionUnits='10',RebirthFoodWaterMl='100' if r['category']=='Soup' else '0',
   RebirthStomachVolumeMl='300',RebirthDigestionProfile='normal',RebirthFoodSafetyProfile='safe',
   RebirthMoodFoodProfile='simple',RebirthMoodInfluence='0',RebirthDietTags=r['tags'],
   RebirthMoodVarietyFamily='improvised_'+r['category'].lower())
  for k,v in props.items():ET.SubElement(item,'property',name=k,value=v)
  action=ET.SubElement(item,'property',{'class':'Action0'})
  for k,v in [('Class','ItemActionConsumeMetabolismRebirth, RebirthUtils'),('Delay','1'),('Sound_start','player_eating')]:ET.SubElement(action,'property',name=k,value=v)
 ET.indent(root,space='  ')
 (assets.ROOT/'Config/_Cooking/improvised_items.xml').write_text(ET.tostring(root,encoding='unicode')+'\n',encoding='utf-8')
 p=assets.ROOT/'Config/_Rebirth/items.xml';s=p.read_text(encoding='utf-8')
 include='  <include filename="../_Cooking/improvised_items.xml"/>'
 if include not in s:p.write_text(s.replace('</configs>',include+'\n</configs>'),encoding='utf-8')
 p=assets.ROOT/'Config/Localization.csv'
 with p.open(encoding='utf-8-sig',newline='') as f:existing=list(csv.reader(f))
 ids={r['id'] for r in rows}|{r['id']+'Desc' for r in rows}
 with p.open('w',encoding='utf-8',newline='') as f:
  w=csv.writer(f,lineterminator='\n');w.writerows(r for r in existing if not r or r[0] not in ids)
  for r in rows:
   w.writerow([r['id'],r['title']]);w.writerow([r['id']+'Desc','An improvised meal made from available ingredients.'])
 sheet=Image.new('RGB',(960,3*205),(45,45,45));draw=ImageDraw.Draw(sheet)
 for i,r in enumerate(rows):
  x=i%4*240;y=i//4*205
  draw.text((x+5,y+5),r['category']+' / '+r['variant'],fill='white')
  im=Image.open(r['icon']);sheet.paste(im,(x+40,y+32),im)
 sheet.save(OUT/'review.png')
 lines=['# Improvised cooking outputs', '',
  '12 item definitions and native-style icons, created with the built-in image-generation tool. Full prompts, reference icons, generated sources and master paths are in [manifest.json](manifest.json).', '',
  '![All 12 icons at game size](review.png)', '',
  '## Integration status', '',
  'Items are included through Config/_Rebirth/items.xml and available in the creative menu. No recipe cards, fixed crafting recipes or loot entries are added. The ingredient-grid producer and automatic variant selection are not implemented by this asset/XML change.', '',
  'Current XML has conservative creative-preview fallback values: 10 nutrition, unspecified diet tags, zero base comfort, 100 ml food water for soup and zero for other categories. These are not calculated meal results or approved final balance. Do not use these fixed defaults as the ingredient-driven production contract. Actual nutrition, water, diet tags, safety, comfort and prepared bonuses must be calculated and persisted per batch by the future cooking producer and honored by consumption. The current resolver reads class properties. Stack size is one until batch identity and stacking are implemented.', '',
  '## Selection contract for the future producer', '',
  'Match station, tool, cooking method and actual ingredients first. Select only a compatible appearance, optionally avoiding the previous eligible variant. All artwork is intentionally ingredient-ambiguous. Do not infer ingredient identities from its color or texture. Station/tool and ingredient descriptions below are authoring guidance, not a runtime parser or functioning selection implementation. Each method category has four eligible generic appearances; choose one per batch and retain it.', '',
  'Persist the selected item identity with the batch. Do not reroll on inventory movement or loading. Appearance must not award nutrition, comfort, XP or dietary suitability. Variants within each category share a variety family so swapping pictures cannot evade meal repetition rules. Final ingredient-based family classification remains producer work.', '',
  '| Item | Display name | Stations | Tool | Appearance |', '|---|---|---|---|---|']
 for r in rows:lines.append(f"| `{r['id']}` | {r['title']} | {r['stations']} | {r['tool']} | {r['subject']} |")
 (OUT/'README.md').write_text('\n'.join(lines)+'\n',encoding='utf-8')
 print('Built 12 items, localization and review sheet; no recipes, cards or loot.')

if __name__=='__main__':
 if len(sys.argv)==1:print(json.dumps(init()))
 elif sys.argv[1]=='accept':accept(sys.argv[2],Path(sys.argv[3]))
 elif sys.argv[1]=='build':build()
