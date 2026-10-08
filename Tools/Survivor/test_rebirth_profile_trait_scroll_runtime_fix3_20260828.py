from pathlib import Path
import argparse
p=argparse.ArgumentParser();p.add_argument("--root",required=True);a=p.parse_args();r=Path(a.root);e=[]
m=(r/"Scripts/Survivor/UI/XUiC_RebirthSurvivorProfileManager.cs").read_text()
for tok in ["traitNativeInitialSyncFrames = 2;","if (traitNativeInitialSyncFrames > 0)","SetLabelColor(detailValues[slot], traitColor);","SetSpriteColor(detailIcons[slot], traitColor);"]:
    if tok not in m:e.append("missing "+tok)
block=m[m.find("private void UpdateTraitNativeScroll"):m.find("private void PollTraitNativeScroll")]
if block.count("RefreshNativeScrollView(traitNativeScrollView);")<2:e.append("native refresh not repeated")
print("Profile Trait runtime fix3 audit errors=%d"%len(e))
for x in e:print("ERROR:",x)
raise SystemExit(1 if e else 0)
