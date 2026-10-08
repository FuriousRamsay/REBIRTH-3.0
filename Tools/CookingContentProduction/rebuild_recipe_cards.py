from pathlib import Path
from PIL import Image,ImageDraw
import sys,json,xml.etree.ElementTree as E
import content_model as m
OUT=m.ROOT/'_Documentation/RecipeCardConsistency/FullSet'
def prepare():
 OUT.mkdir(exist_ok=True)
 for d in ['Templates','Masters','Icons','Review','Backups']:(OUT/d).mkdir(exist_ok=True)
 items={}
 for p in [m.ROOT.parents[1]/'Data/Config/items.xml',*sorted((m.ROOT/'Config').rglob('*.xml'))]:
  try:t=E.parse(p)
  except:continue
  for n in t.iter('item'):
   if n.get('name'):
    props=items.setdefault(n.get('name'),{})
    props.update({p.get('name'):p.get('value') for p in n.findall('property')})
 def icon(ident):
  props=items.get(ident,{});name=props.get('CustomIcon',ident)
  for folder in [m.ROOT/'UIAtlases/ItemIconAtlas',m.ROOT.parents[1]/'Data/ItemIcons']:
   p=folder/(name+'.png')
   if p.exists():return dict(path=str(p),tint=props.get('CustomIconTint'))
  raise ValueError((ident,name))
 jobs=[]
 for c in m.CATALOGUE['cards']:
  r=m.recipe(c['recipe']);t=m.ASSETS[c['id']]
  jobs.append(dict(key=c['id'],title=r['title'],kind='food',source=t['master'],icon=t['icon'],recipe=r['ingredients'],ingredients=[dict(id=i['id'],**icon(i['id'])) for i in r['ingredients']],installed=t['path']))
 meds=[('first-aid-kit','medicalFirstAidKit'),('plaster-cast','medicalPlasterCast'),('herbal-antibiotics','drugHerbalAntibiotics'),('antibiotics','drugAntibiotics'),('steroids','drugSteroids'),('fort-bites','drugFortBites'),('recog','drugRecog')]
 recipes=list(E.parse(m.ROOT.parents[1]/'Data/Config/recipes.xml').iter('recipe'))
 for slug,ident in meds:
  candidates=[n for n in recipes if n.get('name')==ident]
  r=next((n for n in candidates if n.get('craft_area')=='chemistryStation'),candidates[0]);ings=[dict(id=n.get('name'),count=int(n.get('count'))) for n in r.findall('ingredient')]
  jobs.append(dict(key=slug,title=slug.replace('-',' ').title(),kind='medical',source=str(m.ROOT/'_Documentation/MedicalRecipeCards/Drafts'/f'{slug}-illustrated-draft.png'),icon='rebirthMedicalCard'+''.join(x.title() for x in slug.split('-')),recipe=ings,ingredients=[dict(id=i['id'],**icon(i['id'])) for i in ings],station=r.get('craft_area')))
 (OUT/'jobs.json').write_text(json.dumps(jobs,indent=2))
 for start in range(0,len(jobs),24):
  rows=jobs[start:start+24];im=Image.new('RGB',(1200,((len(rows)+5)//6)*220),'#242424');dr=ImageDraw.Draw(im)
  for j,t in enumerate(rows):
   a=Image.open(t['source']).convert('RGBA');a.thumbnail((190,190));x=j%6*200;y=j//6*220;im.paste(a,(x,y),a);dr.text((x,y+192),t['key'],fill='white')
  im.save(OUT/'Review'/f'sources-{start//24+1}.jpg')
 print('Prepared',len(jobs),'jobs with all ingredient icons resolved')
if __name__=='__main__':prepare()
