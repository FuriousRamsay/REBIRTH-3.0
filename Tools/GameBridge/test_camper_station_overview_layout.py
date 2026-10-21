"""Offline authored-geometry/atlas checks; does not launch or validate game rendering."""
from pathlib import Path
import csv
import json
import sys
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]
XML = Path(sys.argv[1]) if len(sys.argv) > 1 else ROOT / "Config/_Vehicles/camper_station_picker_windows.xml"
tree = ET.parse(XML)


def node(name, parent=tree):
    found = parent.findall(f".//*[@name='{name}']")
    assert len(found) == 1, (name, len(found))
    return found[0]


def bounds(e):
    x, y = map(float, e.get("pos", "0,0").split(","))
    w, h = float(e.get("width")), float(e.get("height"))
    if e.get("pivot") == "center":
        return x - w / 2, y - h / 2, x + w / 2, y + h / 2
    return x, y - h, x + w, y


def center(e):
    x0, y0, x1, y1 = bounds(e)
    return (x0 + x1) / 2, (y0 + y1) / 2


summary = node("workshopSummary")
summary_pairs = []
for i, suffix in enumerate(("Installed", "Working", "Idle", "NeedsFuel")):
    icon, label = node(f"workshopMetricIcon{i}"), node(f"workshop{suffix}")
    gap = bounds(label)[0] - bounds(icon)[2]
    assert gap == 18, (suffix, gap)
    assert center(icon)[1] == center(label)[1] == -float(summary.get("height")) / 2
    assert bounds(label)[2] <= float(summary.get("width"))
    summary_pairs.append({"label": suffix, "gap": gap, "center_y": center(label)[1]})

cards = []
for i in range(20):
    card = node(f"camperStationChoice{i}")
    metrics = []
    for suffix in ("Fuel", "Queue", "Output"):
        label, icon = node(f"station{suffix}{i}"), node(f"stationMetricIcon{suffix}{i}")
        assert center(label)[1] == center(icon)[1]
        assert bounds(label)[0] - bounds(icon)[2] == 8
        assert bounds(label)[2] <= float(card.get("width")) - 18
        assert bounds(label)[1] > bounds(node(f"stationProgressTrack{i}"))[3]
        metrics.append(suffix)
    caption, button = node("stationUseCaption", card), node("clickable", card)
    assert center(caption) == center(button)
    assert bounds(caption) == bounds(button)
    assert caption.get("justify") == "center"
    assert button.get("controller") == "RebirthVehicleActionButton, RebirthUtils"
    cards.append({"index": i, "centered_metrics": metrics, "caption_center": center(caption)})

# Compare with actual existing sibling windows, rather than inventing a palette.
background = node("camperOverviewBackground")
for source in ("camper_energy_windows.xml", "camper_modules_windows.xml"):
    reference = ET.parse(ROOT / "Config/_Vehicles" / source)
    assert any(e.get("color") == background.get("color") and e.get("depth") == "0"
               and e.get("sprite") == background.get("sprite") for e in reference.findall(".//sprite"))

# Recorded native atlas inventory includes alpha bounds, not just XML references.
inventory = ROOT / "_Documentation/Implementation/SurvivorSystem/Chunk01/AuditArtifacts/VanillaUIAtlasSpriteMetrics.csv"
with inventory.open(newline="", encoding="utf-8-sig") as f:
    sprites = {row["sprite"]: row for row in csv.DictReader(f)}
checked = set()
for e in tree.findall(".//sprite"):
    if not e.get("name", "").startswith(("workshopMetricIcon", "stationMetricIcon")):
        continue
    sprite = e.get("sprite")
    assert sprite in sprites, sprite
    assert float(sprites[sprite]["alpha_area_ratio"]) > 0, sprite
    assert e.get("atlas") == "UIAtlas"
    checked.add(sprite)
assert node("workshopMetricIcon0").get("sprite") == "ui_game_symbol_workbench"
assert node("workshopMetricIcon1").get("sprite") == "ui_game_symbol_hammer"
print(json.dumps({"result": "PASS", "summary": summary_pairs, "cards_checked": len(cards),
                  "metric_icons_present_in_recorded_native_atlas": sorted(checked),
                  "main_background": background.attrib,
                  "limits": "Authored rectangles and recorded atlas inventory only; no game or font rendering tested."}, indent=2))
