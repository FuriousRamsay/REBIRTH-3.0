"""Validate dedicated challenge artwork and build a small-size visual review sheet."""
from pathlib import Path
import json, hashlib, collections
from lxml import etree as E
from PIL import Image, ImageDraw

ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'_Documentation/ChallengeIcons'
OUT.mkdir(exist_ok=True)
art=json.loads(Path(__file__).with_name('challenge_icon_art.json').read_text())
nodes=E.parse(str(ROOT/'Config/_Rebirth/challenges.xml')).xpath('//challenge')
assert nodes, 'No authored challenges found'
names=[n.get('icon') for n in nodes]
assert len(set(names))==len(nodes), 'Repeated challenge icon references'
hashes={}; report=[]
sheet=Image.new('RGB',(960,((len(nodes)+5)//6)*130),(24,24,29)); draw=ImageDraw.Draw(sheet)
for i,n in enumerate(nodes):
    suffix=n.get('name').removeprefix('rebirthLesson')
    assert suffix in art['concepts']
    assert n.get('icon')=='rb_challenge_'+suffix
    p=ROOT/'UIAtlases/UIAtlas'/(n.get('icon')+'.png')
    with Image.open(p) as src:
        assert src.mode=='RGBA', (p,src.mode)
        im=src.copy()
    assert im.width==im.height and im.width>=128,(p,im.size)
    alpha=im.getchannel('A')
    assert alpha.getextrema()==(0,255),(p,'transparency')
    assert alpha.getbbox(),(p,'empty')
    digest=hashlib.sha256(im.tobytes()).hexdigest()
    assert digest not in hashes,(p,'duplicate of '+hashes.get(digest,''))
    hashes[digest]=p.name
    x=(i%6)*160;y=(i//6)*130
    small=im.resize((64,64),Image.Resampling.LANCZOS)
    sheet.paste(small,(x+48,y+5),small)
    label=suffix.replace('Skill_','').replace('Route','').replace('_',' ')
    draw.text((x+4,y+77),label[:25],fill=(235,230,220))
    report.append({'challenge':n.get('name'),'icon':n.get('icon'),'concept':art['concepts'][suffix], 'size':list(im.size), 'sha256':hashlib.sha256(p.read_bytes()).hexdigest()})
sheet.save(OUT/'challenge_icons_review.png')
(OUT/'icon_manifest.json').write_text(json.dumps(report,indent=2)+'\n')
print(f'PASS: {len(nodes)} unique references, {len(hashes)} unique RGBA images, square at least 128px, transparent and nonempty.')
