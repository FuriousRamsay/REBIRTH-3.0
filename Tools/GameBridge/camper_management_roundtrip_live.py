"""One-session native management roundtrips. Run PLAN.json NEW_REPORT.json.
Caller supplies observed real radial open actions; no setup cheats or fallback.
"""
import argparse
from copy import deepcopy
import json
from pathlib import Path
from camper_consolidated_regression_live import Runner, need
from cooking_strict_regression_live import owner, same_owner, unique, native_hash


def frame(r, entity):
    value = r.snapshot(entity)["camper"]
    need(value["lockOwner"] == value["localPlayerEntityId"] and value["localPlayerAttachedEntityId"] == -1,
         "current native management lease required")
    need(value["queuedJobCount"] == 0 and all(not s["burning"] and not s["queue"] for s in value["stations"]),
         "all stations must be unlit and queue-idle for exact preservation")
    return value


def equal_camper(a, b):
    for key in ("vehicleIdentity", "storage", "storageCount", "stations", "stationCount", "cargo", "capacity", "cargoSlots"):
        need(a[key] == b[key], "original camper not restored: " + key)


def slots(r, window):
    tree = r.call("/ui/tree", window=window, all=1, limit=5000)
    need(tree.get("truncated") is False and window in tree["openWindows"], "visible management window required")
    need("ingameMenu" not in tree["openWindows"], "unowned game menu blocks management input")
    nodes = {n["path"]: n for n in tree["nodes"]}
    rows = [s for s in r.source_observation()["visibleSlots"] if s["path"].startswith(window + "/")]
    return [s for s in rows if s["path"] in nodes and nodes[s["path"]].get("clickable") is True
            and nodes[s["path"]].get("rect", [0, 0, 0, 0])[2] > 0]


def module(rows, slot):
    return unique([s for s in rows if s["role"] == "module" and s["slotNumber"] == slot], "actual module path")


def bags(rows):
    return [s for s in rows if s["controller"] == "XUiC_RebirthBackpackSectionNativeSlot"
            and s["path"].split("/")[-1].startswith("sellAvailable")]


def input_move(r, mode, source, target):
    r.guard_idle()
    if mode == "shift":
        return r.call("/ui/click", "POST", path=source, shift=1)
    reply = r.call("/ui/drag", "POST", **{"from.path": source, "to.path": target, "strict": 1})
    need(reply.get("placedByClick") is False and reply.get("stillHeld") is None, "strict native release failed")
    need(reply.get("from", {}).get("path") == source and reply.get("to", {}).get("path") == target,
         "strict receipt selected another source/target")
    return reply


