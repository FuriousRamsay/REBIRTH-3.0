"""Build the review gallery, source audit and install completed card exports."""
from pathlib import Path
from hashlib import sha256
from html import escape
import json,csv,shutil,sys
from PIL import Image
from rebuild_recipe_cards import OUT
ROOT=Path(__file__).resolve().parents[2]

def build(install=False):
 jobs=json.loads((OUT/'jobs.json').read_text());labels={}
 for p in [ROOT.parents[1]/'Data/Config/Localization.csv',ROOT/'Config/Localization.txt']:
  if p.exists():
   for row in csv.DictReader(p.open(encoding='utf-8-sig')):
    if row.get('Key') and row.get('english'):labels[row['Key']]=row['english']
 valid={r['key'] for r in json.loads((OUT/'main-art-bounds.json').read_text())}|{'C-meatstew','plaster-cast'}
 completed=[j for j in jobs if j['key'] in valid and (OUT/'Masters'/(j['icon']+'.png')).exists()]
 if install:
  assert len(completed)==135, 'All 135 cards must pass before installation'
  measured=json.loads((OUT/'main-art-bounds.json').read_text());assert len(measured)==133
  for row in measured:
   assert row['method'] in ['existing isolated master','isolated illustration']
   x,y,r,b=row['bounds'];w,h=row['canvas'];assert x>=w*.15 and y>=h*.12 and r<=w*.85 and b<=h*.64
 report=[];cards=[]
 for j in completed:
  icon=OUT/'Icons'/(j['icon']+'.png');master=OUT/'Masters'/(j['icon']+'.png')
  im=Image.open(icon);assert im.size==(160,160) and im.mode=='RGBA'
  assert im.getchannel('A').getextrema()[0]==0, j['key']+' lacks transparency'
  assert len(j['recipe'])==len(j['ingredients'])
  recipe=' + '.join(str(i['count'])+' '+labels.get(i['id'],i['id']) for i in j['recipe'])
  record=dict(key=j['key'],title=j['title'],kind=j['kind'],recipe=recipe,master=str(master),icon=str(icon),ingredients=[dict(id=i['id'],source=i['path'],sha256=sha256(Path(i['path']).read_bytes()).hexdigest(),tint=i.get('tint')) for i in j['ingredients']])
  report.append(record)
  cards.append('<article data-kind="'+j['kind']+'" data-search="'+escape((j['title']+' '+recipe).lower(),quote=True)+'"><a href="Masters/'+j['icon']+'.png"><img loading="lazy" src="Masters/'+j['icon']+'.png" alt="'+escape(j['title'],quote=True)+'"></a><h2>'+escape(j['title'])+'</h2><p>'+escape(recipe)+'</p><small>'+('Blue medical card' if j['kind']=='medical' else 'Green food / drink card')+'</small></article>')
  if install:
   target=ROOT/'UIAtlases/ItemIconAtlas'/(j['icon']+'.png')
   if target.exists() and not (OUT/'Backups'/target.name).exists():shutil.copy2(target,OUT/'Backups'/target.name)
   shutil.copy2(icon,target)
   if j['kind']=='food':
    old=Path(j['source']);backup=OUT/'Backups/Masters'/old.name;backup.parent.mkdir(exist_ok=True)
    if not backup.exists():shutil.copy2(old,backup)
    shutil.copy2(master,old)
 (OUT/'audit.json').write_text(json.dumps(report,indent=2))
 html='''<!doctype html><html><head><meta charset="utf-8"><title>Recipe cards</title><style>body{margin:0;background:#161b20;color:#eee;font:16px system-ui}header{padding:30px 5%;background:#212932;position:sticky;top:0;z-index:2}h1{margin:0 0 8px;font-size:28px}header p{color:#bfcad2;margin:8px 0 20px}input,select{padding:12px;border-radius:7px;border:1px solid #637180;background:#10161b;color:white;margin-right:12px}input{width:min(460px,60%)}main{padding:30px 5%;display:grid;grid-template-columns:repeat(auto-fill,minmax(235px,1fr));gap:25px}article{background:#252c32;padding:15px;border-radius:10px}img{width:100%;height:auto}h2{font-size:18px;margin:12px 0 6px}article p{font-size:14px;color:#d0d5da;line-height:1.6}small{color:#9aaebb}article[hidden]{display:none}</style></head><body><header><h1>Recipe cards — balanced artwork, original ingredient icons</h1><p>128 food and drink cards · 7 medical cards. Select a card to open the full-size image.</p><input id="search" placeholder="Find a dish or ingredient"><select id="kind"><option value="all">All cards</option><option value="food">Food and drink</option><option value="medical">Medical</option></select></header><main>'''+''.join(cards)+'''</main><script>function filter(){let q=document.getElementById('search').value.toLowerCase(),k=document.getElementById('kind').value;document.querySelectorAll('article').forEach(a=>a.hidden=!a.dataset.search.includes(q)||(k!=='all'&&a.dataset.kind!==k))}document.getElementById('search').oninput=filter;document.getElementById('kind').onchange=filter;</script></body></html>'''
 (OUT/'index.html').write_text(html,encoding='utf-8')
 print(json.dumps({'complete':len(completed),'installed':install}))
if __name__=='__main__':build('--install' in sys.argv)
