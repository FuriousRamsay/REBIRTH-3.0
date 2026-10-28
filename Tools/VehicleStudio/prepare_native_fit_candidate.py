"""Prepare review-only seating proposals from actual Unity mesh/bone evidence."""
import argparse
import hashlib
import json
from pathlib import Path
import xml.etree.ElementTree as ET


def prepare(source, evidence_path, output):
    source, evidence_path, output = map(Path, (source, evidence_path, output))
    original = source.read_bytes()
    evidence = json.loads(evidence_path.read_text(encoding="utf-8-sig"))
    if evidence.get("status") != "PASS_SIX_NATIVE_BODIES_RENDERED_NOT_COMFORT_ACCEPTED":
        raise ValueError("A completed actual six-native-body Unity review is required")
    occupants = {row["seat"]: row for row in evidence["occupants"]}
    if set(occupants) != set(range(6)) or any(not row.get("headMeshPresent") for row in occupants.values()):
        raise ValueError("All six actual bodies must include native head meshes")
    faces = evidence.get("measuredFurniture", [])
    if not faces:
        raise ValueError("Current Unity prefab furniture measurements required")

    def surface(region, plane="top"):
        selected = [face for face in faces if face["region"] == region and face["area"] > .01]
        if not selected:
            raise ValueError("Measured surface unavailable: " + region)
        # Use exact measured vertices; broad combined cabin bounds are unsuitable.
        points = [face[key] for face in selected for key in ("a", "b", "c")]
        low = {axis: min(p[axis] for p in points) for axis in "xyz"}
        high = {axis: max(p[axis] for p in points) for axis in "xyz"}
        return {"minimum": low, "maximum": high,
                "center": {axis: (low[axis] + high[axis]) / 2 for axis in "xyz"},
                "y": high["y"] if plane == "top" else low["y"],
                "triangles": [face["triangle"] for face in selected]}

    table = surface("table", "bottom")
    proposals = []
    tree = ET.fromstring(original)
    vehicle = tree.find("./append/vehicle[@name='rebirthCamper80s']")
    if vehicle is None:
        raise ValueError("Current camper XML missing")
    if Path(evidence.get("xml", "")).resolve() != source.resolve():
        raise ValueError("Review evidence must identify this exact source XML")
    for seat, row in occupants.items():
        node = vehicle.find(f"property[@class='seat{seat}']")
        if node is None:
            raise ValueError("Reviewed seat no longer exists")
        for field in ("position", "rotation"):
            values = [float(v.strip()) for v in node.find(f"property[@name='{field}']").get("value").split(",")]
            if len(values) != 3 or any(abs(value - row[field][axis]) > .00001 for axis, value in zip("xyz", values)):
                raise ValueError("Seat XML changed after review: " + str(seat) + " " + field)
        if int(node.find("property[@name='pose']").get("value")) != row["pose"]:
            raise ValueError("Seat pose changed after review")
    for seat, region in ((2, "front_cushion"), (3, "rear_cushion"), (5, "sink_seat")):
        row, cushion = occupants[seat], surface(region)
        support_shift = cushion["y"] + .005 - row["pelvisBottomY"]
        max_shift = table["y"] - .02 - row["thighTopY"] if seat in (2, 3) else None
        possible = max_shift is None or support_shift <= max_shift
        position = dict(row["position"])
        if possible:
            position["y"] += support_shift
        proposals.append({"seat": seat, "surface": cushion, "pelvisBottomY": row["pelvisBottomY"],
                          "thighTopY": row["thighTopY"], "supportShift": support_shift,
                          "maximumTableClearanceShift": max_shift, "anchorOnlyFitPossible": possible,
                          "proposedPosition": position, "status": "REVIEW_CANDIDATE" if possible else "REQUIRES_POSE_OR_GENERATED_TABLE_CHANGE",
                          "minimumAdditionalTableHeight": max(0, support_shift - max_shift) if max_shift is not None else 0})
    bed, row = surface("bed"), occupants[4]
    width = bed["maximum"]["x"] - bed["minimum"]["x"]
    fits_width = row["boundsSize"]["x"] <= width - .02
    position = dict(row["position"])
    position["x"] += bed["center"]["x"] - row["boundsCenter"]["x"]
    proposals.append({"seat": 4, "surface": bed, "proposedPosition": position,
                      "meshWidth": row["boundsSize"]["x"], "mattressWidth": width,
                      "anchorOnlyFitPossible": fits_width,
                      "status": "CENTERED_BED_REQUIRES_POSE_CLEARANCE_REVIEW" if fits_width else "REQUIRES_SHORTER_BENT_BED_POSE",
                      "minimumMeshY": row["boundsCenter"]["y"] - row["boundsSize"]["y"] / 2,
                      "note": "Vertical bed anchor remains unchanged; centering alone cannot correct feet penetrating the mattress."})
    for proposal in proposals:
        node = vehicle.find(f"property[@class='seat{proposal['seat']}']/property[@name='position']")
        if node is None:
            raise ValueError("Authored seat position absent")
        node.set("value", ", ".join(format(proposal["proposedPosition"][axis], ".9g") for axis in "xyz"))
    ET.indent(tree, space="  ")
    candidate = ET.tostring(tree, encoding="utf-8") + b"\n"
    baseline = ET.fromstring(original)
    for element in (tree, baseline):
        owner = element.find("./append/vehicle[@name='rebirthCamper80s']")
        for seat in range(2, 6):
            position = owner.find(f"property[@class='seat{seat}']/property[@name='position']")
            position.set("value", "REVIEWED_POSITION")
    def semantic(node):
        return node.tag, tuple(sorted(node.attrib.items())), tuple(semantic(child) for child in node)
    if semantic(tree) != semantic(baseline):
        raise ValueError("Protected vehicle behavior changed")
    output.mkdir(parents=True, exist_ok=False)
    (output / "camper_vehicles_review_candidate.xml").write_bytes(candidate)
    report = {"status": "STAGED_REVIEW_ONLY_NOT_INSTALLABLE", "installed": False, "installable": False,
              "source": str(source), "sourceSha256": hashlib.sha256(original).hexdigest(),
              "evidence": str(evidence_path), "evidenceSha256": hashlib.sha256(evidence_path.read_bytes()).hexdigest(),
              "candidateSha256": hashlib.sha256(candidate).hexdigest(), "proposals": proposals,
              "remaining": ["Actual images must show cushion support and table/aisle/mattress clearance", "Native vehicle IK and varied player bodies remain unqualified"]}
    (output / "result.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
    if source.read_bytes() != original:
        raise RuntimeError("Current source changed during preparation; re-review required")
    return report


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--source", type=Path, required=True)
    parser.add_argument("--evidence", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    print(json.dumps(prepare(args.source, args.evidence, args.output), indent=2))
