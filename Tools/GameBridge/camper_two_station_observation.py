"""Camper captures/assertions; run-navigation explicitly invokes the existing real-input scenario."""
import argparse
import json
import math
import subprocess
from pathlib import Path
import urllib.request


def need(condition, message):
    if not condition:
        raise ValueError(message)


def stations(capture):
    need(capture.get("schema") == 1, "unsupported capture")
    need(type(capture.get("bridgePid")) is int and isinstance(capture.get("bridgeStartedUtc"), str), "missing captured session evidence")
    state, camper = capture["state"], capture["camper"]
    need(state.get("ok") is True and camper.get("ok") is True, "bridge observation failed")
    need(state["world"]["gameName"] == "CodexTest", "disposable CodexTest required")
    need(camper.get("inventorySchema") == 1, "detailed native inventory observation required")
    need(isinstance(camper.get("vehicleIdentity"), str) and camper["vehicleIdentity"], "persistent vehicle identity missing")
    need(isinstance(camper.get("cargo"), list) and isinstance(camper.get("storage"), list), "exact cargo/storage arrays missing")
    need(type(camper.get("capacity")) is int and type(camper.get("cargoSlots")) is int, "cargo dimensions missing")
    need(camper.get("pending") is False and camper.get("capacity") == camper.get("cargoSlots"), "unsettled custody/cargo")
    need(camper.get("cursorEmpty") is True, "cursor evidence unavailable or nonempty")
    need(camper.get("stationCount") == 2 and len(camper["stations"]) == 2, "exactly two installations required")
    found = {station["slot"]: station for station in camper["stations"]}
    need(set(found) == {0, 1}, "campfire0 and workbench1 required")
    need(found[0].get("block") == "campfire" and found[1].get("block") == "workbench", "distinct physical campfire/workbench required")
    need(found[0].get("id") and found[1].get("id") and found[0]["id"] != found[1]["id"], "distinct installation identities required")
    for station in found.values():
        for key in ("queue", "tools", "fuel", "input", "output"):
            need(isinstance(station.get(key), list), "missing station array: " + key)
        for area in ("tools", "fuel", "input", "output"):
            for stack in station[area]:
                need(type(stack.get("count")) is int and stack["count"] >= 0, "invalid native stack")
                need(stack["count"] == 0 or bool(stack.get("nativeHash")), "exact native item hash missing")
        need(isinstance(station.get("fuelSeconds"), (int, float)) and math.isfinite(station["fuelSeconds"]), "missing/nonfinite fuel time")
    return found


def idle(capture):
    result = stations(capture)
    need(capture["camper"].get("lockOwner") == -1, "native interaction lock remains")
    for station in result.values():
        need(station["burning"] is False and station["queue"] == [], "idle comparison requires stopped empty queues")
    return result


def scope(before, after, reload):
    need(before["camper"].get("vehicleIdentity") == after["camper"].get("vehicleIdentity"), "vehicle identity changed")
    need(before["state"]["world"]["gameWorld"] == after["state"]["world"]["gameWorld"], "world changed")
    same_process = before.get("bridgePid") == after.get("bridgePid") and before.get("bridgeStartedUtc") == after.get("bridgeStartedUtc")
    need(not same_process if reload else same_process, "expected fresh process after reload" if reload else "unexpected process/session replacement")


def compare(before, after, mode, expected=None, distance=20):
    old = idle(before) if mode in ("preserve", "reload") else stations(before)
    new = idle(after) if mode in ("preserve", "reload") else stations(after)
    scope(before, after, mode == "reload")
    for key in ("storage", "storageCount", "cargo", "capacity", "cargoSlots"):
        need(before["camper"].get(key) == after["camper"].get(key), "cargo/storage changed: " + key)
    for slot in (0, 1):
        need(old[slot]["id"] == new[slot]["id"], "installation identity changed")
    if mode in ("preserve", "reload"):
        for slot in (0, 1):
            for key in ("tools", "fuel", "input", "output", "queue", "burning", "fuelSeconds"):
                need(old[slot][key] == new[slot][key], "station " + str(slot) + " changed: " + key)
        return
    need(isinstance(expected, dict) and set(expected) == {"0", "1"}, "progress needs explicit expectation for both station types")
    if mode == "drive":
        for observation in (before, after):
            c = observation["camper"]
            need(c.get("vehicleDriverEntityId") == c.get("localPlayerEntityId") and
                 c.get("localPlayerAttachedEntityId") == c.get("entity"), "native driver attachment required")
        p, q = before["state"]["player"]["position"], after["state"]["player"]["position"]
        need(isinstance(p, list) and isinstance(q, list) and len(p) == 3 and len(q) == 3, "native driver positions unavailable")
        need(math.isfinite(distance) and distance >= 20, "drive distance must be at least 20 metres")
        need(math.dist(p, q) >= distance,
             "vehicle driver did not travel required distance")
    for slot in (0, 1):
        rule = expected[str(slot)]
        recipe = rule["recipe"]
        jobs = [job for job in old[slot]["queue"] if job["recipe"] == recipe]
        need(len(jobs) == 1 and jobs[0]["count"] == rule["jobCount"], "missing exact prepared real job")
        remaining = [job for job in new[slot]["queue"] if job["recipe"] == recipe]
        need(len(remaining) <= 1, "duplicate same-recipe job")
        need(not remaining or remaining[0]["count"] < jobs[0]["count"] or
             remaining[0]["remaining"] < jobs[0]["remaining"], "station did not progress: " + str(slot))
        for area in ("tools", "input"):
            need(old[slot][area] == new[slot][area], "station exact state changed unexpectedly: " + area)
        need(new[slot]["fuelSeconds"] <= old[slot]["fuelSeconds"], "fuel time increased during progress")
        # Expected native output/fuel arrays must be independently captured/prepared;
        # hash includes the complete stack, so do not treat it as a per-item hash.
        for area in ("output", "fuel"):
            need(new[slot][area] == rule[area], "unexpected exact station " + area + ": " + str(slot))
    need(after["camper"].get("lockOwner") == -1, "drive/progress left native lock")


