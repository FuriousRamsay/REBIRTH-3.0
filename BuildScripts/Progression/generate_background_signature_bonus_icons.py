from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path
from typing import Callable, Dict, Iterable, List, Tuple
from PIL import Image, ImageDraw
import csv
import hashlib
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]
ATLAS_DIR = ROOT / 'UIAtlases' / 'RebirthSurvivorIcons'
MASTER_DIR = ROOT / '_Documentation' / 'Art' / 'Survivor' / 'BackgroundBonusMasters'
MANIFEST_PATH = ROOT / '_Documentation' / 'ProjectChanges' / 'REBIRTH_3_0_BACKGROUND_SIGNATURE_BONUSES_PC016_CHUNK_C_ICON_MANIFEST_20260902.csv'
REGISTRY_PATH = ROOT / 'Config' / '_Survivor' / 'background_bonuses.xml'
SETTINGS_PATH = ATLAS_DIR / 'settings.xml'
SOURCE_SHEET = ROOT / '_Documentation' / 'ProjectChanges' / 'REBIRTH_3_0_BACKGROUND_SIGNATURE_BONUSES_PC016_CHUNK_C_SOURCE_CONTACT_SHEET_20260902.jpg'
FINAL_SHEET = ROOT / '_Documentation' / 'ProjectChanges' / 'REBIRTH_3_0_BACKGROUND_SIGNATURE_BONUSES_PC016_CHUNK_C_64PX_CONTACT_SHEET_20260902.jpg'

SOURCE_SIZE = 256
FINAL_SIZE = 64
S = SOURCE_SIZE / FINAL_SIZE
WHITE = (244, 244, 239, 255)
ACCENT = (184, 155, 70, 255)  # restrained REBIRTH warm-gold accent
CLEAR = (0, 0, 0, 0)


def sc(v: float) -> int:
    return int(round(v * S))


def pts(seq: Iterable[Tuple[float, float]]) -> List[Tuple[int, int]]:
    return [(sc(x), sc(y)) for x, y in seq]


def line(d: ImageDraw.ImageDraw, xy, fill=WHITE, width=4, joint='curve'):
    d.line(pts(xy), fill=fill, width=sc(width), joint=joint)


def poly(d: ImageDraw.ImageDraw, xy, fill=WHITE):
    d.polygon(pts(xy), fill=fill)


def rect(d: ImageDraw.ImageDraw, box, fill=WHITE, outline=None, width=1, radius=0):
    b = tuple(sc(v) for v in box)
    if radius:
        d.rounded_rectangle(b, radius=sc(radius), fill=fill, outline=outline, width=sc(width))
    else:
        d.rectangle(b, fill=fill, outline=outline, width=sc(width))


def ellipse(d: ImageDraw.ImageDraw, box, fill=WHITE, outline=None, width=1):
    d.ellipse(tuple(sc(v) for v in box), fill=fill, outline=outline, width=sc(width))


def arc(d: ImageDraw.ImageDraw, box, start, end, fill=WHITE, width=3):
    d.arc(tuple(sc(v) for v in box), start=start, end=end, fill=fill, width=sc(width))


def shield(d, cx=43, cy=33, w=20, h=25, fill=WHITE):
    x0, x1 = cx - w / 2, cx + w / 2
    y0 = cy - h / 2
    poly(d, [(x0,y0),(x1,y0),(x1-1,cy+5),(cx,cy+h/2),(x0+1,cy+5)], fill)


def plus(d, cx, cy, size=10, thick=4, fill=WHITE):
    rect(d,(cx-thick/2,cy-size/2,cx+thick/2,cy+size/2),fill)
    rect(d,(cx-size/2,cy-thick/2,cx+size/2,cy+thick/2),fill)


