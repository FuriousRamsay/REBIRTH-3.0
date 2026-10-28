"""Guarded full-collection delivery. Never installs or modifies production."""
import argparse
import datetime
import hashlib
import json
import re
import shutil
import zipfile
from pathlib import Path
from validate_collection import validate

ROOT=Path(__file__).resolve().parents[3]
HERE=Path(__file__).resolve().parent
NATIVE_SHA='4184efcb37ae3a55c6691c2749a213953e465de7d3268c22c5f1f48fc8df5290'


def sha(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream,'sha256').hexdigest()


def main():
    parser=argparse.ArgumentParser()
    parser.add_argument('--build-label',required=True)
    parser.add_argument('--review-label',action='append',required=True)
    args=parser.parse_args()
    inventory=validate()
    assert inventory['allRequiredFilesPresent'] and inventory['allViewsReviewed']==200,'Assets or explicit visual reviews incomplete'
    guide=(ROOT/'_Documentation/CamperWrapArt/COLLECTION_200_INTEGRATION.md').read_text(encoding='utf-8-sig')
    assert 'COMPLETION VERDICT: COMPLETE' in guide and 'DRAFT:' not in guide,'Full design-conformance confirmation missing'
    catalog=json.loads((ROOT/'Resources/CamperWraps/catalog.json').read_text())
    expected={row['id'] for row in catalog['wraps']}
    build_log=(HERE/('_Build-'+args.build_label+'.log')).read_text(encoding='utf-8-sig')
    assert 'WRAP_COLLECTION_BUNDLE_PASS assets=204' in build_log
    assert 'WRAP_COLLECTION_ICONS_RENDERED' in build_log
    assert 'Application will terminate with return code 0' in build_log
    rendered=set()
    for label in args.review_label:
        log=(HERE/('_Build-'+label+'.log')).read_text(encoding='utf-8-sig')
        assert not re.search(r'Exception:|Shader error|error CS|\[REBIRTH Camper Wraps\]',log),label
        assert 'Application will terminate with return code 0' in log,label
        rendered.update(re.findall(r'WRAP_MESH_REVIEW_RENDERED (\w+) paintMaterials=23',log))
    assert expected<=rendered,'Native renderer coverage incomplete'
    receipt=json.loads((ROOT/'_Documentation/CamperWrapArt/NATIVE_COMPILE_RECEIPT_20261027.json').read_text())
    assert receipt['exitCode']==0
    for name,digest in receipt['sources'].items():assert sha(ROOT/name)==digest,name
    checks=json.loads((ROOT/'_Documentation/CamperWrapArt/STATIC_CHECKS_20261027.json').read_text(encoding='utf-8-sig'))
    assert checks['selectorChecks']==810 and checks['legacyRendererChecks']==16
    assert checks['selectorResult']==checks['legacyResult']=='PASS'
    for name,digest in checks['sources'].items():assert sha(ROOT/name)==digest,name
    native=Path('C:/Program Files (x86)/Steam/steamapps/common/7 Days To Die/Mods/zzz_REBIRTH__3_0/Resources/rebirth_camper80s.unity3d')
    assert sha(native)==NATIVE_SHA,'Installed native vehicle bundle changed; coordinate baseline'
    stamp=datetime.datetime.now(datetime.timezone.utc).strftime('%Y%m%dT%H%M%SZ')
    folder=HERE/'_Deliveries'/('CamperWrapCollection200_'+stamp)
    folder.mkdir(parents=True)
    copied=[]
    def copy(relative,source=None):
        relative=Path(relative);target=folder/relative
        target.parent.mkdir(parents=True,exist_ok=True)
        shutil.copyfile(source or ROOT/relative,target);copied.append(relative)
    for name in ['RebirthCamperPattern','RebirthCamperPatternAppearance','RebirthCamperWrapAppearance','RebirthCamperWrapCatalog']:
        copy('Scripts/Vehicles/Camper/'+name+'.cs')
    for name in ['camper_items.xml','camper_pattern_item_modifiers.xml','camper_wrap_collection_item_modifiers.xml']:
        copy('Config/_Vehicles/'+name)
    for row in catalog['wraps']:row['status']='ready-for-lead-integration; in-game validation not performed'
    relative=Path('Resources/CamperWraps/catalog.json')
    (folder/relative).parent.mkdir(parents=True,exist_ok=True)
    (folder/relative).write_text(json.dumps(catalog,indent=2)+'\n',encoding='utf-8');copied.append(relative)
    for row in catalog['wraps']:
        copy(row['artwork']);copy('UIAtlases/ItemIconAtlas/'+row['item']+'.png')
    # Historical prompt receipts are supporting records, including later layout
    # corrections. All 200 final artworks are independently hashed by visual QA.
    for path in (HERE/'Receipts').glob('*.json'):
        copy('Tools/VehicleStudio/WrapCollection/Receipts/'+path.name)
    copy('Resources/rebirth_camper_wraps.unity3d',HERE/'_Build/rebirth_camper_wraps.unity3d')
    for path in HERE.iterdir():
        if path.suffix in ['.py','.ps1','.csv'] or path.name=='.gitignore':
            copy('Tools/VehicleStudio/WrapCollection/'+path.name)
    for path in (HERE/'Unity').iterdir():
        if path.suffix in ['.cs','.shader']:copy('Tools/VehicleStudio/WrapCollection/Unity/'+path.name)
    for name in ['Program.cs','SelectorFixture.csproj']:copy('Tools/VehicleStudio/WrapCollection/SelectorFixture/'+name)
    for name in ['Program.cs','PatternTests.csproj']:copy('Tools/CamperPatternTests/'+name)
    copy('Tools/GameBridge/tests/camper_wrap_items_available.json')
    for name in ['NATIVE_COMPILE_RECEIPT_20261027.json','STATIC_CHECKS_20261027.json','visual-reviews.json','collection-inventory.json','COLLECTION_200_INTEGRATION.md']:
        copy('_Documentation/CamperWrapArt/'+name)
    for label in args.review_label:
        for path in (HERE/('_Review-'+label)).glob('contact-*'):
            copy('_Documentation/CamperWrapArt/CollectionReview/'+label+'/'+path.name,path)
    proof={'scope':'Offline installed-model rendering; not in-game acceptance','editor':'2022.3.62f2','bundleTarget':'StandaloneWindows64',
           'vehicleBundleSHA256Preserved':NATIVE_SHA,'collectionBundleSHA256':sha(HERE/'_Build/rebirth_camper_wraps.unity3d'),
           'bundleAssetCount':204,'rendererAssertionIds':sorted(rendered&expected),'paintMaterialsPerWrap':23,
           'visualReviewCount':200,'capturedAndInspectedViews':1000,'reviewLabels':args.review_label,
           'assertions':['Renderer/indexed property blocks preserved','Stable selections retain clone identity','Original material references restored','Shared texture leases retained for second camper','Unused textures released','World reset rebinds textures','Legacy Disco resumes'],
           'nativeEditorLimitation':'Existing installed-prefab mesh-data assertion is documented; explicit checks and captures completed.',
           'inGameValidation':'Not performed','otherClientPlatforms':'Not qualified'}
    relative=Path('_Documentation/CamperWrapArt/COLLECTION_200_VALIDATION.json')
    (folder/relative).write_text(json.dumps(proof,indent=2)+'\n',encoding='utf-8');copied.append(relative)
    copy('_Documentation/CamperWrapArt/CollectionReview/icon-provenance.tsv',HERE/'_Build/icon-provenance.tsv')
    copy('_Documentation/CamperWrapArt/CollectionReview/assets.txt',HERE/'_Build/assets.txt')
    baseline=ROOT/'_Documentation/CamperWrapArt/Baseline'
    entries=[{'path':path.as_posix(),'baselineSHA256':sha(baseline/path) if (baseline/path).is_file() else None,'proposedSHA256':sha(folder/path)} for path in copied]
    manifest={'delivery':'200 camper wraps and native cosmetic modifiers','vehicleBundleSHA256Preserved':NATIVE_SHA,'bundleTarget':'StandaloneWindows64','inGameValidation':'Not performed','files':entries}
    relative=Path('_Documentation/CamperWrapArt/COLLECTION_200_MANIFEST.json')
    (folder/relative).write_text(json.dumps(manifest,indent=2)+'\n',encoding='utf-8');copied.append(relative)
    archive=folder.with_suffix('.zip')
    with zipfile.ZipFile(archive,'w',compression=zipfile.ZIP_DEFLATED,compresslevel=6) as bundle:
        for path in copied:bundle.write(folder/path,path.as_posix())
    with zipfile.ZipFile(archive) as bundle:
        assert set(bundle.namelist())=={path.as_posix() for path in copied}
        for row in entries:
            with bundle.open(row['path']) as stream:
                assert hashlib.file_digest(stream,'sha256').hexdigest()==row['proposedSHA256'],row['path']
    result={'archive':str(archive),'archiveSHA256':sha(archive),'files':len(copied),'manifest':str(folder/relative)}
    (HERE/'_Deliveries/latest-collection-200.json').write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8')
    print(json.dumps(result,indent=2))


if __name__=='__main__':main()
