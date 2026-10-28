from pathlib import Path
import math,json,hashlib
from PIL import Image,ImageDraw
ROOT=Path(__file__).resolve().parents[2]
SVG=ROOT/'Resources/UI/VehicleTuningIcons';PNG=ROOT/'UIAtlases/RebirthUiIcons'
SVG.mkdir(parents=True,exist_ok=True);PNG.mkdir(parents=True,exist_ok=True)
class Icon:
 def __init__(self):self.parts=[];self.holes=[];self.im=Image.new('RGBA',(512,512));self.d=ImageDraw.Draw(self.im)
 def pt(self,p):return tuple(round(v*8) for v in p)
 def line(self,pts,w=3.5):
  self.parts.append('<polyline points="'+ ' '.join(f'{x},{y}' for x,y in pts)+f'" fill="none" stroke="white" stroke-width="{w}" stroke-linecap="round" stroke-linejoin="round"/>')
  self.d.line([self.pt(p) for p in pts],fill='white',width=round(w*8),joint='curve')
  for x,y in pts:self.d.ellipse(self.pt((x-w/2,y-w/2))+self.pt((x+w/2,y+w/2)),fill='white')
 def circle(self,x,y,r,w=3.5,fill=False,hole=False):
  el=f'<circle cx="{x}" cy="{y}" r="{r}" fill="'+('black' if hole else 'white' if fill else 'none')+('"/>' if fill or hole else f'" stroke="white" stroke-width="{w}"/>')
  (self.holes if hole else self.parts).append(el)
  box=self.pt((x-r,y-r))+self.pt((x+r,y+r));self.d.ellipse(box,fill=(0,0,0,0) if hole else 'white' if fill else None,outline=None if hole or fill else 'white',width=round(w*8))
 def rect(self,x,y,w,h,r=0,fill=False,stroke=3.5):
  self.parts.append(f'<rect x="{x}" y="{y}" width="{w}" height="{h}" rx="{r}" fill="'+('white' if fill else 'none')+f'" stroke="white" stroke-width="{stroke}"/>')
  self.d.rounded_rectangle(self.pt((x,y))+self.pt((x+w,y+h)),radius=round(r*8),fill='white' if fill else None,outline='white',width=round(stroke*8))
 def polygon(self,pts,fill=False,w=3.5):
  self.parts.append('<polygon points="'+' '.join(f'{x},{y}' for x,y in pts)+'" fill="'+('white' if fill else 'none')+f'" stroke="white" stroke-width="{w}" stroke-linejoin="round"/>')
  if fill:self.d.polygon([self.pt(p) for p in pts],fill='white')
  self.d.line([self.pt(p) for p in pts+[pts[0]]],fill='white',width=round(w*8),joint='curve')
 def arc(self,x,y,r,start,end,w=3.5):
  a=(x+r*math.cos(math.radians(start)),y+r*math.sin(math.radians(start)));b=(x+r*math.cos(math.radians(end)),y+r*math.sin(math.radians(end)))
  self.parts.append(f'<path d="M{a[0]:.4f},{a[1]:.4f} A{r},{r} 0 {int(end-start>180)},1 {b[0]:.4f},{b[1]:.4f}" fill="none" stroke="white" stroke-width="{w}" stroke-linecap="round"/>')
  pts=[(x+r*math.cos(math.radians(start+(end-start)*i/160)),y+r*math.sin(math.radians(start+(end-start)*i/160))) for i in range(161)]
  self.d.line([self.pt(p) for p in pts],fill='white',width=round(w*8),joint='curve')
  for p in [a,b]:self.d.ellipse(self.pt((p[0]-w/2,p[1]-w/2))+self.pt((p[0]+w/2,p[1]+w/2)),fill='white')
 def save(self,name):
  mask='<defs><mask id="cut"><rect width="64" height="64" fill="white"/>'+''.join(self.holes)+'</mask></defs>' if self.holes else ''
  group='<g mask="url(#cut)">' if self.holes else '<g>'
  (SVG/(name+'.svg')).write_text('<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 64 64">'+mask+group+''.join(self.parts)+'</g></svg>',encoding='utf8')
  self.im.resize((256,256),Image.Resampling.LANCZOS).save(PNG/(name+'.png'))
def vehicle(a):
 a.polygon([(10,33),(15,28),(21,14),(43,14),(49,28),(54,33),(54,50),(10,50)])
 a.line([(16,28),(48,28)]);a.circle(19,38,3,fill=True);a.circle(45,38,3,fill=True);a.line([(17,51),(17,57)]);a.line([(47,51),(47,57)])
