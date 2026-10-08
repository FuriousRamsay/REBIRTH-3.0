"""Package generated masters at the project's established 160px atlas size."""
from pathlib import Path
import json,shutil,hashlib
from PIL import Image
root=Path(__file__).resolve().parents[2]
doc=root/'_Documentation/Icon_Restoration'
rows=json.loads((doc/'generated_asset_manifest_20260930.json').read_text())
masters=doc/'Support_Masters';masters.mkdir(exist_ok=True)
for r in rows:
 src=Path(r['path']); dst=root/'UIAtlases/ItemIconAtlas'/(r['id']+'.png')
 im=Image.open(src).convert('RGBA'); a=im.getchannel('A')
 assert a.getextrema()==(0,255)
 shutil.copy2(src,masters/dst.name)
 box=a.point(lambda v:255 if v>=16 else 0).getbbox()
 icon=im.crop(box);icon.thumbnail((148,148),Image.Resampling.LANCZOS)
 canvas=Image.new('RGBA',(160,160));canvas.alpha_composite(icon,((160-icon.width)//2,(160-icon.height)//2));canvas.save(dst)
 r['master']=str((masters/dst.name).relative_to(root));r['installed']=str(dst.relative_to(root));r['sha256']=hashlib.sha256(dst.read_bytes()).hexdigest()
(doc/'support_icon_installation_20260930.json').write_text(json.dumps(rows,indent=2)+'\n')
print('Installed',len(rows),'transparent 160x160 icons; original masters retained.')