def capture(destination, session_path, entity):
    session = json.loads(Path(session_path).read_text(encoding="utf-8-sig"))
    def get(path):
        request = urllib.request.Request("http://127.0.0.1:" + str(session["port"]) + path,
                                         headers={"X-Bridge-Token": session["token"]})
        with urllib.request.urlopen(request, timeout=20) as reply:
            return json.load(reply)
    result = {"schema": 1, "bridgePid": session["pid"], "bridgeStartedUtc": session["startedUtc"],
              "state": get("/state?sections=player,world"), "camper": get("/camper?entity=" + str(entity))}
    path = Path(destination)
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(result, indent=2), encoding="utf-8")
    stations(result)
    print(path.resolve())


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    sub = parser.add_subparsers(dest="command", required=True)
    cap = sub.add_parser("capture")
    cap.add_argument("output")
    cap.add_argument("--entity", type=int, default=718)
    cap.add_argument("--session", default=str(Path(__file__).parent / "out/session.json"))
    check = sub.add_parser("assert")
    check.add_argument("before")
    check.add_argument("after")
    check.add_argument("--mode", choices=("preserve", "progress", "drive", "reload"), required=True)
    check.add_argument("--expected")
    check.add_argument("--distance", type=float, default=20)
    nav = sub.add_parser("run-navigation", help="explicitly execute existing bridge real-input scenario after strict preflight")
    nav.add_argument("output_directory")
    nav.add_argument("--session", default=str(Path(__file__).parent / "out/session.json"))
    args = parser.parse_args()
    if args.command == "capture":
        capture(args.output, args.session, args.entity)
    elif args.command == "run-navigation":
        directory = Path(args.output_directory)
        first, last = directory / "before.json", directory / "after.json"
        capture(first, args.session, 718)
        before = json.loads(first.read_text(encoding="utf-8"))
        # Start with the camper UI closed/native lease released. The scenario
        # opens overview itself, then closes it before the after-idle comparison.
        idle(before)  # Fail before any input for duplicate/wrong types or pending work.
        session = json.loads(Path(args.session).read_text(encoding="utf-8-sig"))
        default_session = Path(__file__).parent / "out/session.json"
        need(session == json.loads(default_session.read_text(encoding="utf-8-sig")), "scenario runner requires the same local bridge session")
        result = subprocess.run(["powershell", "-NoProfile", "-ExecutionPolicy", "Bypass", "-File",
                                 str(Path(__file__).parent / "gamebridge.ps1"), "test",
                                 str(Path(__file__).parent / "tests/camper_two_type_navigation.json")],
                                capture_output=True, text=True)
        directory.mkdir(parents=True, exist_ok=True)
        (directory / "scenario_output.txt").write_text(result.stdout + result.stderr, encoding="utf-8")
        capture(last, args.session, 718)
        need(result.returncode == 0, "navigation scenario failed; saved output and observations")
        compare(before, json.loads(last.read_text(encoding="utf-8")), "preserve")
        print("PASS navigation and exact exposed independent station preservation")
    else:
        read = lambda path: json.loads(Path(path).read_text(encoding="utf-8-sig"))
        compare(read(args.before), read(args.after), args.mode, read(args.expected) if args.expected else None, args.distance)
        print("PASS two distinct stations: " + args.mode)


if __name__ == "__main__":
    main()
