from pathlib import Path
from lxml import etree
import argparse,re
p=argparse.ArgumentParser();p.add_argument("--root",required=True);a=p.parse_args();r=Path(a.root);e=[]
x=(r/"Config/XUi_Menu/windows.xml").read_text(encoding="utf-8")
m=(r/"Scripts/Survivor/UI/XUiC_RebirthSurvivorProfileManager.cs").read_text(encoding="utf-8")
g=(r/"Scripts/Survivor/Progression/Explorer/RebirthProgressionGraphRegistry.cs").read_text(encoding="utf-8")
u=(r/"Scripts/Survivor/Progression/Explorer/RebirthProgressionExplorerUiProjection.cs").read_text(encoding="utf-8")
for tok in ["profileTraitNativeScrollHost","profileTraitNativeScrollView","profileTraitNativeScrollProxy"]:
    if tok not in x:e.append("missing native trait scroll "+tok)
for tok in ["PollTraitNativeScroll","UpdateTraitNativeScroll","CompactStartingSkillSummary"]:
    if tok not in m:e.append("profile manager missing "+tok)
if "CompactTraitSummary(profile)" in m[m.find("BuildProfileRowSummary"):m.find("CompactTraitSummary")]:e.append("profile card still summarizes traits")
recipe_method=g[g.find("private static void AddRecipeKnowledgeEdges"):g.find("private static void AddCapabilityEdges")]
if "RebirthProgressionGraphEdgeType.RequiresKnowledge" in recipe_method or "RebirthProgressionGraphEdgeType.UnlocksRecipe" in recipe_method:e.append("legacy recipe method still emits duplicate gate edges")
cap=g[g.find("private static void AddRequirementEdges"):g.find("private static string ResolveCapabilityTargetNode")]
if "capability unlock" in cap:e.append("capability knowledge requirement still emits redundant unlock edge")
for tok in ["BuildRecipeDescription","Craft at:","No "," Skill minimum is authored for this recipe"]:
    if tok not in g:e.append("recipe details missing "+tok)
for tok in ["xuiRebirthProgressionExplorerRequiresKnowledge","xuiRebirthProgressionExplorerTrainsSkill"]:
    if tok not in u:e.append("projection missing player-facing relationship "+tok)
print("Profile/recipe clarity audit errors=%d"%len(e))
for q in e:print("ERROR:",q)
raise SystemExit(1 if e else 0)
