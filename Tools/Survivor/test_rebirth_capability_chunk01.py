#!/usr/bin/env python3
from pathlib import Path
import re
import sys
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]
CFG = ROOT / "Config" / "_Survivor"
SRC = ROOT / "Scripts" / "Survivor"
checks = []

def check(name, ok, detail=""):
    checks.append((name, bool(ok), detail))

def parse(name):
    return ET.parse(CFG / name).getroot()

progression = parse("progression.xml")
skill_ids = {x.attrib.get("id", "") for x in progression.findall("./skills/skill")}
knowledge_ids = {x.attrib.get("id", "") for x in progression.findall("./knowledge/knowledge")}
cap = parse("capabilities.xml")
blue = parse("blueprints.xml")
projects = parse("projects.xml")
legacy = parse("recipe_knowledge.xml")

check("capability schema root", cap.tag == "survivor_capabilities" and cap.attrib.get("schema_version") == "1")
check("blueprint schema root", blue.tag == "survivor_blueprints" and blue.attrib.get("schema_version") == "1")
check("project schema root", projects.tag == "survivor_projects" and projects.attrib.get("schema_version") == "1")

caps = cap.findall("capability")
check("controlled authored capability count", len(caps) == 1, str(len(caps)))
ef = next((x for x in caps if x.attrib.get("target_id") == "electricfencepost"), None)
check("electric fence capability authored", ef is not None)
if ef is not None:
    check("electric fence target is recipe", ef.attrib.get("target_type") == "recipe")
    req = ef.find("requires_all")
    check("electric fence uses AND group", req is not None)
    knowledge = req.findall("knowledge") if req is not None else []
    skills = req.findall("skill") if req is not None else []
    check("electric fence knowledge requirement", len(knowledge) == 1 and knowledge[0].attrib.get("id") == "knowledge.electrical.fundamentals")
    values = {x.attrib.get("id"): (x.attrib.get("minimum"), x.attrib.get("recommended")) for x in skills}
    check("electric fence metalworking 20/30", values.get("skill.metalworking") == ("20", "30"), repr(values.get("skill.metalworking")))
    check("electric fence electrical 20/30", values.get("skill.electrical") == ("20", "30"), repr(values.get("skill.electrical")))
    check("all authored skill IDs exist", all(x.attrib.get("id") in skill_ids for x in skills))
    check("all authored knowledge IDs exist", all(x.attrib.get("id") in knowledge_ids for x in knowledge))

legacy_names = {x.attrib.get("name") for x in legacy.findall("recipe")}
check("electric fence removed from legacy knowledge map", "electricfencepost" not in legacy_names)
check("legacy knowledge mappings preserved", len(legacy_names) >= 20, str(len(legacy_names)))

runtime = (SRC / "Progression" / "RebirthProgressionRuntimeConfig.cs").read_text(encoding="utf-8")
patches = (SRC / "Progression" / "RebirthSurvivorProgressionPatches.cs").read_text(encoding="utf-8")
registry = (SRC / "Capability" / "RebirthCapabilityRegistry.cs").read_text(encoding="utf-8")
service = (SRC / "Capability" / "RebirthCapabilityService.cs").read_text(encoding="utf-8")
integration = (SRC / "Capability" / "RebirthRecipeCapabilityIntegration.cs").read_text(encoding="utf-8")
console = (SRC / "Debug" / "ConsoleCmdRebirthSurvivor.cs").read_text(encoding="utf-8")

