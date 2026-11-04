"""Inventory block economic data; no automatic price changes."""
import csv
import json
import xml.etree.ElementTree as ET
from collections import Counter
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
EFFECTIVE = ROOT / "_Documentation/BlockPricing/Effective/EFFECTIVE_blocks_rebirth.xml"
BASE = Path(r"C:\Program Files (x86)\Steam\steamapps\common\7 Days To Die\Data\Config\blocks.xml")
OUT = ROOT / "_Documentation/BlockPricing"


def blocks(path):
    return {node.get("name"): node for node in ET.parse(path).getroot().findall("block") if node.get("name")}


def direct(node, name):
    return next((p.get("value", "") for p in node.findall("property") if p.get("name") == name), "") if node is not None else ""


def prop(nodes, name, key):
    seen = set()
    while name and name not in seen:
        seen.add(name)
        node = nodes.get(name)
        if node is None:
            return ""
        value = direct(node, key)
        if value:
            return value
        name = direct(node, "Extends")
    return ""


def value(text):
    try:
        return float(text)
    except (ValueError, TypeError):
        return 0.0


current = blocks(EFFECTIVE)
base = blocks(BASE)
rows = []
for name in sorted(current, key=str.casefold):
    node = current[name]
    price = value(prop(current, name, "EconomicValue"))
    original = value(prop(base, name, "EconomicValue"))
    rows.append({
        "name": name,
        "price": f"{price:g}",
        "base_price": f"{original:g}" if name in base else "",
        "direct_price": direct(node, "EconomicValue"),
        "extends": direct(node, "Extends"),
        "creative": prop(current, name, "CreativeMode"),
        "sellable": prop(current, name, "SellableToTrader"),
        "economic_sell_scale": prop(current, name, "EconomicSellScale"),
        "take_delay": prop(current, name, "TakeDelay"),
        "group": prop(current, name, "Group"),
        "filter_tags": prop(current, name, "FilterTags"),
        "material": prop(current, name, "Material"),
        "loot_list": prop(current, name, "LootList"),
        "source": "native" if name in base else "rebirth",
        "changed_from_base": "yes" if name in base and price != original else "",
    })
with (OUT / "BLOCK_PRICES.csv").open("w", newline="", encoding="utf-8") as stream:
    writer = csv.DictWriter(stream, fieldnames=list(rows[0]))
    writer.writeheader()
    writer.writerows(rows)

summary = {
    "effective_blocks": len(rows),
    "zero_or_missing_economic_value": sum(value(r["price"]) <= 0 for r in rows),
    "priced_above_1000": sum(value(r["price"]) > 1000 for r in rows),
    "native_blocks_changed_by_mod": sum(bool(r["changed_from_base"]) for r in rows),
    "new_rebirth_blocks": sum(r["source"] == "rebirth" for r in rows),
    "creative_modes": dict(Counter(r["creative"] or "(unset)" for r in rows)),
}
(OUT / "SUMMARY.json").write_text(json.dumps(summary, indent=2) + "\n", encoding="utf-8")
print(json.dumps(summary, indent=2))
