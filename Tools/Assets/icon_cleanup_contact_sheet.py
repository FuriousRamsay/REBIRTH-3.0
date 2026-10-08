"""Build a review sheet from installed icons without modifying the assets."""
from pathlib import Path
from PIL import Image, ImageDraw
import json, math
root=Path(__file__).resolve().parents[2]; doc=root/'_Documentation/Icon_Restoration'
for kind,file in [('support','support_icon_installation_20260930.json'),('cassettes','generated_cassette_manifest_20260930.json')]:
 rows=json.loads((doc/file).read_text()); out=Image.new('RGB',(1000,math.ceil(len(rows)/5)*190),(38,38,43)); draw=ImageDraw.Draw(out)
 for i,r in enumerate(rows):
  x=(i%5)*200;y=(i//5)*190
  path=root/'UIAtlases/ItemIconAtlas'/((r.get('icon') or r['id'])+'.png')
  im=Image.open(path).convert('RGBA');out.paste(im,(x+20,y),im)
  label=r.get('title') or r['id'].replace('rebirth','').replace('Support','').replace('Gear','')
  draw.text((x+5,y+163),label,fill='white')
 out.save(doc/(kind+'_review_20260930.png'))
