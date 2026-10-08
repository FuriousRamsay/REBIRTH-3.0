from pathlib import Path
import argparse
p=argparse.ArgumentParser();p.add_argument("--root",required=True);a=p.parse_args();r=Path(a.root);e=[]
m=(r/"Scripts/Survivor/UI/XUiC_RebirthSurvivorProfileManager.cs").read_text()
block=m[m.find("private static void SetSpriteColor"):m.find("private static void SetSprite(")]
for tok in ["sprite.Color = color;","sprite.SetColorImmediately(color);"]:
    if tok not in block:e.append("SetSpriteColor missing "+tok)
renderer=m[m.find("private void RenderDetailSection"):m.find("private sealed class ProfileDetailDisplay")]
if "SetSpriteColor(detailIcons[slot], traitColor);" not in renderer:e.append("trait renderer not using immediate color helper")
print("Profile Trait icon immediate-color audit errors=%d"%len(e))
for q in e:print("ERROR:",q)
raise SystemExit(1 if e else 0)
