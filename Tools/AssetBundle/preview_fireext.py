"""Contact sheet from actual original and repacked texture payloads."""
import sys
from pathlib import Path
sys.path.insert(0,str(Path(__file__).parent/'.python'))
import UnityPy
from PIL import Image, ImageDraw
ROOT=Path(__file__).resolve().parents[2]
original=UnityPy.load(str(ROOT/'Resources/RB_TOOL_FireEXT.unity3d'))
candidate=UnityPy.load(str(ROOT/'_Deliverables/FireEXT_2048_1024_256/Resources/RB_TOOL_FireEXT.unity3d'))
objects=[{o.path_id:o for o in env.objects} for env in [original,candidate]]
ids=[163,135,106,122,130,156]
sheet=Image.new('RGB',(600,50+len(ids)*285),(35,35,35));draw=ImageDraw.Draw(sheet)
draw.text((12,15),'Original (thumbnail)',fill='white');draw.text((312,15),'Optimized (thumbnail)',fill='white')
for row,key in enumerate(ids):
    for col,lookup in enumerate(objects):
        t=lookup[key].read();image=t.image.convert('RGBA');image.thumbnail((256,256),Image.Resampling.LANCZOS)
        x,y=col*300+12,50+row*285
        draw.text((x,y),f'{t.m_Name} ({t.m_Width})',fill='white')
        sheet.paste(image,(x,y+20),image)
path=ROOT/'_Documentation/AssetBundleOptimization/FireEXT/texture-comparison.png'
sheet.save(path);print(path)
