"""Check owned Entity Studio map channels without modifying image assets."""
import argparse
import hashlib
import json
from pathlib import Path

from PIL import Image, ImageChops, ImageOps


def channel(path, name):
    with Image.open(path) as image:
        return image.convert("RGBA").getchannel(name)


def equal(first, second):
    return first.size == second.size and ImageChops.difference(first, second).getbbox() is None


def statistics(image):
    histogram = image.histogram()
    return {
        "mean": sum(value * count for value, count in enumerate(histogram)) / (image.width * image.height * 255),
        "min": image.getextrema()[0] / 255,
        "max": image.getextrema()[1] / 255,
    }


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("folder", type=Path, help="Generated entity folder containing Maps and RepairMaps")
    parser.add_argument("--entity", default="RebirthFarmer")
    parser.add_argument("--output", required=True, type=Path)
    args = parser.parse_args()
    rough_path = args.folder / "RepairMaps/roughness.png"
    metal_path = args.folder / "RepairMaps/metallic.png"
    rough, metal = channel(rough_path, "R"), channel(metal_path, "R")
    checks = {}
    paths = [rough_path, metal_path]
    for tier in ("Normal", "Feral", "Special"):
        path = args.folder / "Maps" / f"{args.entity}_RMOE_{tier}.png"
        paths.append(path)
        checks[f"{tier}.roughness"] = equal(rough, channel(path, "R"))
        checks[f"{tier}.metallic"] = equal(metal, channel(path, "G"))
        checks[f"{tier}.occlusion"] = channel(path, "B").getextrema() == (255, 255)
        if tier != "Special":
            checks[f"{tier}.bodyEmissionOff"] = channel(path, "A").getextrema() == (0, 0)
    preview = args.folder / "Maps" / f"{args.entity}_Preview_Nonmetal_Smoothness.png"
    paths.append(preview)
    checks["preview.smoothness"] = equal(ImageOps.invert(rough), channel(preview, "A"))
    report = {
        "passed": all(checks.values()), "checks": checks,
        "roughness": statistics(rough), "metallic": statistics(metal),
        "sha256": {str(path): hashlib.sha256(path.read_bytes()).hexdigest() for path in paths},
        "scope": "Raw owned map channels only; does not approve shader response, surface masks, or in-game appearance.",
    }
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(report, indent=2), encoding="utf-8")
    print(json.dumps({"passed": report["passed"], "checks": checks}, indent=2))
    return 0 if report["passed"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
