from pathlib import Path
from PIL import Image,ImageOps,ImageDraw
import json,shutil,hashlib
r=Path('.');d=r/'_Documentation/GearTransfers';rows=json.loads((d/'art_v2_sources.json').read_text())
sheet=Image.new('RGB',(960,580),(26,26,29));draw=ImageDraw.Draw(sheet)
for i,row in enumerate(rows):
 name=row['name'];item='rebirthGear'+name+'Belt';src=Path(row['path']);dst=d/'Masters'/f'{item}.png';shutil.copy2(src,dst)
 im=Image.open(dst).convert('RGBA');assert im.getextrema()[3][0]==0
 icon=ImageOps.contain(im,(160,160),Image.Resampling.LANCZOS);canvas=Image.new('RGBA',(160,160));canvas.alpha_composite(icon,((160-icon.width)//2,(160-icon.height)//2));canvas.save(r/'UIAtlases/ItemIconAtlas'/f'{item}.png')
 if i==0:canvas.save(r/'UIAtlases/UIAtlas/rb_challenge_StorageBelt.png')
 preview=ImageOps.contain(im,(280,240),Image.Resampling.LANCZOS);x=(i%3)*320;y=(i//3)*290;sheet.paste(preview,(x+(320-preview.width)//2,y),preview);draw.text((x+65,y+245),f'{name}: {6+2*i} slots',fill=(240,230,210))
 row['installed']='UIAtlases/ItemIconAtlas/'+item+'.png';row['sha256']=hashlib.sha256((r/row['installed']).read_bytes()).hexdigest()
sheet.save(d/'six_belt_models.png');(d/'art_v2_manifest.json').write_text(json.dumps(rows,indent=2))
