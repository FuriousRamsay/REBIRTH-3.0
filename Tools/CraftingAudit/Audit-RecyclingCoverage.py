"""Static scrap-rule coverage against a freshly composed native + REBIRTH registry.

This is an inventory, not a gameplay eligibility or output-balance verdict. External
mods and runtime-generated item classes are not included in the composed inputs.
"""
import argparse
import csv
import json
import re
import xml.etree.ElementTree as ET
from collections import Counter, defaultdict
from pathlib import Path


STATIONS = (
    "SonjaRecyclerStation",
    "SonjaOutfitDesigner",
    "WorkbenchShredder001_FR",
    "SonjaAmmoRecyclerStation",
)


def parse_rule(node, index):
    fields = {key: node.get(key, "") for key in ("item", "item_prefix", "item_suffix", "item_contains", "item_regex")}
    star = False
    if "*" in fields["item_prefix"]:
        raw = fields["item_prefix"]
        core = raw.strip("*")
        if core:
            star = True
            fields["item_prefix"] = ""
            if raw.startswith("*") and raw.endswith("*"):
                fields["item_contains"] = core
            elif raw.startswith("*"):
                fields["item_suffix"] = core
            elif raw.endswith("*"):
                fields["item_prefix"] = core
    present = [(key, value) for key, value in fields.items() if value]
    if len(present) != 1:
        return None
    kind, pattern = present[0]
    weight = 5000 if star else {"item": 4000, "item_regex": 3000, "item_prefix": 2000,
                                "item_suffix": 2000, "item_contains": 1000}[kind]
    regex = None
    if kind == "item_regex":
        try:
            regex = re.compile(pattern, re.IGNORECASE)
        except re.error:
            return None
    return {"station": node.get("station", ""), "index": index, "kind": kind,
            "pattern": pattern, "score": weight + len(pattern), "regex": regex,
            "outputs": [child.get("name", "") for child in node.findall("ingredient")],
            "tools": [part.strip() for part in re.split(r"[,;]", node.get("scrap_tool", node.get("scrap_tools", ""))) if part.strip()]}


def matches(rule, name):
    pattern = rule["pattern"].lower()
    value = name.lower()
    return (value == pattern if rule["kind"] == "item" else
            value.startswith(pattern) if rule["kind"] == "item_prefix" else
            value.endswith(pattern) if rule["kind"] == "item_suffix" else
            pattern in value if rule["kind"] == "item_contains" else
            bool(rule["regex"].search(name)))


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--root", type=Path, default=Path(__file__).resolve().parents[2])
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()
    root = args.root.resolve()
    output = (args.output or root / "_Documentation/Recycling/Coverage_20261031").resolve()
    output.mkdir(parents=True, exist_ok=True)
    items = ET.parse(output / "EFFECTIVE_items_rebirth.xml").getroot().findall("item")
    blocks = ET.parse(output / "EFFECTIVE_blocks_rebirth.xml").getroot().findall("block")
    modifiers = ET.parse(output / "EFFECTIVE_item_modifiers_rebirth.xml").getroot().findall("item_modifier")
    item_names = {node.get("name") for node in items if node.get("name")}
    block_names = {node.get("name") for node in blocks if node.get("name")}
    modifier_names = {node.get("name") for node in modifiers if node.get("name")}
    definitions = item_names | block_names | modifier_names
    rules = [rule for index, node in enumerate(ET.parse(root / "Config/RebirthScrap.xml").getroot().findall("scrap"), 1)
             if (rule := parse_rule(node, index)) and rule["station"] in STATIONS]
    by_station = defaultdict(list)
    for rule in rules:
        by_station[rule["station"]].append(rule)
    rows = []
    matched = Counter()
    invalid = []
    for rule in rules:
        missing = [name for name in rule["outputs"] + rule["tools"] if name not in definitions]
        if rule["kind"] == "item" and rule["pattern"] not in definitions:
            missing.append("input:" + rule["pattern"])
        if missing:
            invalid.append({"station": rule["station"], "index": rule["index"],
                            "matcher": rule["kind"] + ":" + rule["pattern"], "missing": ";".join(missing)})
    for name in sorted(definitions, key=str.casefold):
        kinds = ("item" if name in item_names else "") + ("+block" if name in block_names else "") + ("+modifier" if name in modifier_names else "")
        for station in STATIONS:
            best = None
            for rule in by_station[station]:
                if matches(rule, name) and (best is None or rule["score"] > best["score"]):
                    best = rule
            if best:
                matched[station] += 1
            rows.append({"name": name, "definition": kinds, "station": station,
                         "rule_index": best["index"] if best else "",
                         "matcher": best["kind"] + ":" + best["pattern"] if best else "",
                         "outputs": ";".join(best["outputs"]) if best else ""})
    for filename, data in (("COVERAGE.csv", rows), ("UNRESOLVED_RULE_REFERENCES.csv", invalid)):
        with (output / filename).open("w", newline="", encoding="utf-8") as stream:
            writer = csv.DictWriter(stream, fieldnames=list(data[0]) if data else ["name"])
            writer.writeheader()
            writer.writerows(data)
    likely_blocked = [row for row in invalid if not any(part.startswith("input:") for part in row["missing"].split(";"))]
    with (output / "LIKELY_BLOCKED_CURRENT_INPUT_RULES.csv").open("w", newline="", encoding="utf-8") as stream:
        writer = csv.DictWriter(stream, fieldnames=list(invalid[0]) if invalid else ["name"])
        writer.writeheader()
        writer.writerows(likely_blocked)
    summary = {"composed_items": len(item_names), "composed_blocks": len(block_names), "composed_modifiers": len(modifier_names),
               "distinct_definition_names": len(definitions), "rules": len(rules),
               "matched_names_by_station": dict(matched), "unresolved_rule_references": len(invalid),
               "likely_blocked_current_input_rules": len(likely_blocked),
               "scope": "Static native + this mod XML only; block names are candidates, not proof of a player-held item class."}
    (output / "SUMMARY.json").write_text(json.dumps(summary, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(summary, indent=2))


if __name__ == "__main__":
    main()
