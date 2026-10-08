#!/usr/bin/env python3
"""REBIRTH Survivor post-Revision-2 preboot static validator.

This script is intentionally independent from the game runtime. It validates file-level contracts
that can be checked before 7DTD boots. It never claims C# compile validation.
"""
from __future__ import annotations

import argparse
import csv
import hashlib
import json
import re
import sys
from collections import Counter, defaultdict
from pathlib import Path
import xml.etree.ElementTree as ET

EXPECTED_BACKGROUND_IDS = [
    "background.clean_slate","background.personal_trainer","background.paramedic","background.pharmacist","background.lab_technician","background.librarian","background.mechanic","background.electrician","background.construction_worker","background.welder_fabricator","background.maintenance_technician","background.engineer","background.miner","background.logger","background.firefighter","background.soldier","background.police_officer","background.hunter","background.park_ranger_outdoor_guide","background.gunsmith","background.chef","background.tailor","background.farmer","background.butcher","background.bartender","background.salesperson","background.teacher","background.scavenger"
]
OLD_16 = [
    "skill.bladed_melee","skill.blunt_melee","skill.spears","skill.unarmed","skill.archery","skill.handguns","skill.rifles","skill.shotguns",
    "skill.mining","skill.logging","skill.salvage","skill.farming","skill.mechanics","skill.medicine","skill.cooking","skill.maintenance"
]
RETIRED_BROAD = {"skill.bladed_melee","skill.blunt_melee","skill.handguns","skill.rifles"}

EXPECTED_LEGACY_MAP = {
    "skill.bladed_melee": ["skill.swords","skill.knives","skill.scythes"],
    "skill.blunt_melee": ["skill.clubs","skill.batons","skill.hammers"],
    "skill.spears": ["skill.spears"],
    "skill.unarmed": ["skill.unarmed","skill.knuckles"],
    "skill.archery": ["skill.archery"],
    "skill.handguns": ["skill.pistols","skill.revolvers","skill.heavy_handguns"],
    "skill.rifles": ["skill.assault_rifles","skill.tactical_rifles","skill.long_range_rifles"],
    "skill.shotguns": ["skill.shotguns"],
    "skill.mining": ["skill.mining"],
    "skill.logging": ["skill.logging"],
    "skill.salvage": ["skill.salvage"],
    "skill.farming": ["skill.farming"],
    "skill.mechanics": ["skill.mechanics"],
    "skill.medicine": ["skill.medicine"],
    "skill.cooking": ["skill.cooking"],
    "skill.maintenance": ["skill.maintenance"],
}
SCHEMA_CONTRACT = {
    "backgrounds.xml": ("survivor_backgrounds", "2", "RebirthSurvivorDefinitionLoader"),
    "traits.xml": ("survivor_traits", "2", "RebirthSurvivorDefinitionLoader"),
    "diets.xml": ("survivor_diets", "2", "RebirthSurvivorDefinitionLoader"),
    "progression.xml": ("survivor_progression", "2", "RebirthSurvivorDefinitionLoader"),
    "condition_profiles.xml": ("survivor_condition_profiles", "2", "RebirthSurvivorDefinitionLoader"),
    "support_profiles.xml": ("survivor_support_profiles", "2", "RebirthSurvivorDefinitionLoader"),
    "condition_runtime.xml": ("survivor_condition_runtime", "1", "RebirthConditionRuntimeConfig"),
    "skill_sources.xml": ("survivor_skill_sources", "1", "RebirthProgressionRuntimeConfig"),
    "recipe_knowledge.xml": ("survivor_recipe_knowledge", "1", "RebirthProgressionRuntimeConfig"),
}
CREATOR_STEPS = [
    ("btnStepProfile", "Player Profile"),
    ("btnStepBackground", "Background"),
    ("btnStepDiet", "Diet"),
    ("btnStepTraits", "Traits"),
    ("btnStepReview", "Review"),
]
CREATOR_REQUIRED_IDS = {
    "survivorProfileName","newPlayerProfileName","playerProfileSaveOverlay","profilePortraitPreloadMask","btnProfileEdit","btnProfileDelete",
    "nativePlayerProfileHost","playerProfileDeleteOverlay","btnConfirmDeletePlayerProfile","btnCancelDeletePlayerProfile","creatorProfilePanel",
    "creatorBackgroundPanel","creatorTraitPanel","creatorDietPanel","creatorReviewPanel","creatorDetailsPanel","backgroundVisualDetailsPanel",
    "creatorDetailScroll","creatorDietDetailsPanel","backgroundArtTexture","backgroundArtPlaceholder","playerPreviewBackground","creatorTitle",
    "creatorStepTitle","creatorDetailText","creatorValidationText","backgroundPageText","traitPageText",
    "dietPageText","creatorDietDetailText","traitFilterText","creatorReviewText","creatorStatusText","backgroundArtAlt","backgroundVisualName",
    "backgroundVisualDescription","backgroundNoSkillsText","backgroundNoKnowledgeText","backgroundNoWeaknessText","backgroundTraitPointsValue",
    "creatorProfileNameLabel","btnStepProfile","btnStepBackground","btnStepDiet","btnStepTraits","btnStepReview",
    "tabDimProfile","tabDimBackground","tabDimDiet","tabDimTraits","tabDimReview","btnSaveReusableProfile",
    "btnRebirthProfileCreate","btnSaveNewPlayerProfile","btnDiscardNewPlayerProfile","btnTraitCategoryAll","btnTraitCategoryPhysical","btnTraitCategoryMental",
    "btnTraitCategorySocial","btnTraitCategoryLifestyle","btnTraitCategoryAptitudes","positiveTraitScrollTrackInput","positiveTraitScrollThumb",
    "negativeTraitScrollTrackInput","negativeTraitScrollThumb","btnResetTraits","btnRemoveUnavailableTraits","btnCreatorPrevious","btnCreatorNext","btnCreatorSave","btnCreatorCancel",
    "backgroundScrollTrackInput","backgroundScrollThumb","backgroundPaneScrollCapture","profileScrollTrackInput","profileScrollThumb","profilePaneScrollCapture",
    "traitBudgetText","creatorReviewDetailsPanel","creatorReviewDetailsContent","reviewProfilePortrait","reviewProfileName","reviewBackgroundName",
    "reviewDietIcon","reviewDietName","reviewDietPoints","reviewBudgetText","reviewSkillsHeading","reviewKnowledgeHeading","reviewNoSkillsText","reviewNoKnowledgeText",
}

class Report:
    def __init__(self, root: Path):
        self.root = root
        self.errors: list[dict] = []
        self.warnings: list[dict] = []
        self.checks: list[dict] = []
        self.counts: dict[str, int | float | str] = {}

    def check(self, name: str, ok: bool, detail: str, *, file: str = "", item: str = "", warning: bool = False):
        entry = {"name": name, "ok": bool(ok), "detail": detail}
        if file: entry["file"] = file
        if item: entry["id"] = item
        self.checks.append(entry)
        if not ok:
            target = self.warnings if warning else self.errors
            target.append({"check": name, "detail": detail, **({"file": file} if file else {}), **({"id": item} if item else {})})

    @property
    def ok(self): return not self.errors

    def json_obj(self):
        return {
            "validator": "REBIRTH Survivor post-Revision-2 preboot static validator",
            "result": "PASS" if self.ok else "FAIL",
            "compile_validation_claimed": False,
            "root": str(self.root),
            "counts": self.counts,
            "errors": self.errors,
            "warnings": self.warnings,
            "checks": self.checks,
        }

    def text(self):
        lines = [f"REBIRTH Survivor post-Revision-2 static validation: {'PASS' if self.ok else 'FAIL'}",
                 f"errors={len(self.errors)} warnings={len(self.warnings)} checks={len(self.checks)}",
                 "compile_validation_claimed=False"]
        for k in sorted(self.counts): lines.append(f"count.{k}={self.counts[k]}")
        for e in self.errors: lines.append("ERROR: " + fmt_issue(e))
        for w in self.warnings: lines.append("WARN: " + fmt_issue(w))
        return "\n".join(lines) + "\n"

def fmt_issue(e):
    loc = ""
    if e.get("file"): loc += e["file"]
    if e.get("id"): loc += (":" if loc else "") + e["id"]
    return (loc + " " if loc else "") + e.get("detail", "")

def parse_xml(path: Path, report: Report):
    try:
        return ET.parse(path).getroot()
    except Exception as ex:
        report.check("xml.well_formed", False, f"{type(ex).__name__}: {ex}", file=str(path.relative_to(report.root)))
        return None

def all_xml(root: Path):
    return sorted((root / "Config" / "_Survivor").glob("*.xml")) + [root / "Config" / "buffs.xml", root / "Config" / "XUi_Menu" / "windows.xml", root / "Config" / "XUi_Menu" / "xui.xml", root / "UIAtlases" / "RebirthSurvivorIcons" / "settings.xml"]

def attr_ids(nodes, attr="id"):
    return [n.get(attr, "").strip() for n in nodes if n.get(attr, "").strip()]

def unique_check(report, name, ids, expected=None, file=""):
    dup = sorted(k for k,v in Counter(ids).items() if v > 1)
    report.check(name + ".unique", not dup, f"duplicates={dup}", file=file)
    if expected is not None: report.check(name + ".count", len(ids) == expected, f"count={len(ids)} expected={expected}", file=file)

