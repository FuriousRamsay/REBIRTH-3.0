from pathlib import Path
from lxml import etree
import argparse
p=argparse.ArgumentParser();p.add_argument("--root",required=True);a=p.parse_args();r=Path(a.root);e=[]
x=etree.parse(str(r/"Config/XUi_Menu/windows.xml"))
for i in range(18):
    q=x.xpath("//*[@name='btnProfileDetail%d']"%i)
    if not q:e.append("missing detail button %d"%i);continue
    b=q[0];par=b.getparent()
    if b.get("hovercolor")!="0,0,0,1":e.append("visible hover remains %d"%i)
    if b.get("width")!=par.get("width") or b.get("height")!=par.get("height"):e.append("hitbox mismatch %d"%i)
pcode=(r/"Scripts/Survivor/UI/RebirthPlayerProfilePortraitBinder.cs").read_text()
ccode=(r/"Scripts/Survivor/UI/XUiC_RebirthSurvivorCreator.cs").read_text()
for tok in ["CacheVersion = 4","CaptureNativePreview","Graphics.Blit(source, temp"]:
    if tok not in pcode:e.append("portrait binder missing "+tok)
for tok in ["CaptureNativePreview(pendingSelectedPortraitName","CaptureNativePreview(nativePortraitPreloadTarget.Name"]:
    if tok not in ccode:e.append("creator missing "+tok)
print("Native portrait + deep-link hover audit errors=%d"%len(e))
for q in e:print("ERROR:",q)
raise SystemExit(1 if e else 0)
