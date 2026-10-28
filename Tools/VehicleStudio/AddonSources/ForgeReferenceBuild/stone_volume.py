"""Deterministic irregular stone volume hypothesis, preserving measured silhouette.

No texture or displacement is used. Units follow the provisional camera plan.
The observed outline is fixed; hidden depth and fracture facets are inferred.
"""
import hashlib
import numpy as np

def stone_volume(outline, lift, name, rear=-.12, front=.09):
    outline=np.asarray(outline,float)
    center=outline.mean(0)
    rng=np.random.default_rng(int.from_bytes(hashlib.sha256(name.encode()).digest()[:4],'little'))
    n=len(outline)
    # Broad, differently tilted fracture facets, not concentric identical bevels.
    corner_height=rng.uniform(-.018,.018,n)
    inset=rng.uniform(.68,.84,n)
    vertices=[]
    for row in range(4):
        for j,p in enumerate(outline):
            if row==0: scale,depth=.83,rear
            elif row==1: scale,depth=1.,front-.060+corner_height[j]
            elif row==2: scale,depth=inset[j],front+corner_height[j]
            else: scale,depth=.36,front+.006+corner_height[j]*.35
            q=center+(p-center)*scale
            vertices.append(lift(q,depth))
    faces=[]
    for row in range(3):
        for j in range(n):
            k=(j+1)%n
            # Explicit triangles make nonplanar fracture surfaces reproducible.
            faces.extend([[row*n+j,row*n+k,(row+1)*n+k],
                          [row*n+j,(row+1)*n+k,(row+1)*n+j]])
    vertices.append(lift(center,front+.007))
    for j in range(n):faces.append([3*n+j,3*n+(j+1)%n,4*n])
    faces.append(list(range(n-1,-1,-1)))
    return vertices,faces