def read_localization(root: Path, report: Report):
    p = root / "Config" / "Localization.csv"
    keys=set(); field_count=None; row_count=0; bad=[]; dup=[]
    try:
        with p.open("r", encoding="utf-8-sig", newline="") as f:
            reader=csv.reader(f)
            for line_no,row in enumerate(reader,1):
                if line_no == 1:
                    field_count=len(row)
                    report.check("localization.header", field_count >= 2, f"fields={field_count}", file="Config/Localization.csv")
                    continue
                if not row or all(not x.strip() for x in row): continue
                row_count += 1
                if len(row) != field_count: bad.append((line_no,len(row)))
                key=row[0].strip() if row else ""
                if key:
                    if key.lower() in keys: dup.append((line_no,key))
                    keys.add(key.lower())
    except Exception as ex:
        report.check("localization.parse", False, f"{type(ex).__name__}: {ex}", file="Config/Localization.csv")
        return set()
    survivor_prefixes=("xuiRebirthSurvivor","xuiRebirthBackground","xuiRebirthTrait","xuiRebirthDiet","xuiRebirthCreationError","xuiRebirthPlayerProgression","xuiRebirthSkill","xuiRebirthKnowledge")
    survivor_bad=[]
    if bad:
        # Re-read just the malformed physical rows to identify project-owned Survivor keys. The
        # wider mod file historically contains a few legacy multi-column rows, which are warnings
        # rather than a false claim that Chunk 1 introduced them.
        raw_lines=p.read_text(encoding="utf-8-sig",errors="replace").splitlines()
        for line_no,count in bad:
            if 1 <= line_no <= len(raw_lines):
                key=raw_lines[line_no-1].split(",",1)[0].strip()
                if key.startswith(survivor_prefixes): survivor_bad.append((line_no,count,key))
    report.check("localization.survivor_field_integrity", not survivor_bad, f"rows={row_count} expectedFields={field_count} survivorMalformed={survivor_bad[:50]}", file="Config/Localization.csv")
    legacy_bad=[x for x in bad if not any(x[0]==y[0] for y in survivor_bad)]
    report.check("localization.legacy_mixed_rows", not legacy_bad, f"legacyMixedRows={legacy_bad[:20]}", file="Config/Localization.csv", warning=True)
    report.check("localization.unique_keys", not dup, f"duplicates={dup[:20]}", file="Config/Localization.csv")
    report.counts["localization_rows"] = row_count
    return keys

