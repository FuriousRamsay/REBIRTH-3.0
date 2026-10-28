"""Export exact installed vanilla vehicle reference settings for repeatable rigging."""
import argparse
import hashlib
import json
from pathlib import Path
import xml.etree.ElementTree as ET

REFERENCES = {
    "motorcycle": ("vehicleMotorcycle", "Assets/RetroStyleGames/LastGuns/BRP/Bike_A/Prefabs/P_RSG_Bike_A_Clean_BRP.prefab"),
    "buggy": ("vehicleTruck4x4", "Assets/RetroStyleGames/LastGuns/BRP/Buggy/Prefabs/P_RSG_Buggy_Clean_BRP.prefab"),
}


def export(source, output):
    source, output = Path(source), Path(output)
    data = source.read_bytes()
    root = ET.fromstring(data)
    records = {}

    def properties(node):
        return [dict(p.attrib, children=properties(p)) if len(p) else dict(p.attrib) for p in node]

    for kind, (name, prefab) in REFERENCES.items():
        matches = root.findall("vehicle[@name='%s']" % name)
        if len(matches) != 1:
            raise ValueError("Exactly one native reference is required: " + name)
        records[kind] = dict(schemaVersion=1, type=kind, sourcePrefab=prefab,
            vanillaVehicle=name, vanillaXmlSha256=hashlib.sha256(data).hexdigest(),
            nativeProperties=properties(matches[0]), camperManagement=False,
            brakeLightsRequired=True, status="REFERENCE_PROFILE_NOT_RIGGED",
            note="Native transforms and IK values are reference measurements, not approved coordinates for the supplied model.")
    output.mkdir(parents=True, exist_ok=True)
    for kind, record in records.items():
        (output / (kind + ".json")).write_text(json.dumps(record, indent=2) + "\n", encoding="utf-8")
    return records


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    print("Exported reference profiles: " + ", ".join(export(args.source, args.output)))
