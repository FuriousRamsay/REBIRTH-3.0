"""Persist the owner's corrected-template standard without altering geometry."""
from pathlib import Path
import hashlib,json
H=Path(__file__).resolve().parent
R=H.parents[2]
standard='''
### 10.1.3 Corrected body templates are the standard (Owner approval, 2026-10-28)

Use Tools/VehicleStudio/WrapCollection/Templates/BodySurfaces-default for every new or reimagined camper wrap. These twelve continuous native body canvases supersede older rectangular plates and the pre-correction hood. The personal artifact-template-camper-body-surface-templates skill retains the same masks and metadata.

- Hood: native canvas 1024 x 1012; physical frame width1.60m, height1.58m, center(0,1.25,1.27), rearZ.48 through frontZ2.06. This includes the curved front surface. The vertical lip and grille surround use truck-front-face; verify the join in native closeups. Never reuse the old1024x948 hood.
- Tailgate: native canvas1088x512, center(0,.85,-2.7), width1.70,height.80,Y.45..1.25, native depth slice.20m. Native128/130/132 proves the oldYmax1.10 omitted a broad upperface. New replacements use layout2; preserve oldlayout1 mapping for installed special wraps. Redo entire affected tailgate panels on corrected canvas, never fit old art.
- Start artwork on the exact native canvas with its real openings. Diagnostic colour may mark exclusions, but must never appear in final textures, filtered edges, icons or renders. Removing that diagnostic key is export processing, not permission to fill gaps or clip misplaced artwork.
- Keep the native mesh unchanged; accessories do not define artwork holes. Preserve the theme, approved style, palette, detail and source resolution. Redo defective panels on corrected templates rather than repair them.
- Judge noticeable defects in actual vehicle renders before unnecessary regeneration. This owner's acceptance of small Midnight tailgate boundary differences is not a general template-conformance PASS or permission to relax future geometry checks.
- Review five native views plus hood/front-lip and tailgate closeups; confirm full intended coverage and no diagnostic colour before installation. Save template hashes, source provenance and runtime render evidence. Do not claim exact source-mask compliance from visual approval alone.
- Use built-in image generation only; no separately billed API workflow. Remaining wraps require unique interpretations of their existing themes. Owner authorized remaining199 with root plus three agents after in-game pilot validation.
'''
p=R/'_Documentation/REBIRTH_PROJECT_OPERATING_DIRECTIVES.md'
s=p.read_text(encoding='utf-8-sig')
if '### 10.1.3 Corrected body templates' not in s:
 s=s.replace('## 10.2 Mandatory artwork quality and theme directive',standard+'\n## 10.2 Mandatory artwork quality and theme directive')
 p.write_text(s,encoding='utf-8')
p=R/'AGENTS.md';s=p.read_text()
note='\nOwner-approved standard (2026-10-28): corrected BodySurfaces-default hood1024x1012 / Z.48..2.06 plus truck-front-face lip is mandatory for future wraps. Old1024x948 hood is retired. No diagnostic pink in final exports/renders/icons; verify curved-edge coverage in native views. See section10.1.3 and Templates/BodySurfaces-default/STANDARD.json.\n'
if 'Old1024x948 hood is retired' not in s:p.write_text(s+note)
t=H/'Templates/BodySurfaces-default'
files={p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in sorted(t.iterdir()) if p.name.endswith('-paintable.png') or p.name in ['continuous-layout.json','native-cutout-layout.json']}
(t/'STANDARD.json').write_text(json.dumps({'version':'native-body-2026-10-28-hood107-tailgate130','status':'NATIVE_MAPPING_VERIFIED_DEFAULT','supersedes':'nine rectangular plates, hood1024x948, tailgate1088x416','nativeMaskCount':len([f for f in files if f.endswith('-paintable.png')]),'hoodCanvas':[1024,1012],'hoodZRange':[.48,2.06],'tailgateCanvas':[1088,512],'tailgateYRange':[.45,1.25],'tailgateDepthMetres':.20,'legacyTailgate':'BodySurfaces-before-tailgate-129 retained for installed layout1 wraps','frontLipPanel':'truck-front-face','geometryEvidence':'_Review-midnight-template-probe-107; _Review-tailgate-extent-128; _Review-tailgate-cutouts-130; _Review-tailgate-grid-132','appearanceEvidence':'Existing layout1 preserved in build131; new artwork requires separate native review','files':files},indent=2))
(t/'README.md').write_text('# Standard camper wrap canvases\n\n'+standard.split('\n\n',1)[1]+'\nNative masks and JSON are authoritative; older overview thumbnails are not geometry. STANDARD.json records their hashes.\n')
print('Saved owner standard, exact mask hashes and project directives.')