def arrow_head(d, tip, direction='right', size=5, fill=WHITE):
    x,y=tip
    if direction=='right': poly(d,[(x,y),(x-size,y-size),(x-size,y+size)],fill)
    elif direction=='left': poly(d,[(x,y),(x+size,y-size),(x+size,y+size)],fill)
    elif direction=='up': poly(d,[(x,y),(x-size,y+size),(x+size,y+size)],fill)
    elif direction=='down': poly(d,[(x,y),(x-size,y-size),(x+size,y-size)],fill)


def gear(d, cx, cy, r=10, fill=WHITE):
    # bold 8-tooth gear silhouette
    import math
    outer=[]
    for i in range(32):
        a=math.radians(i*11.25-90)
        rr = r + (3 if (i//2)%2==0 else 0)
        outer.append((cx+rr*math.cos(a),cy+rr*math.sin(a)))
    poly(d,outer,fill)
    ellipse(d,(cx-r*0.42,cy-r*0.42,cx+r*0.42,cy+r*0.42),fill=CLEAR)


def wrench(d, x0=10,y0=48,x1=42,y1=16, width=7, fill=WHITE):
    line(d,[(x0,y0),(x1,y1)],fill,width)
    ellipse(d,(x0-5,y0-5,x0+5,y0+5),fill=fill)
    ellipse(d,(x0-2.2,y0-2.2,x0+2.2,y0+2.2),fill=CLEAR)
    # open jaw at far end
    poly(d,[(x1-6,y1-2),(x1-1,y1-7),(x1+4,y1-2),(x1+1,y1+1),(x1+7,y1+5),(x1+2,y1+10),(x1-3,y1+5)],fill)
    poly(d,[(x1-1,y1-4),(x1+2,y1-1),(x1+5,y1-4),(x1+7,y1-2),(x1+1,y1+4),(x1-4,y1)],CLEAR)


def book(d, x=11,y=19,w=42,h=28, fill=WHITE):
    # open book with center gutter
    poly(d,[(x,y),(x+w/2-2,y+4),(x+w/2-2,y+h),(x+2,y+h-5)],fill)
    poly(d,[(x+w,y),(x+w/2+2,y+4),(x+w/2+2,y+h),(x+w-2,y+h-5)],fill)
    line(d,[(x+w/2,y+5),(x+w/2,y+h)],CLEAR,2)


def crosshair(d,cx,cy,r=14,fill=WHITE):
    arc(d,(cx-r,cy-r,cx+r,cy+r),0,360,fill,3)
    line(d,[(cx-r-4,cy),(cx-r+4,cy)],fill,3)
    line(d,[(cx+r-4,cy),(cx+r+4,cy)],fill,3)
    line(d,[(cx,cy-r-4),(cx,cy-r+4)],fill,3)
    line(d,[(cx,cy+r-4),(cx,cy+r+4)],fill,3)


def flame(d,cx=32,cy=32,scale=1.0,fill=WHITE):
    poly(d,[(cx,cy-20*scale),(cx+9*scale,cy-7*scale),(cx+6*scale,cy+4*scale),(cx+14*scale,cy+12*scale),(cx+7*scale,cy+22*scale),(cx-6*scale,cy+21*scale),(cx-13*scale,cy+9*scale),(cx-5*scale,cy-1*scale),(cx-4*scale,cy-11*scale)],fill)
    poly(d,[(cx+1*scale,cy-4*scale),(cx+5*scale,cy+5*scale),(cx+2*scale,cy+14*scale),(cx-4*scale,cy+11*scale),(cx-5*scale,cy+4*scale)],CLEAR)


def pistol(d, x=8,y=20, fill=WHITE):
    rect(d,(x,y,x+34,y+11),fill,radius=2)
    rect(d,(x+28,y+8,x+38,y+13),fill)
    poly(d,[(x+10,y+10),(x+23,y+10),(x+20,y+31),(x+11,y+31)],fill)
    rect(d,(x+16,y+12,x+20,y+16),CLEAR)


def pot(d,x=12,y=22,w=40,h=25,fill=WHITE):
    rect(d,(x,y+6,x+w,y+h),fill,radius=4)
    rect(d,(x-5,y+12,x+2,y+17),fill)
    rect(d,(x+w-2,y+12,x+w+5,y+17),fill)
    rect(d,(x+5,y,x+w-5,y+6),fill,radius=3)
    line(d,[(x+10,y-5),(x+10,y-12)],fill,3)
    line(d,[(x+w/2,y-4),(x+w/2,y-14)],fill,3)
    line(d,[(x+w-10,y-5),(x+w-10,y-12)],fill,3)


def sprout(d,cx=30,base=49,fill=WHITE):
    line(d,[(cx,base),(cx,28)],fill,4)
    ellipse(d,(cx-14,20,cx,33),fill)
    ellipse(d,(cx,17,cx+15,31),fill)
    poly(d,[(cx-15,base+1),(cx+15,base+1),(cx+10,base+7),(cx-10,base+7)],fill)


def knife(d,x=10,y=46,fill=WHITE):
    poly(d,[(x,y),(x+24,y-25),(x+34,y-20),(x+18,y-3)],fill)
    rect(d,(x-3,y-2,x+16,y+6),fill,radius=3)


def martini(d,cx=24,top=15,fill=WHITE):
    poly(d,[(cx-16,top),(cx+16,top),(cx,top+22)],fill)
    poly(d,[(cx-11,top+4),(cx+11,top+4),(cx,top+17)],CLEAR)
    line(d,[(cx,top+21),(cx,top+39)],fill,3)
    line(d,[(cx-10,top+39),(cx+10,top+39)],fill,3)


def vest(d,cx=28,cy=34,fill=WHITE):
    poly(d,[(cx-14,cy-19),(cx-5,cy-23),(cx,cy-14),(cx+5,cy-23),(cx+14,cy-19),(cx+18,cy+18),(cx-18,cy+18)],fill)
    poly(d,[(cx-4,cy-13),(cx+4,cy-13),(cx+5,cy+12),(cx-5,cy+12)],CLEAR)


def draw_efficient_movement(d):
    # boot + restrained movement/energy trail
    poly(d,[(22,13),(36,13),(36,31),(48,38),(48,46),(29,46),(22,39),(12,39),(12,31),(22,31)],WHITE)
    line(d,[(8,20),(18,20)],ACCENT,3); line(d,[(5,27),(17,27)],ACCENT,3); line(d,[(8,34),(18,34)],ACCENT,3)


def draw_trauma_specialist(d):
    plus(d,28,31,26,9,WHITE)
    # bandage stripe and recovery arc
    poly(d,[(13,44),(43,14),(49,20),(19,50)],ACCENT)
    line(d,[(22,38),(28,32)],WHITE,2); line(d,[(31,29),(37,23)],WHITE,2)
    arc(d,(35,30,61,56),210,355,WHITE,3); arrow_head(d,(59,42),'up',4,WHITE)


def draw_pharmacy_eye(d):
    rect(d,(9,20,31,46),WHITE,radius=4); rect(d,(14,15,26,21),WHITE,radius=2)
    # capsule label
    rect(d,(12,31,28,35),ACCENT,radius=2)
    ellipse(d,(31,25,55,49),fill=CLEAR,outline=WHITE,width=4)
    line(d,[(48,43),(58,53)],WHITE,5)
    ellipse(d,(38,32,45,39),ACCENT)


def draw_reagent_recovery(d):
    # flask
    poly(d,[(24,10),(38,10),(38,23),(51,48),(13,48),(26,23),(26,10)],WHITE)
    poly(d,[(20,40),(44,40),(39,29),(25,29)],ACCENT)
    ellipse(d,(45,14,53,24),ACCENT)
    arc(d,(7,8,58,58),15,205,WHITE,3); arrow_head(d,(10,37),'down',4,WHITE)


def draw_bookworm(d):
    book(d,9,19,46,28,WHITE)
    # second-page duplicate and reading marks
    line(d,[(16,28),(25,30)],ACCENT,2); line(d,[(16,34),(25,36)],ACCENT,2)
    line(d,[(48,28),(39,30)],ACCENT,2); line(d,[(48,34),(39,36)],ACCENT,2)
    rect(d,(20,11,44,15),WHITE,radius=2)


def draw_parts_salvager(d):
    gear(d,42,23,9,WHITE); wrench(d,8,52,36,24,6,WHITE)
    ellipse(d,(42,42,51,51),ACCENT); ellipse(d,(45,45,48,48),CLEAR)


def draw_power_saver(d):
    # plug and battery with down gauge
    rect(d,(8,22,27,39),WHITE,radius=4); rect(d,(11,15,15,23),WHITE); rect(d,(20,15,24,23),WHITE)
    line(d,[(17,39),(17,50),(29,50)],WHITE,4)
    rect(d,(34,18,56,42),WHITE,radius=3); rect(d,(56,25,59,35),WHITE)
    rect(d,(39,23,51,37),CLEAR,radius=1); rect(d,(40,31,50,36),ACCENT)
    arrow_head(d,(45,49),'down',5,ACCENT); line(d,[(45,42),(45,49)],ACCENT,3)


def draw_built_to_last(d):
    # block wall left, shield right
    rect(d,(6,18,37,48),WHITE)
    for y in [28,38]: line(d,[(6,y),(37,y)],CLEAR,2)
    line(d,[(16,18),(16,28)],CLEAR,2); line(d,[(27,28),(27,38)],CLEAR,2); line(d,[(16,38),(16,48)],CLEAR,2)
    shield(d,45,34,20,28,ACCENT); plus(d,45,33,9,3,WHITE)


def draw_heat_treatment(d):
    # anvil and flame/hardened edge
    poly(d,[(10,36),(37,36),(47,31),(54,31),(48,39),(37,42),(37,48),(18,48),(18,43),(9,43)],WHITE)
    flame(d,27,22,0.55,ACCENT)
    line(d,[(12,35),(42,35)],WHITE,3)


def draw_nothing_disposable(d):
    wrench(d,10,50,38,22,6,WHITE)
    arc(d,(28,9,60,41),25,305,ACCENT,4); arrow_head(d,(54,17),'right',4,ACCENT)
    ellipse(d,(40,20,50,30),fill=CLEAR,outline=WHITE,width=3)


def draw_engineered_reliability(d):
    # compact turret/trap plus shield
    rect(d,(8,33,34,43),WHITE,radius=2); rect(d,(15,25,29,33),WHITE,radius=2)
    line(d,[(29,28),(42,22)],WHITE,5); ellipse(d,(16,43,22,49),WHITE); ellipse(d,(28,43,34,49),WHITE)
    shield(d,47,36,19,26,ACCENT); plus(d,47,34,8,3,WHITE)


def draw_ore_sense(d):
    # pick and ore nodes with scan arcs
    line(d,[(13,51),(40,16)],WHITE,5); arc(d,(16,10,44,27),200,340,WHITE,5)
    ellipse(d,(39,35,49,45),ACCENT); ellipse(d,(48,42,56,50),ACCENT); ellipse(d,(34,46,42,54),ACCENT)
    arc(d,(30,27,62,59),205,340,WHITE,2); arc(d,(25,22,64,62),205,335,WHITE,2)


def draw_professional_logging(d):
    ellipse(d,(27,27,57,51),WHITE); ellipse(d,(32,32,52,47),CLEAR); arc(d,(36,35,49,46),20,320,ACCENT,2)
    line(d,[(10,49),(34,17)],WHITE,6); poly(d,[(26,12),(42,18),(35,28),(20,22)],WHITE)


def draw_fire_resistant(d):
    flame(d,24,32,0.85,WHITE); shield(d,43,35,25,32,ACCENT); plus(d,43,34,8,3,WHITE)


def draw_combat_momentum(d):
    # three rising chevrons and cartridge/reload arc
    for i,(x,y) in enumerate([(10,43),(20,34),(30,25)]):
        poly(d,[(x,y+6),(x+8,y),(x+16,y+6),(x+13,y+10),(x+8,y+6),(x+3,y+10)],WHITE if i<2 else ACCENT)
    rect(d,(46,18,53,40),WHITE,radius=3); rect(d,(47.5,16,51.5,20),ACCENT,radius=1)
    arc(d,(37,31,60,55),30,265,WHITE,3); arrow_head(d,(39,48),'left',4,WHITE)


def draw_patrol_car_familiarity(d):
    # light bar + three rounds
    rect(d,(8,20,39,28),WHITE,radius=3); rect(d,(12,15,22,22),ACCENT,radius=2); rect(d,(25,15,35,22),WHITE,radius=2)
    for x in [16,31,46]:
        rect(d,(x,34,x+7,51),WHITE,radius=3); poly(d,[(x,34),(x+3.5,29),(x+7,34)],ACCENT)


def draw_hunters_mark(d):
    crosshair(d,32,31,19,WHITE)
    # simplified deer/animal head silhouette
    poly(d,[(27,25),(32,22),(37,25),(39,34),(32,39),(25,34)],ACCENT)
    line(d,[(28,24),(23,17),(20,18)],ACCENT,3); line(d,[(36,24),(41,17),(44,18)],ACCENT,3)
    ellipse(d,(13,48,18,53),WHITE); ellipse(d,(21,51,26,56),WHITE)


def draw_calming_presence(d):
    # animal head + open hand + calming waves
    poly(d,[(10,24),(17,18),(26,20),(31,28),(27,39),(17,42),(9,35)],WHITE)
    poly(d,[(13,23),(9,14),(18,20)],WHITE); poly(d,[(24,20),(29,13),(28,24)],WHITE)
    ellipse(d,(17,28,20,31),CLEAR)
    # open hand on right
    rect(d,(39,34,53,46),ACCENT,radius=5); line(d,[(43,34),(43,25)],ACCENT,3); line(d,[(47,34),(47,23)],ACCENT,3); line(d,[(51,35),(51,27)],ACCENT,3)
    arc(d,(29,15,60,47),210,300,WHITE,2); arc(d,(33,11,64,43),210,300,WHITE,2)


def draw_gunsmith_master_restoration(d):
    pistol(d,6,20,WHITE)
    arc(d,(31,24,60,53),40,310,ACCENT,4); arrow_head(d,(53,27),'right',4,ACCENT)
    plus(d,46,39,8,3,WHITE)


def draw_professional_cooking(d):
    pot(d,8,24,37,22,WHITE)
    # sustained energy pulse
    line(d,[(44,46),(48,39),(52,47),(57,34),(61,41)],ACCENT,3)


def draw_tailor_master_restoration(d):
    vest(d,27,33,WHITE)
    line(d,[(42,47),(55,17)],ACCENT,3); ellipse(d,(51,14,58,21),fill=CLEAR,outline=ACCENT,width=2)
    arc(d,(32,28,61,57),35,300,WHITE,3); arrow_head(d,(54,31),'right',4,WHITE)


def draw_rapid_cultivation(d):
    sprout(d,27,47,WHITE)
    for x,y0,y1 in [(46,48,27),(54,43,20)]:
        line(d,[(x,y0),(x,y1)],ACCENT,3); arrow_head(d,(x,y1),'up',4,ACCENT)


def draw_whole_animal(d):
    knife(d,8,48,WHITE)
    # meat cut and bone set
    ellipse(d,(37,19,57,35),ACCENT); ellipse(d,(43,24,49,30),CLEAR)
    line(d,[(39,46),(54,38)],WHITE,5); ellipse(d,(35,44,42,51),WHITE); ellipse(d,(52,34,59,41),WHITE)


def draw_master_mixologist(d):
    martini(d,23,13,WHITE)
    # clock/double-duration motif: two arcs instead of text
    ellipse(d,(38,27,59,48),fill=CLEAR,outline=ACCENT,width=3)
    line(d,[(48.5,37.5),(48.5,31),(54,37.5)],ACCENT,2)
    arc(d,(34,23,63,52),210,340,WHITE,2); arc(d,(31,20,64,55),210,335,WHITE,2)


def draw_more_options(d):
    # three reward cards with added choice
    rect(d,(8,18,27,42),WHITE,radius=2); rect(d,(22,14,41,42),ACCENT,radius=2); rect(d,(36,18,55,42),WHITE,radius=2)
    plus(d,50,50,13,4,ACCENT)


def draw_scholar_and_mentor(d):
    book(d,8,27,36,22,WHITE)
    # two heads + instruction arc
    ellipse(d,(42,15,50,23),ACCENT); ellipse(d,(52,24,60,32),WHITE)
    arc(d,(33,12,62,42),190,315,ACCENT,3); arrow_head(d,(59,25),'down',3,ACCENT)


def draw_nothing_is_junk(d):
    # chaotic scrap on left -> organized components on right
    gear(d,17,30,7,WHITE); line(d,[(7,48),(22,39)],WHITE,5); ellipse(d,(9,45,15,51),WHITE)
    line(d,[(28,32),(38,32)],ACCENT,4); arrow_head(d,(40,32),'right',4,ACCENT)
    rect(d,(43,19,55,29),WHITE,radius=2); rect(d,(43,34,55,44),WHITE,radius=2); ellipse(d,(47,48,55,56),WHITE)


DRAWERS: Dict[str, Callable[[ImageDraw.ImageDraw], None]] = {
    'rb_bonus_efficient_movement': draw_efficient_movement,
    'rb_bonus_trauma_specialist': draw_trauma_specialist,
    'rb_bonus_pharmacy_eye': draw_pharmacy_eye,
    'rb_bonus_reagent_recovery': draw_reagent_recovery,
    'rb_bonus_bookworm': draw_bookworm,
    'rb_bonus_parts_salvager': draw_parts_salvager,
    'rb_bonus_power_saver': draw_power_saver,
    'rb_bonus_built_to_last': draw_built_to_last,
    'rb_bonus_heat_treatment': draw_heat_treatment,
    'rb_bonus_nothing_disposable': draw_nothing_disposable,
    'rb_bonus_engineered_reliability': draw_engineered_reliability,
    'rb_bonus_ore_sense': draw_ore_sense,
    'rb_bonus_professional_logging': draw_professional_logging,
    'rb_bonus_fire_resistant': draw_fire_resistant,
    'rb_bonus_combat_momentum': draw_combat_momentum,
    'rb_bonus_patrol_car_familiarity': draw_patrol_car_familiarity,
    'rb_bonus_hunters_mark': draw_hunters_mark,
    'rb_bonus_calming_presence': draw_calming_presence,
    'rb_bonus_gunsmith_master_restoration': draw_gunsmith_master_restoration,
    'rb_bonus_professional_cooking': draw_professional_cooking,
    'rb_bonus_tailor_master_restoration': draw_tailor_master_restoration,
    'rb_bonus_rapid_cultivation': draw_rapid_cultivation,
    'rb_bonus_whole_animal': draw_whole_animal,
    'rb_bonus_master_mixologist': draw_master_mixologist,
    'rb_bonus_more_options': draw_more_options,
    'rb_bonus_scholar_and_mentor': draw_scholar_and_mentor,
    'rb_bonus_nothing_is_junk': draw_nothing_is_junk,
}

CONCEPTS = {
    'rb_bonus_efficient_movement': 'moving boot with restrained energy/motion trail',
    'rb_bonus_trauma_specialist': 'medical cross with bandage and accelerated recovery arc',
    'rb_bonus_pharmacy_eye': 'medicine bottle with inspection lens',
    'rb_bonus_reagent_recovery': 'laboratory flask with returning droplet/recovery arrow',
    'rb_bonus_bookworm': 'open book with duplicated readable-page motif',
    'rb_bonus_parts_salvager': 'vehicle gear/component recovered by wrench',
    'rb_bonus_power_saver': 'electrical plug and battery with reduced-consumption gauge',
    'rb_bonus_built_to_last': 'structural block wall with durability shield',
    'rb_bonus_heat_treatment': 'anvil with controlled heat/hardened edge motif',
    'rb_bonus_nothing_disposable': 'repair wrench with protected maximum-condition cycle',
    'rb_bonus_engineered_reliability': 'automated trap/turret with reinforced shield',
    'rb_bonus_ore_sense': 'pickaxe and ore nodes with through-rock scan rings',
    'rb_bonus_professional_logging': 'tree log cross-section with efficient axe',
    'rb_bonus_fire_resistant': 'flame behind a protective shield',
    'rb_bonus_combat_momentum': 'three rising combat chevrons with reload/handling motif',
    'rb_bonus_patrol_car_familiarity': 'police light bar with ammunition rounds',
    'rb_bonus_hunters_mark': 'animal silhouette inside observation reticle with tracks',
    'rb_bonus_calming_presence': 'wild animal head with open hand and calming waves',
    'rb_bonus_gunsmith_master_restoration': 'firearm with restored durability cycle',
    'rb_bonus_professional_cooking': 'cooking pot with sustained Energy pulse',
    'rb_bonus_tailor_master_restoration': 'wearable/armor with needle and restored durability cycle',
    'rb_bonus_rapid_cultivation': 'sprout with accelerated growth arrows',
    'rb_bonus_whole_animal': 'carcass-processing knife with meat/bone resource set',
    'rb_bonus_master_mixologist': 'prepared drink with clock and double-duration arcs',
    'rb_bonus_more_options': 'multiple reward cards with an added choice',
    'rb_bonus_scholar_and_mentor': 'open book with instructor/student learning arc',
    'rb_bonus_nothing_is_junk': 'scrap transformed into organized useful components',
}


def sha256(path: Path) -> str:
    h=hashlib.sha256()
    with path.open('rb') as f:
        for chunk in iter(lambda:f.read(1024*1024), b''):
            h.update(chunk)
    return h.hexdigest()


def normalize_source(im: Image.Image) -> Image.Image:
    # Keep a 10 px source margin (2.5 px at 64) while centering the alpha extent.
    alpha=im.getchannel('A')
    bbox=alpha.getbbox()
    if not bbox:
        return im
    x0,y0,x1,y1=bbox
    crop=im.crop(bbox)
    max_dim=SOURCE_SIZE-20
    scale=min(max_dim/crop.width,max_dim/crop.height,1.0)
    nw=max(1,int(round(crop.width*scale))); nh=max(1,int(round(crop.height*scale)))
    if (nw,nh)!=crop.size:
        crop=crop.resize((nw,nh),Image.Resampling.LANCZOS)
    out=Image.new('RGBA',(SOURCE_SIZE,SOURCE_SIZE),CLEAR)
    out.alpha_composite(crop,((SOURCE_SIZE-nw)//2,(SOURCE_SIZE-nh)//2))
    return out



def integrate_registry_and_settings(icon_keys: List[str]) -> None:
    text=REGISTRY_PATH.read_text(encoding='utf-8')
    text=text.replace('icon_asset_contract="pending_chunk_c"','icon_asset_contract="integrated_chunk_c"')
    text=text.replace('icon_state="pending_chunk_c"','icon_state="integrated_chunk_c"')
    REGISTRY_PATH.write_text(text,encoding='utf-8')
    settings=SETTINGS_PATH.read_text(encoding='utf-8')
    for key in icon_keys:
        if f'<sprite name="{key}"' not in settings:
            settings=settings.replace('</sprites>', f'  <sprite name="{key}" />\n</sprites>')
    SETTINGS_PATH.write_text(settings,encoding='utf-8')


def build_contact_sheet(paths: List[Path], keys: List[str], output: Path, display_size: int, cell_w: int, cell_h: int) -> None:
    cols=5
    rows=(len(keys)+cols-1)//cols
    sheet=Image.new('RGB',(cell_w*cols,cell_h*rows),(28,28,28))
    draw=ImageDraw.Draw(sheet)
    for i,(key,path) in enumerate(zip(keys,paths)):
        icon=Image.open(path).convert('RGBA').resize((display_size,display_size),Image.Resampling.LANCZOS)
        x=(i%cols)*cell_w; y=(i//cols)*cell_h
        tile=Image.new('RGBA',(display_size,display_size),(28,28,28,255)); tile.alpha_composite(icon)
        sheet.paste(tile.convert('RGB'),(x+(cell_w-display_size)//2,y+2))
        draw.text((x+4,y+display_size+7),key,fill='white')
    output.parent.mkdir(parents=True,exist_ok=True)
    sheet.save(output,quality=92)

def main() -> None:
    MASTER_DIR.mkdir(parents=True, exist_ok=True)
    ATLAS_DIR.mkdir(parents=True, exist_ok=True)
    registry=ET.parse(REGISTRY_PATH).getroot()
    bonuses=registry.findall('bonus')
    icon_keys=[b.get('icon_key','') for b in bonuses]
    missing=[k for k in icon_keys if k not in DRAWERS]
    extra=[k for k in DRAWERS if k not in icon_keys]
    if missing or extra:
        raise SystemExit(f'drawer coverage mismatch missing={missing} extra={extra}')

    rows=[]
    for b in bonuses:
        key=b.get('icon_key')
        im=Image.new('RGBA',(SOURCE_SIZE,SOURCE_SIZE),CLEAR)
        d=ImageDraw.Draw(im)
        DRAWERS[key](d)
        im=normalize_source(im)
        master=MASTER_DIR/(key+'.png')
        final=ATLAS_DIR/(key+'.png')
        im.save(master,optimize=True)
        small=im.resize((FINAL_SIZE,FINAL_SIZE),Image.Resampling.LANCZOS)
        small.save(final,optimize=True)
        rows.append({
            'bonus_id':b.get('id',''),
            'background_id':b.get('background_id',''),
            'sprite_key':key,
            'source_method':'programmatic_vector_silhouette',
            'source_prompt_concept':CONCEPTS[key],
            'source_master_path':master.relative_to(ROOT).as_posix(),
            'source_sha256':sha256(master),
            'atlas_path':final.relative_to(ROOT).as_posix(),
            'atlas_sha256':sha256(final),
            'source_size':'256x256 RGBA',
            'atlas_size':'64x64 RGBA',
            'ui_surfaces':'Survivor Creator Background detail; Survivor Creator review; pre-spawn Survivor Profile review; Character Origin; Progression Explorer Background detail',
        })
    integrate_registry_and_settings(icon_keys)
    build_contact_sheet([MASTER_DIR/(k+'.png') for k in icon_keys],icon_keys,SOURCE_SHEET,128,220,175)
    build_contact_sheet([ATLAS_DIR/(k+'.png') for k in icon_keys],icon_keys,FINAL_SHEET,96,180,130)
    MANIFEST_PATH.parent.mkdir(parents=True, exist_ok=True)
    fields=list(rows[0].keys())
    with MANIFEST_PATH.open('w',encoding='utf-8',newline='') as f:
        w=csv.DictWriter(f,fieldnames=fields)
        w.writeheader(); w.writerows(rows)
    print(f'generated={len(rows)} manifest={MANIFEST_PATH.relative_to(ROOT)}')

if __name__=='__main__':
    main()
