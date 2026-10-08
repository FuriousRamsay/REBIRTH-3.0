from pathlib import Path
from PIL import Image
import sys,json
root=Path.cwd();out=root/'_Documentation/RecipeCardConsistency'
# Exact game sprites arranged in one compact overlapping row.
def compose(template):
 card=Image.open(template).convert('RGBA');w,h=card.size
 entries=[('foodGrilledMeat',.18,.835,.265,.18),('foodCropCorn',.535,.832,.265,.195),('foodCropPotato',.365,.84,.255,.17),('resourceAnimalFat',.695,.84,.25,.17),('drinkJarBoiledWater',.86,.837,.205,.195)]
 for name,x,y,bw,bh in entries:
  p=root/'UIAtlases/ItemIconAtlas'/f'{name}.png'
  if not p.exists():p=root.parents[1]/'Data/ItemIcons'/f'{name}.png'
  im=Image.open(p).convert('RGBA');im=im.crop(im.getbbox())
  scale=min(w*bw/im.width,h*bh/im.height)
  im=im.resize((round(im.width*scale),round(im.height*scale)),Image.Resampling.LANCZOS)
  card.alpha_composite(im,(round(w*x-im.width/2),round(h*y-im.height/2)))
 card.save(out/'meat-stew-compact-v2.png')
 card.resize((160,160),Image.Resampling.LANCZOS).save(out/'meat-stew-compact-v2-160.png')
if __name__=='__main__':compose(sys.argv[1])

