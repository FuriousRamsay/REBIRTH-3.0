from pathlib import Path
from lxml import etree
import argparse
p=argparse.ArgumentParser();p.add_argument("--root",required=True);a=p.parse_args();r=Path(a.root);e=[]
x=(r/"Config/XUi_Menu/windows.xml").read_text()
m=(r/"Scripts/Survivor/UI/XUiC_RebirthSurvivorProfileManager.cs").read_text()
for tok in ["profileTraitNativeScrollHost","profileTraitNativeScrollView","profileTraitNativeScrollProxy","profileTraitScrollCapture","<defaultscrollbar/>"]:
    if tok not in x:e.append("xml missing "+tok)
for tok in ["WireScroll(traitScrollCapture, TraitListScroll)","traitNativeScrollHost.ViewComponent.IsVisible = true","PollTraitNativeScroll","TryGetNativeScrollValue","UpdateTraitNativeScroll"]:
    if tok not in m:e.append("manager missing "+tok)
if 'xuiRebirthSurvivorSelectedTraitsDiet' in x:e.append("old traits/diet column heading remains")
print("Profile Trait runtime scrollbar audit errors=%d"%len(e))
for q in e:print("ERROR:",q)
raise SystemExit(1 if e else 0)
