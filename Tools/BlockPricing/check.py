"""Static checks for the isolated block-price fragment."""
import xml.etree.ElementTree as ET
from pathlib import Path

root = Path(__file__).resolve().parents[2]
blocks = {b.get("name"): b for b in ET.parse(root / "_Documentation/BlockPricing/Effective/EFFECTIVE_blocks_rebirth.xml").getroot().findall("block")}
fragment = ET.parse(root / "Config/_Economy/block_prices.xml").getroot()
station_names = {"cntForgeWorkstationBroken", "cntCollapsedWorkbench", "cntCollapsedWorkbenchEmpty", "cntCollapsedCementMixer", "cntCollapsedChemistryStation"}
assert len(fragment.findall("append")) == 12
for name in station_names:
    assert name in blocks
    assert all(not any(p.get("name") == key for p in blocks[name].findall("property")) for key in ("EconomicValue", "SellableToTrader"))
assert "candleWallLight" in blocks
assert not any(p.get("name") == "EconomicValue" for p in blocks["candleWallLight"].findall("property"))
assert all(name in fragment.findall("append")[0].get("xpath") for name in station_names)
assert "candleWallLight" in fragment.findall("append")[1].get("xpath")
assert [(p.get("name"), p.get("value")) for p in fragment.findall("append")[0].findall("property")] == [("EconomicValue", "1"), ("SellableToTrader", "false")]
assert [(p.get("name"), p.get("value")) for p in fragment.findall("append")[1].findall("property")] == [("EconomicValue", "24")]
for name, value in (("wallTorchLightPlayer", "10"), ("chairCampingVariantHelper", "5")):
    assert name in blocks
    assert not any(p.get("name") == "EconomicValue" for p in blocks[name].findall("property"))
    override = next(a for a in fragment.findall("append") if name in a.get("xpath"))
    assert [(p.get("name"), p.get("value")) for p in override.findall("property")] == [("EconomicValue", value)]
art_names = {"paintingBen", "paintingDerek", "paintingDuke", "paintingKen", "paintingLorien", "paintingNoah"}
art_names.update(name for name in blocks if (name.startswith("pictureCanvas_") or name.startswith("pictureFrame")) and any(p.get("name") == "CreativeMode" and p.get("value") == "Player" for p in blocks[name].findall("property")))
assert len(art_names) == 38
assert all(not any(p.get("name") == "EconomicValue" for p in blocks[name].findall("property")) for name in art_names)
assert [(p.get("name"), p.get("value")) for p in fragment.findall("append")[4].findall("property")] == [("EconomicValue", "5")]
assert "pictureCanvasRandomHelper" not in art_names
additional = [
    ({"cntBackpack01"}, "5"),
    ({"cntBookcaseCornerEmpty", "cntBookcaseCornerFull", "cntBookcaseEmpty", "cntBookcaseFull",
      "cntMedicineCabinetOpen", "cntMedicineCabinetFaucetOpen"}, "17"),
    ({"cntLuggageMediumOpen", "cntSuitcase"}, "17"),
    ({"cntShoppingBasketEmptyWhite"}, "80"),
    ({"cntwallOvenEmptyWhite", "tvCRT"}, "60"),
    ({"cntCharcoalGrillOpen"}, "25"),
    ({"cntGasGrillOpen"}, "50"),
]
for op, (names, value) in zip(fragment.findall("append")[5:], additional):
    assert [(p.get("name"), p.get("value")) for p in op.findall("property")] == [("EconomicValue", value)]
    assert all(name in op.get("xpath") for name in names)
    assert all(name in blocks and not any(p.get("name") == "EconomicValue" for p in blocks[name].findall("property")) for name in names)
bed_patch = ET.parse(root / "Config/_Trader/bed_economy.xml").getroot()
bed_overrides = bed_patch.findall("append")
assert len(bed_overrides) == 3
assert [(b.get("name"), b.get("value")) for b in bed_overrides[0].findall("property")] == [("EconomicSellScale", "0.015")]
assert [(b.get("name"), b.get("value")) for b in bed_overrides[1].findall("property")] == [("EconomicSellScale", "0.03")]
assert [(b.get("name"), b.get("value")) for b in bed_overrides[2].findall("property")] == [("EconomicSellScale", "0.03")]
for name in ("bed02", "bunkBedMade", "gurneyBed", "wheelchairWhite"):
    assert name in blocks
    base_value = next(float(p.get("value")) for p in blocks[name].findall("property") if p.get("name") == "EconomicValue")
    proposed_scale = 0.015 if name == "bed02" else 0.03
    assert base_value * proposed_scale * 2.5 <= 100
assert '<include filename="_Economy/block_prices.xml"/>' in (root / "Config/blocks.xml").read_text(encoding="utf-8")
assert (root / "Config/blocks.xml").read_text(encoding="utf-8").index('_Workstations/blocks.xml') < (root / "Config/blocks.xml").read_text(encoding="utf-8").index('_Economy/block_prices.xml')
print("PASS: station shells, lighting, chair, 38 art, seven storage/decor price groups, beds and wheelchair; unique properties; ordered include; XML parses")
