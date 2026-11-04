"""Fail closed on stale geometry, shaders, masks, evidence or wrap exports."""
from pathlib import Path
import argparse,hashlib,json,sys
from PIL import Image,ImageChops

def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def require(condition,message):
    if not condition:raise ValueError(message)

def verify(templates,geometry=None,art=None,wrap_id=None):
    standard=json.loads((templates/'STANDARD.json').read_text())
    require(standard['version']=='native-body-2026-10-31-coverage3','Obsolete template version')
    for name,digest in standard['files'].items():
        p=(templates/name).resolve()
        require(p.is_relative_to(templates.resolve()),'Unsafe contract path')
        require(p.is_file() and sha(p)==digest,'Changed contract file: '+name)
    if geometry:require(sha(geometry)==standard['geometrySHA256'],'Native vehicle geometry changed')
    proof=json.loads((templates/'NativeSurfaceCoverage.json').read_text())
    require(proof['status']=='PASS' and proof['version']==standard['version'],'Coverage proof not PASS')
    require(len(proof['views'])>=98 and len({v['name'] for v in proof['views']})==len(proof['views']),'Incomplete native camera sweep')
    require(set(proof['requiredViews'])=={v['name'] for v in proof['views']} and len(proof['requiredViews'])==len(proof['views']),'Missing required native view')
    require(all(v['uncoveredPixels']==0 for v in proof['views']),'Uncovered native paint')
    require(proof['geometrySHA256']==standard['geometrySHA256'],'Proof geometry mismatch')
    require(proof['compiledShaderFamilies']==4,'Unverified shader family')
    require(len(proof['maskFiles'])==12 and len({f['target'] for f in proof['maskFiles']})==12,'Incomplete mask evidence')
    for f in proof['maskFiles']:require(sha(templates/f['target'])==f['sha256'],'Mask changed after native verification: '+f['target'])
    require(len(proof['mappingFiles'])==4 and len({f['target'] for f in proof['mappingFiles']})==4,'Incomplete shader evidence')
    for f in proof['mappingFiles']:require(sha(templates/'mapping'/f['target'])==f['sha256'],'Mapper changed after native verification: '+f['target'])
    layout=json.loads((templates/'continuous-layout.json').read_text())
    require(len(layout['canvases'])==12,'Expected twelve native surfaces')
    suffixes={'hood':'HoodMaster','left-continuous':'SceneLayers','right-continuous':'BotanicalLayers','camper-front-continuous':'GardenCorner','camper-rear-fixed-continuous':'MusicCorner','rear-door':'LeftBandSharp','camper-roof':'RightBandSharp','cab-roof':'RearSharp','truck-front-face':'FrontSharp','cab-back':'RearDoor','pickup-tailgate':'RearLeft','pickup-bed-interior-unfolded':'RearRight'}
    for panel in layout['canvases']:
        name=panel['id'];mask=Image.open(templates/(name+'-paintable.png')).convert('RGBA')
        require(mask.size==(panel['w'],panel['h']),'Metadata dimensions disagree: '+name)
        alpha=mask.getchannel('A')
        require(set(alpha.getdata())<={0,255},'Nonbinary geometry mask: '+name)
        if art:
            require(wrap_id is not None,'--wrap-id required with artwork')
            file=art/(wrap_id+suffixes[name]+'.png');image=Image.open(file).convert('RGBA')
            require(image.size==mask.size,'Incorrect artwork dimensions: '+name)
            require(ImageChops.difference(image.getchannel('A'),alpha).getbbox() is None,'Artwork cutout disagrees: '+name)
        else:image=mask
        empty=image.getchannel('A').point(lambda a:255 if a==0 else 0)
        for channel in image.split()[:3]:require(ImageChops.multiply(channel,empty).getbbox() is None,'Transparent RGB contamination: '+name)
    return standard,proof

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--templates',type=Path,required=True);p.add_argument('--geometry',type=Path);p.add_argument('--art-directory',type=Path);p.add_argument('--wrap-id');p.add_argument('--quality-receipt',type=Path)
    a=p.parse_args()
    try:
        standard,proof=verify(a.templates,a.geometry,a.art_directory,a.wrap_id)
        if a.quality_receipt:
            require(a.art_directory and a.wrap_id,'Quality sealing requires verified artwork')
            receipt=json.loads(a.quality_receipt.read_text());require(receipt['id']==a.wrap_id and receipt['status']=='PASS','Invalid existing quality receipt')
            for asset in receipt['assets']:require(sha(a.art_directory/asset['target'])==asset['sha256'],'Artwork changed after quality inspection')
            receipt.update(surfaceContractVersion=standard['version'],geometrySHA256=standard['geometrySHA256'],mappingSHA256=standard['files']['mapping/RebirthCamperReferenceWrapCollection.shader'],nativeCoverageSHA256=sha(a.templates/'NativeSurfaceCoverage.json'))
            a.quality_receipt.write_text(json.dumps(receipt,indent=2))
        print('PASS: twelve masks, immutable contract and zero-gap native sweep'+('; artwork verified' if a.art_directory else ''))
    except Exception as e:
        print('FAIL: '+str(e),file=sys.stderr);sys.exit(1)
