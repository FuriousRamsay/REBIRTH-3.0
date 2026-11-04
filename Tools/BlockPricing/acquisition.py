"""Classify block acquisition evidence without inferring lootability from CreativeMode."""
import csv
import xml.etree.ElementTree as ET
from collections import Counter
from pathlib import Path

root = Path(__file__).resolve().parents[2]
mod = Path(r"C:\Program Files (x86)\Steam\steamapps\common\7 Days To Die\Mods\zzz_REBIRTH__3_0")
native = Path(r"C:\Program Files (x86)\Steam\steamapps\common\7 Days To Die\Data\Config")
blocks = {b.get("name"): b for b in ET.parse(root / "_Documentation/BlockPricing/Effective/EFFECTIVE_blocks_rebirth.xml").getroot().findall("block")}
price_rows = list(csv.DictReader((root / "_Documentation/BlockPricing/BLOCK_PRICES.csv").open(encoding="utf-8")))


def lineage(name):
    seen = set()
    while name and name not in seen and name in blocks:
        seen.add(name)
        node = blocks[name]
        yield node
        name = next((p.get("value") for p in node.findall("property") if p.get("name") == "Extends"), None)


def prop(name, key):
    for node in lineage(name):
        for p in node.findall("property"):
            if p.get("name") == key:
                return p.get("value", "")
    return ""


recipe_outputs = set()
for path in [native / "recipes.xml", *(p for p in mod.glob("Config/**/*.xml") if p.name == "recipes.xml" or p.stem.endswith("_recipes"))]:
    try:
        doc = ET.parse(path)
    except ET.ParseError:
        continue
    for recipe in doc.iter("recipe"):
        if recipe.get("name"):
            recipe_outputs.add(recipe.get("name"))

other_sources = Counter()
for kind in ("loot", "traders"):
    doc = ET.parse(root / f"_Documentation/BlockPricing/Effective/EFFECTIVE_{kind}_rebirth.xml")
    for node in doc.iter():
        if node.get("name") in blocks and node.tag in ("item", "block"):
            other_sources[(node.get("name"), kind)] += 1

rows = []
for price in price_rows:
    name = price["name"]
    drops_self = any(d.get("name") == name and d.get("event") in ("Destroy", "Harvest", "Fall") for node in lineage(name) for d in node.findall("drop"))
    evidence = []
    if name in recipe_outputs:
        evidence.append("recipe_output")
    if prop(name, "CanPickup").lower() == "true":
        evidence.append("can_pickup")
    try:
        delay = float(prop(name, "TakeDelay"))
    except ValueError:
        delay = 0
    if delay > 0:
        evidence.append("take_delay")
    if drops_self:
        evidence.append("self_drop")
    if other_sources[(name, "loot")]:
        evidence.append("loot_item")
    if other_sources[(name, "traders")]:
        evidence.append("trader_item")
    rows.append({**price, "evidence": ";".join(evidence), "recipe": name in recipe_outputs,
                 "pickup": prop(name, "CanPickup"), "self_drop": drops_self,
                 "loot_refs": other_sources[(name, "loot")], "trader_refs": other_sources[(name, "traders")]})

out = root / "_Documentation/BlockPricing/ACQUISITION_EVIDENCE.csv"
with out.open("w", newline="", encoding="utf-8") as stream:
    writer = csv.DictWriter(stream, fieldnames=list(rows[0]))
    writer.writeheader()
    writer.writerows(rows)

zero_player = [r for r in rows if r["creative"] == "Player" and float(r["price"]) == 0]
high = [r for r in rows if float(r["price"]) > 1000]
print("zero_player", len(zero_player), Counter(e for r in zero_player for e in r["evidence"].split(";") if e))
print("high", len(high), Counter(e for r in high for e in r["evidence"].split(";") if e))
print("zero_player_with_evidence")
for r in zero_player:
    if r["evidence"]:
        print(r["name"], r["evidence"])


def review_bucket(row):
    name = row["name"]
    tags = set(row["filter_tags"].split(","))
    if "SC_automotive" in tags:
        return "automotive_absolute_denial"
    if name.startswith("hiddenSafe"):
        return "hidden_safe_downgrade_denial"
    if name.startswith(("cntWallSafeOpen", "cntDeskSafeOpen", "cntGunSafeOpen")):
        return "open_safe_priced_pickup_target"
    if "SC_loot" in tags:
        return "conditional_storage"
    if name.startswith("rock") and row["pickup"] == "true":
        return "pickup_returns_resource_rock"
    if name in ("candleTableLightPlayer", "candleWallLightPlayer", "wallTorchLightPlayer"):
        return "priced_lighting"
    if name == "chairCampingVariantHelper":
        return "priced_chair"
    if name in {"pictureFrameCarrier", "pictureFrameGunSchematic", "pictureFrameJetGirls", "pictureFrameTank"}:
        return "special_artwork_inherits_frame"
    if (name.startswith(("pictureCanvas_", "pictureFrame")) or name in
            {"paintingBen", "paintingDerek", "paintingDuke", "paintingKen", "paintingLorien", "paintingNoah"}):
        return "player_artwork"
    if name.startswith(("cntGasGrill", "cntCharcoalGrill")):
        return "grill_priced"
    if name.startswith("tvCRT"):
        return "tv_priced"
    if "SC_crops" in tags or "SC_trees" in tags or "SC_shrubbery" in tags:
        return "vegetation_denial"
    return "other_review"


review = [{"name": r["name"], "bucket": review_bucket(r), "evidence": r["evidence"],
           "filter_tags": r["filter_tags"], "source": r["source"]} for r in zero_player]
with (root / "_Documentation/BlockPricing/ZERO_PLAYER_REVIEW.csv").open("w", newline="", encoding="utf-8") as stream:
    writer = csv.DictWriter(stream, fieldnames=list(review[0]))
    writer.writeheader()
    writer.writerows(review)
print("zero_player_buckets", Counter(r["bucket"] for r in review))
