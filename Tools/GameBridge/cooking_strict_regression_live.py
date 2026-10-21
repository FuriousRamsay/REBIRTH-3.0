"""One-session strict cooking qualification; parent owns idle fixture/UI setup.
Run PLAN.json REPORT.json. No launch/give/save/queue mutation or input fallback.
Source counts and physical paths are captured live, never assumed from old runs.
"""
import argparse
import base64
from copy import deepcopy
import hashlib
import json
from pathlib import Path
import struct
from camper_consolidated_regression_live import Runner, need

CONTROLLERS = {"ingredient": "XUiC_RebirthCookingSlot", "fuel": "XUiC_RebirthCookingFuelSlot", "tool": "XUiC_RebirthCookingToolSlot"}
AREAS = {"ingredient": "input", "fuel": "fuel", "tool": "tools"}


def native_hash(payload, count):
    raw = base64.b64decode(payload, validate=True)
    need(raw and type(count) is int and count > 0, "nonempty native image/count required")
    # Exact RebirthCamperInventorySnapshot.Capture one-row framing, RCS1:
    # UInt32 magic, Int32 rows/count/item-length, then native ItemValue bytes.
    framed = struct.pack("<Iiii", 0x31534352, 1, count, len(raw)) + raw
    return base64.b64encode(hashlib.sha256(framed).digest()).decode("ascii")


def unique(rows, label):
    need(len(rows) == 1, "unique " + label + " required (matches=" + str(len(rows)) + ")")
    return rows[0]


def owner(runner):
    value = runner.call("/gearobserve")
    need(value.get("schema") == 1 and value.get("gameName") == "CodexTest", "exact disposable owner required")
    need(value["cursor"]["count"] == 0, "native cursor not empty")
    for key in ("gearHeld", "libraryHeld", "resourcePending", "creationHeld", "offerPending"):
        need(value[key] is False, "unrelated custody pending: " + key)
    return value


def same_owner(before, after, expected_bag=None):
    for key in ("entityId", "worldKey", "creationId", "belt", "bagLocks", "cursor"):
        need(before[key] == after[key], "original owner state changed: " + key)
    expected = before["bag"] if expected_bag is None else expected_bag
    need([(row["itemData"], row["count"]) for row in after["bag"]]
         == [(row["itemData"], row["count"]) for row in expected], "exact physical backpack differs")


def idle(runner, plan):
    frame = runner.call("/camper", entity=plan["entity"])
    need(frame.get("pending") is False and frame.get("cursorEmpty") is True, "native transfer/cursor not idle")
    need(frame["lockOwner"] == frame["localPlayerEntityId"] and frame["localPlayerAttachedEntityId"] == -1, "current native station lease and unattached owner required")
    need(frame.get("queuedJobCount") == 0, "native queue must be completely empty; TAKE old paid jobs first")
    for station in frame["stations"]:
        need(station["queue"] == [] and station["burning"] is False,
             "unlit queue-idle station required; no burn/queue normalization allowed")
    station = unique([row for row in frame["stations"] if row["slot"] == plan["stationSlot"]], "physical installed station")
    need(station["block"] == "campfire", "this qualification requires actual Campfire cooking")
    return frame, station


def ui(runner, plan):
    value = runner.call("/ui/tree", window=plan["window"], all=1, limit=5000)
    need(value.get("truncated") is False and plan["window"] in value["openWindows"], "actual untruncated cooking window required")
    need("ingameMenu" not in value["openWindows"], "unowned pause menu blocks cooking input")
    return value


def ui_cell(tree, path):
    row = unique([row for row in tree["nodes"] if row["path"] == path], "visible UI cell")
    need(row.get("rect") and row["rect"][2] > 0 and row["rect"][3] > 0 and row.get("clickable") is True, "live clickable cell geometry required")
    return row


def physical(runner):
    return runner.source_observation()