def run_case(r, plan, case):
    window = case["window"]
    need(window == {"storage": "rebirthCamperStorageManagement", "station": "rebirthCamperStationManagement"}[case["kind"]], "actual management kind/window mismatch")
    opened = r.call("/ui/tree", all=1, limit=5000)["openWindows"]
    need(window not in opened and "ingameMenu" not in opened, "open actions require closed owned window and no game menu")
    r.actions(case["openActions"], window)
    before = frame(r, plan["entity"]); original = owner(r)
    collection = "storage" if case["kind"] == "storage" else "stations"
    installed = before[collection]
    need(installed, "original installed module required")
    selected = unique([s for s in installed if s["slot"] == case["slot"]], "selected installed slot") if case.get("slot") is not None else min(installed, key=lambda s: s["slot"])
    slot = selected["slot"]
    # Native preferred install chooses the first empty module, never an explicit target.
    need(all(any(s["slot"] == i for s in installed) for i in range(slot)), "Shift reinstall would choose an earlier empty installation")
    if collection == "stations":
        need(selected["fuelSeconds"] == 0 and all(c["count"] == 0 for area in ("tools", "fuel", "input", "output") for c in selected[area]),
             "selected station must have no removable contents/timer; other idle stations may retain contents")
        initial_rows = slots(r, window)
        unique([s for s in bags(initial_rows) if s.get("source", {}).get("name") == selected["block"]
                and s["source"].get("plainStation") is True], "preparatory duplicate physical station")
        need(any(s["role"] == "module" and s["empty"] for s in initial_rows), "duplicate refusal requires an empty module control")
    for mode in ("shift", "drag"):
        rows = slots(r, window); source = module(rows, slot)
        need(source["empty"] is False, "positive installed source required")
        free = [s for s in bags(rows) if 0 <= s["slotNumber"] < min(len(original["bag"]), len(original["bagLocks"]))
                and s["empty"] and original["bag"][s["slotNumber"]]["count"] == 0
                and not original["bagLocks"][s["slotNumber"]]]
        need(free, "visible unlocked physical free backpack cell required")
        target = min(free, key=lambda s: s["slotNumber"])
        forward = input_move(r, mode, source["path"], target["path"])
        removed = frame(r, plan["entity"]); held_owner = owner(r)
        need(removed[collection] == [s for s in before[collection] if s["slot"] != slot], "wrong module removed")
        other = "stations" if collection == "storage" else "storage"
        need(removed[other] == before[other], "unselected modules/stations changed during removal")
        need(removed["vehicleIdentity"] == before["vehicleIdentity"], "vehicle identity changed")
        if collection == "stations":
            for key in ("cargo", "capacity", "cargoSlots"):
                need(removed[key] == before[key], "station removal changed cargo: " + key)
        changed = [i for i, (a, b) in enumerate(zip(original["bag"], held_owner["bag"])) if a != b]
        need(len(changed) == 1, "removal must credit exactly one physical cell")
        physical_slot = changed[0]; carrier = held_owner["bag"][physical_slot]
        need(original["bag"][physical_slot]["count"] == 0 and carrier["count"] == 1 and carrier["itemData"], "positive packed carrier receipt required")
        expected = deepcopy(original["bag"]); expected[physical_slot] = carrier
        same_owner(original, held_owner, expected)
        row = unique([s for s in bags(slots(r, window)) if s["slotNumber"] == physical_slot], "actual credited backpack path")
        witness = row.get("source")
        need(witness and witness["payload"] == carrier["itemData"] and witness["count"] == 1, "native full packed payload witness differs")
        need(witness["name"] == selected.get("item", selected.get("block")), "removed carrier type differs")
        need(mode != "drag" or row["path"] == target["path"], "strict removal used another physical destination")
        if mode == "shift":
            need(any(s["slotNumber"] == physical_slot for s in free), "native Shift credited hidden/locked destination")
        empty_module = module(slots(r, window), slot); need(empty_module["empty"], "removed module is not empty")
        reverse = input_move(r, mode, row["path"], empty_module["path"])
        restored = frame(r, plan["entity"]); same_owner(original, owner(r)); equal_camper(before, restored)
        receipt = {"kind": case["kind"], "mode": mode, "installedSlot": slot, "installationId": selected.get("id"),
                   "physicalSlot": physical_slot, "modulePath": source["path"], "backpackPath": row["path"],
                   "packedPayload": carrier["itemData"], "packedNativeHash": native_hash(carrier["itemData"], 1),
                   "count": 1, "forward": forward, "reverse": reverse, "originalRestored": True}
        r.report.setdefault("roundtrips", []).append(receipt); r.save()
    if collection == "stations":
        rows = slots(r, window)
        duplicate = unique([s for s in bags(rows) if s.get("source", {}).get("name") == selected["block"]
                           and s["source"].get("plainStation") is True], "existing physical duplicate station source")
        empty = next((s for s in rows if s["role"] == "module" and s["empty"]), None)
        need(empty is not None, "empty module control required to prove duplicate-type refusal")
        input_move(r, "shift", duplicate["path"], empty["path"])
        same_owner(original, owner(r)); equal_camper(before, frame(r, plan["entity"]))
        r.report["duplicateRefusal"] = {"source": duplicate["source"], "path": duplicate["path"], "unchanged": True}
    # Never toggle the pause menu when native UI has closed asynchronously.
    # This window was opened by this case; validate its real visible cells and lease immediately before closing.
    need(slots(r, window), "owned management window no longer visibly open; refuse Escape")
    frame(r, plan["entity"])
    r.call("/key", "POST", name="Escape")
    after_close = r.call("/ui/tree", window=window)["openWindows"]
    need(window not in after_close and "ingameMenu" not in after_close, "owned management close failed or toggled game menu")
    closed = r.snapshot(plan["entity"])["camper"]
    need(closed["lockOwner"] == -1, "native management lease not released on close")
    equal_camper(before, closed); same_owner(original, owner(r)); r.errors()


def main():
    p = argparse.ArgumentParser(__doc__); p.add_argument("plan"); p.add_argument("report"); args = p.parse_args()
    need(not Path(args.report).exists(), "new report path required")
    plan = json.loads(Path(args.plan).read_text(encoding="utf-8-sig"))
    report = {"schema": 1, "pass": False, "plan": plan}; r = Runner(report, Path(args.report))
    try:
        need(plan["schema"] == 1 and {c["kind"] for c in plan["cases"]} == {"storage", "station"} and len(plan["cases"]) == 2, "one storage and station case required")
        status = r.call("/ping"); need(status["state"] == "ingame", "current one-session game required"); r.start = status["lastLogSeq"]
        need(r.snapshot(plan["entity"])["camper"]["lockOwner"] == -1, "start from closed native idle")
        for case in plan["cases"]:
            run_case(r, plan, case)
        report["pass"] = True
    except Exception as error:
        report["failure"] = type(error).__name__ + ": " + str(error); raise
    finally:
        r.save()


if __name__ == "__main__":
    main()
