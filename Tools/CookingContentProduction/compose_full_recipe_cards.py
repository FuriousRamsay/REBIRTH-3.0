"""Place canonical game sprites into compact single-row recipe-card footers."""
from pathlib import Path
from hashlib import sha256
from PIL import Image,ImageDraw
import json,shutil,sys
import numpy as np
from rebuild_recipe_cards import OUT
ROOT=Path(__file__).resolve().parents[2]

def ingredient(entry):
 im=Image.open(entry['path']).convert('RGBA')
 if entry.get('tint'):
  rgb=tuple(int(entry['tint'][i:i+2],16) for i in (0,2,4))
  a=np.array(im);a[:,:,:3]=(a[:,:,:3].astype(float)*np.array(rgb)/255).astype('uint8');im=Image.fromarray(a)
 return im.crop(im.getbbox())

def compose(job):
 destination=OUT/'Masters'/(job['icon']+'.png')
 approved={'C-meatstew':'meat-stew-compact-v3.png','plaster-cast':'plaster-cast-compact-v2.png'}
 if job['key'] in approved:
  shutil.copy2(OUT.parent/approved[job['key']],destination)
  if job['key']=='C-meatstew':
   card=Image.open(destination).convert('RGBA');old=Image.open(OUT.parent/'meat-stew-compact-v2.png').convert('RGBA')
   assert card.size==old.size
   y=round(card.height*.70);card.paste(old.crop((0,y,old.width,old.height)),(0,y));card.save(destination)
 else:
  template=OUT/'Templates'/(job['key']+'.png')
  if not template.exists():return False
  card=Image.open(template).convert('RGBA');w,h=card.size
  images=[ingredient(i) for i in job['ingredients']];n=len(images)
  # Crop transparent padding before fitting: original sprites remain recognizable.
  max_width={1:.36,2:.36,3:.34,4:.30,5:.27}.get(n,.23)*w
  sizes=[]
  for im in images:
   scale=min(max_width/im.width,.19*h/im.height)
   sizes.append([im.width*scale,im.height*scale])
  overlap=.12 if n<6 else .24
  gaps=[min(sizes[i][0],sizes[i+1][0])*overlap for i in range(n-1)]
  total=sum(s[0] for s in sizes)-sum(gaps)
  factor=min(1,.89*w/total)
  sizes=[[round(x*factor),round(y*factor)] for x,y in sizes];gaps=[round(g*factor) for g in gaps]
  total=sum(s[0] for s in sizes)-sum(gaps);x=round((w-total)/2)
  for i,(im,size) in enumerate(zip(images,sizes)):
   im=im.resize(tuple(size),Image.Resampling.LANCZOS)
   # Common baseline keeps every icon inside the cream lower border.
   y=round(.927*h)-im.height
   card.alpha_composite(im,(x,y));x+=im.width-(gaps[i] if i<len(gaps) else 0)
  card.save(destination)
 card=Image.open(destination).convert('RGBA');card.thumbnail((160,160),Image.Resampling.LANCZOS)
 canvas=Image.new('RGBA',(160,160));canvas.alpha_composite(card,((160-card.width)//2,(160-card.height)//2));canvas.save(OUT/'Icons'/(job['icon']+'.png'))
 return True

def review(jobs):
 valid={r['key'] for r in json.loads((OUT/'main-art-bounds.json').read_text())}|{'C-meatstew','plaster-cast'}
 available=[j for j in jobs if j['key'] in valid and (OUT/'Icons'/(j['icon']+'.png')).exists()]
 for start in range(0,len(available),24):
  rows=available[start:start+24];sheet=Image.new('RGB',(1200,((len(rows)+5)//6)*220),'#242424');draw=ImageDraw.Draw(sheet)
  for i,j in enumerate(rows):
   im=Image.open(OUT/'Masters'/(j['icon']+'.png')).convert('RGBA');im.thumbnail((190,190));x=i%6*200;y=i//6*220;sheet.paste(im,(x,y),im);draw.text((x,y+192),j['key'],fill='white')
  sheet.save(OUT/'Review'/f'finished-{start//24+1}.jpg')
 return len(available)
if __name__=='__main__':
 jobs=json.loads((OUT/'jobs.json').read_text())
 for j in jobs:compose(j)
 print('Completed',review(jobs),'of',len(jobs))
