from pathlib import Path
import csv, hashlib, sys, xml.etree.ElementTree as ET
from PIL import Image

R=Path(__file__).resolve().parents[2]
checks=[]
def ck(name,cond,detail=''): checks.append((name,bool(cond),detail))
def text(rel): return (R/rel).read_text(encoding='utf-8')
def sha(p:Path):
    h=hashlib.sha256()
    with p.open('rb') as f:
        for c in iter(lambda:f.read(1024*1024),b''): h.update(c)
    return h.hexdigest()

# Authoritative bonus registry and Background coverage carried forward from PC015.
bp=R/'Config/_Survivor/background_bonuses.xml'; root=ET.parse(bp).getroot(); entries=root.findall('bonus')
ck('bonus schema version 1',root.get('schema_version')=='1',root.get('schema_version',''))
ck('clean slate authority frozen',root.get('clean_slate_background_id')=='background.clean_slate',root.get('clean_slate_background_id',''))
ck('Chunk C icon asset contract integrated',root.get('icon_asset_contract')=='integrated_chunk_c',root.get('icon_asset_contract',''))
ck('27 bonus definitions',len(entries)==27,str(len(entries)))
ids=[e.get('id') for e in entries]; bgs=[e.get('background_id') for e in entries]; icons=[e.get('icon_key') for e in entries]
ck('bonus IDs unique',len(ids)==len(set(x.lower() for x in ids)))
ck('background bindings unique',len(bgs)==len(set(x.lower() for x in bgs)))
ck('bonus icon keys unique',len(icons)==len(set(x.lower() for x in icons)))
ck('all icon states integrated Chunk C',all(e.get('icon_state')=='integrated_chunk_c' for e in entries),str(sorted(set(e.get('icon_state') for e in entries))))
bgroot=ET.parse(R/'Config/_Survivor/backgrounds.xml').getroot(); current=[e.get('id') for e in bgroot.findall('background')]
ck('28 current backgrounds',len(current)==28,str(len(current)))
ck('Clean Slate has no bonus','background.clean_slate' not in bgs)
ck('every non-Clean-Slate background bound exactly once',set(x.lower() for x in bgs)==set(x.lower() for x in current if x!='background.clean_slate'))
ck('Metal Worker stable ID retained','background.welder_fabricator' in bgs)
ck('Park Ranger stable ID retained','background.park_ranger_outdoor_guide' in bgs)

# Atlas route/source audit.
atlas=R/'UIAtlases/RebirthSurvivorIcons'; settings_path=atlas/'settings.xml'; settings=ET.parse(settings_path).getroot()
registered=[e.get('name') for e in settings.findall('sprite') if e.get('name')]
registered_set=set(registered)
ck('every bonus sprite registered',set(icons)<=registered_set,str(sorted(set(icons)-registered_set)))
ck('every bonus sprite registered exactly once',all(registered.count(k)==1 for k in icons),str([k for k in icons if registered.count(k)!=1]))
ck('no Clean Slate bonus sprite registered',not any('clean_slate' in k for k in registered if k.startswith('rb_bonus_')))
final_paths=[atlas/(k+'.png') for k in icons]
ck('all 27 final bonus PNGs exist',all(p.is_file() for p in final_paths),str([p.name for p in final_paths if not p.is_file()]))
ck('no extra rb_bonus PNG outside registry',{p.stem for p in atlas.glob('rb_bonus_*.png')}==set(icons),str(sorted({p.stem for p in atlas.glob('rb_bonus_*.png')}-set(icons))))

# Exact 64x64 RGBA, transparency, occupancy/padding and uniqueness.
final_hashes=[]; final_stats=[]; bad_dims=[]; bad_mode=[]; bad_empty=[]; bad_padding=[]; bad_occupancy=[]; bad_corner=[]
for p in final_paths:
    im=Image.open(p)
    if im.size!=(64,64): bad_dims.append((p.name,im.size))
    if im.mode!='RGBA': bad_mode.append((p.name,im.mode))
    rgba=im.convert('RGBA'); a=rgba.getchannel('A')
    # Ignore tiny LANCZOS ringing when measuring visual extent.
    strong=a.point(lambda v:255 if v>8 else 0); bbox=strong.getbbox()
    if not bbox: bad_empty.append(p.name); continue
    x0,y0,x1,y1=bbox; strong_pixels=strong.histogram()[255]
    final_stats.append((p.stem,bbox,strong_pixels))
    if min(x0,y0,64-x1,64-y1)<2: bad_padding.append((p.name,bbox))
    if not (600<=strong_pixels<=2200): bad_occupancy.append((p.name,strong_pixels))
    corners=[a.getpixel((0,0)),a.getpixel((63,0)),a.getpixel((0,63)),a.getpixel((63,63))]
    if any(v>8 for v in corners): bad_corner.append((p.name,corners))
    final_hashes.append(sha(p))
