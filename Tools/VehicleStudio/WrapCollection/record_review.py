"""Record explicitly inspected native contact pages; never infers visual approval."""
import argparse
import hashlib
import json
from pathlib import Path

ROOT=Path(__file__).resolve().parents[3]
HERE=Path(__file__).resolve().parent


def main():
    parser=argparse.ArgumentParser()
    parser.add_argument('--label',required=True)
    parser.add_argument('--view',choices=['quarter','side','right','rear','hood'],required=True)
    parser.add_argument('--pages',required=True,help='Comma-separated page numbers actually inspected')
    parser.add_argument('--note',required=True)
    args=parser.parse_args()
    output=ROOT/'_Documentation/CamperWrapArt/visual-reviews.json'
    data=json.loads(output.read_text(encoding='utf-8-sig')) if output.exists() else {'wraps':[]}
    records={row['id']:row for row in data['wraps']}
    reviewed=0
    for page in args.pages.split(','):
        stem=HERE/('_Review-'+args.label)/('contact-'+args.view+'-'+page.zfill(2))
        assert stem.with_suffix('.png').is_file(),stem
        for key in stem.with_suffix('.txt').read_text(encoding='utf-8-sig').splitlines():
            art=ROOT/'Resources/CamperWraps/Artwork'/(key+'.png')
            digest=hashlib.sha256(art.read_bytes()).hexdigest()
            staged=HERE/'_UnityProject/Assets/REBIRTHCamperWrapCollection/Artwork'/art.name
            assert hashlib.sha256(staged.read_bytes()).hexdigest()==digest,key
            row=records.get(key,{})
            if row.get('artSHA256')!=digest:row={'id':key,'artSHA256':digest,'views':[],'evidence':[]}
            row['views']=sorted(set(row['views']+[args.view]))
            evidence={'label':args.label,'view':args.view,'page':page.zfill(2),'note':args.note}
            if evidence not in row['evidence']:row['evidence'].append(evidence)
            row['verdict']='PASS'
            records[key]=row;reviewed+=1
    data['wraps']=sorted(records.values(),key=lambda row:row['id'])
    output.write_text(json.dumps(data,indent=2)+'\n',encoding='utf-8')
    print(f'Recorded {args.view} review for {reviewed} artwork files; other views stay pending.')


if __name__=='__main__':main()
