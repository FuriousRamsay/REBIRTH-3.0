"""Specify two closed curved boards following the envelope's top/bottom shoulders."""
import json
from pathlib import Path
import numpy as np
r=Path(__file__).parent
body=json.loads((r/'bellows-frame-aligned-plan.json').read_text());rings=np.array(body['vertices']).reshape(41,256,3)
parts=[]
for name,center,sign in [('upper_board',0,1),('lower_board',128,-1)]:
 indices=[(center+i)%256 for i in range(-32,33,4)]
 surface=rings[:,indices,:].copy();surface[:,:,2]+=sign*.002
 outer=surface.copy();outer[:,:,2]+=sign*.014
 v=np.concatenate([surface.reshape(-1,3),outer.reshape(-1,3)])
 rows,cols=surface.shape[:2];count=rows*cols;faces=[]
 for i in range(rows-1):
  for j in range(cols-1):
   q=[i*cols+j,i*cols+j+1,(i+1)*cols+j+1,(i+1)*cols+j]
   faces.append(q[::-1]);faces.append([k+count for k in q])
 boundary=list(range(cols))+[i*cols+cols-1 for i in range(1,rows)]+list(range((rows-1)*cols+cols-2,(rows-1)*cols-1,-1))+[i*cols for i in range(rows-2,0,-1)]
 for i,a in enumerate(boundary):
  b=boundary[(i+1)%len(boundary)];faces.append([a,b,b+count,a+count])
 edges={}
 for face in faces:
  for i,a in enumerate(face):
   key=tuple(sorted([a,face[(i+1)%len(face)]]));edges[key]=edges.get(key,0)+1
 parts.append({'id':name,'vertices':v.tolist(),'faces':faces,'thickness':.014,'edgeBevelPlanned':.004,'nonTwoFaceEdges':sum(n!=2 for n in edges.values())})
report={'status':'BOARD_VOLUME_CANDIDATE_REQUIRES_REFERENCE_AND_CLEARANCE_REVIEW','parts':parts,'limitations':['Edges not yet beveled','Boards follow current envelope, so inherit its unvalidated shoulder shape','Throat and frame clearance must include boards'],'blenderAllowed':False}
(r/'bellows-board-plan.json').write_text(json.dumps(report))
print([(p['id'],len(p['vertices']),p['nonTwoFaceEdges']) for p in parts])
