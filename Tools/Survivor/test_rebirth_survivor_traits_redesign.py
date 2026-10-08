#!/usr/bin/env python3
"""Revision 4 Traits-window redesign contract tests.

Static/preboot only. This validates the approved dual-pane creator implementation and
its menu/in-game parity; it does not claim a C# compile or runtime acceptance.
"""
from __future__ import annotations
import argparse, re, sys
from pathlib import Path
import xml.etree.ElementTree as ET

class T:
    def __init__(self): self.rows=[]
    def check(self,name,ok,detail=""): self.rows.append((name,bool(ok),detail))
    @property
    def ok(self): return all(x[1] for x in self.rows)
    def text(self):
        out=["REBIRTH Survivor Revision 4 Traits redesign vectors: "+("PASS" if self.ok else "FAIL")]
        for n,ok,d in self.rows: out.append(("PASS " if ok else "FAIL ")+n+(" — "+d if d else ""))
        out.append(f"pass={sum(1 for _,o,_ in self.rows if o)} fail={sum(1 for _,o,_ in self.rows if not o)} total={len(self.rows)}")
        out.append("compile_validation_claimed=False")
        return "\n".join(out)+"\n"

def names(path:Path):
    root=ET.parse(path).getroot()
    return {e.get("name"):e for e in root.iter() if e.get("name")}

def creator_blob(path:Path):
    s=path.read_text(encoding="utf-8",errors="replace")
    start=s.index('<window name="rebirthSurvivorCreatorWindow"')
    pat=re.compile(r'<(/?)window\b[^>]*?(\/?)>'); depth=0
    for m in pat.finditer(s,start):
        if m.group(1)=="/": depth-=1
        elif m.group(2)!="/": depth+=1
        if depth==0:return s[start:m.end()]
    return ""

