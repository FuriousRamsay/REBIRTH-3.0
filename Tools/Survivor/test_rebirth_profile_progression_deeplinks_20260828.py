from pathlib import Path
from lxml import etree
import argparse
p=argparse.ArgumentParser();p.add_argument("--root",required=True);a=p.parse_args();r=Path(a.root);e=[]
x=etree.parse(str(r/"Config/XUi_Menu/windows.xml"))
m=(r/"Scripts/Survivor/UI/XUiC_RebirthSurvivorProfileManager.cs").read_text()
for i in range(18):
    if not x.xpath("//*[@name='btnProfileDetail%d']"%i):e.append("missing detail button %d"%i)
for tok in ["ProfileDetail_OnPressed","OpenProfileProgressionExplorer","ProgressionTargetId = skill.SkillId","ProgressionTargetId = id","RETURN TO SURVIVOR PROFILES"]:
    if tok not in m:e.append("manager missing "+tok)
if "ProgressionTargetId = string.Empty" not in m:e.append("trait non-progression safeguard missing")
print("Profile progression deep-link audit errors=%d"%len(e))
for q in e:print("ERROR:",q)
raise SystemExit(1 if e else 0)
