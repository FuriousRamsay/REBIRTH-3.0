#!/usr/bin/env python3
"""Post-Revision-2 Chunk 9 creator UI contract tests.

Static/preboot only. This does not claim a C# compile or runtime acceptance.
"""
from __future__ import annotations
import argparse, json, re, sys
from pathlib import Path
import xml.etree.ElementTree as ET

STEPS=["btnStepProfile","btnStepBackground","btnStepDiet","btnStepTraits","btnStepReview"]

class T:
    def __init__(self): self.rows=[]
    def check(self,name,ok,detail=""):
        self.rows.append((name,bool(ok),detail))
    @property
    def ok(self): return all(x[1] for x in self.rows)
    def text(self):
        out=["REBIRTH Survivor Chunk 9 creator UI vectors: "+("PASS" if self.ok else "FAIL")]
        for n,ok,d in self.rows: out.append(("PASS " if ok else "FAIL ")+n+(" — "+d if d else ""))
        out.append(f"pass={sum(1 for _,o,_ in self.rows if o)} fail={sum(1 for _,o,_ in self.rows if not o)} total={len(self.rows)}")
        out.append("compile_validation_claimed=False")
        return "\n".join(out)+"\n"
    def obj(self):
        return {"suite":"REBIRTH Survivor Chunk 9 creator UI vectors","result":"PASS" if self.ok else "FAIL","compile_validation_claimed":False,
                "pass":sum(1 for _,o,_ in self.rows if o),"fail":sum(1 for _,o,_ in self.rows if not o),"total":len(self.rows),
                "checks":[{"name":n,"ok":o,"detail":d} for n,o,d in self.rows]}

def load_xml(path:Path): return ET.parse(path).getroot()
def by_name(root): return {e.get("name"):e for e in root.iter() if e.get("name")}
def x_pos(e):
    try:return int(e.get("pos","").split(",")[0])
    except:return None