def cell_profile(profile, path):
    return unique([row for row in profile["visibleSlots"] if row["path"] == path], "actual native slot profile")


def bindings(tree, prefix):
    return {row["path"]: {key: row[key] for key in ("item", "recipe", "text") if key in row}
            for row in tree["nodes"] if row["path"].startswith(prefix)}


def strict(runner, plan, source, target, payload, count):
    runner.guard_idle()
    idle(runner, plan); owner(runner)
    ui(runner, plan)
    current = physical(runner)
    actual = cell_profile(current, source)
    stack = actual.get("source") or actual.get("stack")
    need(actual["empty"] is False and stack and stack["payload"] == payload and stack["count"] == count,
         "actual positive strict source changed before mouse input")
    need(cell_profile(current, target)["empty"] is True, "strict target changed before mouse input")
    reply = runner.call("/ui/drag", "POST", **{"from.path": source, "to.path": target, "strict": 1})
    need(reply.get("placedByClick") is False and reply.get("stillHeld") is None, "strict release retained custody or used fallback")
    need(reply.get("from", {}).get("path") == source and reply.get("to", {}).get("path") == target, "actual strict source/target receipt differs")
    runner.errors()
    return reply


def choose(runner, plan, case):
    profile = physical(runner); visible = ui(runner, plan)
    candidates = [row for row in profile["visibleSlots"] if row["path"].startswith(plan["window"] + "/")
                  and row.get("controller") == "XUiC_RebirthCraftingInventorySlot" and row.get("source", {}).get("name") == case["item"]]
    if case.get("sourceSlot") is not None:
        candidates = [row for row in candidates if row["source"]["slot"] == case["sourceSlot"]]
    source = unique(candidates, "visible physical preparatory source")
    image = source["source"]
    need(image["location"] == "backpack" and image["count"] > 0 and image.get("payload"), "physical positive source proof required")
    if case["kind"] == "tool":
        need(image["count"] == 1, "whole native required-tool roundtrip needs one actual tool")
    targets = [row for row in profile["visibleSlots"] if row["path"].startswith(plan["window"] + "/")
               and row.get("controller") == CONTROLLERS[case["kind"]] and row["slotNumber"] == case["targetSlot"]]
    target = unique(targets, "actual native cooking target")
    need(target["role"] == case["kind"] and target["empty"] is True, "native target must start empty")
    if case["kind"] == "tool":
        need(case["item"] in target.get("acceptsTools", []), "native required-tool target does not accept this tool")
    shown = ui_cell(visible, source["path"])
    need(shown.get("item", {}).get("name") == image["name"] and shown["item"]["count"] == image["count"], "UI presentation does not match physical source")
    need(ui_cell(visible, target["path"]).get("item") is None, "UI target is occupied")
    return source["path"], target["path"], image