ck('all final icons are 64x64',not bad_dims,str(bad_dims))
ck('all final icons are RGBA',not bad_mode,str(bad_mode))
ck('all final icons have visible pixels',not bad_empty,str(bad_empty))
ck('all final icons retain at least 2px visual padding',not bad_padding,str(bad_padding))
ck('all final icons use readable occupancy band',not bad_occupancy,str(bad_occupancy))
ck('all final icon corners transparent',not bad_corner,str(bad_corner))
ck('all 27 final icon checksums distinct',len(final_hashes)==27 and len(set(final_hashes))==27,str(len(set(final_hashes))))

# High-resolution source masters.
masters=R/'_Documentation/Art/Survivor/BackgroundBonusMasters'; master_paths=[masters/(k+'.png') for k in icons]
ck('all 27 high-resolution source masters exist',all(p.is_file() for p in master_paths),str([p.name for p in master_paths if not p.is_file()]))
master_hashes=[]; master_bad=[]
for p in master_paths:
    im=Image.open(p)
    if im.size!=(256,256) or im.mode!='RGBA': master_bad.append((p.name,im.size,im.mode))
    master_hashes.append(sha(p))
ck('all source masters are 256x256 RGBA',not master_bad,str(master_bad))
ck('all 27 source master checksums distinct',len(master_hashes)==27 and len(set(master_hashes))==27,str(len(set(master_hashes))))

# Manifest covers prompt/concept, source/final checksums and UI references.
man=R/'_Documentation/ProjectChanges/REBIRTH_3_0_BACKGROUND_SIGNATURE_BONUSES_PC016_CHUNK_C_ICON_MANIFEST_20260902.csv'
rows=list(csv.DictReader(man.open(encoding='utf-8',newline='')))
ck('icon manifest has 27 rows',len(rows)==27,str(len(rows)))
ck('icon manifest sprite coverage exact',{r['sprite_key'] for r in rows}==set(icons))
ck('icon manifest bonus coverage exact',{r['bonus_id'] for r in rows}==set(ids))
required=['source_method','source_prompt_concept','source_master_path','source_sha256','atlas_path','atlas_sha256','source_size','atlas_size','ui_surfaces']
ck('icon manifest required fields populated',all(all((r.get(k) or '').strip() for k in required) for r in rows))
manifest_mismatch=[]
for r in rows:
    sp=R/r['source_master_path']; ap=R/r['atlas_path']
    if not sp.is_file() or sha(sp)!=r['source_sha256']: manifest_mismatch.append((r['sprite_key'],'source'))
    if not ap.is_file() or sha(ap)!=r['atlas_sha256']: manifest_mismatch.append((r['sprite_key'],'atlas'))
ck('icon manifest checksums match files',not manifest_mismatch,str(manifest_mismatch))
ck('manifest references planned UI surfaces',all('Survivor Creator' in r['ui_surfaces'] and 'Character Origin' in r['ui_surfaces'] and 'Progression Explorer' in r['ui_surfaces'] for r in rows))


# Current data-derived catalogue reflects integrated icon state.
cat=R/'_Documentation/ProjectChanges/REBIRTH_3_0_BACKGROUND_SIGNATURE_BONUSES_PC016_CHUNK_C_CATALOGUE_20260902.csv'
cat_rows=list(csv.DictReader(cat.open(encoding='utf-8',newline='')))
ck('PC016 catalogue has 27 rows',len(cat_rows)==27,str(len(cat_rows)))
ck('PC016 catalogue icon state integrated',all(r.get('icon_state')=='integrated_chunk_c' for r in cat_rows))
ck('PC016 catalogue sprite coverage exact',{r.get('icon_key') for r in cat_rows}==set(icons))

# Generator provenance and deterministic registry coverage.
gen=text('BuildScripts/Progression/generate_background_signature_bonus_icons.py')
ck('generator reads authoritative bonus registry','Config' in gen and 'background_bonuses.xml' in gen and 'DRAWERS' in gen)
ck('generator enforces drawer coverage','drawer coverage mismatch' in gen)
ck('generator emits source masters and final atlas PNGs','BackgroundBonusMasters' in gen and 'RebirthSurvivorIcons' in gen)
ck('generator integrates registry icon state and settings.xml','integrated_chunk_c' in gen and 'settings.xml' in gen and 'integrate_registry_and_settings' in gen)
ck('generator emits icon manifest','PC016_CHUNK_C_ICON_MANIFEST' in gen)