def main():
    ap=argparse.ArgumentParser(); ap.add_argument("--root",type=Path,default=Path(__file__).resolve().parents[2]); ap.add_argument("--json",type=Path); ap.add_argument("--text",type=Path)
    a=ap.parse_args(); root=a.root.resolve(); t=T()

    layouts={}
    for rel in ["Config/XUi_Menu/windows.xml","Config/XUi_InGame/windows.xml"]:
        p=root/rel; xr=load_xml(p); names=by_name(xr); layouts[rel]=(xr,names)
        positions=[x_pos(names.get(s)) if names.get(s) is not None else None for s in STEPS]
        t.check(rel+".step_order", None not in positions and positions==sorted(positions) and len(set(positions))==5, str(list(zip(STEPS,positions))))
        t.check(rel+".trait_budget", "traitBudgetText" in names, "always-visible accounting label")
        diet_ids=[]
        for i in range(4): diet_ids += [f"dietIcon{i}",f"dietName{i}",f"dietSummary{i}",f"dietPoints{i}",f"dietSelection{i}",f"btnDiet{i}"]
        t.check(rel+".diet_cards", all(x in names for x in diet_ids), "4 structured Diet cards")
        review_fixed=["creatorReviewPanel","reviewIdentityPanel","reviewSummaryChoices","creatorReviewDetailsContent","reviewBackgroundArt","reviewProfileName","reviewBackgroundName","reviewDietName","reviewSummaryDescription","reviewSummaryPlayerProfileMeta","reviewPlayerPreview","reviewSkillsHeading","reviewStartingItemsHeading","creatorProfileNameLabel","survivorProfileName"]
        t.check(rel+".review_fixed", all(x in names for x in review_fixed), "Profile Summary-style Review regions")
        t.check(rel+".review_skill_capacity", all(f"reviewSkillRow{i}" in names and f"reviewSkillIcon{i}" in names and f"reviewSkillName{i}" in names and f"reviewSkillValue{i}" in names for i in range(10)), "10 combined Skills/Knowledge slots available")
        t.check(rel+".review_knowledge_capacity", all(f"reviewKnowledgeRow{i}" in names and f"reviewKnowledgeIcon{i}" in names and f"reviewKnowledgeName{i}" in names and f"reviewKnowledgeValue{i}" in names for i in range(2)), "2 Knowledge rows")
        t.check(rel+".review_trait_capacity", all(f"reviewTraitRow{i}" in names and f"reviewTraitIcon{i}" in names and f"reviewTraitName{i}" in names and f"reviewTraitValue{i}" in names for i in range(8)), "8 Trait rows")
        t.check(rel+".review_weakness_capacity", all(f"reviewWeaknessRow{i}" in names and f"reviewWeaknessIcon{i}" in names and f"reviewWeaknessName{i}" in names and f"reviewWeaknessValue{i}" in names for i in range(2)), "2 Experience weakness rows")
        t.check(rel+".review_starting_item_capacity", all(f"reviewStartingItemRow{i}" in names and f"reviewStartingItemIcon{i}" in names and f"reviewStartingItemName{i}" in names and f"reviewStartingItemMeta{i}" in names for i in range(8)), "8 icon Starting Item rows beneath Weaknesses")
        review_panel=names.get("creatorReviewPanel")
        t.check(rel+".review_full_width", review_panel is not None and review_panel.get("width")=="1744", "Review replaces the old two-pane layout")
        t.check(rel+".review_no_budget_card", "reviewBudgetText" not in names and "reviewDietPoints" not in names, "no point-budget/bonus card on final Review")

    # The two creator window copies must not drift again.
    def creator_blob(rel):
        p=root/rel; s=p.read_text(encoding="utf-8",errors="replace")
        start=s.index('<window name="rebirthSurvivorCreatorWindow"')
        pat=re.compile(r'<(/?)window\b[^>]*?(\/?)>'); depth=0
        for m in pat.finditer(s,start):
            if m.group(1)=="/": depth-=1
            elif m.group(2)!="/": depth+=1
            if depth==0:return s[start:m.end()]
        return ""
    t.check("creator.layouts_identical", creator_blob("Config/XUi_Menu/windows.xml")==creator_blob("Config/XUi_InGame/windows.xml"), "same creator topology in menu and in-game")

    vm=(root/"Scripts/Survivor/UI/RebirthSurvivorCreatorViewModel.cs").read_text(encoding="utf-8",errors="replace")
    ui=(root/"Scripts/Survivor/UI/RebirthSurvivorUiText.cs").read_text(encoding="utf-8",errors="replace")
    ctl=(root/"Scripts/Survivor/UI/XUiC_RebirthSurvivorCreator.cs").read_text(encoding="utf-8",errors="replace")
    apt=(root/"Scripts/Survivor/Definitions/RebirthSkillAptitudeTraitFactory.cs").read_text(encoding="utf-8",errors="replace")

    t.check("traits.unavailable_visible", "if (!IsEligibleForBackground(trait, background)" not in vm[vm.index("private List<RebirthTraitDefinition> GetVisibleTraits()"):vm.index("private bool MatchesFilter",vm.index("private List<RebirthTraitDefinition> GetVisibleTraits()"))], "background-locked options are retained for explanation")
    t.check("traits.deferred_hidden", "trait.Availability == RebirthDefinitionAvailability.Deferred" in vm, "deferred launch choices remain hidden")
    t.check("traits.positive_excludes_aptitudes", "trait.Polarity == RebirthTraitPolarity.Positive && !RebirthSkillAptitudeTraitFactory.IsAptitude(trait)" in vm, "Aptitudes have their own category")
    t.check("traits.aptitude_filter", "case RebirthSurvivorTraitFilter.SkillAptitudes" in vm and "RebirthSkillAptitudeTraitFactory.IsAptitude(trait)" in vm, "explicit aptitude filter")
    t.check("traits.visual_badges", all(x in ui for x in ["TraitBadgeAptitude","TraitBadgeBackground","TraitBadgePositive","TraitBadgeNegative","TraitBadgeMixed","TraitStateLocked","TraitStateDietConflict","TraitStateConflict"]), "positive/negative/aptitude/background/conflict distinction")
    t.check("traits.full_budget_formula", all(x in ui for x in ["NegativeApplied","PositiveSpent","AptitudeSpent","b.Base + b.Background + b.Diet + b.NegativeApplied - b.PositiveSpent - b.AptitudeSpent"]), "Base + Background + Diet + Negative - Positive - Aptitudes")
    t.check("traits.budget_bound_to_screen", "SetLabel(traitBudgetText, RebirthSurvivorUiText.BuildTraitBudgetCompact(model)" in ctl, "budget visible independent of focused detail")

    t.check("diet.real_rule_summary", "string rule = (diet.CompositionRule ?? string.Empty)" in ui and "BuildDietCompatibilityShort(diet)" in ctl, "card summary derives from authored composition rule")
    t.check("diet.point_adjustment", "SetLabel(dietPoints[i]" in ctl and "SignedPoints(diet.Points)" in ctl, "signed adjustment shown per card")
    t.check("diet.consequences", all(x in ui for x in ["physical Nutrition/hydration","when nutrients are absorbed","Diet Satisfaction/Mood"]), "runtime consequences are concise, concrete, and scoped")
    t.check("diet.no_blocking_claim", "Restricted food is still edible" in ui, "presentation does not claim Diet blocks food")

    t.check("review.separate_trait_groups", all(x in ui for x in ["ReviewPositiveTraits","ReviewWeaknesses","ReviewMixedTraits","ReviewAptitudes"]), "traits/weaknesses/aptitudes summarized separately")
    t.check("review.player_preview", 'TrySelectPlayerProfileForPreview(this, "reviewPlayerPreview"' in ctl, "selected base Player Profile appearance is reused in a live SDCS preview")
    t.check("review.experience_skills_only", "bg.StartingSkills" in ctl and "Trait-derived aptitude effects are not" in ctl, "Review mirrors Profile Summary instead of duplicating Trait aptitude effects")
    t.check("review.skill_icons", "RebirthSkillAptitudeTraitFactory.SkillIconKey(entry.SkillId)" in ctl, "same real Skill icon mapping used by Experience rows")
    t.check("review.separate_weaknesses", "positiveSkills.Add(display); else weaknesses.Add(display);" in ctl and "reviewWeaknessRows" in ctl, "Experience weaknesses have their own right column")
    t.check("review.starting_knowledge", "bg.StartingKnowledgeIds" in ctl and 'SetLabel(reviewKnowledgeValues[i], "[C49EFF]KNOWN[-]")' in ctl, "Experience Knowledge is appended to Skills & Knowledge")
    t.check("review.traits_column", "model.SelectedTraitIds" in ctl and "reviewTraitRows" in ctl, "selected Traits occupy the middle column")
    t.check("review.no_final_budget", "BuildTraitBudgetSummary(model)" not in ctl[ctl.index("private void RenderReviewVisual()"):ctl.index("private void RenderBackgroundVisualDetails")], "point accounting is omitted from final Review")
    t.check("review.full_width_panel", "SetVisible(detailsPanel, model.Step == RebirthSurvivorCreatorStep.Background);" in ctl, "old right Details pane is hidden on Review")
    t.check("review.starting_items_icon_rows", "reviewStartingItemRows" in ctl and "SetStartingItemIcon(reviewStartingItemIcons[i], item)" in ctl, "Starting Items use compact icon rows beneath Weaknesses")

    # 41 current Skills and their actual icons.
    prog=load_xml(root/"Config/_Survivor/progression.xml")
    skill_ids=[e.get("id") for e in prog.findall(".//skills/skill")]
    settings=load_xml(root/"UIAtlases/RebirthSurvivorIcons/settings.xml")
    sprites={e.get("name") for e in settings.findall(".//sprite")}
    missing=[]
    for sid in skill_ids:
        key="rb_skill_"+sid.removeprefix("skill.").replace(".","_").replace("-","_")
        if key not in sprites or not (root/"UIAtlases/RebirthSurvivorIcons"/(key+".png")).exists(): missing.append((sid,key))
    t.check("review.42_skill_icons_authored", len(skill_ids)==42 and not missing, f"skills={len(skill_ids)} missing={missing}")
    t.check("aptitudes.use_actual_skill_icons", "SkillIconKey(skill.Id)" in apt, "generated Aptitudes use the same Skill icon key")

    # Localized strings newly exposed by Chunk 9.
    loc=(root/"Config/Localization.csv").read_text(encoding="utf-8-sig",errors="replace").splitlines()
    keys={line.split(",",1)[0].strip() for line in loc if "," in line}
    required={
        "xuiRebirthSurvivorTraitBudgetPositiveTraits","xuiRebirthSurvivorTraitBudgetAptitudes","xuiRebirthSurvivorReviewIdentity",
        "xuiRebirthSurvivorReviewPositiveTraits","xuiRebirthSurvivorReviewWeaknesses","xuiRebirthSurvivorReviewMixedTraits","xuiRebirthSurvivorReviewAptitudes",
        "xuiRebirthSurvivorDietConsequences","xuiRebirthSurvivorDietShortUnrestricted","xuiRebirthSurvivorDietShortVegetarian","xuiRebirthSurvivorDietShortCarnivore","xuiRebirthSurvivorDietShortVegan",
        "xuiRebirthSurvivorTraitBadgeAptitude","xuiRebirthSurvivorTraitBadgeBackground","xuiRebirthSurvivorTraitBadgePositive","xuiRebirthSurvivorTraitBadgeNegative","xuiRebirthSurvivorTraitBadgeMixed",
        "xuiRebirthSurvivorTraitStateLocked","xuiRebirthSurvivorTraitStateDietConflict","xuiRebirthSurvivorTraitStateConflict",
        "xuiRebirthSurvivorTraitCategoryAll","xuiRebirthSurvivorTraitCategoryPhysical","xuiRebirthSurvivorTraitCategoryMental","xuiRebirthSurvivorTraitCategorySocial","xuiRebirthSurvivorTraitCategoryLifestyle","xuiRebirthSurvivorTraitCategoryAptitudes",
        "xuiRebirthSurvivorPositiveMixedTraits","xuiRebirthSurvivorNegativeTraitsWeaknesses","xuiRebirthSurvivorTraitPointsAvailable","xuiRebirthSurvivorSelectedTraits","xuiRebirthSurvivorResetTraits","xuiRebirthSurvivorNoTraitsSelected"}
    t.check("chunk9.localization", not (required-keys), "missing="+str(sorted(required-keys)))

    out=t.text(); print(out,end="")
    if a.text: a.text.parent.mkdir(parents=True,exist_ok=True); a.text.write_text(out,encoding="utf-8")
    if a.json: a.json.parent.mkdir(parents=True,exist_ok=True); a.json.write_text(json.dumps(t.obj(),indent=2)+"\n",encoding="utf-8")
    return 0 if t.ok else 1
if __name__=="__main__": sys.exit(main())