def wheel(a):
 a.circle(32,32,23);a.circle(32,32,7);a.line([(10,26),(25,29)]);a.line([(39,29),(54,26)]);a.line([(32,39),(32,54)])
icons={}
a=Icon();a.arc(32,43,24,180,360);a.line([(32,43),(43,27)]);a.circle(32,43,3,fill=True)
for angle in [205,240,275,310,345]:
 a.line([(32+19*math.cos(math.radians(angle)),43+19*math.sin(math.radians(angle))),(32+22*math.cos(math.radians(angle)),43+22*math.sin(math.radians(angle)))],2.5)
icons['speed']=a
a=Icon();wheel(a);icons['steering']=a
a=Icon();a.circle(29,32,21);a.circle(29,32,5)
for angle in [40,100,160,220,280]:a.circle(29+13*math.cos(math.radians(angle)),32+13*math.sin(math.radians(angle)),1.5,fill=True)
a.rect(47,18,10,28,3,fill=True);icons['brakes']=a
a=Icon();a.circle(32,8,4);a.circle(32,56,4);a.line([(32,12),(32,17)]);a.line([(32,47),(32,52)]);a.line([(22,17),(42,21),(22,27),(42,33),(22,39),(42,45),(25,48)],3.8);icons['suspension']=a
a=Icon();a.polygon([(18,20),(23,10),(41,10),(46,20),(49,24),(49,36),(15,36),(15,24)]);a.line([(21,21),(43,21)]);a.line([(21,36),(21,41)]);a.line([(43,36),(43,41)]);a.line([(21,45),(16,49),(23,54),(19,59)],3.5);a.line([(42,45),(37,49),(44,54),(40,59)],3.5);icons['stability']=a
a=Icon();a.rect(10,15,44,39,2);a.line([(18,15),(18,9),(46,9),(46,15)]);a.line([(10,25),(54,25)]);a.rect(26,33,12,9,1);icons['storage']=a
a=Icon();a.arc(42,20,13,-40,220,4);a.line([(31,29),(12,49)],8);a.circle(12,49,5);a.line([(52,11),(44,19),(48,27),(57,19)],3.5);icons['advanced']=a
a=Icon();a.arc(32,33,21,-65,230);a.polygon([(9,15),(10,30),(25,25)],True,2);icons['reset']=a
a=Icon();a.rect(16,13,34,43,2);a.rect(25,7,16,12,2,True);a.line([(24,29),(42,29)],3);a.line([(24,37),(42,37)],3);a.line([(24,45),(37,45)],3);icons['log']=a
a=Icon();wheel(a);icons['drive']=a
a=Icon();vehicle(a);icons['vehicle']=a
a=Icon();a.polygon([(22,39),(22,23),(44,8),(56,8),(56,20),(41,35),(26,41)]);a.circle(42,21,5);a.polygon([(21,26),(12,31),(11,43),(21,38)],True,2);a.polygon([(38,37),(34,51),(22,53),(27,41)],True,2);a.line([(17,45),(8,54)]);a.line([(22,49),(16,56)],2.5);icons['boost']=a
a=Icon();a.line([(49,51),(49,29),(45,20),(36,15),(26,15),(19,22),(17,30)],4);a.polygon([(8,26),(17,39),(27,26)],True,2);icons['reverse']=a
a=Icon();pts=[]
for n in range(32):
 angle=2*math.pi*n/32;r=25 if n%4 in [1,2] else 20;pts.append((32+r*math.cos(angle),32+r*math.sin(angle)))
a.polygon(pts,True,2);a.circle(32,32,9,hole=True);icons['acceleration']=a
for name,a in icons.items():a.save('rb_tuner_'+name)
# One inspectable contact sheet uses the exact shipped PNGs, not placeholder glyphs.
sheet=Image.new('RGBA',(700,400),(22,22,29,255));draw=ImageDraw.Draw(sheet)
from PIL import ImageFont
font=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',16)
for i,name in enumerate(icons):
 x=20+(i%7)*98;y=16+(i//7)*190
 im=Image.open(PNG/('rb_tuner_'+name+'.png')).resize((72,72),Image.Resampling.LANCZOS);sheet.alpha_composite(im,(x,y));draw.text((x,y+86),name,font=font,fill='white')
sheet.save(ROOT/'_Documentation/TunerRegression/tuner-icons-contact.png')
print('Wrote',len(icons),'vector source + RGBA atlas icons')
