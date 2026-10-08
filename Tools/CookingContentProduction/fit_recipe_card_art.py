"""Extract existing illustrations and fit deterministic, measured card bounds."""
from pathlib import Path
import json,sys
import numpy as np
from PIL import Image,ImageFilter,ImageDraw
import content_model as model
from rebuild_recipe_cards import OUT
ROOT=model.ROOT

def background(kind):
 source=Image.open(OUT.parent/'Templates/five-ingredient-green.png').convert('RGBA');w,h=source.size
 split=round(h*.624);dest=round(h*.70)
 top=source.crop((0,0,w,split)).resize((w,dest),Image.Resampling.LANCZOS)
 bottom=source.crop((0,split,w,h)).resize((w,h-dest),Image.Resampling.LANCZOS)
 result=Image.new('RGBA',(w,h));result.paste(top,(0,0));result.paste(bottom,(0,dest))
 if kind=='medical':
  a=np.array(result);rgb=a[:dest,:,:3].astype(float)
  green=(rgb[:,:,1]>rgb[:,:,0]*.91)&(rgb[:,:,1]>rgb[:,:,2]*1.045)&(rgb[:,:,1]<180)
  # Preserve the cream frame while changing the empty green field to medical blue.
  new=rgb.copy();new[:,:,0]=rgb[:,:,0]*.68;new[:,:,1]=rgb[:,:,1]*.87;new[:,:,2]=rgb[:,:,1]*1.08
  rgb[green]=new[green];a[:dest,:,:3]=np.clip(rgb,0,255).astype('uint8');result=Image.fromarray(a)
 return result

def main():
 jobs=json.loads((OUT/'jobs.json').read_text());(OUT/'Cutouts').mkdir(exist_ok=True);audit=[]
 for j in jobs:
  if j['key'] in ['C-meatstew','plaster-cast']:continue
  key=j['key'][2:] if j['kind']=='food' else None
  override=OUT/'Isolated'/(j['key']+'.png')
  task=model.ASSETS.get(key,{})
  p=Path(task.get('master',''))
  if override.exists():
   art=Image.open(override).convert('RGBA');art=art.crop(art.getchannel('A').point(lambda x:255 if x>32 else 0).getbbox());method='isolated illustration'
  elif task.get('category')=='food' and p.is_file() and Image.open(p).width>500:
   art=Image.open(p).convert('RGBA');alpha=art.getchannel('A');art=art.crop(alpha.point(lambda x:255 if x>32 else 0).getbbox());method='existing isolated master'
  else:
   continue  # Incomplete jobs remain excluded from the validated bounds audit.
  art.save(OUT/'Cutouts'/(j['key']+'.png'))
  card=background(j['kind']);w,h=card.size
  scale=min(w*.67/art.width,h*.50/art.height)
  art=art.resize((round(art.width*scale),round(art.height*scale)),Image.Resampling.LANCZOS)
  x=round((w-art.width)/2);y=round(h*.38-art.height/2)
  assert x>=w*.15 and y>=h*.12 and x+art.width<=w*.85 and y+art.height<=h*.64
  card.alpha_composite(art,(x,y));card.save(OUT/'Templates'/(j['key']+'.png'))
  audit.append(dict(key=j['key'],method=method,bounds=[x,y,x+art.width,y+art.height],canvas=[w,h]))
 (OUT/'main-art-bounds.json').write_text(json.dumps(audit,indent=2));print('Measured',len(audit),'main illustrations')
if __name__=='__main__':main()
