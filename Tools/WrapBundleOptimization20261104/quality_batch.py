"""Compare every compressed surface with its original, in premultiplied color."""
import json, sys
from pathlib import Path
import numpy as np
from PIL import Image
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[1]
sys.path.insert(0,str(ROOT/'Tools/AssetBundle/.python'))
import UnityPy
OUT=Path('F:/REBIRTH_Wrap_Optimization/2026-11-04/Candidates')

def sample(im,size=(512,512)):
    # Compare visible color, ignoring arbitrary RGB stored behind transparency.
    im=im.convert('RGBA');im=Image.merge('RGBA',[b.resize(size,Image.Resampling.LANCZOS) for b in im.split()])
    a=np.asarray(im,dtype=np.float32);a[:,:,:3]*=a[:,:,3:4]/255;return a

def main():
    batch=json.loads((OUT/'batch.json').read_text());assert batch['status']=='PASS'
    rows=[]
    for entry in batch['results']:
        receipt=json.loads(Path(entry['receipt']).read_text())
        a=UnityPy.load(receipt['source']);b=UnityPy.load(receipt['candidate'])
        originals={o.path_id:o for o in a.objects if o.type.name=='Texture2D'}
        for o in b.objects:
            if o.type.name!='Texture2D':continue
            source=originals[o.path_id].read();candidate=o.read()
            aa=sample(source.image);bb=sample(candidate.image)
            color=float(np.sqrt(np.mean((aa[:,:,:3]-bb[:,:,:3])**2)))
            alpha=float(np.sqrt(np.mean((aa[:,:,3]-bb[:,:,3])**2)))
            row=dict(id=entry['id'],name=source.m_Name,premultipliedRgbRmse=color,alphaRmse=alpha)
            rows.append(row)
            assert color<8 and alpha<5,('Compression needs individual review',row)
        print(entry['id'],'quality PASS',flush=True)
    result=dict(status='PASS',textures=len(rows),method='512x512 channel-wise Lanczos comparison; premultiplied RGB; 0..255 units',
        maxRgbRmse=max(r['premultipliedRgbRmse'] for r in rows),maxAlphaRmse=max(r['alphaRmse'] for r in rows),rows=rows)
    (OUT/'quality.json').write_text(json.dumps(result,indent=2));print(json.dumps({k:v for k,v in result.items() if k!='rows'}))
if __name__=='__main__':main()
