"""Plot numeric plan contours only. Does not load, alter or render reference image."""
import json
from pathlib import Path
from PIL import Image,ImageDraw
root=Path(__file__).parent
out=Path('E:/_Haven/shared/REBIRTHTemp/ForgeReferenceBuild/PlanReview')
out.mkdir(parents=True,exist_ok=True)
im=Image.new('RGB',(1448,1086),'#f3f3f3');draw=ImageDraw.Draw(im)
files=[('furnace-reference-records.json','#666666'),('arch-stone-records.json','#986400'),('left-reference-records.json','#006c9b'),('connection-outlines.json','#006c9b')]
for fn,color in files:
 for r in json.loads((root/fn).read_text())['records']:
  for key in ['contourPixels','frontContour','outerContour','innerRimContour','centerlinePixels','axisPixels','pathPixels','bottomContourPixels']:
   pts=[tuple(p) for p in r.get(key,[])]
   if len(pts)<2:continue
   if 'Contour' in key or key=='contourPixels':pts.append(pts[0])
   if key=='pathPixels' and 'widthPixels' in r:
    # Explicit draft projected width, not a claimed measured band boundary.
    draw.line(pts,fill='#c6c6c6',width=r['widthPixels'],joint='curve')
   draw.line(pts,fill=color,width=2)
  for pts in r.get('foldRidgePaths',[]):draw.line([tuple(p) for p in pts],fill=color,width=2)
  for key in ['topEllipse','ellipse']:
   if key in r:
    x,y=r[key]['center'];w,h=r[key]['diameters'];draw.ellipse((x-w/2,y-h/2,x+w/2,y+h/2),outline=color,width=2)
for r in json.loads((root/'hardware-reference-records.json').read_text())['fasteners']:
 x,y=r['center'];radius=r['diameter']/2;draw.ellipse((x-radius,y-radius,x+radius,y+radius),outline='#943b68',width=2)
draw.text((25,25),'PLAN CONTOURS ONLY - measured draft, not Blender geometry or a validated model',fill='black')
im.save(out/'contour-plan-connections.png')
print(out/'contour-plan-connections.png')