check("runtime config loads capability registry", "RebirthCapabilityRegistry.Load(root, GetRecipeRulesSnapshot())" in runtime)
check("presentation patch routes to capability integration", "RebirthRecipeCapabilityIntegration.ApplyPresentationGate" in patches)
check("workstation patch routes to capability integration", "RebirthRecipeCapabilityIntegration.AuthorizeActiveQueue" in patches)
check("completion routing preserved", "RebirthRecipeCapabilityIntegration.OnWorkstationCraftComplete" in patches)
check("server authority remains explicit", "RebirthWorldCharacterRepository.IsServerAuthority" in integration)
check("unauthorized workstation queue fails closed", "RejectActiveQueue" in integration and "missing-capability:" in integration)
check("no second ingredient consumption path", not any(t in integration for t in ["RemoveItem", "DecItem", "TakeItem", "ConsumeItem"]))
check("structured evaluation exists", "class RebirthCapabilityEvaluation" in (SRC / "Capability" / "RebirthCapabilityEvaluation.cs").read_text(encoding="utf-8"))
check("AND evaluator implemented", "requirement.Kind==RebirthCapabilityKinds.All" in service)
check("OR evaluator implemented", "requirement.Kind==RebirthCapabilityKinds.Any" in service)
check("skill evaluator uses authoritative existing skill reader", "RebirthServiceCraftSkillService.TryGetSkillValue" in service)
check("knowledge evaluator uses knowledge owner service", "RebirthKnowledgeService.HasKnowledge" in service)
check("recommended proficiency is warning-only", "WarningOnly=true" in service and "recommended" in service.lower())
check("blueprint fail-closed stub exists", "class RebirthBlueprintService" in service and "return string.IsNullOrEmpty(blueprintId);" in service)
check("capability semantic hash exists", "ComputeSemanticHash" in registry and "SHA256.Create" in registry)
check("unknown Skill validation exists", "references unknown Skill ID" in registry)
check("unknown Knowledge validation exists", "references unknown Knowledge ID" in registry)
check("unknown Blueprint validation exists", "references unknown Blueprint ID" in registry)
check("duplicate capability validation exists", "duplicate Capability ID" in registry)
check("recipe target collision validation exists", "recipe target collision" in registry)
check("skill signed-range validation exists", "Skill minimum out of range" in registry and "Skill recommended out of range" in registry)
check("recommended cannot be below minimum", "Skill recommended < minimum" in registry)
check("project dependency validation exists", "references unknown dependency" in registry)
check("project cycle validation exists", "cyclic operation dependency" in registry)
check("legacy adapter exists", "AdaptLegacyRules" in registry and "LegacyAdapted" in registry)
check("pre-index by skill exists", "BySkill" in registry and "GetBySkill" in registry)
check("pre-index by knowledge exists", "ByKnowledge" in registry and "GetByKnowledge" in registry)
check("capability diagnostic command exists", 'rbsurvivor capability' in console and "ExecuteCapability" in console)
check("recipe diagnostic command exists", "BuildRecipeDiagnostic" in console)

# No Blueprint requirement may be live before persistence/network ownership exists.
check("no live blueprint requirement in chunk01", not any(x.tag == "blueprint" for x in cap.iter()))
check("no enabled project in chunk01", not any(x.attrib.get("enabled", "false").lower() == "true" for x in projects.findall("project")))

# Synthetic requirement semantics: verify intended hard-min/recommended behavior independent of game runtime.
def skill(value, minimum=None, recommended=None):
    hard = minimum is None or value >= minimum
    warning = hard and recommended is not None and value < recommended
    return hard, warning

check("vector skill below minimum blocks", skill(19, 20, 30) == (False, False))
check("vector skill at minimum allows with recommendation", skill(20, 20, 30) == (True, True))
check("vector skill at recommendation fully allows", skill(30, 20, 30) == (True, False))
check("vector recommended-only never hard blocks", skill(-10, None, 20) == (True, True))
check("vector AND fails when one domain missing", all([skill(20,20,30)[0], skill(19,20,30)[0]]) is False)
check("vector AND passes when both domains meet hard min", all([skill(20,20,30)[0], skill(20,20,30)[0]]) is True)
check("vector OR passes when one alternative passes", any([skill(5,20,30)[0], skill(35,30,40)[0]]) is True)

passed = sum(1 for _, ok, _ in checks if ok)
failed = [(n, d) for n, ok, d in checks if not ok]
print(f"REBIRTH Capability Chunk01: {passed}/{len(checks)} checks passed")
for name, detail in failed:
    print("FAIL:", name, detail)
if failed:
    sys.exit(1)
