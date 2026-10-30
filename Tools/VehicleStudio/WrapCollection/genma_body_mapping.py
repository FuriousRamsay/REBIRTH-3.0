"""Twelve approved continuous paintings, projected with native template frames."""
import json
from pathlib import Path
HERE=Path(__file__).parent

def overlay():
    panels={p['name']:p for p in json.loads((HERE/'Templates/BodySurfaces-default/native-cutout-layout.json').read_text())['panels']}
    rows=[('left-continuous','_ConceptSceneAtlas','sideLeft'),('right-continuous','_ConceptBotanicalAtlas','sideRight'),('hood','_ConceptHoodAtlas','hood'),('camper-front-continuous','_ConceptGardenCorner','frontCamper'),('camper-rear-fixed-continuous','_ConceptMusicCorner','rearCamper'),('rear-door','_WrapLeftBandSharp','door'),('camper-roof','_WrapRightBandSharp','roofCamper'),('cab-roof','_WrapRearSharp','roofCab'),('truck-front-face','_WrapFrontSharp','frontTruck'),('cab-back','_WrapRearDoorArt','backCab'),('pickup-tailgate','_WrapRearLeftArt','tailgate')]
    code='''
   if(_WrapLayoutVariant>.5){
    float3 bp=IN.vehiclePos,bn=normalize(IN.vehicleNormal);
    float3 ba=abs(bn);float sideAxis=step(ba.y,ba.x)*step(ba.z,ba.x);
    float roofAxis=(1-sideAxis)*step(ba.z,ba.y)*step(0,bn.y);
    float endAxis=(1-sideAxis)*(1-roofAxis);
    float outward=step(0,bp.x*bn.x);
    float sideLeft=sideAxis*step(bn.x,0)*outward;
    float sideRight=sideAxis*step(0,bn.x)*outward;
    float hood=roofAxis*(1-_IsCamper)*step(.65,bp.z);
    float roofCab=roofAxis*(1-_IsCamper)*step(bp.z,.65)*step(-.65,bp.z);
    float roofCamper=roofAxis*_IsCamper;
    float frontCamper=endAxis*step(0,bn.z)*_IsCamper;
    float rearCamper=endAxis*step(bn.z,0)*_IsCamper*(1-_WrapRearDoorPanel);
    float door=endAxis*_WrapRearDoorPanel;
    float frontTruck=endAxis*step(0,bn.z)*(1-_IsCamper)*step(1.7,bp.z);
    float backCab=endAxis*step(bn.z,0)*(1-_IsCamper)*step(-1.5,bp.z);
    float tailgate=endAxis*step(bn.z,0)*(1-_IsCamper)*step(bp.z,-1.5);
    wrapColor=_WrapBaseCoat.rgb;
'''
    def vec(v):return 'float3('+','.join(str(v[k]) for k in ('x','y','z'))+')'
    for i,(name,texture,region) in enumerate(rows):
        p=panels[name]
        # Layout1 is already owner-validated in installed special wraps. New
        # originals use layout2 and the corrected tailgate upper extent. Never
        # remap old tailgate art simply because the default template advanced.
        if name=='pickup-tailgate':
            legacy=json.loads((HERE/'Templates/BodySurfaces-before-tailgate-129/native-cutout-layout.json').read_text())
            old=next(q for q in legacy['panels'] if q['name']==name)
            code+=f'''    {{float upgraded=step(1.5,_WrapLayoutVariant);float3 frame=lerp({vec(old['center'])},{vec(p['center'])},upgraded);float3 delta=bp-frame;
     float2 uv=float2(.5+dot(delta,{vec(p['right'])})/lerp({old['width']},{p['width']},upgraded),.5+dot(delta,{vec(p['up'])})/lerp({old['height']},{p['height']},upgraded));
     float bounds=step(0,uv.x)*step(uv.x,1)*step(0,uv.y)*step(uv.y,1);float4 ink=tex2D({texture},saturate(uv));wrapColor=lerp(wrapColor,ink.rgb,{region}*bounds*ink.a);
    }}
'''
            continue
        code+=f'''    {{float3 delta=bp-{vec(p['center'])};
     float2 uv=float2(.5+dot(delta,{vec(p['right'])})/{p['width']},.5+dot(delta,{vec(p['up'])})/{p['height']});
     float bounds=step(0,uv.x)*step(uv.x,1)*step(0,uv.y)*step(uv.y,1);
     float4 ink=tex2D({texture},saturate(uv));
     wrapColor=lerp(wrapColor,ink.rgb,{region}*bounds*ink.a);
    }}
'''
    # Native inner-bed net at original pixel scale; no floor paint is invented.
    # Dest mapping is the exact unfolding in RebirthNativeCutoutTemplates.WriteBedNet.
    from PIL import Image
    src=HERE/'Templates/NativeCutouts-v3'
    names=['pickup-bed-floor','pickup-inner-bed-left','pickup-inner-bed-right','pickup-inner-bed-front','pickup-inner-tailgate']
    sizes=[Image.open(src/(n+'-paintable.png')).size for n in names]
    middle=max(sizes[0][0],sizes[3][0],sizes[4][0]);side=sizes[1][1];bottom=sizes[3][1];top=sizes[4][1]
    W=middle+2*side;H=bottom+sizes[0][1]+top
    detail={p['name']:p for p in json.loads((HERE/'Templates/NativeCutouts-v3/native-cutout-layout.json').read_text())['panels']}
    code+='''    if(_IsCamper<.5 && bp.z<-.4 && bp.z> -2.95 && bp.y<1.3){
      float2 net=float2(0,0);float inner=0;
'''
    conditions=['roofAxis','sideAxis*step(0,-bp.x)*step(0,bn.x)','sideAxis*step(0,bp.x)*step(bn.x,0)','endAxis*step(bn.z,0)*step(-1.5,bp.z)','endAxis*step(0,bn.z)*step(bp.z,-1.5)']
    for i,name in enumerate(names):
        p=detail[name];w,h=sizes[i]
        dest=[f'float2({side+(middle-w)/2}+uv.x*{w},{bottom}+uv.y*{h})',f'float2({side+middle}+uv.y*{h},{bottom+w}-uv.x*{w})',f'float2({h}-uv.y*{h},{bottom}+uv.x*{w})',f'float2({side+w}-uv.x*{w},{h}-uv.y*{h})',f'float2({side+(middle-w)/2}+uv.x*{w},{bottom+sizes[0][1]}+uv.y*{h})'][i]
        code+=f'''      {{float3 delta=bp-{vec(p['center'])};float2 uv=float2(.5+dot(delta,{vec(p['right'])})/{p['width']},.5+dot(delta,{vec(p['up'])})/{p['height']});float ok={conditions[i]}*step(0,uv.x)*step(uv.x,1)*step(0,uv.y)*step(uv.y,1);if(ok>.5){{net={dest}/float2({W},{H});inner=1;}}}}
'''
    code+='''      float4 ink=tex2D(_WrapRearRightArt,saturate(net));wrapColor=lerp(wrapColor,ink.rgb,inner*ink.a);
    }
   }
'''
    return code
