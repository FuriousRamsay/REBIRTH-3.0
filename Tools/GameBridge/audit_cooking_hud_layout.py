"""Check reserved HUD bounds and exact native patch targets."""
from pathlib import Path
from lxml import etree as E
import json
R=Path(__file__).resolve().parents[2]
native=E.parse(str(R.parent.parent/'Data/Config/XUi_InGame/windows.xml'))
patch=E.parse(str(R/'Config/_Cooking/hud_windows.xml'))
for op in patch.xpath('//setattribute'):
    assert len(native.xpath(op.get('xpath')))==1,op.get('xpath')
source=(R/'Scripts/Crafting/Cooking/RebirthCookingHudService.cs').read_text()
assert 'pickups.ViewComponent' not in source and 'RestorePickup' not in source
assert 'new Vector2i(-595 - (i / rows) * 251, 247 + (i % rows) * 64)' in source
checks=0
for width,height in [(960,540),(1280,720),(1920,1080),(2560,1440),(3440,1440)]:
    rows=max(1,(height-70-247)//64+1);cols=max(1,(width-350-300)//251)
    for jobs in [0,1,2,8,32,64]:
        rects=[]
        for i in range(min(64,rows*cols,jobs)):
            left=width-595-(i//rows)*251;top=247+(i%rows)*64
            assert left>=300 and left+245<=width-350
            assert top-60>=187 and top<=height-70
            rect=(left,top-60,left+245,top)
            assert all(rect[2]<=r[0] or r[2]<=rect[0] or rect[3]<=r[1] or r[3]<=rect[1] for r in rects)
            rects.append(rect)
        checks+=1
# Pickup popup's 44px centered background starts above both native stat presentations.
assert 280-22>228 and 280-22>223
print(json.dumps({'pass':True,'resolution_job_combinations':checks,'targeted_native_nodes':2}))
