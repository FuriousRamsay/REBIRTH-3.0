"""Derive collection shaders without editing the cumulative prefab or shader files."""
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
SOURCE = ROOT / "Tools/VehicleStudio/UnitySources"
OUT = ROOT / "Tools/VehicleStudio/WrapCollection/Unity"


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    for stem in ["RebirthCamperReferenceWrap", "RebirthCamperTallReferenceWrap", "RebirthPickupOriginalFinish", "RebirthCamperAlignedStripe"]:
        text = (SOURCE / (stem + ".shader")).read_text(encoding="utf-8-sig")
        old_name = text.split('"')[1]
        text = text.replace('Shader "' + old_name + '"', 'Shader "' + old_name + 'Collection"', 1)
        text = text.replace(' Properties {', ''' Properties {
  _WrapAtlas ("Collection artwork", 2D) = "black" {}
  _WrapFrontPanel ("Front logo panel layout", Float) = 0
  _WrapMetallic ("Collection metallic", Range(0,1)) = .08
  _WrapSmoothness ("Collection smoothness", Range(0,1)) = .45
''', 1)
        text = text.replace('  #include "UnityStandardUtils.cginc"', '  #include "UnityStandardUtils.cginc"\n  sampler2D _WrapAtlas; float _WrapMetallic,_WrapSmoothness,_WrapFrontPanel;', 1)
        # Replace only the final cosmetic composition. Native dye, masks,
        # normals, ambient occlusion and vehicle-space mapping remain intact.
        anchor = '   o.Albedo=lerp(nativeBase,baseColor,saturate(_PatternEnabled));'
        assert text.count(anchor) == 1, stem
        if 'float hu=' not in text:
            text = text.replace(anchor, '''   float hu=(IN.vehiclePos.x+.68)/1.36;
   float hv=(IN.vehiclePos.z-.95)/.90;
   float hood=step(0,hu)*step(hu,1)*step(0,hv)*step(hv,1)*step(1.05,IN.vehiclePos.y)*step(IN.vehiclePos.y,1.40)*smoothstep(.85,.97,IN.vehicleNormal.y)*(1-_IsCamper);
   float bandU=u;
   float bandSurface=side;
''' + anchor)
        text = text.replace(anchor, '''   // Atlas image coordinates: side top half, hood lower-left,
   // upper/lower camper bands in lower-right. PNG and Unity V are inverted.
   float shade=lerp(.75,1.15,saturate(detail));
   // The atlas owns its base palette. Sample the reserved lower-right margin
   // in the same sRGB texture path as the artwork, avoiding coat gamma seams.
   float3 wrapBase=tex2D(_WrapAtlas,float2(.98,.02)).rgb;
   // Keep lettering readable from outside either side and from in front of
   // the hood. The legacy swirl mapping did not expose mirrored typography.
   float wrapU=IN.vehicleNormal.x>0 ? 1-u : u;
   // Optional two-panel side layout keeps a front-door wordmark ahead of the
   // wheel on BOTH sides. Reverse within panels rather than swapping panels.
   float sideU=wrapU;
   if(_WrapFrontPanel>.5 && IN.vehicleNormal.x>0)sideU=u<=.5 ? .5-u : 1.5-u;
   float4 wrapSide=tex2D(_WrapAtlas,float2(saturate(sideU),.5+.5*saturate(v)));
   float4 wrapHood=tex2D(_WrapAtlas,float2(.5*(1-saturate(hu)),.5*(1-saturate(hv))));
   float bandV=band1>0 ? lerp(.27,.42,saturate(bv)) : lerp(.07,.22,saturate(bv));
   // Narrow native lower stripe needs more repeats to retain emblem proportions.
   float bandRepeat=band1>0 ? 2 : 6;
   float readableBandU=side>.5 ? wrapU : (IN.vehicleNormal.z>0 ? 1-bandU : bandU);
   float4 wrapBand=tex2D(_WrapAtlas,float2(.5+.5*frac(readableBandU*bandRepeat),bandV));
   float3 wrapColor=wrapBase;
   wrapColor=lerp(wrapColor,wrapSide.rgb,region*wrapSide.a);
   wrapColor=lerp(wrapColor,wrapBand.rgb,max(band1,band2)*bandSurface*_IsCamper*wrapBand.a);
   wrapColor=lerp(wrapColor,wrapHood.rgb,hood*wrapHood.a);
   baseColor=lerp(original,wrapColor*shade,mask);
   o.Albedo=lerp(nativeBase,baseColor,saturate(_PatternEnabled));''')
        text = text.replace('finish.r,_DiscoMetallic,', 'finish.r,_WrapMetallic,')
        text = text.replace('finish.a*_GlossMapScale,_DiscoSmoothness,', 'finish.a*_GlossMapScale,_WrapSmoothness,')
        (OUT / (stem + "Collection.shader")).write_text(text, encoding="utf-8")
    print("Derived 4 collection shaders; cumulative shaders and prefab untouched.")


if __name__ == '__main__':
    main()
