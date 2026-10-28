"""Produce a guarded six-wrap integration milestone; never deploy production.

The 200-wrap objective remains incomplete. Only these six item definitions are
exposed in this package, so unfinished catalogue entries cannot appear in-game.
"""
import csv
import datetime
import hashlib
import json
import re
import shutil
import zipfile
from copy import deepcopy
from pathlib import Path
from xml.etree import ElementTree as ET
from validate_collection import validate

ROOT=Path(__file__).resolve().parents[3]
HERE=Path(__file__).resolve().parent
PRODUCTION=Path('C:/Program Files (x86)/Steam/steamapps/common/7 Days To Die/Mods/zzz_REBIRTH__3_0')
READY=['MidnightCartographer','TheHungryRoad','DeadZoneRunner','ShotgunMessiah','JensClinic','FuriousRamsaySignature']
LABEL='approved-six-v2'


def sha(path):
    h=hashlib.sha256()
    with path.open('rb') as f:
        for block in iter(lambda:f.read(1024*1024),b''):h.update(block)
    return h.hexdigest()


def main():
    inventory=validate();by_id={r['id']:r for r in inventory['wraps']}
    for key in READY:
        row=by_id[key]
        assert row['artwork'] and row['icon'] and row['squareAtlas'] and row['iconHasClearMargin'] and row['iconSourceMatchesArtwork'],row
    log=(HERE/('_Build-'+LABEL+'.log')).read_text(encoding='utf-8-sig')
    assert 'WRAP_COLLECTION_BUNDLE_PASS assets=10' in log
    assert 'WRAP_COLLECTION_ICONS_RENDERED' in log
    assert not re.search(r'Exception:|Shader error|error CS|\[REBIRTH Camper Wraps\]',log)
    for key in READY:assert 'WRAP_MESH_REVIEW_RENDERED '+key+' paintMaterials=23' in log,key
    receipt=json.loads((ROOT/'_Documentation/CamperWrapArt/NATIVE_COMPILE_RECEIPT_20261027.json').read_text())
    assert receipt['exitCode']==0
    for name,value in receipt['sources'].items():assert sha(ROOT/name)==value,name
    for name in ['RebirthCamperPatternAppearance','RebirthCamperWrapAppearance','RebirthCamperWrapCatalog']:
        assert sha(ROOT/'Scripts/Vehicles/Camper'/(name+'.cs'))==sha(HERE/'_UnityProject/Assets/Editor'/(name+'.cs')),name
    for path in (HERE/'Unity').glob('*.shader'):
        assert sha(path)==sha(HERE/'_UnityProject/Assets/REBIRTHCamperWrapCollection/Shaders'/path.name),path
    vehicle=PRODUCTION/'Resources/rebirth_camper80s.unity3d'
    vehicle_sha=sha(vehicle)
    assert vehicle_sha=='4184efcb37ae3a55c6691c2749a213953e465de7d3268c22c5f1f48fc8df5290'
    timestamp=datetime.datetime.now(datetime.timezone.utc).strftime('%Y%m%dT%H%M%SZ')
    folder=HERE/'_Deliveries'/('ApprovedSix_'+timestamp)
    folder.mkdir(parents=True)
    copied=[]

    def copy(relative,source=None):
        path=folder/relative;path.parent.mkdir(parents=True,exist_ok=True)
        shutil.copyfile(source or ROOT/relative,path);copied.append(Path(relative));return path

    # Only owned runtime changes; copied current baseline files are not shipped.
    for name in ['RebirthCamperPattern','RebirthCamperPatternAppearance','RebirthCamperWrapAppearance','RebirthCamperWrapCatalog']:
        copy('Scripts/Vehicles/Camper/'+name+'.cs')
    for name in ['camper_items.xml','camper_pattern_item_modifiers.xml']:
        copy('Config/_Vehicles/'+name)
    catalog=json.loads((ROOT/'Resources/CamperWraps/catalog.json').read_text())
    selected=[r for r in catalog['wraps'] if r['id'] in READY]
    for row in selected:row['status']='ready-for-lead-integration; game acceptance pending'
    all_xml=ET.parse(ROOT/'Config/_Vehicles/camper_wrap_collection_item_modifiers.xml').getroot()
    configs=ET.Element('configs');append=ET.SubElement(configs,'append',xpath='/item_modifiers')
    for node in all_xml.findall('./append/item_modifier'):
        if node.attrib['name'] in {r['item'] for r in selected}:append.append(deepcopy(node))
    assert len(append)==6
    rel=Path('Config/_Vehicles/camper_wrap_collection_item_modifiers.xml');path=folder/rel;path.parent.mkdir(parents=True,exist_ok=True);ET.indent(configs,space='  ');ET.ElementTree(configs).write(path,encoding='utf-8');copied.append(rel)
    rel=Path('Resources/CamperWraps/catalog.json');path=folder/rel;path.parent.mkdir(parents=True,exist_ok=True);path.write_text(json.dumps({'version':1,'count':6,'plannedCollectionTotal':200,'scope':'Approved six integration milestone; remaining designs intentionally not exposed.','wraps':selected},indent=2)+'\n',encoding='utf-8');copied.append(rel)
    for row in selected:
        copy(row['artwork']);copy('UIAtlases/ItemIconAtlas/'+row['item']+'.png')
    copy('Resources/rebirth_camper_wraps.unity3d',HERE/'_Build/rebirth_camper_wraps.unity3d')
    rel=Path('Tools/VehicleStudio/WrapCollection/localization-additions-approved-six.csv');path=folder/rel;path.parent.mkdir(parents=True,exist_ok=True)
    with path.open('w',encoding='utf-8',newline='') as f:
        w=csv.writer(f);w.writerow(['Key','english'])
        for row in selected:w.writerow([row['item'],row['displayName']]);w.writerow([row['item']+'Desc',row['description']])
    copied.append(rel)
    for name in ['.gitignore','prepare_shader.py','prepare_unity.py','run_unity.ps1','merge_localization.py','check_runtime_compile.py','prepare_game_scenario.py']:
        copy('Tools/VehicleStudio/WrapCollection/'+name)
    for path in (HERE/'Unity').iterdir():
        if path.suffix in ['.cs','.shader']:copy('Tools/VehicleStudio/WrapCollection/Unity/'+path.name)
    for name in ['Program.cs','SelectorFixture.csproj']:copy('Tools/VehicleStudio/WrapCollection/SelectorFixture/'+name)
    for name in ['Program.cs','PatternTests.csproj']:copy('Tools/CamperPatternTests/'+name)
    copy('Tools/GameBridge/tests/camper_wrap_items_available.json')
    for key in READY:
        for view in ['quarter','side','right','rear','hood']:
            copy('_Documentation/CamperWrapArt/ApprovedSixReview/'+key+'-'+view+'.png',HERE/('_Review-'+LABEL)/(key+'-'+view+'.png'))
    copy('_Documentation/CamperWrapArt/NATIVE_COMPILE_RECEIPT_20261027.json')
    proof={'scope':'Offline actual-installed-prefab rendering, not gameplay','vehicleBundleSHA256':vehicle_sha,'collectionBundleSHA256':sha(HERE/'_Build/rebirth_camper_wraps.unity3d'),
           'editor':'2022.3.62f2','bundleTarget':'StandaloneWindows64','assets':(HERE/'_Build/assets.txt').read_text().splitlines(),
           'readyIds':READY,'linkedRuntimeAssertions':['23 paint materials selected per wrap','Stable selections keep clone identities','Renderer and indexed property-block sentinel survives native-style overwrite','Original material references restored on removal','Two campers share one texture; removing one retains other lease','Unused textures unloaded','World-session reset rebinds artwork','Legacy Disco resumes after custom wrap'],
           'nativeGameValidation':'Not performed','baselineEditorMeshAssertion':'Installed prefab produced meshVD.GetDataPtr()==NULL assertion during editor load; all explicit renderer assertions and image captures completed.'}
    rel=Path('_Documentation/CamperWrapArt/APPROVED_SIX_VALIDATION.json');(folder/rel).write_text(json.dumps(proof,indent=2)+'\n',encoding='utf-8');copied.append(rel)
    # Every modified existing file has a captured production baseline hash.
    baseline=ROOT/'_Documentation/CamperWrapArt/Baseline'
    entries=[]
    for rel in copied:
        old=baseline/rel
        entries.append({'path':rel.as_posix(),'baselineSHA256':sha(old) if old.is_file() else None,'proposedSHA256':sha(folder/rel)})
    guide=f'''# Approved six camper wraps — integration milestone

Source: current cumulative REBIRTH 3.0 production pipeline captured in the isolated camper-wrap-art worktree (base HEAD23dbd14c; exact baselines in manifest). Target: current REBIRTH cumulative project and installed native game assemblies. Delivery: changed relevant source/assets only; no full cumulative project, production DLL, tool runtimes or game logs.

This package exposes SIX approved items only: {', '.join(r['displayName'] for r in selected)}. The complete user goal remains 200 wraps. At packaging time {inventory['artworkFiles']} artwork files and {inventory['iconFiles']} icon files exist in the authoring worktree; the remaining catalogue is not qualified or included as game items here.

## Integration

Check every non-null baseline hash before copying corresponding source/XML. New files must not overwrite unexpected existing files. Preserve the installed Resources/rebirth_camper80s.unity3d byte-for-byte (SHA256{vehicle_sha}). The new Resources/rebirth_camper_wraps.unity3d contains four compatible shaders and six artwork textures only; no vehicle prefab, geometry, physics or station changes.

Merge the twelve rows in Tools/VehicleStudio/WrapCollection/localization-additions-approved-six.csv into CURRENT Config/Localization.csv. Do not replace cumulative localization with the authoring worktree's400-row draft. merge_localization.py is read-only by default; the lead supplies --target, --additions and --apply after review. It appends without reserializing existing CSV and rejects conflicting keys.

The existing camper_pattern_item_modifiers.xml includes the new sibling collection file after legacy Disco. New mods use native dye/cosmetic classification and camper-only eligibility. Buggy/bike Disco support remains unchanged. The C# registry reserves all200 planned IDs, but only these six XML definitions are exposed. Native ItemValue cosmetic modifications remain the only save/network state.

Only the lead builds/installs the production DLL and runs game acceptance. Root RebirthUtils.csproj already excludes Tools sources. Source compile checker produces an isolated check library only. SelectorFixture supports --expected-count 6 for this milestone (default 200 in the full worktree). The shader bundle is Windows64; dedicated-server code never loads it. Other client platforms are not qualified.

## Validation and limits

Owned four-file runtime compilation against actual installed assemblies: PASS. Full-worktree selector/XML fixture:810PASS. Legacy Disco/None fixture:16PASS. Isolated Unity bundle build/reopen:10assets PASS. Production appearance sources linked into the Unity fixture against actual native renderers/materials: six selection/removal/multi-camper/cache/world-reset/legacy-restoration cycles PASS;23paint materials each. Thirty native mesh review images are included. These are editor renders, not screenshots from a running game. Source-backed vinyl-roll icons have transparent margins and match the supplied artwork hashes.

Main art generation used built-in image_gen; per-wrap motifs are recorded in the supplied catalogue. Approved styles: navy/copper cartography; ivory/black/crimson skeletal horror; olive/yellow zombie quarantine; charcoal/cream/oxblood Shotgun Messiah; ivory/teal/coral Jen's Clinic; turquoise/violet FuriousRamsay with supplied eye logo. Artwork uses a square atlas: side mural top half, hood bottom-left, camper strips bottom-right. The lower-right margin supplies the base paint. Hood/front/opposite-side coordinates are corrected for readable lettering. FuriousRamsay's front-panel layout keeps the wordmark ahead of the wheel on either side.

Game Bridge availability smoke and observation-driven native install/remove scenario generator are supplied but NOT RUN. Lead acceptance should check all six through actual cosmetic installation, dye replacement/removal, Camp/Travel, both sides/hood/door/LODs, pickup/re-place, save/reload and remote client replication. No loot/trader distribution redesign is included; item economics match existing Disco.

## Requirement traceability

| Requirement | Implementation | Impact | Validation | Result |
|---|---|---|---|---|
|Six approved designs with hood/side/camper strips|Six artwork PNGs, four derived shaders, collection bundle|Appearance only|Actual installed mesh views and linked production renderer path|PASS for this milestone|
|Matching native cosmetic items|Three scoped XML files, selector/catalog|Native cosmetic slot; no new state format|Definitions/include resolution and selector fixture|PASS for six|
|Localized names and descriptions|Twelve additive CSV rows|Native item text|Unique keys, existing bytes preserved|PASS for six|
|Matching icons|Six native vinyl-roll renders|Existing ItemIconAtlas|Transparency margins and artwork/icon SHA provenance|PASS for six|
|Preserve current camper and legacy behavior|Original vehicle bundle untouched; original materials restored|No geometry/save/net changes|Baseline bundle hash,16legacy checks, linked native material assertions|PASS statically/editor|
|Full200collection|Authoring worktree continues separately|All200names/definitions drafted|Inventory records missing/unqualified artwork/icons|FAIL — not finished|

REBIRTH FEATURE COMPLETION CONFIRMATION

Feature:200-wrap collection, approved-six integration milestone
Source cumulative project: current production baseline, exact file hashes in manifest
Target project/version: REBIRTH3.0 cumulative project / actual installed native assemblies
Design requirements reviewed: PASS
Relevant existing documentation reviewed: PASS
Accepted architecture preserved: PASS
Code requirements: PASS for shared pipeline and six-item milestone
XML requirements: PASS for six exposed items
UI requirements: PASS — existing native cosmetic UI and icons
Localization requirements: PASS for twelve supplied milestone rows
Images/assets requirements: FAIL for full200objective; six milestone assets provided
Serialization/persistence requirements: PASS — native existing selector retained; game acceptance not performed
Networking/synchronization requirements: PASS — native cosmetic replication retained; game acceptance not performed
Documentation requirements: PASS
Packaging requirements: PASS for this changed-files milestone; full200delivery pending
Compilation: Performed (scoped native-assembly source check and Unity asset build; no production DLL build)
In-game validation: Not performed
COMPLETION VERDICT: NOT COMPLETE
Basis for verdict: The full200-wrap goal is still active. Six approved wraps are supplied for lead integration; other artwork/icons and final collection review/delivery remain unfinished.
'''
    rel=Path('_Documentation/CamperWrapArt/APPROVED_SIX_INTEGRATION.md');(folder/rel).write_text(guide,encoding='utf-8');copied.append(rel)
    entries.append({'path':rel.as_posix(),'baselineSHA256':None,'proposedSHA256':sha(folder/rel)})
    manifest={'delivery':'Approved six changed-files milestone','overallGoal':'200 wraps — NOT COMPLETE','vehicleBundleSHA256Preserved':vehicle_sha,'files':entries}
    rel=Path('_Documentation/CamperWrapArt/APPROVED_SIX_MANIFEST.json');(folder/rel).write_text(json.dumps(manifest,indent=2)+'\n',encoding='utf-8');copied.append(rel)
    archive=folder.with_suffix('.zip')
    with zipfile.ZipFile(archive,'w',compression=zipfile.ZIP_DEFLATED,compresslevel=6) as z:
        for rel in copied:z.write(folder/rel,rel.as_posix())
    with zipfile.ZipFile(archive) as z:
        assert set(z.namelist())=={r.as_posix() for r in copied}
        for entry in entries:
            h=hashlib.sha256()
            with z.open(entry['path']) as f:
                for block in iter(lambda:f.read(1024*1024),b''):h.update(block)
            assert h.hexdigest()==entry['proposedSHA256'],entry['path']
    result={'archive':str(archive),'archiveSHA256':sha(archive),'files':len(copied),'scope':'Six ready-for-integration items; overall200goal NOT COMPLETE','manifest':str(folder/'_Documentation/CamperWrapArt/APPROVED_SIX_MANIFEST.json')}
    (HERE/'_Deliveries/latest-approved-six.json').write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8')
    print(json.dumps(result,indent=2))


if __name__=='__main__':main()
