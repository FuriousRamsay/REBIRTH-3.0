"""Conformance inventory. Never turns missing art into a passing 200-wrap claim."""
import argparse
import csv
import hashlib
import json
from pathlib import Path
from xml.etree import ElementTree as ET
from PIL import Image

ROOT=Path(__file__).resolve().parents[3]


def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()


def validate():
    catalog=json.loads((ROOT/'Resources/CamperWraps/catalog.json').read_text(encoding='utf-8'))
    rows=catalog['wraps']
    assert catalog['count']==len(rows)==200
    assert len({r['id'] for r in rows})==200
    mods=ET.parse(ROOT/'Config/_Vehicles/camper_wrap_collection_item_modifiers.xml').getroot().findall('./append/item_modifier')
    assert len(mods)==200
    by_name={m.attrib['name']:m for m in mods}
    with (ROOT/'Config/Localization.csv').open(encoding='utf-8-sig',newline='') as f:
        all_loc=list(csv.reader(f))
    keys=[r[0] for r in all_loc[1:] if r]
    for row in rows:
        assert keys.count(row['item'])==1 and keys.count(row['item']+'Desc')==1,row['id']
        assert by_name[row['item']].attrib['installable_tags']=='rebirthCamperWrapCollection'
        props={p.attrib['name']:p.attrib['value'] for p in by_name[row['item']].findall('property')}
        assert props['CustomIcon']==row['item'] and props['RebirthCamperWrapId']==row['id']
    baseline=ROOT/'_Documentation/CamperWrapArt/Baseline/Config/Localization.csv'
    assert (ROOT/'Config/Localization.csv').read_bytes().startswith(baseline.read_bytes()),'Existing cumulative localization changed'
    result=[]
    provenance={}
    review_file=ROOT/'_Documentation/CamperWrapArt/visual-reviews.json'
    reviews={}
    if review_file.is_file():
        reviews={r['id']:r for r in json.loads(review_file.read_text(encoding='utf-8-sig'))['wraps']}
    source_receipt=ROOT/'Tools/VehicleStudio/WrapCollection/_Build/icon-provenance.tsv'
    if source_receipt.is_file():
        with source_receipt.open(encoding='utf-8-sig',newline='') as f:
            provenance={r['id']:r for r in csv.DictReader(f,delimiter='\t')}
    for row in rows:
        art=ROOT/row['artwork'];icon=ROOT/'UIAtlases/ItemIconAtlas'/ (row['item']+'.png')
        record={'id':row['id'],'name':row['displayName'],'artwork':art.is_file(),'icon':icon.is_file(),'visualReview':'not-recorded'}
        if art.is_file():
            with Image.open(art) as image:record.update(artSize=list(image.size),squareAtlas=image.width==image.height)
            record['artSHA256']=sha(art)
            reviewed=reviews.get(row['id'],{})
            if reviewed.get('artSHA256','').lower()==record['artSHA256']:
                record['reviewedViews']=reviewed.get('views',[])
                record['visualReview']='PASS' if set(record['reviewedViews'])=={'quarter','side','right','rear','hood'} and reviewed.get('verdict')=='PASS' else 'partial'
        if icon.is_file():
            with Image.open(icon) as image:
                alpha=image.getchannel('A') if image.mode=='RGBA' else None
                box=alpha.getbbox() if alpha else None
                record['iconSize']=list(image.size);record['iconAlphaBounds']=list(box) if box else None
                record['iconHasClearMargin']=bool(box and min(box[0],box[1],image.width-box[2],image.height-box[3])>=6)
            record['iconSHA256']=sha(icon)
            stamp=provenance.get(row['id'],{})
            record['iconSourceMatchesArtwork']=(stamp.get('artSHA256','').lower()==record.get('artSHA256') and stamp.get('iconSHA256','').lower()==record['iconSHA256'])
        result.append(record)
    existing=[r['artSHA256'] for r in result if 'artSHA256' in r]
    assert len(set(existing))==len(existing),'Identical artwork files counted twice'
    all_ready=all(r['artwork'] and r['icon'] and r.get('squareAtlas') and r.get('iconHasClearMargin') and r.get('iconSourceMatchesArtwork') for r in result)
    return {'required':200,'itemDefinitions':len(mods),'localizedNames':200,'localizedDescriptions':200,
            'artworkFiles':sum(r['artwork'] for r in result),'iconFiles':sum(r['icon'] for r in result),
            'allViewsReviewed':sum(r['visualReview']=='PASS' for r in result),
            'allRequiredFilesPresent':all_ready,'assetReadiness':'READY' if all_ready and all(r['visualReview']=='PASS' for r in result) else 'NOT READY',
            'basis':'This inventory reports asset readiness. The integration document supplies the full design-conformance and delivery completion verdict.',
            'wraps':result}


def main():
    p=argparse.ArgumentParser();p.add_argument('--require-files-complete',action='store_true');a=p.parse_args()
    report=validate();out=ROOT/'_Documentation/CamperWrapArt/collection-inventory.json';out.write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
    print(json.dumps({k:v for k,v in report.items() if k!='wraps'},indent=2))
    if a.require_files_complete and not report['allRequiredFilesPresent']:raise SystemExit(1)


if __name__=='__main__':main()
