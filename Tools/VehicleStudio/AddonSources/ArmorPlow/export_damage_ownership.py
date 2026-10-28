import bpy,json,struct,sys
from pathlib import Path
from mathutils import Vector
from mathutils.bvhtree import BVHTree
P=Path('E:/_Haven/shared/REBIRTHStaged/VehicleStudio/CamperReferenceSurvey_20261017'); O=P/'DamageOwnership_20261018';O.mkdir(exist_ok=True)
plan=json.loads(Path('Tools/VehicleStudio/AddonSources/ArmorPlow/armor-damage-threshold-plan.json').read_text()); pieces=plan['pieces']
bpy.ops.wm.open_mainfile(filepath=str(P/'Thickness40Review_20261018/protective-thickness-review.blend'))
keys={'Guard00':'Left large camper window','Guard01':'Right large camper window','CabSide-1':'Left cab side window','CabSide1':'Right cab side window','Guard07':'Left upper camper window','Guard08':'Front upper camper window','Guard09':'Right upper camper window','Guard10':'Windshield','RearDoorGuard':'Rear door window','ArmorFrontGuard':'Front radiator/headlights guard'}
def grille_id(name):
 if 'Guard06' in name:return 45 # Occluded cab rear guard follows right cab guard, not an extra visible piece.
 if any(k in name for k in ['Perimeter','transverse guard welded','front tee weld']):return 50 # Entire connected perimeter and all feet follow windshield.
 if 'Armor center diamond grille wire' in name:return 53
 for key,value in keys.items():
  if key in name:return next(p['order'] for p in pieces if p['type']=='grille' and p['name']==value)
 return None
def plate_id(name):
 side='Left' if name.startswith('Opposite ') else 'Right';name=name.removeprefix('Opposite ')
 name=name.replace('Front crown','Front arch crown').replace('Rear crown','Rear arch crown').replace('Front forward angled shoulder','Front arch forward shoulder').replace('Front rearward angled shoulder','Front arch rearward shoulder').replace('Rear forward angled shoulder','Rear arch forward shoulder').replace('Rear rearward angled shoulder','Rear arch rearward shoulder')
 if name.startswith(('Front forward lower','Front rearward lower','Rear forward lower','Rear rearward lower')):name=name.replace('Front ','Front arch ',1) if name.startswith('Front ') else name.replace('Rear ','Rear arch ',1)
 return next(p['order'] for p in pieces if p['type']=='plate' and p['side']==side and p['name']==name)
dg=bpy.context.evaluated_depsgraph_get();report={}
for label,collections in [('plates',['Reference traced plating samples','Armor opposite side review']),('grilles',['Reference connected window guards']),('door',['Reference connected window guards'])]:
 objs=[o for c in collections for o in bpy.data.collections[c].objects if o.type in ['MESH','CURVE'] and (label=='plates' or ('RearDoorGuard' in o.name)==(label=='door'))]
 owners={}; centers={}
 for o in objs:
  pts=[o.matrix_world@Vector(v) for v in o.bound_box];centers[o.name]=sum(pts,Vector())/8
  if label=='plates':
   if 'panel_outline_zy' in o:owners[o.name]=plate_id(o.name)
   elif o.get('seat_support'):owners[o.name]=plate_id(o['seat_support'])
  else:
   k=grille_id(o.name)
   if k:owners[o.name]=k
 # Hardware follows its explicit washer support or matching name suffix.
 for o in objs:
  if o.name in owners:continue
  if 'hex fastener' in o.name:
   w=o.name.replace('hex fastener','washer')
   if w in owners:owners[o.name]=owners[w];continue
  support=o.get('seat_support')
  if support and support in owners:owners[o.name]=owners[support];continue
 # Remaining perimeter tubes and their feet belong to closest visible grille, as whole connected objects.
 anchors=[o for o in objs if o.name in owners and ('rounded frame' in o.name or 'fitted metal foot' in o.name or 'panel_outline_zy' in o)]
 for o in objs:
  if o.name not in owners:
   nearest=min(anchors,key=lambda a:(centers[a.name]-centers[o.name]).length_squared);owners[o.name]=owners[nearest.name]
 vs=[];fs=[];ids=[]
 for o in objs:
  m=bpy.data.meshes.new_from_object(o.evaluated_get(dg),depsgraph=dg);m.calc_loop_triangles();base=len(vs);vs.extend(o.matrix_world@v.co for v in m.vertices)
  for t in m.loop_triangles:fs.append(tuple(base+i for i in t.vertices));ids.append(owners[o.name])
  bpy.data.meshes.remove(m)
 tree=BVHTree.FromPolygons(vs,fs,all_triangles=True)
 data=(P/'Approved40Export_20261018'/(label+'.meshbin')).read_bytes();nv,ni=struct.unpack_from('<ii',data);positions=[Vector(struct.unpack_from('<3f',data,8+i*32)) for i in range(nv)];indices=struct.unpack_from('<'+str(ni)+'i',data,8+nv*32)
 output=[];maxerr=0
 for i in range(0,ni,3):
  c=sum((positions[indices[i+j]] for j in range(3)),Vector())/3;loc,n,face,dist=tree.find_nearest(c);maxerr=max(maxerr,dist);output.append(ids[face])
 assert maxerr<.002,(label,maxerr)
 (O/(label+'.owners')).write_bytes(bytes(output));report[label]={'triangles':len(output),'maxSurfaceError':maxerr,'objects':[{'name':o.name,'order':owners[o.name]} for o in objs],'counts':{str(k):output.count(k) for k in sorted(set(output))}}
 print(label,len(output),'error',maxerr,flush=True)
assert set(k for r in report.values() for k in r['counts'])==set(str(p['order']) for p in pieces)
(O/'ownership-report.json').write_text(json.dumps(report,indent=2));(O/'plan.json').write_text(json.dumps(plan,indent=2));print('OWNERSHIP_PASS',flush=True)


