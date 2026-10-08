"""Import generated burnt masters, preserving alpha, and export native-size item icons."""
import json, shutil, sys
from pathlib import Path
from PIL import Image
from assets import export, ROOT, CAT

folder=CAT/'Production/Burnt'
manifest_path=folder/'manifest.json'
manifest=json.loads(manifest_path.read_text(encoding='utf-8'))
sources=json.loads(Path(sys.argv[1]).read_text(encoding='utf-8')) if len(sys.argv)>1 else []
for source in sources:
    row=next(r for r in manifest if r['item']==source['item'])
    master=folder/(row['item']+'_burnt.png')
    shutil.copy2(source['source'],master)
    im=Image.open(master)
    alpha=im.getchannel('A').getextrema() if im.mode=='RGBA' else None
    row.update(source=source['source'],master=str(master),status='generated' if alpha and alpha[0]==0 else 'needs_alpha',alpha=alpha)
    export(master,Path(row['output']))
manifest_path.write_text(json.dumps(manifest,indent=2)+'\n',encoding='utf-8')
print(json.dumps({s:sum(r['status']==s for r in manifest) for s in sorted({r['status'] for r in manifest})}))