def main():
    ap=argparse.ArgumentParser(); ap.add_argument("--root",type=Path,default=Path(__file__).resolve().parents[2]); a=ap.parse_args()
    root=a.root.resolve(); t=T()
    rels=["Config/XUi_Menu/windows.xml","Config/XUi_InGame/windows.xml"]
    layouts={rel:names(root/rel) for rel in rels}

    for rel,n in layouts.items():
        pfx=rel+"."
        t.check(pfx+"full_width_traits", n.get("creatorTraitPanel") is not None and n["creatorTraitPanel"].get("width")=="1744", "approved Traits step uses the full creator content width")
        t.check(pfx+"dual_panes", all(x in n for x in ["positiveTraitPane","negativeTraitPane","traitPointSummaryPanel","traitSelectedPanel","traitFocusedPanel"]), "left/center/right layout")
        t.check(pfx+"positive_rows", all(all(f"{base}{i}" in n for base in ["positiveTraitRow","positiveTraitIcon","positiveTraitName","positiveTraitSummary","positiveTraitPoints","positiveTraitAction","btnPositiveTrait"]) for i in range(7)), "7 positive/mixed rows")
        t.check(pfx+"negative_rows", all(all(f"{base}{i}" in n for base in ["negativeTraitRow","negativeTraitIcon","negativeTraitName","negativeTraitSummary","negativeTraitPoints","negativeTraitAction","btnNegativeTrait"]) for i in range(7)), "7 weakness rows")
        t.check(pfx+"independent_scrollbars", all(x in n for x in ["positiveTraitScrollTrackInput","positiveTraitScrollThumb","positiveTraitPageText","negativeTraitScrollTrackInput","negativeTraitScrollThumb","negativeTraitPageText"]) and all(x not in n for x in ["btnPositiveTraitPrev","btnPositiveTraitNext","btnNegativeTraitPrev","btnNegativeTraitNext"]), "each pane scrolls independently without Prev/Next paging buttons")
        cats=["All","Physical","Mental","Social","Lifestyle","Aptitudes"]
        t.check(pfx+"categories", all("btnTraitCategory"+x in n for x in cats), "ALL / PHYSICAL / MENTAL / SOCIAL / LIFESTYLE / APTITUDES")
        t.check(pfx+"point_accounting", all(x in n for x in ["traitAvailablePoints","traitBudgetText"]), "available and full budget remain visible")
        t.check(pfx+"selected_summary", all(x in n for x in ["traitSelectedHeading","traitSelectedText"]), "central selected-trait summary")
        t.check(pfx+"detail_panel", all(x in n for x in ["traitFocusedIcon","traitFocusedDetails"]), "central focused Trait details")
        t.check(pfx+"reset", "btnResetTraits" in n, "Reset Traits control")
        t.check(pfx+"global_navigation", all(x in n for x in ["btnCreatorPrevious","btnCreatorNext"]), "existing Back/Next workflow retained")

    t.check("creator.layouts_identical", creator_blob(root/rels[0])==creator_blob(root/rels[1]), "menu and in-game creator copies cannot drift")

    ctl=(root/"Scripts/Survivor/UI/XUiC_RebirthSurvivorCreator.cs").read_text(encoding="utf-8",errors="replace")
    vm=(root/"Scripts/Survivor/UI/RebirthSurvivorCreatorViewModel.cs").read_text(encoding="utf-8",errors="replace")
    ui=(root/"Scripts/Survivor/UI/RebirthSurvivorUiText.cs").read_text(encoding="utf-8",errors="replace")

    t.check("controller.side_rows", "private const int TraitSideRows = 6;" in ctl and "RenderTraitSide(positivePage" in ctl and "RenderTraitSide(negativePage" in ctl)
    t.check("controller.category_wiring", all(f'WireTraitCategory("btnTraitCategory{x}"' in ctl for x in ["All","Physical","Mental","Social","Lifestyle","Aptitudes"]))
    t.check("controller.side_click", "TraitSideRow_OnPressed" in ctl and "model.ToggleTrait(focusedTraitId" in ctl)
    t.check("controller.readonly_inspection", "if (!model.IsReadOnly)" in ctl[ctl.index("private void TraitSideRow_OnPressed"):ctl.index("private void TraitRow_OnPressed")], "focus is updated before mutation gate")
    t.check("controller.independent_scroll", all(x in ctl for x in ["PositiveTraitScroll","NegativeTraitScroll","PositiveTraitOffset","NegativeTraitOffset"]))
    t.check("controller.category_selection", "model.SetTraitCategoryFilter(filter);" in ctl and "focusedTraitId = string.Empty;" in ctl)
    t.check("controller.reset", "model.ClearTraits();" in ctl and "ResetTraits_OnPressed" in ctl)
    t.check("controller.details_full_width", "model.Step != RebirthSurvivorCreatorStep.Traits" in ctl, "generic right details panel is hidden on Traits")
    t.check("controller.live_points", "GetTraitPointsRemaining(model)" in ctl and "BuildTraitBudgetCompact(model)" in ctl)
    t.check("controller.selected_summary", "BuildSelectedTraitSummary(model)" in ctl)
    t.check("controller.focused_details", "BuildTraitDetails(model.GetTraitChoice(focused.Id), null)" in ctl)
    t.check("controller.mixed_badge", 'xuiRebirthSurvivorTraitBadgeMixed' in ctl)
    t.check("controller.aptitude_badge", 'xuiRebirthSurvivorTraitBadgeAptitude' in ctl)

    t.check("viewmodel.category_enum", all(x in vm for x in ["Physical = 1","Mental = 2","Social = 3","Lifestyle = 4","Aptitudes = 5"]))
    t.check("viewmodel.dual_pages", all(x in vm for x in ["GetPositiveTraitPage","GetNegativeTraitPage","GetPositiveTraitCount","GetNegativeTraitCount"]))
    t.check("viewmodel.negative_separation", "if (trait.Polarity != RebirthTraitPolarity.Negative) continue;" in vm and "if (trait.Polarity == RebirthTraitPolarity.Negative) continue;" in vm)
    t.check("viewmodel.mixed_preserved", "Mixed Traits remain" in vm and "trait.Polarity == RebirthTraitPolarity.Negative" in vm, "neutral authored Traits are not dropped by the two-pane layout")
    t.check("viewmodel.aptitudes_filter", "return aptitude || string.Equals(trait.Category, \"Skill Weakness\"" in vm)
    t.check("viewmodel.filter_resets_pages", "PositiveTraitOffset = 0;" in vm[vm.index("public void SetTraitCategoryFilter"):vm.index("public void ClearTraits") ] and "NegativeTraitOffset = 0;" in vm[vm.index("public void SetTraitCategoryFilter"):vm.index("public void ClearTraits")])
    t.check("viewmodel.clear_revalidates", "selectedTraits.Clear();" in vm[vm.index("public void ClearTraits"):vm.index("public RebirthDietDefinition[] GetDietPage")] and "Revalidate();" in vm[vm.index("public void ClearTraits"):vm.index("public RebirthDietDefinition[] GetDietPage")])
    t.check("ui.remaining_formula", "GetTraitBudgetBreakdown(model)" in ui[ui.index("public static int GetTraitPointsRemaining"):ui.index("public static string BuildSelectedTraitSummary")])
    t.check("ui.selected_groups", all(x in ui for x in ["List<RebirthTraitDefinition> positive","List<RebirthTraitDefinition> mixed","List<RebirthTraitDefinition> negative","List<RebirthTraitDefinition> aptitude"]))

    loc=(root/"Config/Localization.csv").read_text(encoding="utf-8-sig",errors="replace").splitlines()
    keys={line.split(",",1)[0].strip() for line in loc if "," in line}
    required={
        "xuiRebirthSurvivorTraitCategoryAll","xuiRebirthSurvivorTraitCategoryPhysical","xuiRebirthSurvivorTraitCategoryMental","xuiRebirthSurvivorTraitCategorySocial","xuiRebirthSurvivorTraitCategoryLifestyle","xuiRebirthSurvivorTraitCategoryAptitudes",
        "xuiRebirthSurvivorPositiveMixedTraits","xuiRebirthSurvivorPositiveMixedTraitsHelp","xuiRebirthSurvivorNegativeTraitsWeaknesses","xuiRebirthSurvivorNegativeTraitsHelp","xuiRebirthSurvivorTraitPointsAvailable","xuiRebirthSurvivorSelectedTraits","xuiRebirthSurvivorResetTraits","xuiRebirthSurvivorNoTraitsSelected"}
    t.check("localization.redesign", not (required-keys), "missing="+str(sorted(required-keys)))

    print(t.text(),end="")
    return 0 if t.ok else 1

if __name__=="__main__": sys.exit(main())
