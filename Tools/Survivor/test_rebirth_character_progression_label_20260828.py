from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
loc = (ROOT / "Config" / "Localization.csv").read_text(encoding="utf-8-sig")
rows = [line for line in loc.splitlines() if line.startswith("xuiRebirthPlayerProgressionRebirth,")]
assert rows == ["xuiRebirthPlayerProgressionRebirth,Rebirth"], rows
print("PASS: Character Progression Rebirth mode is labeled Rebirth")