# Contact sheets are review evidence at source and actual atlas size.
source_sheet=R/'_Documentation/ProjectChanges/REBIRTH_3_0_BACKGROUND_SIGNATURE_BONUSES_PC016_CHUNK_C_SOURCE_CONTACT_SHEET_20260902.jpg'
final_sheet=R/'_Documentation/ProjectChanges/REBIRTH_3_0_BACKGROUND_SIGNATURE_BONUSES_PC016_CHUNK_C_64PX_CONTACT_SHEET_20260902.jpg'
ck('source-size review contact sheet exists',source_sheet.is_file())
ck('64px review contact sheet exists',final_sheet.is_file())

# Existing localization, service binding and Base Game isolation remain intact.
rows_loc=list(csv.reader((R/'Config/Localization.csv').open(encoding='utf-8-sig',newline='')))
keys=[r[0].strip() for r in rows_loc[1:] if r and r[0].strip()]; counts={k:keys.count(k) for k in set(keys)}
loc_needed=[]
for e in entries: loc_needed.extend([e.get('name_key'),e.get('description_key')])
ck('all 54 bonus localization keys remain exactly once',len(set(loc_needed))==54 and all(counts.get(k,0)==1 for k in loc_needed))
svc=text('Scripts/Survivor/Backgrounds/RebirthBackgroundBonusService.cs')
ck('service Base Game world gate retained','!RebirthSurvivorMode.IsEnabledForCurrentWorld()' in svc)
ck('service Base Game profile gate retained','RebirthSurvivorMode.ConfiguredMode != RebirthPlayerProgressionMode.Rebirth' in svc)
ck('no separate bonus entitlement persisted','SignatureBonusId' not in text('Scripts/Survivor/Persistence/RebirthSurvivorProfileModels.cs') and 'SignatureBonusId' not in text('Scripts/Survivor/Domain/RebirthSurvivorRuntimeModels.cs'))
ck('no separate bonus entitlement network field','SignatureBonusId' not in text('Scripts/Survivor/Network/RebirthSurvivorNetworkModels.cs'))

# Chunk C itself remains asset-only. Later chunks may add source-audited provenance capture
# without activating a Signature Bonus gameplay effect. Keep this historical validator useful
# by allowing only the explicit PC017 Farmer provenance capture adapter.
consumer_hits=[]
allowed_later_foundation={'Scripts/Survivor/Provenance/RebirthCropProvenanceAdapter.cs'}
for p in (R/'Scripts').rglob('*.cs'):
    rel=p.relative_to(R).as_posix()
    if rel.endswith('RebirthBackgroundBonusRegistry.cs') or rel.endswith('RebirthBackgroundBonusService.cs') or rel.endswith('RebirthBackgroundBonusDefinition.cs') or rel in allowed_later_foundation: continue
    s=p.read_text(encoding='utf-8',errors='ignore')
    if 'background_bonus.' in s or 'RebirthBackgroundBonusService.HasBonus' in s: consumer_hits.append(rel)
ck('Chunk C icon integration has no direct gameplay bonus consumers',not consumer_hits,str(consumer_hits[:8]))

# General XML integrity.
xmls=list(R.rglob('*.xml')); bad=[]
for p in xmls:
    try: ET.parse(p)
    except Exception as e: bad.append((p.relative_to(R).as_posix(),str(e)))
ck('all project XML parses',not bad,str(bad[:3]))

failed=[x for x in checks if not x[1]]
for n,v,d in checks: print(('PASS ' if v else 'FAIL ')+n+((' :: '+d) if d and not v else ''))
# Aggregate loose-atlas set hash for reproducibility.
h=hashlib.sha256()
for p in sorted(final_paths,key=lambda p:p.name): h.update(p.name.encode()); h.update(b'\0'); h.update(bytes.fromhex(sha(p)))
print('PC016_ATLAS_SET_SHA256='+h.hexdigest())
print(f'PC016_STATIC_CHECKS={len(checks)} PASSED={len(checks)-len(failed)} FAILED={len(failed)} BONUSES={len(entries)} FINAL_ICONS={len(final_paths)} SOURCE_MASTERS={len(master_paths)} XML={len(xmls)}')
sys.exit(1 if failed else 0)
