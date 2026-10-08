"""Static qualification; does not launch or interact with the game."""
from pathlib import Path
import json, hashlib, xml.etree.ElementTree as E
from PIL import Image
root=Path(__file__).resolve().parents[2]; doc=root/'_Documentation/Icon_Restoration'
support=json.loads((doc/'support_icon_installation_20260930.json').read_text())
cassettes=json.loads((doc/'generated_cassette_manifest_20260930.json').read_text())
retired=json.loads((doc/'retired_literature_20260930.json').read_text())
queue=json.loads((doc/'cassette_art_queue_20260930.json').read_text())
trees={n:E.parse(root/'Config/_Survivor'/n) for n in ('items.xml','loot.xml','traders.xml','literature_distribution.xml','audiobooks.xml','literature.xml')}
items={e.get('name'):e for e in trees['items.xml'].findall('.//item')}
checked=[]
for row in support+cassettes:
    key=row.get('icon',row['id']); path=root/'UIAtlases/ItemIconAtlas'/(key+'.png')
    im=Image.open(path); assert im.size==(160,160) and im.mode=='RGBA',path
    assert im.getchannel('A').getextrema()==(0,255),path
    props={p.get('name'):p.get('value') for p in items[row['id']].findall('property')}
    assert props.get('CustomIcon',row['id'])==key,(row['id'],props)
    checked.append({'id':row['id'],'path':str(path.relative_to(root)),'sha256':hashlib.sha256(path.read_bytes()).hexdigest()})
for row in retired:
    for key in ('item_id','source_literature_id'):
        id=row[key]; assert items[id].find("property[@name='CreativeMode']").get('value')=='None',id
        for name in ('loot.xml','traders.xml','literature_distribution.xml'):
            assert all(e.get('name',e.get('id'))!=id for e in trees[name].findall('.//item')),(id,name)
audio=trees['audiobooks.xml'].findall('.//audiobook')
active=[r.get('item_id') for r in audio if r.get('subtype') in ('primer','field_notes')]
withdrawn=[r.get('item_id') for r in audio if r.get('subtype') not in ('primer','field_notes')]
assert len(active)==84 and len(withdrawn)==68
for id in withdrawn:
    assert items[id].find("property[@name='CreativeMode']").get('value')=='None'
    for name in ('loot.xml','traders.xml','literature_distribution.xml'):
        assert all(e.get('name',e.get('id'))!=id for e in trees[name].findall('.//item')),(id,name)
for id in active:
    for name in ('loot.xml','traders.xml'):
        assert any(e.get('name')==id for e in trees[name].findall('.//item')),(id,name)
    assert items[id].find("property[@name='CreativeMode']") is None,id
assert items['rebirthElectricalServiceTool'].find("property[@class='Action0']") is not None
report={'support_icons':len(support),'cassette_icons':len(cassettes),'retired_pairs':len(retired),'active_cassettes':len(active),'withdrawn_cassettes':len(withdrawn),'checks':'XML parse, icon references, RGBA 160x160 alpha, retired acquisition absent, active acquisition retained, Action0 class','in_game_tested':False,'files':checked}
(doc/'validation_20260930.json').write_text(json.dumps(report,indent=2)+'\n')
print({k:v for k,v in report.items() if k!='files'})
