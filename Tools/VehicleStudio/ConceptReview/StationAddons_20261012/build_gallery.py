from pathlib import Path
import json,html,re
from PIL import Image
root=Path(__file__).parent
groups=[('CompactEnergy','Compact energy concepts'),('PencilFreeInterior','Interior — no pencils'),('UltraCompactInterior','Compact interior originals'),('UltraCompactRoof','Compact roof'),('IndividualSet','Earlier individual concepts'),('ApprovedVehicleAddons','Vehicle addons'),('', 'Earlier forge concept')]
items=[]
for folder,label in groups:
    directory=root/folder
    manifest=directory/'manifest.json'
    names={f['filename']:f['station'] for f in json.loads(manifest.read_text()).get('files',[])} if manifest.exists() else {}
    for p in sorted(directory.glob('*.png')):
        with Image.open(p) as im: im.verify()
        name=names.get(p.name,re.sub(r'^\d+-','',p.stem).replace('-',' ').title())
        items.append({'name':name,'group':label,'src':p.relative_to(root).as_posix()})
cards=''.join(f'<button class="card" data-group="{html.escape(i["group"])}" data-name="{html.escape(i["name"].lower())}" onclick="show({n})"><img loading="lazy" src="{html.escape(i["src"])}" alt="{html.escape(i["name"])}"><span>{html.escape(i["name"])}</span><small>{html.escape(i["group"])}</small></button>' for n,i in enumerate(items))
options=''.join(f'<option>{html.escape(label)}</option>' for _,label in groups)
page='''<!doctype html><html lang="en"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>REBIRTH station concept gallery</title><style>
*{box-sizing:border-box}body{margin:0;background:#171b20;color:#edf1f4;font:16px system-ui}header{padding:28px 4vw;background:#20262d}h1{margin:0 0 8px;font-size:30px}p{color:#b9c5cf}nav{display:flex;gap:12px;flex-wrap:wrap}input,select{padding:12px;background:#101419;color:white;border:1px solid #56616c;border-radius:7px}input{width:280px}main{padding:24px 4vw;display:grid;grid-template-columns:repeat(auto-fit,minmax(330px,1fr));gap:22px}.card{padding:0 0 16px;text-align:left;background:#252c34;color:white;border:1px solid #414c58;border-radius:10px;overflow:hidden;cursor:pointer}.card:hover{border-color:#e8b46e}.card img{display:block;width:100%;aspect-ratio:4/3;object-fit:contain;background:#13171b}.card span,.card small{display:block;margin:12px 16px 0}.card small{color:#b4c1cd}dialog{width:96vw;max-width:1800px;height:95vh;background:#11161c;color:white;border:1px solid #56616c;padding:16px}dialog::backdrop{background:#000d}dialog img{display:block;width:100%;height:calc(100% - 70px);object-fit:contain}.bar{display:flex;gap:16px;align-items:center;height:60px}.bar strong{flex:1}.bar button,.bar a{background:#303c48;color:white;padding:10px;border:0;border-radius:5px}#count{color:#e8b46e}
</style><header><h1>REBIRTH station concept gallery</h1><p>All saved generated station concepts and revisions, plus the plow and armor concepts. These are concept images; placement and scale in earlier images are not validated. Click an image for a full-size view.</p><nav><input id="search" placeholder="Search stations" aria-label="Search stations"><select id="group" aria-label="Filter collection"><option>All collections</option>OPTIONS</select><span id="count"></span></nav></header><main>CARDS</main><dialog id="viewer"><div class="bar"><button onclick="step(-1)">←</button><strong id="caption"></strong><a id="original" target="_blank">Open original</a><button onclick="step(1)">→</button><button onclick="viewer.close()">Close</button></div><img id="large"></dialog><script>
const images=IMAGES;let current=0;const viewer=document.querySelector('#viewer');
function show(n){current=n;large.src=images[n].src;caption.textContent=images[n].name+' · '+images[n].group;original.href=images[n].src;if(!viewer.open)viewer.showModal()}
function step(d){show((current+d+images.length)%images.length)}
function filter(){let count=0;document.querySelectorAll('.card').forEach(c=>{let ok=c.dataset.name.includes(search.value.toLowerCase())&&(group.value==='All collections'||c.dataset.group===group.value);c.hidden=!ok;c.style.display=ok?'':'none';if(ok)count++});document.querySelector('#count').textContent=count+' images'}
search.oninput=filter;group.onchange=filter;document.onkeydown=e=>{if(viewer.open&&e.key==='ArrowRight')step(1);if(viewer.open&&e.key==='ArrowLeft')step(-1)};filter();
</script></html>'''
page=page.replace('OPTIONS',options).replace('CARDS',cards).replace('IMAGES',json.dumps(items))
(root/'gallery.html').write_text(page,encoding='utf-8')
(root/'gallery-index.json').write_text(json.dumps(items,indent=2),encoding='utf-8')
print(json.dumps({'images':len(items),'gallery':str(root/'gallery.html'),'allImagesVerified':True}))

