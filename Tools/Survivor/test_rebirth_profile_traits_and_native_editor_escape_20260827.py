from pathlib import Path
from lxml import etree
import argparse
p=argparse.ArgumentParser();p.add_argument("--root",required=True);a=p.parse_args();r=Path(a.root);e=[]
x=(r/"Config/XUi_Menu/windows.xml").read_text()
m=(r/"Scripts/Survivor/UI/XUiC_RebirthSurvivorProfileManager.cs").read_text()
c=(r/"Scripts/Survivor/UI/XUiC_RebirthSurvivorCreator.cs").read_text()
g=(r/"Scripts/Survivor/UI/XUiC_RebirthNativeCharacterEditorGuard.cs").read_text()
for bad in ["btnSelectedProfileChangeView","ChangeView_OnPressed"]:
    if bad in x or bad in m:e.append("change-view residue "+bad)
for tok in ["positiveTraits.Sort","negativeTraits.Sort","TraitListScroll","profileTraitScrollThumb","RebirthSurvivorUiText.PointText"]:
    if tok not in m:e.append("trait summary missing "+tok)
for tok in ["TryHandleNativeCharacterEditorEscape","isEscClosable = false","activeNativeEditorOwner"]:
    if tok not in c:e.append("native editor escape missing "+tok)
if "TryHandleNativeCharacterEditorEscape" not in g:e.append("guard not wired")
print("Profile trait/ESC audit errors=%d"%len(e))
for q in e:print("ERROR:",q)
raise SystemExit(1 if e else 0)
