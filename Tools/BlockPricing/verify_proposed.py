"""Check inherited prices on the XML-projected block registry."""
import csv
import xml.etree.ElementTree as ET
from collections import Counter
from pathlib import Path

root = Path(__file__).resolve().parents[2]
registry = {b.get("name"): b for b in ET.parse(root / "_Documentation/BlockPricing/Final/EFFECTIVE_blocks_rebirth.xml").getroot().findall("block")}


def inherited(name, property_name):
    seen = set()
    while name in registry and name not in seen:
        seen.add(name)
        block = registry[name]
        direct = {p.get("name"): p.get("value") for p in block.findall("property")}
        if property_name in direct:
            return direct[property_name]
        name = direct.get("Extends")
    return ""


review = list(csv.DictReader((root / "_Documentation/BlockPricing/ZERO_PLAYER_REVIEW.csv").open(encoding="utf-8")))
for bucket in ("conditional_storage", "open_safe_priced_pickup_target", "special_artwork_inherits_frame", "grill_priced", "tv_priced"):
    rows = [r for r in review if r["bucket"] == bucket]
    tally = Counter(inherited(r["name"], "EconomicValue") or "0" for r in rows)
    print(bucket, len(rows), dict(tally))

safe_sources = [r["name"] for r in review if r["bucket"] == "open_safe_priced_pickup_target"]
assert len(safe_sources) == 14
for source in safe_sources:
    intact = source.replace("Open", "", 1)
    assert intact in registry
    expected = 690 if source.startswith("cntGunSafe") else 300
    assert float(inherited(intact, "EconomicValue")) == expected
    current = intact
    visited = set()
    for _ in range(8):
        if current == source or current in visited or current not in registry:
            break
        visited.add(current)
        current = inherited(current, "DowngradeBlock")
    assert current == source, (intact, source, current)

for name, expected in (("bed02", "0.015"), ("bed02White", "0.015"),
                       ("bunkBedMade", "0.03"), ("bunkBedMessy", "0.03"),
                       ("gurneyBed", "0.03"), ("hospitalBed", "0.03"),
                       ("wheelchairWhite", "0.03"), ("wheelchairBlack", "0.03")):
    assert inherited(name, "EconomicSellScale") == expected, name

assert all(float(inherited(r["name"], "EconomicValue") or 0) > 0
           for r in review if r["bucket"] in
           ("conditional_storage", "special_artwork_inherits_frame", "grill_priced", "tv_priced"))
print("PASS: all non-safe target families have inherited positive block prices; beds and wheelchair scales inherit")
