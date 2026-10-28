"""Low-complexity convex impact shapes from approved native plough geometry."""
import bpy,json,sys
from pathlib import Path
P=Path('E:/_Haven/shared/REBIRTHStaged/VehicleStudio/CamperReferenceSurvey_20261017')
bpy.ops.wm.open_mainfile(filepath=str(P/('ThicknessReview_20261018/protective-thickness-review.blend' if '--thick' in sys.argv else 'ConnectedGrilles/camper-connected-grilles.blend')))
shapes=[]
def shape(name,vertices,triangles):
    shapes.append({'name':name,'vertices':[{'x':p[0],'y':p[1],'z':p[2]} for p in vertices],'triangles':triangles})
def front(y):
    t=(y-.35)/.87;return 2.39+.30*(1-t)**2+.06*t**8
for i in range(12):
    y0=.35+.87*i/12;y1=.35+.87*(i+1)/12
    if '--thick' in sys.argv and i==11:y1=1.245
    vertices=[(x,y,front(min(y,1.22))+d) for d in [-.022,.031 if '--thick' in sys.argv and i==11 else .016 if '--thick' in sys.argv else 0] for y in [y0,y1] for x in [-1.15,1.15]]
    faces=[(0,2,3,1),(4,5,7,6),(0,1,5,4),(2,6,7,3),(0,4,6,2),(1,3,7,5)]
    shape('Moldboard convex band '+str(i+1),vertices,[v for a,b,c,d in faces for v in [a,b,c,a,c,d]])
for o in bpy.data.collections['Reference plough clay'].objects:
    if not o.name.startswith(('Individually placed turned spike','Short overlapping cutting tooth')):continue
    m=o.data.copy();m.calc_loop_triangles()
    shape(o.name,[tuple(o.matrix_world@v.co) for v in m.vertices],[i for f in m.loop_triangles for i in f.vertices]);bpy.data.meshes.remove(m)
out=P/('ApprovedThickExport_20261018' if '--thick' in sys.argv else 'PloughCollision');out.mkdir(exist_ok=True)
(out/'collision-shapes.json').write_text(json.dumps({'coordinateSpace':'Unity original prefab XYZ, meters','shapes':shapes},indent=2))
print('EXPORTED',len(shapes),'convex impact shapes')