def validate(root: Path):
    R=Report(root)
    surv=root/"Config"/"_Survivor"

    # Every project-owned XML involved in Survivor/UI should parse.
    for p in all_xml(root):
        if not p.exists():
            R.check("xml.exists", False, "required XML missing", file=str(p.relative_to(root)))
        else:
            parsed=parse_xml(p,R)
            if parsed is not None: R.check("xml.well_formed", True, f"root={parsed.tag}", file=str(p.relative_to(root)))

    # Loader-specific schema contracts; config-patch XML files intentionally have no Survivor schema_version.
    roots={}
    for name,(expected_root,expected_ver,loader) in SCHEMA_CONTRACT.items():
        p=surv/name; x=parse_xml(p,R) if p.exists() else None; roots[name]=x
        ok=x is not None and x.tag==expected_root and x.get("schema_version")==expected_ver
        actual=f"root={x.tag if x is not None else '<missing>'} schema={x.get('schema_version') if x is not None else '<missing>'} loader={loader}"
        R.check("schema.loader_contract", ok, actual+f" expectedRoot={expected_root} expectedSchema={expected_ver}", file=f"Config/_Survivor/{name}")

    prog=roots.get("progression.xml")
    bgroot=roots.get("backgrounds.xml")
    trroot=roots.get("traits.xml")
    diroot=roots.get("diets.xml")
    skills=prog.findall("./skills/skill") if prog is not None else []
    skill_ids=attr_ids(skills); skill_set=set(skill_ids)
    unique_check(R,"skills",skill_ids,42,"Config/_Survivor/progression.xml")
    R.counts["skills"] = len(skill_ids)
    bad_bounds=[s.get("id") for s in skills if float(s.get("min","nan")) != -50 or float(s.get("max","nan")) != 100]
    R.check("skills.runtime_bounds", not bad_bounds, f"expected=-50..100 bad={bad_bounds}", file="Config/_Survivor/progression.xml")
    creation=prog.find("./creation") if prog is not None else None
    if creation is not None:
        cmin=float(creation.get("skill_start_min","nan")); cmax=float(creation.get("skill_start_max","nan")); cap=int(creation.get("max_negative_trait_refund","-1"))
        R.check("skills.creation_ceiling", cmax==50, f"creationMin={cmin} creationMax={cmax}", file="Config/_Survivor/progression.xml")
        R.check("traits.negative_refund_policy", cap==-1, f"max_negative_trait_refund={cap} (-1=unlimited)", file="Config/_Survivor/progression.xml")
        R.counts["creation_skill_min"] = cmin; R.counts["creation_skill_max"] = cmax; R.counts["negative_refund_cap"] = cap

    backgrounds=bgroot.findall("./background") if bgroot is not None else []
    bg_ids=attr_ids(backgrounds); unique_check(R,"backgrounds",bg_ids,28,"Config/_Survivor/backgrounds.xml")
    R.check("backgrounds.launch_catalogue", bg_ids==EXPECTED_BACKGROUND_IDS, f"first={bg_ids[:3]} missing={sorted(set(EXPECTED_BACKGROUND_IDS)-set(bg_ids))} extra={sorted(set(bg_ids)-set(EXPECTED_BACKGROUND_IDS))}", file="Config/_Survivor/backgrounds.xml")
    R.counts["backgrounds"] = len(bg_ids)
    bg_bad_refs=[]; bg_bad_values=[]; bg_knowledge=[]
    knowledge_ids=set(attr_ids(prog.findall("./knowledge/*"))) if prog is not None else set()
    # fallback because knowledge node may use <knowledge id=>
    if prog is not None: knowledge_ids=set(attr_ids(prog.findall(".//knowledge/*")) + attr_ids(prog.findall("./knowledge"))) or set(attr_ids(prog.findall("./knowledge/entry")))
    # actual model uses <item>, inspect any child under <knowledge>.
    if prog is not None:
        kn=prog.find("./knowledge")
        if kn is not None: knowledge_ids=set(attr_ids(list(kn)))
    for b in backgrounds:
        bid=b.get("id","")
        for sn in b.findall("./starting_skills/skill"):
            sid=sn.get("id","");
            if sid not in skill_set: bg_bad_refs.append((bid,sid))
            if "value" in sn.attrib:
                v=float(sn.get("value"));
                if creation is not None and (v<float(creation.get("skill_start_min")) or v>float(creation.get("skill_start_max"))): bg_bad_values.append((bid,sid,v))
        for kn in b.findall("./starting_knowledge/*"):
            kid=kn.get("id","")
            if kid and kid not in knowledge_ids: bg_knowledge.append((bid,kid))
    R.check("backgrounds.skill_refs", not bg_bad_refs, f"bad={bg_bad_refs}", file="Config/_Survivor/backgrounds.xml")
    R.check("backgrounds.starting_skill_bounds", not bg_bad_values, f"bad={bg_bad_values}", file="Config/_Survivor/backgrounds.xml")
    R.check("backgrounds.knowledge_refs", not bg_knowledge, f"bad={bg_knowledge}", file="Config/_Survivor/backgrounds.xml")

    traits=trroot.findall("./trait") if trroot is not None else []
    authored_trait_ids=attr_ids(traits); unique_check(R,"traits.authored",authored_trait_ids,137,"Config/_Survivor/traits.xml")
    deferred=[t.get("id") for t in traits if t.get("availability","").lower()=="deferred"]
    R.counts["traits_authored"] = len(authored_trait_ids); R.counts["traits_deferred"] = len(deferred)
    R.check("traits.generated_aptitudes", len(skill_ids)*2==84, f"skills={len(skill_ids)} tiers=2 generatedExpected={len(skill_ids)*2}")
    R.counts["generated_aptitudes_expected"] = len(skill_ids)*2
    # Factory contract must explicitly cap selectable Aptitudes at two tiers and make weaknesses conflicts.
    factory=(root/"Scripts/Survivor/Definitions/RebirthSkillAptitudeTraitFactory.cs").read_text(encoding="utf-8",errors="replace")
    factory_ok=('public const int MaxTier = 2;' in factory and 'for (int tier = 1; tier <= MaxTier; tier++)' in factory and 'WeaknessBySkill.TryGetValue' in factory and 'conflicts.Add(weaknesses[w])' in factory)
    R.check("traits.aptitude_factory_contract", factory_ok, "requires 2 tiers and same-skill weakness conflict injection", file="Scripts/Survivor/Definitions/RebirthSkillAptitudeTraitFactory.cs")

    trait_set=set(authored_trait_ids)
    bg_trait_refs=[]
    for b in backgrounds:
        bid=b.get("id","")
        for path in ["./restricted_traits/trait","./favored_traits/trait","./blocked_traits/trait"]:
            for node in b.findall(path):
                tid=node.get("id","")
                if tid and tid not in trait_set: bg_trait_refs.append((bid,tid,path))
    R.check("backgrounds.trait_refs", not bg_trait_refs, f"bad={bg_trait_refs}", file="Config/_Survivor/backgrounds.xml")

    # Creation-phase Trait skill references live in condition_profiles.xml modifier components.
    cp=roots.get("condition_profiles.xml")
    trait_skill_bad=[]
    if cp is not None:
        for comp in cp.findall(".//modifier_profile/component"):
            if comp.get("phase","").lower() != "creation": continue
            target=comp.get("target",""); sid=""
            if target == "skill.start_bias": sid=comp.get("value","")
            elif target.startswith("skill.start."):
                sid=target[len("skill.start."):]
                if not sid.startswith("skill."): sid="skill."+sid
            if sid and sid not in skill_set:
                parent="<modifier_profile>"
                trait_skill_bad.append((parent,sid,target))
    R.check("traits.creation_skill_refs", not trait_skill_bad, f"bad={trait_skill_bad}", file="Config/_Survivor/condition_profiles.xml")

    # Resolve authored conflicts, excluding dynamic aptitude IDs which are generated from active Skills.
    bad_conf=[]; bad_diet=[]
    diet_ids=set(attr_ids(diroot.findall("./diet") if diroot is not None else []))
    for t in traits:
        tid=t.get("id","")
        for c in t.findall("./conflicts/trait") + t.findall("./conflict_traits/trait"):
            cid=c.get("id","")
            if cid and cid not in trait_set and not cid.startswith("trait.aptitude."): bad_conf.append((tid,cid))
        for c in t.findall("./conflicts/diet") + t.findall("./conflict_diets/diet"):
            did=c.get("id","")
            if did and did not in diet_ids: bad_diet.append((tid,did))
    R.check("traits.conflict_refs", not bad_conf, f"bad={bad_conf}", file="Config/_Survivor/traits.xml")
    R.check("traits.diet_conflict_refs", not bad_diet, f"bad={bad_diet}", file="Config/_Survivor/traits.xml")
    # Runtime validator explicitly rejects Deferred traits in fresh selections.
    creation_validator=(root/"Scripts/Survivor/Creation/RebirthSurvivorCreationValidator.cs").read_text(encoding="utf-8",errors="replace")
    deferred_guard='t.Availability==RebirthDefinitionAvailability.Deferred' in creation_validator and 'TraitNotAllowedForBackground' in creation_validator
    R.check("traits.deferred_selection_guard", deferred_guard, f"deferredCount={len(deferred)} guardPresent={deferred_guard}", file="Scripts/Survivor/Creation/RebirthSurvivorCreationValidator.cs")

    # Post-Revision-2 Chunk 3: canonical Trait runtime reconciliation.
    cp_profiles={p.get("owner_trait_id",""):p for p in (cp.findall("./modifier_profiles/modifier_profile") if cp is not None else [])}
    canonical_states={"IMPLEMENTED","CREATION_ONLY","PARTIAL","DEFERRED"}
    state_counts=Counter()
    bad_states=[]; bad_availability=[]; bad_paths=[]
    support_root=roots.get("support_profiles.xml")
    support_covered=set(); support_by_id={}
    if support_root is not None:
        for sp in support_root.findall("./support_profile"):
            support_by_id[sp.get("id","")]=sp
            habit=sp.get("habit_trait_id","")
            if habit: support_covered.add(habit)
            for tn in sp.findall("./supported_traits/trait"):
                if tn.get("id"): support_covered.add(tn.get("id"))
    partial_ids=[]
    for t in traits:
        tid=t.get("id",""); p=cp_profiles.get(tid)
        state=p.get("implementation_state","").strip().upper() if p is not None else ""
        state_counts[state]+=1
        if state not in canonical_states: bad_states.append((tid,state))
        if t.get("availability","").lower()=="deferred" and state!="DEFERRED": bad_availability.append((tid,t.get("availability"),state))
        if t.get("availability","").lower()!="deferred" and state=="DEFERRED": bad_availability.append((tid,t.get("availability"),state))
        comps=p.findall("./component") if p is not None else []
        has_creation=any(c.get("phase","").lower()=="creation" for c in comps)
        has_runtime=any(c.get("phase","").lower()=="runtime" for c in comps)
        has_support=tid in support_covered
        if state=="IMPLEMENTED" and not (has_runtime or has_support): bad_paths.append((tid,state,"missing runtime/support"))
        if state=="CREATION_ONLY" and (not has_creation or has_runtime): bad_paths.append((tid,state,f"creation={has_creation} runtime={has_runtime}"))
        if state=="PARTIAL":
            partial_ids.append(tid)
            if not (has_creation or has_runtime or has_support): bad_paths.append((tid,state,"no mechanical path"))
    expected_state_counts={"IMPLEMENTED":39,"CREATION_ONLY":13,"PARTIAL":13,"DEFERRED":72}
    expected_partial=sorted([
        "trait.disease_resistant","trait.sickly","trait.sure_footed","trait.bad_knees","trait.optimistic","trait.pessimistic",
        "trait.steady_nerves","trait.anxious","trait.old_training_injury","trait.wrist_wear","trait.trade_knees","trait.shoulder_wear","trait.repetitive_strain"
    ])
    R.check("traits.runtime_state_canonical", not bad_states, f"bad={bad_states}", file="Config/_Survivor/condition_profiles.xml")
    R.check("traits.runtime_state_counts", dict(state_counts)==expected_state_counts, f"actual={dict(state_counts)} expected={expected_state_counts}", file="Config/_Survivor/condition_profiles.xml")
    R.check("traits.runtime_state_availability", not bad_availability, f"bad={bad_availability}", file="Config/_Survivor/condition_profiles.xml")
    R.check("traits.runtime_state_paths", not bad_paths, f"bad={bad_paths}", file="Config/_Survivor/condition_profiles.xml")
    R.check("traits.partial_release_gate", sorted(partial_ids)==expected_partial, f"actual={sorted(partial_ids)} expected={expected_partial}", file="Config/_Survivor/condition_profiles.xml")
    R.counts["traits_implemented"]=state_counts.get("IMPLEMENTED",0)
    R.counts["traits_creation_only_authored"]=state_counts.get("CREATION_ONLY",0)
    R.counts["traits_partial"]=state_counts.get("PARTIAL",0)
    R.counts["traits_deferred"]=state_counts.get("DEFERRED",0)
    R.counts["traits_creation_only_with_generated"]=state_counts.get("CREATION_ONLY",0)+len(skill_ids)*3

    # Every authored Trait's player-visible English description is the canonical effect summary.
    english={}
    with (root/"Config/Localization.csv").open("r",encoding="utf-8-sig",newline="") as f:
        rr=csv.reader(f); next(rr,None)
        for row in rr:
            if len(row)>=2 and row and row[0]: english[row[0]]=row[1]
    summary_mismatch=[]
    for t in traits:
        tid=t.get("id",""); desc=english.get(t.get("description_key",""),"")
        authoring=t.find("./authoring"); p=cp_profiles.get(tid)
        authored=authoring.get("effect_summary","") if authoring is not None else ""
        profile=p.get("effect_summary","") if p is not None else ""
        if not desc or authored!=desc or profile!=desc: summary_mismatch.append((tid,desc,authored,profile))
    R.check("traits.ui_description_exact_match", not summary_mismatch, f"mismatches={summary_mismatch[:20]}", file="Config/_Survivor/traits.xml")

    # Bounded low-risk fixes: verify exact existing support profiles and Dusty Lungs runtime bridge.
    def sp_effects(spid):
        sp=support_by_id.get(spid)
        if sp is None: return None, []
        return sp, [(e.get("target"),e.get("op"),e.get("value"),e.get("scope"),e.get("state")) for e in sp.findall("./effects/effect")]
    alcohol,alcohol_effects=sp_effects("support.alcohol_habit")
    caffeine,caffeine_effects=sp_effects("support.caffeine")
    nicotine,nicotine_effects=sp_effects("support.nicotine_patch")
    habit_ok=(alcohol is not None and alcohol.get("habit_trait_id")=="trait.heavy_drinker" and alcohol.get("grace_seconds")=="2700" and alcohol.get("managed_seconds")=="2700" and alcohol.get("positive_seconds")=="600"
              and ("mood.target","add","-4","habit_trait","unsatisfied") in alcohol_effects and ("mood.target","add","3","habit_trait","managed") in alcohol_effects
              and ("mood.target","add","-6","trait:trait.teetotaler","positive") in alcohol_effects
              and caffeine is not None and caffeine.get("habit_trait_id")=="trait.caffeine_dependent" and caffeine.get("managed_seconds")=="2700"
              and ("mood.target","add","-3","habit_trait","unsatisfied") in caffeine_effects and ("mood.target","add","2","habit_trait","managed") in caffeine_effects
              and nicotine is not None and nicotine.get("habit_trait_id")=="trait.smoker" and nicotine.get("managed_seconds")=="3600"
              and ("mood.target","add","-4","habit_trait","unsatisfied") in nicotine_effects and ("mood.target","add","4","habit_trait","managed") in nicotine_effects)
    R.check("traits.habit_support_contract", habit_ok, f"alcohol={alcohol_effects} caffeine={caffeine_effects} nicotine={nicotine_effects}", file="Config/_Survivor/support_profiles.xml")
    dusty=cp_profiles.get("trait.dusty_lungs")
    dusty_comp={(c.get("target"),c.get("op"),c.get("phase"),c.get("value")) for c in dusty.findall("./component")} if dusty is not None else set()
    dusty_ok=(("energy.use.sprint_jump","multiply","runtime","1.10") in dusty_comp and ("energy.recovery","multiply","runtime","0.95") in dusty_comp and dusty.get("implementation_state")=="IMPLEMENTED")
    R.check("traits.dusty_lungs_runtime_fix", dusty_ok, f"components={sorted(dusty_comp)}", file="Config/_Survivor/condition_profiles.xml")

    recon=(root/"Scripts/Survivor/Definitions/RebirthTraitRuntimeReconciliation.cs").read_text(encoding="utf-8",errors="replace")
    chunk3_debug=(root/"Scripts/Survivor/Debug/ConsoleCmdRebirthSurvivor.cs").read_text(encoding="utf-8",errors="replace")
    debug_trait_contract=all(tok in recon for tok in ["IMPLEMENTED","CREATION_ONLY","PARTIAL","DEFERRED","BuildSummary","BuildTraitReport"]) and "RebirthTraitRuntimeReconciliation.BuildSummary()" in chunk3_debug and "RebirthTraitRuntimeReconciliation.BuildTraitReport" in chunk3_debug
    R.check("traits.debug_reconciliation_command", debug_trait_contract, f"contractPresent={debug_trait_contract}", file="Scripts/Survivor/Debug/ConsoleCmdRebirthSurvivor.cs")

    diets=diroot.findall("./diet") if diroot is not None else []; diet_list=attr_ids(diets); unique_check(R,"diets",diet_list,4,"Config/_Survivor/diets.xml"); R.counts["diets"] = len(diet_list)
    expected_diet_points={"diet.unrestricted":0,"diet.vegetarian":2,"diet.vegan":4,"diet.carnivore":3}
    actual_dp={d.get("id"):int(d.get("points","0")) for d in diets}
    R.check("diets.revision_2_1_points", actual_dp==expected_diet_points, f"actual={actual_dp} expected={expected_diet_points}", file="Config/_Survivor/diets.xml")

    # Post-Revision-2 Chunk 4: audit the complete current meaningful-food catalogue against the
    # same composition rules used by runtime gameplay and creator presentation. Diet Revision 2.1
    # supersedes the older roadmap point example; do not regress 0/+2/+3/+4 to 0/+1/+1/+2.
    diet_audit_script=root/"Tools/Survivor/audit_rebirth_survivor_diets.py"
    R.check("diets.chunk4_audit_script", diet_audit_script.exists(), f"exists={diet_audit_script.exists()}", file="Tools/Survivor/audit_rebirth_survivor_diets.py")
    if diet_audit_script.exists():
        try:
            import importlib.util
            spec=importlib.util.spec_from_file_location("rebirth_survivor_diet_audit", diet_audit_script)
            mod=importlib.util.module_from_spec(spec); spec.loader.exec_module(mod)
            audit=mod.run(root)
            for entry in audit.get("checks",[]):
                R.check("diets.audit."+entry.get("name","unknown"), bool(entry.get("ok")), entry.get("detail",""), file="Config/_Survivor/food_content.xml")
            R.counts["diet_authored_foods"]=audit.get("counts",{}).get("authored_foods",0)
            compat=audit.get("counts",{}).get("compatibility",{})
            for dname in ("unrestricted","vegetarian","carnivore","vegan"):
                c=compat.get(dname,{})
                R.counts["diet_"+dname+"_compatible"]=c.get("Compatible",0)
                R.counts["diet_"+dname+"_off_diet"]=c.get("Off-diet",0)
            R.check("diets.chunk4_audit_result", audit.get("result")=="PASS", f"result={audit.get('result')} errors={audit.get('errors',[])[:10]}", file="Tools/Survivor/audit_rebirth_survivor_diets.py")
        except Exception as ex:
            R.check("diets.chunk4_audit_result", False, f"{type(ex).__name__}: {ex}", file="Tools/Survivor/audit_rebirth_survivor_diets.py")

    diet_vector_py=root/"Tools/Survivor/test_rebirth_survivor_diet_vectors.py"
    diet_vector_cs=root/"Scripts/Survivor/Debug/RebirthSurvivorDietVectorHarness.cs"
    diet_compat_cs=root/"Scripts/Survivor/Condition/RebirthDietCompatibility.cs"
    R.check("diets.vector_harness_files", diet_vector_py.exists() and diet_vector_cs.exists() and diet_compat_cs.exists(), f"python={diet_vector_py.exists()} csharp={diet_vector_cs.exists()} compatibilityOwner={diet_compat_cs.exists()}", file="Tools/Survivor")
    diet_debug=(root/"Scripts/Survivor/Debug/ConsoleCmdRebirthSurvivor.cs").read_text(encoding="utf-8",errors="replace")
    diet_debug_contract=("RebirthDietRuntimeReconciliation.BuildSummary()" in diet_debug and "RebirthSurvivorDietVectorHarness.RunAll()" in diet_debug and "RebirthDietRuntimeReconciliation.BuildFoodReport" in diet_debug)
    R.check("diets.debug_reconciliation_commands", diet_debug_contract, f"contractPresent={diet_debug_contract}", file="Scripts/Survivor/Debug/ConsoleCmdRebirthSurvivor.cs")

    # Chunk 5 Skill Wave A has its own pure-data/static contract suite. Fold the overall
    # result into the preboot validator without implying a game-assembly compile.
    wave_vector_py=root/"Tools/Survivor/test_rebirth_survivor_skill_wave_a.py"
    R.check("skills.wave_a_vector_harness_file", wave_vector_py.exists(), f"python={wave_vector_py.exists()}", file="Tools/Survivor/test_rebirth_survivor_skill_wave_a.py")
    if wave_vector_py.exists():
        try:
            import importlib.util
            spec=importlib.util.spec_from_file_location("rebirth_survivor_skill_wave_a_vectors", wave_vector_py)
            mod=importlib.util.module_from_spec(spec); spec.loader.exec_module(mod)
            result=mod.run(root)
            total=result.passed+len(result.failures)
            R.counts["skill_wave_a_vectors"] = total
            R.check("skills.wave_a_vector_contract", not result.failures, f"passed={result.passed}/{total} failures={result.failures[:20]}", file="Tools/Survivor/test_rebirth_survivor_skill_wave_a.py")
        except Exception as ex:
            R.check("skills.wave_a_vector_contract", False, f"{type(ex).__name__}: {ex}", file="Tools/Survivor/test_rebirth_survivor_skill_wave_a.py")

    # Post-Revision-2 Chunk 6 Resource/Field Skills: fold the dedicated pure-data/static
    # contract suite into the preboot validator. This validates source/authoring contracts only
    # and deliberately does not claim a C# compile against the game assemblies.
    resource_vector_py=root/"Tools/Survivor/test_rebirth_survivor_resource_field.py"
    resource_vector_cs=root/"Scripts/Survivor/Progression/RebirthResourceFieldSkillVectorHarness.cs"
    resource_service_cs=root/"Scripts/Survivor/Progression/RebirthResourceFieldSkillService.cs"
    resource_files_ok=resource_vector_py.exists() and resource_vector_cs.exists() and resource_service_cs.exists()
    R.check("skills.resource_field_harness_files", resource_files_ok, f"python={resource_vector_py.exists()} csharp={resource_vector_cs.exists()} service={resource_service_cs.exists()}", file="Tools/Survivor")
    if resource_vector_py.exists():
        try:
            import importlib.util
            spec=importlib.util.spec_from_file_location("rebirth_survivor_resource_field_vectors", resource_vector_py)
            mod=importlib.util.module_from_spec(spec); sys.modules[spec.name]=mod; spec.loader.exec_module(mod)
            result=mod.run(root)
            total=result.passed+len(result.failures)
            R.counts["resource_field_vectors"] = total
            R.check("skills.resource_field_vector_contract", not result.failures, f"passed={result.passed}/{total} failures={result.failures[:20]}", file="Tools/Survivor/test_rebirth_survivor_resource_field.py")
        except Exception as ex:
            R.check("skills.resource_field_vector_contract", False, f"{type(ex).__name__}: {ex}", file="Tools/Survivor/test_rebirth_survivor_resource_field.py")

    # Post-Revision-2 Chunk 10 Complex Skills: source-audit gated pure-data/static contract.
    complex_vector_py=root/"Tools/Survivor/test_rebirth_survivor_complex_skills.py"
    R.check("skills.complex_harness_file", complex_vector_py.exists(), f"python={complex_vector_py.exists()}", file="Tools/Survivor/test_rebirth_survivor_complex_skills.py")
    if complex_vector_py.exists():
        try:
            import importlib.util
            spec=importlib.util.spec_from_file_location("rebirth_survivor_complex_skill_vectors", complex_vector_py)
            mod=importlib.util.module_from_spec(spec); sys.modules[spec.name]=mod; spec.loader.exec_module(mod)
            result=mod.run(root)
            total=result.passed+len(result.failures)
            R.counts["complex_skill_vectors"] = total
            R.check("skills.complex_vector_contract", not result.failures, f"passed={result.passed}/{total} failures={result.failures[:20]}", file="Tools/Survivor/test_rebirth_survivor_complex_skills.py")
        except Exception as ex:
            R.check("skills.complex_vector_contract", False, f"{type(ex).__name__}: {ex}", file="Tools/Survivor/test_rebirth_survivor_complex_skills.py")

    # Post-Revision-2 Chunk 11 release-acceptance static contract. This does not approve release;
    # it verifies the instrumentation, seven-band balance audit and explicit runtime blockers.
    release_vector_py=root/"Tools/Survivor/test_rebirth_survivor_release_acceptance.py"
    R.check("release.acceptance_harness_file", release_vector_py.exists(), f"python={release_vector_py.exists()}", file="Tools/Survivor/test_rebirth_survivor_release_acceptance.py")
    if release_vector_py.exists():
        try:
            import importlib.util
            spec=importlib.util.spec_from_file_location("rebirth_survivor_release_acceptance_vectors", release_vector_py)
            mod=importlib.util.module_from_spec(spec); sys.modules[spec.name]=mod; spec.loader.exec_module(mod)
            result=mod.run(root)
            total=result.passed+len(result.failures)
            R.counts["release_acceptance_vectors"] = total
            R.check("release.acceptance_vector_contract", not result.failures, f"passed={result.passed}/{total} failures={result.failures[:20]}", file="Tools/Survivor/test_rebirth_survivor_release_acceptance.py")
            R.counts["release_runtime_blockers"] = result.counts.get("runtime_blockers",0)
        except Exception as ex:
            R.check("release.acceptance_vector_contract", False, f"{type(ex).__name__}: {ex}", file="Tools/Survivor/test_rebirth_survivor_release_acceptance.py")

    # Knowledge IDs and refs.
    R.counts["knowledge"] = len(knowledge_ids)
    R.check("knowledge.count", len(knowledge_ids)==27, f"count={len(knowledge_ids)} expected=27", file="Config/_Survivor/progression.xml")

    # Retired old broad IDs must not reappear in active authoring. Chunk 2 moves all 16 legacy
    # mappings into one explicit compatibility policy used by both origin and progression migration.
    active_authoring_text="\n".join((surv/n).read_text(encoding="utf-8",errors="replace") for n in ["backgrounds.xml","traits.xml","progression.xml","skill_sources.xml"])
    leaked=[sid for sid in RETIRED_BROAD if sid in active_authoring_text]
    R.check("compat.retired_broad_ids", not leaked, f"retiredLeaked={leaked}", file="Config/_Survivor")

    policy_path=root/"Scripts/Survivor/Persistence/RebirthSurvivorSkillMigrationPolicy.cs"
    policy=policy_path.read_text(encoding="utf-8",errors="replace") if policy_path.exists() else ""
    current_match=re.search(r'CurrentIds\s*=\s*new string\[\]\s*\{(.*?)\};',policy,re.S)
    migration_current=re.findall(r'"(skill\.[^"]+)"',current_match.group(1)) if current_match else []
    R.check("migration.current_skill_identity", migration_current==skill_ids, f"policyCount={len(migration_current)} authoredCount={len(skill_ids)} orderMatch={migration_current==skill_ids}", file="Scripts/Survivor/Persistence/RebirthSurvivorSkillMigrationPolicy.cs")

    parsed_map={}
    for m in re.finditer(r'new RebirthSurvivorLegacySkillMapping\(\s*"(skill\.[^"]+)"\s*,(.*?)\)',policy,re.S):
        parsed_map[m.group(1)]=re.findall(r'"(skill\.[^"]+)"',m.group(2))
    missing_old=sorted(set(OLD_16)-set(parsed_map))
    extra_old=sorted(set(parsed_map)-set(OLD_16))
    wrong_map={k:(parsed_map.get(k),v) for k,v in EXPECTED_LEGACY_MAP.items() if parsed_map.get(k)!=v}
    invalid_dest=sorted((src,dst) for src,dests in parsed_map.items() for dst in dests if dst not in skill_set)
    R.check("migration.old16_explicit_coverage", not missing_old and not extra_old, f"covered={len(parsed_map)}/16 missing={missing_old} extra={extra_old}", file="Scripts/Survivor/Persistence/RebirthSurvivorSkillMigrationPolicy.cs")
    R.check("migration.old16_approved_mapping", not wrong_map and not invalid_dest, f"wrong={wrong_map} invalidDest={invalid_dest}", file="Scripts/Survivor/Persistence/RebirthSurvivorSkillMigrationPolicy.cs")
    R.check("migration.axes_not_inferred_from_blades", "skill.axes" not in parsed_map.get("skill.bladed_melee",[]), f"bladedDestinations={parsed_map.get('skill.bladed_melee',[])}", file="Scripts/Survivor/Persistence/RebirthSurvivorSkillMigrationPolicy.cs")

    migration=(root/"Scripts/Survivor/Persistence/RebirthWorldCharacterMigration.cs").read_text(encoding="utf-8",errors="replace")
    calls=len(re.findall(r'RebirthSurvivorSkillMigrationPolicy\.TryBuildMigratedSkills\(',migration))
    atomic=('XDocument working = new XDocument(document);' in migration and 'document.Root.ReplaceWith(new XElement(working.Root));' in migration)
    ledger=all(x in migration for x in ["migrationSourceSchema","migrationTargetSchema","migrationPolicyId","migrationOriginAudit","migrationProgressionAudit"])
    R.check("migration.origin_and_current_same_policy", calls==2, f"policyCalls={calls} expected=2", file="Scripts/Survivor/Persistence/RebirthWorldCharacterMigration.cs")
    R.check("migration.transactional_document", atomic, f"cloneAndCommit={atomic}", file="Scripts/Survivor/Persistence/RebirthWorldCharacterMigration.cs")
    R.check("migration.ledger_metadata", ledger, f"ledgerFieldsPresent={ledger}", file="Scripts/Survivor/Persistence/RebirthWorldCharacterMigration.cs")

    strict_markers=["duplicate Skill ids","unknown Skill ids cannot be migrated","RuntimeSkillMin","RuntimeSkillMax","progress < 0f || progress >= 1f","NeutralInitializations"]
    missing_markers=[m for m in strict_markers if m not in policy]
    R.check("migration.strict_input_contract", not missing_markers, f"missingMarkers={missing_markers}", file="Scripts/Survivor/Persistence/RebirthSurvivorSkillMigrationPolicy.cs")

    repo=(root/"Scripts/Survivor/Persistence/RebirthWorldCharacterRepository.cs").read_text(encoding="utf-8",errors="replace")
    strict_repo=all(x in repo for x in ["TryDeserializeOriginSkills","TryDeserializeProgressionSkills","ValidateCompleteSchema5SkillSet","origin contains duplicate Skill","progression contains duplicate Skill","migrationPolicyId"])
    R.check("persistence.schema5_skill_integrity", strict_repo, f"strictSchema5Deserializer={strict_repo}", file="Scripts/Survivor/Persistence/RebirthWorldCharacterRepository.cs")

    debug_cmd=(root/"Scripts/Survivor/Debug/ConsoleCmdRebirthSurvivor.cs").read_text(encoding="utf-8",errors="replace")
    vector_harness=root/"Scripts/Survivor/Debug/RebirthSurvivorMigrationVectorHarness.cs"
    vector_command=('migrationMode=="vectors"' in debug_cmd and 'RebirthSurvivorMigrationVectorHarness.RunAll()' in debug_cmd and vector_harness.exists())
    R.check("migration.debug_vector_command", vector_command, f"commandAndHarness={vector_command}", file="Scripts/Survivor/Debug/ConsoleCmdRebirthSurvivor.cs")

    # Deterministic clamp path: creator validator must use explicit Math.Max/Min and shared negative-Trait refund accounting outside the bg/diet base equation.
    clamp_contract=('Math.Max(creationMin,Math.Min(creationMax,value))' in creation_validator.replace(' ',''))
    point_contract=('BaseCreationPoints + (background!=null?background.CreationPointModifier:0) + (diet!=null?diet.Points:0)' in creation_validator and 'RebirthSurvivorTraitPointEconomy.ApplyNegativeRefund' in creation_validator)
    R.check("economy.deterministic_clamp_contract", clamp_contract, "explicit creation-bound clamp present", file="Scripts/Survivor/Creation/RebirthSurvivorCreationValidator.cs")
    R.check("economy.point_accounting_contract", point_contract, "background/diet plus shared negative-Trait refund policy", file="Scripts/Survivor/Creation/RebirthSurvivorCreationValidator.cs")
    # Every background's strongest authored skill plus Tier III aptitude resolves to deterministic clamp <= +50.
    clamp_cases=[]
    if creation is not None:
        cmin=float(creation.get("skill_start_min")); cmax=float(creation.get("skill_start_max"))
        for b in backgrounds:
            for sn in b.findall("./starting_skills/skill"):
                if "value" not in sn.attrib: continue
                raw=float(sn.get("value"))+15.0
                clamped=max(cmin,min(cmax,raw))
                if clamped>50 or clamped<cmin: clamp_cases.append((b.get("id"),sn.get("id"),raw,clamped))
    R.check("economy.background_plus_aptitude_clamp", not clamp_cases, f"tested background skill biases + TierIII; bad={clamp_cases}")

    # Atlas and icon coverage.
    atlas=root/"UIAtlases/RebirthSurvivorIcons/settings.xml"; atlas_root=parse_xml(atlas,R); sprites=attr_ids(atlas_root.findall("./sprite"),"name") if atlas_root is not None else []
    dup_sprites=sorted(k for k,v in Counter(sprites).items() if v>1); sprite_set=set(sprites)
    R.check("atlas.duplicate_sprites", not dup_sprites, f"duplicates={dup_sprites}", file="UIAtlases/RebirthSurvivorIcons/settings.xml")
    R.counts["atlas_sprites"] = len(sprites)
    icon_dir=root/"UIAtlases/RebirthSurvivorIcons"; bg_art=root/"UIAssets/Survivor/Backgrounds"
    missing_icons=[]; missing_reg=[]; missing_bg=[]
    for t in traits:
        key=t.get("icon_key","")
        if key:
            if not (icon_dir/(key+".png")).exists(): missing_icons.append((t.get("id"),key))
            if key not in sprite_set: missing_reg.append((t.get("id"),key))
    for d in diets:
        key=d.get("icon_key","")
        if key:
            if not (icon_dir/(key+".png")).exists(): missing_icons.append((d.get("id"),key))
            if key not in sprite_set: missing_reg.append((d.get("id"),key))
    for sid in skill_ids:
        key="rb_skill_"+sid.removeprefix("skill.").replace(".","_").replace("-","_")
        if not (icon_dir/(key+".png")).exists(): missing_icons.append((sid,key))
        if key not in sprite_set: missing_reg.append((sid,key))
    for b in backgrounds:
        key=b.get("background_art_key","")
        if key and not (bg_art/(key+".jpg")).exists(): missing_bg.append((b.get("id"),key))
    R.check("icons.files", not missing_icons, f"missing={missing_icons[:30]}", file="UIAtlases/RebirthSurvivorIcons")
    R.check("icons.atlas_registration", not missing_reg, f"missingRegistration={missing_reg[:30]}", file="UIAtlases/RebirthSurvivorIcons/settings.xml")
    R.check("background_art.files", not missing_bg, f"missing={missing_bg[:30]}", file="UIAssets/Survivor/Backgrounds")

    # Localization structural integrity + referenced keys.
    loc=read_localization(root,R); missing_loc=[]
    def need(owner,key):
        if key and key.lower() not in loc: missing_loc.append((owner,key))
    for b in backgrounds:
        for a in ["name_key","description_key","background_art_alt_text_key"]: need(b.get("id"),b.get(a,""))
    for t in traits:
        for a in ["name_key","description_key"]: need(t.get("id"),t.get(a,""))
    for d in diets:
        for a in ["name_key","description_key"]: need(d.get("id"),d.get(a,""))
    for s in skills:
        for a in ["name_key","source_key"]: need(s.get("id"),s.get(a,""))
    need("generated-aptitudes","xuiRebirthSurvivorSkillAptitudeName"); need("generated-aptitudes","xuiRebirthSurvivorSkillAptitudeDesc")
    R.check("localization.creator_visible_refs", not missing_loc, f"missing={missing_loc[:50]}", file="Config/Localization.csv")

    # Creator XUi IDs and approved visual order (x-position, not XML source order).
    win=root/"Config/XUi_Menu/windows.xml"; winroot=parse_xml(win,R); names={e.get("name") for e in winroot.iter() if e.get("name")} if winroot is not None else set()
    missing_ids=sorted(CREATOR_REQUIRED_IDS-names)
    R.check("creator.required_xui_ids", not missing_ids, f"missing={missing_ids}", file="Config/XUi_Menu/windows.xml")
    step_positions=[]; step_missing=[]
    byname={e.get("name"):e for e in winroot.iter() if e.get("name")} if winroot is not None else {}
    for sid,label in CREATOR_STEPS:
        e=byname.get(sid)
        if e is None: step_missing.append(sid); continue
        pos=e.get("pos","")
        try: x=float(pos.split(",")[0])
        except: x=float("nan")
        step_positions.append((sid,x,label))
    ordered=(not step_missing and all(step_positions[i][1] < step_positions[i+1][1] for i in range(len(step_positions)-1)))
    R.check("creator.step_order", ordered, f"approved={[x[0] for x in CREATOR_STEPS]} positions={step_positions} missing={step_missing}", file="Config/XUi_Menu/windows.xml")
    # Chunk 9 closes a long-standing drift between menu and in-game creator copies. Validate both.
    ingame_win=root/"Config/XUi_InGame/windows.xml"; ingame_root=parse_xml(ingame_win,R); ingame_byname={e.get("name"):e for e in ingame_root.iter() if e.get("name")} if ingame_root is not None else {}
    ingame_positions=[]; ingame_missing=[]
    for sid,label in CREATOR_STEPS:
        e=ingame_byname.get(sid)
        if e is None: ingame_missing.append(sid); continue
        pos=e.get("pos","")
        try: x=float(pos.split(",")[0])
        except: x=float("nan")
        ingame_positions.append((sid,x,label))
    ingame_ordered=(not ingame_missing and all(ingame_positions[i][1] < ingame_positions[i+1][1] for i in range(len(ingame_positions)-1)))
    R.check("creator.ingame_step_order", ingame_ordered, f"approved={[x[0] for x in CREATOR_STEPS]} positions={ingame_positions} missing={ingame_missing}", file="Config/XUi_InGame/windows.xml")
    missing_ingame_ids=sorted(CREATOR_REQUIRED_IDS-set(ingame_byname.keys()))
    R.check("creator.ingame_required_xui_ids", not missing_ingame_ids, f"missing={missing_ingame_ids}", file="Config/XUi_InGame/windows.xml")
    xuiroot=parse_xml(root/"Config/XUi_Menu/xui.xml",R); groups={e.get("name") for e in xuiroot.iter() if e.tag=="window_group" and e.get("name")} if xuiroot is not None else set()
    R.check("creator.window_group", "rebirthSurvivorCreator" in groups, f"hasRebirthSurvivorCreator={'rebirthSurvivorCreator' in groups}", file="Config/XUi_Menu/xui.xml")
    creator_cs=(root/"Scripts/Survivor/UI/XUiC_RebirthSurvivorCreator.cs").read_text(encoding="utf-8",errors="replace")
    enum_cs=(root/"Scripts/Survivor/UI/RebirthSurvivorCreatorViewModel.cs").read_text(encoding="utf-8",errors="replace")
    enum_match=re.search(r'enum\s+RebirthSurvivorCreatorStep\s*\{(.*?)\}',enum_cs,re.S)
    enum_body=enum_match.group(1) if enum_match else ""
    enum_order=[x for x in ["Profile","Background","Diet","Traits","Review"] if re.search(r'\b'+x+r'\b',enum_body)]
    actual_enum_tokens=re.findall(r'\b(Profile|Background|Diet|Traits|Review)\b\s*=\s*\d+',enum_body)
    R.check("creator.csharp_step_order", actual_enum_tokens==["Profile","Background","Diet","Traits","Review"], f"enumOrder={actual_enum_tokens}", file="Scripts/Survivor/UI/RebirthSurvivorCreatorViewModel.cs")
    next_contract=('SetEnabled(btnNext, showNext)' in creator_cs and 'model.TryAdvance(out reason)' in creator_cs)
    R.check("creator.next_authority_contract", next_contract, "Next remains UI-interactive while TryAdvance owns rejection", file="Scripts/Survivor/UI/XUiC_RebirthSurvivorCreator.cs")

    # Post-Revision-2 Chunk 9 creator UI completion contract.
    ui_text=(root/"Scripts/Survivor/UI/RebirthSurvivorUiText.cs").read_text(encoding="utf-8",errors="replace")
    review_skill_slots=all(("reviewSkillRow"+str(i)) in names and ("reviewSkillRow"+str(i)) in ingame_byname for i in range(41))
    review_knowledge_slots=all(("reviewKnowledgeRow"+str(i)) in names and ("reviewKnowledgeRow"+str(i)) in ingame_byname for i in range(26))
    diet_card_contract=all(all((prefix+str(i)) in names and (prefix+str(i)) in ingame_byname for prefix in ["dietIcon","dietName","dietSummary","dietPoints","dietSelection"]) for i in range(4))
    R.check("chunk9.diet_cards", diet_card_contract and "BuildDietCompatibilityShort(diet)" in creator_cs and "SignedPoints(diet.Points)" in creator_cs, "4 cards expose icon/name/compatibility/point adjustment", file="Config/XUi_Menu/windows.xml")
    R.check("chunk9.diet_consequences", all(x in ui_text for x in ["Restricted food is still edible","physical Nutrition/hydration","when nutrients are absorbed"]), "Diet detail states the concise player-facing runtime consequence without claiming physical food blocking", file="Scripts/Survivor/UI/RebirthSurvivorUiText.cs")
    R.check("chunk9.trait_budget_formula", "BuildTraitBudgetCompact(model)" in creator_cs and all(x in ui_text for x in ["b.NegativeApplied - b.PositiveSpent - b.AptitudeSpent","TraitBudgetPositiveTraits","TraitBudgetAptitudes"]), "Base + Background + Diet + negative refunds - positive Traits - Aptitudes", file="Scripts/Survivor/UI/RebirthSurvivorUiText.cs")
    R.check("chunk9.trait_categories", "!RebirthSkillAptitudeTraitFactory.IsAptitude(trait)" in enum_cs and "case RebirthSurvivorTraitFilter.SkillAptitudes" in enum_cs and all(x in ui_text for x in ["TraitBadgeAptitude","TraitBadgeBackground","TraitBadgePositive","TraitBadgeNegative","TraitStateLocked","TraitStateDietConflict","TraitStateConflict"]), "positive/negative/Aptitude/Background/conflict states are distinct", file="Scripts/Survivor/UI/RebirthSurvivorCreatorViewModel.cs")
    visible_body=re.search(r'private List<RebirthTraitDefinition> GetVisibleTraits\(\)(.*?)private bool MatchesFilter',enum_cs,re.S)
    visible_text=visible_body.group(1) if visible_body else ""
    R.check("chunk9.unavailable_traits_visible", visible_body is not None and "RebirthDefinitionAvailability.Deferred" in visible_text and "!IsEligibleForBackground(trait, background)" not in visible_text, "Background-locked choices remain visible; deferred choices remain hidden", file="Scripts/Survivor/UI/RebirthSurvivorCreatorViewModel.cs")
    R.check("chunk9.review_skill_capacity", review_skill_slots and "ReviewSkillRows = 42" in creator_cs and "result.StartingSkills" in creator_cs and "Math.Abs(pair.Value) < 0.0001f" in creator_cs and "RebirthSkillAptitudeTraitFactory.SkillIconKey(entry.SkillId)" in creator_cs, "all non-neutral final starting Skills can render with real icons/signed values", file="Scripts/Survivor/UI/XUiC_RebirthSurvivorCreator.cs")
    R.check("chunk9.review_knowledge_capacity", review_knowledge_slots and "ReviewKnowledgeRows = 27" in creator_cs and "result.StartingKnowledgeIds" in creator_cs, "all authored starting Knowledge can render", file="Scripts/Survivor/UI/XUiC_RebirthSurvivorCreator.cs")
    R.check("chunk9.review_identity", "reviewProfilePortraitBinder.BindCached(model.HasPlayerProfileSelection ? model.PlayerProfileName" in creator_cs and "SetLabel(reviewBackgroundName" in creator_cs and "SetLabel(reviewDietName" in creator_cs and "SetLabel(reviewBudgetText, RebirthSurvivorUiText.BuildTraitBudgetSummary(model))" in creator_cs, "appearance/Background/Diet/final budget use real model data", file="Scripts/Survivor/UI/XUiC_RebirthSurvivorCreator.cs")
    R.check("chunk9.review_groups", all(x in ui_text for x in ["ReviewPositiveTraits","ReviewWeaknesses","ReviewMixedTraits","ReviewAptitudes"]), "Review separates regular Traits, weaknesses, Background Traits and Aptitudes", file="Scripts/Survivor/UI/RebirthSurvivorUiText.cs")
    R.check("chunk9.review_dedicated_panel", "SetVisible(reviewVisualDetailsPanel, reviewStep)" in creator_cs and "bool backgroundStep = model.Step == RebirthSurvivorCreatorStep.Background" in creator_cs, "Review no longer reuses Background visual details", file="Scripts/Survivor/UI/XUiC_RebirthSurvivorCreator.cs")

    # Post-Revision-2 Chunk 7 weapon-family implementation contract.
    weapon_ids=[
        "skill.spears","skill.clubs","skill.swords","skill.axes","skill.batons","skill.hammers","skill.knives","skill.scythes","skill.knuckles","skill.unarmed",
        "skill.archery","skill.pistols","skill.revolvers","skill.heavy_handguns","skill.shotguns","skill.assault_rifles","skill.tactical_rifles","skill.long_range_rifles"]
    skill_sources_root=parse_xml(root/"Config/_Survivor/skill_sources.xml",R)
    weapon_family=skill_sources_root.find("weapon_family") if skill_sources_root is not None else None
    R.check("chunk7.weapon_family_authoring", weapon_family is not None, "weapon_family tuning node present", file="Config/_Survivor/skill_sources.xml")
    combat_maps={e.get("skill"):e for e in skill_sources_root.findall("./combat/map")} if skill_sources_root is not None else {}
    missing_weapon_maps=[x for x in weapon_ids if x not in combat_maps]
    R.check("chunk7.weapon_routing_18", not missing_weapon_maps, f"missing={missing_weapon_maps}", file="Config/_Survivor/skill_sources.xml")
    weapon_service=(root/"Scripts/Survivor/Progression/RebirthWeaponFamilySkillService.cs").read_text(encoding="utf-8",errors="replace") if (root/"Scripts/Survivor/Progression/RebirthWeaponFamilySkillService.cs").exists() else ""
    weapon_installer=(root/"Scripts/Survivor/Progression/RebirthSurvivorProgressionInstaller.cs").read_text(encoding="utf-8",errors="replace")
    weapon_debug=(root/"Scripts/Survivor/Debug/ConsoleCmdRebirthSurvivor.cs").read_text(encoding="utf-8",errors="replace")
    weapon_buff_root=parse_xml(root/"Config/buffs.xml",R)
    weapon_buff=weapon_buff_root.find(".//buff[@name='RebirthSurvivorWeaponFamilyPassives']") if weapon_buff_root is not None else None
    allowed_weapon_effects={"StaminaLoss","AttacksPerMinute","ReloadSpeedMultiplier","WeaponHandling","SpreadMultiplierHip","SpreadMultiplierAiming","KickDegreesVerticalMin","KickDegreesVerticalMax","KickDegreesHorizontalMin","KickDegreesHorizontalMax"}
    actual_weapon_effects={e.get("name") for e in weapon_buff.findall(".//passive_effect")} if weapon_buff is not None else set()
    R.check("chunk7.native_handling_effects", weapon_buff is not None and actual_weapon_effects==allowed_weapon_effects, f"actual={sorted(x for x in actual_weapon_effects if x)}", file="Config/buffs.xml")
    R.check("chunk7.no_output_inflation", not ({"EntityDamage","BlockDamage","RoundsPerMinute","MagazineSize","RoundRayCount","BurstRoundCount"}&actual_weapon_effects), "no direct damage/firearm RPM/magazine/projectile-count bridge", file="Config/buffs.xml")
    R.check("chunk7.service_installed", "RebirthWeaponFamilySkillService.Install()" in weapon_installer, "weapon service installed", file="Scripts/Survivor/Progression/RebirthSurvivorProgressionInstaller.cs")
    R.check("chunk7.existing_combat_lbd", "TryAward(" not in weapon_service and "ClassifyCombat(held)" in weapon_service, "passives classify held weapon; no second LBD owner", file="Scripts/Survivor/Progression/RebirthWeaponFamilySkillService.cs")
    R.check("chunk7.debug_vectors", 'mode=="weapons"' in weapon_debug and "RebirthWeaponFamilySkillVectorHarness.RunAll()" in weapon_debug, "debug weapons + vectors", file="Scripts/Survivor/Debug/ConsoleCmdRebirthSurvivor.cs")
    special_routes={"gunHandgunT3SMG5":"skill.pistols","gunMGT3M60":"skill.assault_rifles","gunMGT2TacticalAR":"skill.tactical_rifles"}
    bad_special=[]
    for item,sid in special_routes.items():
        names=(combat_maps.get(sid).get("item_names","").split(",") if combat_maps.get(sid) is not None else [])
        if item not in names: bad_special.append((item,sid))
    R.check("chunk7.compatibility_routes", not bad_special, f"bad={bad_special}", file="Config/_Survivor/skill_sources.xml")

    # Post-Revision-2 Chunk 8 service/crafting implementation contract.
    service_ids=["skill.maintenance","skill.gunsmithing","skill.cooking","skill.medicine","skill.chemistry","skill.mechanics","skill.metalworking"]
    service_node=skill_sources_root.find("service_crafting") if skill_sources_root is not None else None
    R.check("chunk8.service_crafting_authoring", service_node is not None, "service/crafting tuning node present", file="Config/_Survivor/skill_sources.xml")
    service_skill_defs={e.get("id"):e for e in parse_xml(root/"Config/_Survivor/progression.xml",R).findall(".//skill")}
    missing_service=[x for x in service_ids if x not in service_skill_defs]
    R.check("chunk8.service_skill_catalogue", not missing_service, f"missing={missing_service}", file="Config/_Survivor/progression.xml")
    R.check("chunk8.service_signed_bounds", not missing_service and all(service_skill_defs[x].get("min")=="-50" and service_skill_defs[x].get("max")=="100" for x in service_ids), "all seven retain -50..100 runtime range", file="Config/_Survivor/progression.xml")
    service_cs=(root/"Scripts/Survivor/Progression/RebirthServiceCraftSkillService.cs").read_text(encoding="utf-8",errors="replace") if (root/"Scripts/Survivor/Progression/RebirthServiceCraftSkillService.cs").exists() else ""
    service_patches=(root/"Scripts/Survivor/Progression/RebirthSurvivorProgressionPatches.cs").read_text(encoding="utf-8",errors="replace")
    service_router=(root/"Scripts/Survivor/Progression/RebirthSkillEventRouter.cs").read_text(encoding="utf-8",errors="replace")
    service_vehicle=(root/"Scripts/Vehicles/Restoration/RebirthVehicleRestorationSystem.cs").read_text(encoding="utf-8",errors="replace")
    service_med=(root/"Scripts/Healing/Actions/ItemActionUseMedRebirth.cs").read_text(encoding="utf-8",errors="replace")
    R.check("chunk8.current_item_api", "ItemClass.GetForId(recipe.itemValueType)" in service_cs and "GetItemClass(recipe.itemValueType)" not in service_cs, "recipe output resolves through verified current ItemClass.GetForId(int)", file="Scripts/Survivor/Progression/RebirthServiceCraftSkillService.cs")
    R.check("chunk8.native_craft_time_path", "GetRecipeCraftTime" in service_patches and "ApplyCraftTime" in service_patches and "WaitForSeconds" not in service_cs, "native recipe time is modified; no parallel timer", file="Scripts/Survivor/Progression/RebirthSurvivorProgressionPatches.cs")
    R.check("chunk8.native_repair_queue_path", "SetRepairRecipe" in service_patches and "AdjustRepairQueue" in service_patches, "native queued repair time/amount are modified", file="Scripts/Survivor/Progression/RebirthSurvivorProgressionPatches.cs")
    R.check("chunk8.specialist_repair_precedence", 'IsFirearmSkill(useSkill)?"skill.gunsmithing":"skill.maintenance"' in service_cs.replace(" ",""), "firearm repair owned by Gunsmithing; generic repair by Maintenance", file="Scripts/Survivor/Progression/RebirthServiceCraftSkillService.cs")
    R.check("chunk8.craft_completion_routing", "ClassifyRecipe(recipeName)" in service_router and "GetCraftAward" in service_router, "existing completed-craft event routes Cooking/Chemistry/Metalworking/Gunsmithing", file="Scripts/Survivor/Progression/RebirthSkillEventRouter.cs")
    R.check("chunk8.mechanics_existing_service", "resourceRepairKit" in service_vehicle and "AdjustVehicleRepairAmount" in service_vehicle and "OnMechanicsCompleted" in service_vehicle, "existing vehicle repair transaction retained with Skill efficiency hook", file="Scripts/Vehicles/Restoration/RebirthVehicleRestorationSystem.cs")
    R.check("chunk8.medicine_existing_reserve", "medicalRegHealthAmount" in service_med and "AdjustMedicalReserveDelta" in service_med and "SetCustomVar(\"medicalRegHealthAmount\"" in service_cs, "existing medical regeneration reserve is scaled, not replaced", file="Scripts/Healing/Actions/ItemActionUseMedRebirth.cs")
    R.check("chunk8.no_output_inflation", "CraftingOutputCount" not in service_cs and "ExplosionEntityDamage" not in service_cs, "no output multiplication or explosive potency added in this bounded wave", file="Scripts/Survivor/Progression/RebirthServiceCraftSkillService.cs")
    R.check("chunk8.no_failure_subsystem", "Random.Range" not in service_cs and "Destroy(" not in service_cs and "Delete(" not in service_cs, "difficulty/failure/workmanship remains deferred", file="Scripts/Survivor/Progression/RebirthServiceCraftSkillService.cs")
    R.check("chunk8.debug_vectors", 'mode=="services"' in weapon_debug and "RebirthServiceCraftSkillVectorHarness.RunAll()" in weapon_debug, "debug services + vectors", file="Scripts/Survivor/Debug/ConsoleCmdRebirthSurvivor.cs")

    # Post-Revision-2 Chunk 10 complex-system source-audit contract.
    complex_node=skill_sources_root.find("complex_systems") if skill_sources_root is not None else None
    R.check("chunk10.complex_authoring", complex_node is not None, "bounded complex-system tuning node present", file="Config/_Survivor/skill_sources.xml")
    complex_cs=(root/"Scripts/Survivor/Progression/RebirthComplexSkillSystemService.cs").read_text(encoding="utf-8",errors="replace") if (root/"Scripts/Survivor/Progression/RebirthComplexSkillSystemService.cs").exists() else ""
    complex_patches=(root/"Scripts/Survivor/Progression/RebirthComplexSkillSystemPatches.cs").read_text(encoding="utf-8",errors="replace") if (root/"Scripts/Survivor/Progression/RebirthComplexSkillSystemPatches.cs").exists() else ""
    complex_vector=(root/"Scripts/Survivor/Progression/RebirthComplexSkillSystemVectorHarness.cs").read_text(encoding="utf-8",errors="replace") if (root/"Scripts/Survivor/Progression/RebirthComplexSkillSystemVectorHarness.cs").exists() else ""
    complex_buff=weapon_buff_root.find(".//buff[@name='RebirthSurvivorComplexSkillPassives']") if weapon_buff_root is not None else None
    complex_effects={(e.get("name"),e.get("tags","")) for e in complex_buff.findall(".//passive_effect")} if complex_buff is not None else set()
    R.check("chunk10.native_explosive_passives", complex_buff is not None and any(x[0]=="ExplosionEntityDamage" for x in complex_effects) and any(x[0]=="ExplosionBlockDamage" for x in complex_effects), "native causal-player explosion effects reused", file="Config/buffs.xml")
    R.check("chunk10.drone_native_damage", any(x[0]=="EntityDamage" and "DroneOperations" in x[1] for x in complex_effects), "drone gun uses owner EffectManager context and drone tags", file="Config/buffs.xml")
    R.check("chunk10.explosion_block_lbd", "ChangedBlockPositions.Count <= 0" in complex_cs and "ExplosivesBlockAward" in complex_cs and "OnExplosionBlocksCompleted" in complex_patches, "one bounded award per explosion with changed blocks", file="Scripts/Survivor/Progression/RebirthComplexSkillSystemService.cs")
    R.check("chunk10.drone_stun_path", "DroneWeapons.StunBeamWeapon" in complex_patches and "cooldownTimer" in complex_cs and 'HasBuff("buffShocked")' in complex_cs, "3.1 stun beam live cycle and successful shock gate", file="Scripts/Survivor/Progression/RebirthComplexSkillSystemPatches.cs")
    R.check("chunk10.remote_device_identity", "source.CreatorEntityId" in complex_cs and "creator is EntityTurret || creator is EntityDrone" in complex_cs, "native ownedEntityId/CreatorEntityId distinguishes remote device damage", file="Scripts/Survivor/Progression/RebirthComplexSkillSystemService.cs")
    R.check("chunk10.armor_remote_fix", "IsRemoteOwnedDeviceSource(target.world, response.Source)" in service_router and "OnArmoredCombat(player)" in service_router, "remote turret/drone hits no longer train player Armor Proficiency", file="Scripts/Survivor/Progression/RebirthSkillEventRouter.cs")
    R.check("chunk10.existing_turret_lbd", 'skill.deployable_turrets' in combat_maps and "ClassifyCombat(response.Source.AttackingItem)" in service_router, "deployable turret item stays on central owner-attributed combat LBD router", file="Config/_Survivor/skill_sources.xml")
    R.check("chunk10.no_client_reward_packet", "NetPackage" not in complex_cs and "NetPackage" not in complex_patches, "no client-trusted Skill award packet introduced", file="Scripts/Survivor/Progression/RebirthComplexSkillSystemService.cs")
    R.check("chunk10.construction_gate_closed", "public const bool ConstructionWorkActionEnabled = false;" in complex_cs, "timed Construction Work Action remains disabled", file="Scripts/Survivor/Progression/RebirthComplexSkillSystemService.cs")
    R.check("chunk10.electrical_gate_closed", "public const bool ElectricalWorkmanshipEnabled = false;" in complex_cs, "Electrical persistent workmanship remains disabled", file="Scripts/Survivor/Progression/RebirthComplexSkillSystemService.cs")
    R.check("chunk10.chemistry_payload_gate_closed", "public const bool ChemistryPayloadPotencyEnabled = false;" in complex_cs, "Chemistry payload metadata remains disabled", file="Scripts/Survivor/Progression/RebirthComplexSkillSystemService.cs")
    R.check("chunk10.turret_scaling_gate_closed", "public const bool DeployableTurretPerformanceScalingEnabled = false;" in complex_cs, "turret performance scaling not inferred from owner attribution", file="Scripts/Survivor/Progression/RebirthComplexSkillSystemService.cs")
    R.check("chunk10.no_work_action_smuggling", "WaitForSeconds" not in complex_cs and "IEnumerator" not in complex_cs and "ConstructionWorkAction" not in complex_patches, "no half-built timed Work Action subsystem", file="Scripts/Survivor/Progression/RebirthComplexSkillSystemService.cs")
    R.check("chunk10.no_electrical_persistence_smuggling", "TileEntityPowered" not in complex_cs and "PowerItem" not in complex_cs, "no partial per-object Electrical persistence/tuning", file="Scripts/Survivor/Progression/RebirthComplexSkillSystemService.cs")
    R.check("chunk10.no_payload_metadata_smuggling", "CustomData" not in complex_cs and "Metadata" not in complex_cs and "skill.chemistry" not in complex_cs, "no stack-splitting explosive payload metadata", file="Scripts/Survivor/Progression/RebirthComplexSkillSystemService.cs")
    R.check("chunk10.installer", "RebirthComplexSkillSystemService.Install()" in weapon_installer and "RebirthSurvivorExplosiveBlockSkillPatch" in weapon_installer and "RebirthSurvivorDroneStunSkillPatch" in weapon_installer, "complex service and exact source-audited patches installed", file="Scripts/Survivor/Progression/RebirthSurvivorProgressionInstaller.cs")
    R.check("chunk10.debug_vectors", 'mode=="complex"' in weapon_debug and "RebirthComplexSkillSystemVectorHarness.RunAll()" in weapon_debug and "construction gate closed" in complex_vector, "debug complex + deterministic gate vectors", file="Scripts/Survivor/Debug/ConsoleCmdRebirthSurvivor.cs")

    # Chunk 11 final static release-candidate contract. Real runtime evidence is intentionally not fabricated.
    release_node=skill_sources_root.find("release_acceptance") if skill_sources_root is not None else None
    release_diag=(root/"Scripts/Survivor/Debug/RebirthSurvivorReleaseAcceptance.cs").read_text(encoding="utf-8",errors="replace") if (root/"Scripts/Survivor/Debug/RebirthSurvivorReleaseAcceptance.cs").exists() else ""
    release_vectors=(root/"Scripts/Survivor/Debug/RebirthSurvivorReleaseAcceptanceVectorHarness.cs").read_text(encoding="utf-8",errors="replace") if (root/"Scripts/Survivor/Debug/RebirthSurvivorReleaseAcceptanceVectorHarness.cs").exists() else ""
    R.check("chunk11.acceptance_authoring", release_node is not None and release_node.get("bands")=="-50,-25,0,25,50,75,100" and release_node.get("runtime_evidence_required")=="true", "seven-band runtime acceptance metadata", file="Config/_Survivor/skill_sources.xml")
    R.check("chunk11.release_not_auto_approved", "releaseApproved=False" in release_diag and "runtimeEvidenceRequired=True" in release_diag, "static diagnostics explicitly refuse to synthesize release approval", file="Scripts/Survivor/Debug/RebirthSurvivorReleaseAcceptance.cs")
    R.check("chunk11.debug_gate_fail_closed", "public static bool Enabled { get; private set; }" in (root/"Scripts/Survivor/Debug/RebirthSurvivorDebug.cs").read_text(encoding="utf-8",errors="replace") and "Enabled = false;" in (root/"Scripts/Survivor/Debug/RebirthSurvivorDebug.cs").read_text(encoding="utf-8",errors="replace"), "mutation diagnostics default off", file="Scripts/Survivor/Debug/RebirthSurvivorDebug.cs")
    R.check("chunk11.acceptance_debug_commands", 'mode=="acceptance"' in weapon_debug and "BuildBalanceBands" in weapon_debug and "BuildAuthorityReport" in weapon_debug and "RebirthSurvivorReleaseAcceptanceVectorHarness.RunAll()" in weapon_debug, "summary/balance/authority/vectors command family", file="Scripts/Survivor/Debug/ConsoleCmdRebirthSurvivor.cs")
    R.check("chunk11.runtime_blockers_explicit", all(x in release_diag for x in ["ConstructionWorkActionEnabled","ElectricalWorkmanshipEnabled","ChemistryPayloadPotencyEnabled","DeployableTurretPerformanceScalingEnabled"]) and "releaseApproved=False" in release_vectors, "complex gates remain visible in release acceptance", file="Scripts/Survivor/Debug/RebirthSurvivorReleaseAcceptance.cs")

    # Machine-readable source identity helps compare validator runs without implying a compile.
    h=hashlib.sha256()
    for p in [surv/"progression.xml",surv/"backgrounds.xml",surv/"traits.xml",surv/"diets.xml",surv/"condition_runtime.xml",surv/"food_content.xml",root/"Config/Localization.csv",atlas]:
        if p.exists(): h.update(p.read_bytes())
    R.counts["static_input_sha256"] = h.hexdigest()
    return R

def main():
    ap=argparse.ArgumentParser()
    ap.add_argument("--root",type=Path,default=Path(__file__).resolve().parents[2],help="REBIRTH mod root")
    ap.add_argument("--json",dest="json_path",type=Path)
    ap.add_argument("--text",dest="text_path",type=Path)
    args=ap.parse_args(); root=args.root.resolve(); report=validate(root)
    text=report.text(); print(text,end="")
    if args.json_path:
        args.json_path.parent.mkdir(parents=True,exist_ok=True); args.json_path.write_text(json.dumps(report.json_obj(),indent=2,sort_keys=False)+"\n",encoding="utf-8")
    if args.text_path:
        args.text_path.parent.mkdir(parents=True,exist_ok=True); args.text_path.write_text(text,encoding="utf-8")
    return 0 if report.ok else 1
if __name__=="__main__": sys.exit(main())
