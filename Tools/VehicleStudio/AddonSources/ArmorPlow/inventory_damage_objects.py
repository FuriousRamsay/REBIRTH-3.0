import bpy,json
from pathlib import Path
p=Path('E:/_Haven/shared/REBIRTHStaged/VehicleStudio/CamperReferenceSurvey_20261017')
bpy.ops.wm.open_mainfile(filepath=str(p/'Thickness40Review_20261018/protective-thickness-review.blend'))
r={}
for c in ['Reference traced plating samples','Armor opposite side review','Reference connected window guards']:
 r[c]=[{'name':o.name,'type':o.type,'props':{k:str(o[k])[:160] for k in o.keys()}} for o in bpy.data.collections[c].objects]
(p/'damage-object-inventory.json').write_text(json.dumps(r,indent=2))
