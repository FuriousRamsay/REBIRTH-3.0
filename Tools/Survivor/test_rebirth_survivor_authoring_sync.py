#!/usr/bin/env python3
from pathlib import Path
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]
CFG = ROOT / "Config" / "_Survivor"
errors = []
checks = 0

def check(cond, msg):
    global checks
    checks += 1
    if not cond:
        errors.append(msg)

def parse(name):
    p = CFG / name
    check(p.is_file(), f"missing {p.relative_to(ROOT)}")
    try:
        return ET.parse(p).getroot()
    except Exception as exc:
        errors.append(f"parse failure {p.relative_to(ROOT)}: {exc}")
        return ET.Element("invalid")

# All runtime definition/content files are intentionally part of the authoritative resync set.
required = [
    "backgrounds.xml", "condition_profiles.xml", "condition_runtime.xml", "diets.xml",
    "food_content.xml", "item_modifiers.xml", "items.xml", "loot.xml", "progression.xml",
    "recipe_knowledge.xml", "recipes.xml", "skill_sources.xml", "support_profiles.xml",
    "traders.xml", "traits.xml",
]
for name in required:
    check((CFG / name).is_file(), f"required authoring file missing: Config/_Survivor/{name}")

backgrounds_root = parse("backgrounds.xml")
traits_root = parse("traits.xml")
diets_root = parse("diets.xml")
progression_root = parse("progression.xml")

backgrounds = {e.get("id"): e for e in backgrounds_root.findall("background")}
traits = {e.get("id"): e for e in traits_root.findall("trait")}
diets = {e.get("id"): e for e in diets_root.findall("diet")}

check(len(backgrounds) == 28, f"expected 28 backgrounds, got {len(backgrounds)}")
check(len(traits) == 137, f"expected 137 authored traits, got {len(traits)}")
check(len(diets) == 4, f"expected 4 diets, got {len(diets)}")

expected_diet_points = {
    "diet.unrestricted": 0,
    "diet.vegetarian": 2,
    "diet.vegan": 4,
    "diet.carnivore": 3,
}
for diet_id, expected in expected_diet_points.items():
    node = diets.get(diet_id)
    check(node is not None, f"missing {diet_id}")
    if node is not None:
        try:
            actual = int(node.get("points", "999999"))
        except Exception:
            actual = 999999
        check(actual == expected, f"{diet_id} expected points={expected}, got {node.get('points')}")

# Revision-2.1 launch boundary: a Background's restricted list must contain only Traits that
# are actually marked restricted, and every restricted Trait must link back to each allowed Background.
for bg_id, bg in sorted(backgrounds.items()):
    restricted_parent = bg.find("restricted_traits")
    restricted = set()
    if restricted_parent is not None:
        restricted = {x.get("id") for x in restricted_parent.findall("trait") if x.get("id")}
    for trait_id in sorted(restricted):
        tr = traits.get(trait_id)
        check(tr is not None, f"{bg_id} restricted trait missing from traits.xml: {trait_id}")
        if tr is None:
            continue
        check(tr.get("availability") == "restricted",
              f"{bg_id} lists non-restricted Trait {trait_id} as Background-restricted (availability={tr.get('availability')})")
        allowed_parent = tr.find("allowed_backgrounds")
        allowed = set()
        if allowed_parent is not None:
            allowed = {x.get("id") for x in allowed_parent.findall("background") if x.get("id")}
        check(bg_id in allowed, f"{bg_id} lists {trait_id}, but Trait does not allow that Background")

for trait_id, tr in sorted(traits.items()):
    if tr.get("availability") != "restricted":
        continue
    allowed_parent = tr.find("allowed_backgrounds")
    allowed = set()
    if allowed_parent is not None:
        allowed = {x.get("id") for x in allowed_parent.findall("background") if x.get("id")}
    for bg_id in sorted(allowed):
        bg = backgrounds.get(bg_id)
        check(bg is not None, f"{trait_id} allows missing Background {bg_id}")
        if bg is None:
            continue
        restricted_parent = bg.find("restricted_traits")
        restricted = set()
        if restricted_parent is not None:
            restricted = {x.get("id") for x in restricted_parent.findall("trait") if x.get("id")}
        check(trait_id in restricted,
              f"{trait_id} allows Background {bg_id}, but Background does not list Trait in restricted_traits")

# Explicit sentinels for the exact stale-generation failure seen in the 2026-08-26 runtime log.
pt = backgrounds.get("background.personal_trainer")
if pt is not None:
    parent = pt.find("restricted_traits")
    pt_restricted = {x.get("id") for x in parent.findall("trait")} if parent is not None else set()
    check("trait.conditioned" not in pt_restricted, "stale background generation: Personal Trainer still restricts trait.conditioned")
    check("trait.training_discipline" not in pt_restricted, "stale background generation: Personal Trainer still restricts trait.training_discipline")
    check("trait.old_training_injury" in pt_restricted, "Personal Trainer must retain trait.old_training_injury as restricted")

# The current Trait economy intentionally applies every selected negative Trait refund.
creation = progression_root.find("creation")
try:
    max_refund = int(creation.get("max_negative_trait_refund", "-999")) if creation is not None else -999
except Exception:
    max_refund = -999
check(max_refund == -1, f"expected max_negative_trait_refund=-1 (unlimited), got {creation.get('max_negative_trait_refund') if creation is not None else None}")

if errors:
    print("REBIRTH Survivor Runtime Fix 04 authoring sync: FAIL")
    print(f"checks={checks} errors={len(errors)}")
    for e in errors:
        print("ERROR:", e)
    raise SystemExit(1)

print("REBIRTH Survivor Runtime Fix 04 authoring sync: PASS")
print(f"checks={checks} errors=0 backgrounds={len(backgrounds)} traits={len(traits)} diets={len(diets)}")
print("diet_points=unrestricted:0,vegetarian:+2,vegan:+4,carnivore:+3")
