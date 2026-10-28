"""Measure exposed mortar using visible geometry and distance to real stone."""
import bpy,json,sys
from pathlib import Path
from mathutils import Vector
from mathutils.bvhtree import BVHTree
root=Path('E:/_Haven/shared/REBIRTHTemp/ForgeReferenceBuild/ApprovedTextureBaseline')
out=root/'MortarRelief_v16'
source=out/'mortar-relief.blend'
if '--wall-joints' in sys.argv:
    out=root/'WallJointEdges_v18';source=out/'wall-joints.blend'
if '--contact-fit' in sys.argv:
    out=root/'MortarContactFit_v29';source=out/'mortar-contact.blend'
bpy.ops.wm.open_mainfile(filepath=str(source))
sc=bpy.context.scene;cam=sc.camera;deps=bpy.context.evaluated_depsgraph_get()
vertices=[];faces=[];owners=[];sv=[];sf=[];so=[]
for o in sc.objects:
    if o.type!='MESH' or o.hide_render or o.name=='Inspection ground':continue
    ev=o.evaluated_get(deps);mesh=ev.to_mesh();start=len(vertices)
    vertices.extend(o.matrix_world@v.co for v in mesh.vertices)
    for p in mesh.polygons:faces.append([start+i for i in p.vertices]);owners.append(o.name)
    if o.name.startswith(('stone_','inferred_rear_stone_','hearth_stones_')):
        start=len(sv);sv.extend(o.matrix_world@v.co for v in mesh.vertices)
        for p in mesh.polygons:sf.append([start+i for i in p.vertices]);so.append(o.name)
    ev.to_mesh_clear()
visible=BVHTree.FromPolygons(vertices,faces);stones=BVHTree.FromPolygons(sv,sf)
hearth=json.loads((Path(__file__).parent/'upright-hearth-plan.json').read_text());axis=Vector(hearth['outwardAxis'])
collar=next(p for p in json.loads((Path(__file__).parent/'round-components-plan.json').read_text())['parts'] if p['id']=='top_collar')
collarCenter=Vector(collar['center']);collarInnerRadius=min(p[0] for p in collar['radialHeightProfile'])
collarBase=collarCenter.z+min(p[1] for p in collar['radialHeightProfile'])
excluded={'collarInterior':0,'chamber':0,'baseUnderside':0}
width,height=sc.render.resolution_x,sc.render.resolution_y
sx=cam.data.ortho_scale;sy=sx*height/width;direction=cam.matrix_world.to_quaternion()@Vector((0,0,-1))
samples=[]
for y in range(0,height,6):
    for x in range(0,width,6):
        origin=cam.matrix_world@Vector(((x/width-.5)*sx,(.5-y/height)*sy,0))
        p,n,index,d=visible.ray_cast(origin,direction,100)
        if p is None or owners[index]!='furnace_core':continue
        if p.z>=collarBase-.005 and Vector((p.x-collarCenter.x,p.y-collarCenter.y,0)).length<collarInnerRadius+.003:
            excluded['collarInterior']+=1;continue
        if p.dot(axis)<hearth['frontDepth']-.025 and .26<p.z<.80:
            excluded['chamber']+=1;continue
        if p.z<.015:
            excluded['baseUnderside']+=1;continue
        near,normal,stoneIndex,distance=stones.find_nearest(p)
        if near is None:continue
        delta=near-p
        samples.append({'pixel':[x,y],'position':list(p),'normal':list(n),'nearestStone':so[stoneIndex],'distanceToStoneMeters':distance,'approximateJointWidthMeters':distance*2,'cameraDepthDifferenceMeters':delta.dot(direction),'cameraPlaneDistanceMeters':(delta-direction*delta.dot(direction)).length})
broad=[s for s in samples if s['distanceToStoneMeters']>.012]
cells={}
for s in broad:cells.setdefault((s['pixel'][0]//60,s['pixel'][1]//60),[]).append(s)
patches=[{'pixelCell':list(cell),'samples':items,'maxDistanceToStoneMeters':max(s['distanceToStoneMeters'] for s in items)} for cell,items in cells.items() if len(items)>=3]
report={'view':'Saved approved-reference camera','sampleSpacingPixels':6,'exteriorMortarSamples':len(samples),'wideGapSamples':len(broad),'patches':patches,'excludedConstructionRoles':excluded,'collarExclusion':{'center':list(collarCenter),'innerRadius':collarInnerRadius,'base':collarBase},'maximumDistanceToStoneMeters':max((s['distanceToStoneMeters'] for s in samples),default=0),'limits':'Nearest-surface Euclidean distance approximates half joint width; curved depth may overestimate. Pixelcells are planning regions, not validated new stone shapes. Collar interior excluded using authored radial profile; chamber uses axis/depth heuristic; inspect seeds before geometry.'}
(out/'mortar-width-classified-audit.json').write_text(json.dumps(report,indent=2))
print(json.dumps({k:v for k,v in report.items() if k!='patches'}));print('PLANNING_PATCHES',len(patches))