def run_case(runner, plan, case):
    before, station = idle(runner, plan); original = owner(runner)
    source, target, admitted = choose(runner, plan, case)
    slot = admitted["slot"]; count = admitted["count"]; payload = admitted["payload"]
    need(original["bag"][slot]["itemData"] == payload and original["bag"][slot]["count"] == count, "exact owner/source preimage differs")
    old_tree = ui(runner, plan); empty = bindings(old_tree, target)
    # Physical input arrays do not own cooking's transient ingredient cells.
    # Ingredient proof uses the actual slot stack from the existing profile.
    if case["kind"] != "ingredient":
        entry = station[AREAS[case["kind"]]][case["targetSlot"]]
        need(entry["count"] == 0 and entry["nativeHash"] is None, "physical station target must start empty")
    forward = strict(runner, plan, source, target, payload, count)
    deposited = owner(runner); current, installed = idle(runner, plan)
    expected = deepcopy(original["bag"]); expected[slot] = {"itemData": "", "count": 0}
    same_owner(original, deposited, expected)
    profile = physical(runner); actual = cell_profile(profile, target)
    need(actual["empty"] is False and actual["stack"]["payload"] == payload and actual["stack"]["count"] == count,
         "accepted native target does not contain original full item/count")
    value = ui(runner, plan)
    need(ui_cell(value, source).get("item") is None and ui_cell(value, target).get("item", {}).get("count") == count,
         "strict deposit did not visibly debit source and credit target")
    fingerprint = native_hash(payload, count)
    if case["kind"] != "ingredient":
        actual_cell = installed[AREAS[case["kind"]]][case["targetSlot"]]
        need(actual_cell["count"] == count and actual_cell["nativeHash"] == fingerprint, "actual native station hash/count receipt mismatch")
    # Reverse requires a proven occupied target and empty ORIGINAL physical source.
    # A return-to-source after failed forward can never satisfy these predicates.
    need(cell_profile(profile, source)["empty"] is True, "original return destination is not empty")
    reverse = strict(runner, plan, target, source, payload, count)
    returned = owner(runner); after, restored = idle(runner, plan)
    same_owner(original, returned)
    final_profile = physical(runner)
    need(cell_profile(final_profile, target)["empty"] is True, "reverse left target occupied")
    restored_source = cell_profile(final_profile, source).get("source")
    need(restored_source and restored_source["payload"] == payload and restored_source["count"] == count
         and native_hash(restored_source["payload"], restored_source["count"]) == fingerprint,
         "original physical source native hash/count not restored")
    need(ui_cell(ui(runner, plan), source).get("item", {}).get("count") == count, "original source count not restored")
    for field in ("cargo", "capacity", "cargoSlots", "vehicleIdentity", "storage", "storageCount", "stations"):
        need(before[field] == after[field], "roundtrip changed original camper state: " + field)
    need(bindings(ui(runner, plan), target) == empty, "empty native target presentation not restored")
    receipt = {"kind": case["kind"], "sourcePath": source, "targetPath": target, "physicalSourceSlot": slot,
               "name": admitted["name"], "count": count, "quality": admitted.get("quality"), "nativeHash": fingerprint,
               "forward": forward, "reverse": reverse, "originalRestored": True, "physicalStationReceipt": case["kind"] != "ingredient",
               "directionReceipts": {direction: {"positiveSourceCount": count, "destinationCount": count,
                   "nativeHash": fingerprint, "placedByClick": False, "cursorEmpty": True}
                   for direction in ("forward", "reverse")}}
    runner.report.setdefault("cases", []).append(receipt); runner.save()


def main():
    parser = argparse.ArgumentParser(__doc__); parser.add_argument("plan"); parser.add_argument("report"); args = parser.parse_args()
    need(not Path(args.report).exists(), "use a new report path to preserve original evidence")
    plan = json.loads(Path(args.plan).read_text(encoding="utf-8-sig")); report = {"schema": 1, "pass": False, "plan": plan}
    runner = Runner(report, Path(args.report))
    try:
        status = runner.call("/ping"); need(status["state"] == "ingame", "one current in-game session required")
        runner.start = status["lastLogSeq"]
        need(plan.get("schema") == 1 and plan.get("window") == "workstation_campfire", "current Campfire plan schema required")
        need(len(plan["cases"]) == 3 and {case["kind"] for case in plan["cases"]} == set(CONTROLLERS), "one ingredient/fuel/tool case each required")
        ui(runner, plan)  # Caller owns this already-open window; never send Escape on failure.
        initial = owner(runner); idle(runner, plan)
        # Validate every native source/target before any input; no empty-source pass.
        for case in plan["cases"]:
            choose(runner, plan, case)
        report["original"] = initial; runner.save()
        for case in plan["cases"]:
            run_case(runner, plan, case)
        same_owner(initial, owner(runner)); runner.errors(); report["pass"] = True
    except Exception as error:
        report["failure"] = type(error).__name__ + ": " + str(error); raise
    finally:
        runner.save()

if __name__ == "__main__":
    main()

