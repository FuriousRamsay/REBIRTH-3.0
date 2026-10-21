"""Parent-run non-host Daypack then Walkman qualification. PLAN.json NEW_REPORT.json.
Real observed equip actions and strict marker moves only; no setup/launch/fallback.
"""
import argparse
from copy import deepcopy
import json
from pathlib import Path
import time
from camper_consolidated_regression_live import Runner, need
from gear_observation import validate, idle, assert_pair


def observe(r):
    value = validate(r.call("/gearobserve"))
    value["_bridgePid"] = r.scope["pid"]; value["_bridgeStartedUtc"] = r.scope["startedUtc"]
    need(value["gameName"] == "CodexTest", "disposable remote world required")
    return value


def settled(r, before=None, slot=None):
    deadline = time.monotonic() + 60
    while True:
        value = observe(r)
        ready = value["cursor"]["count"] == 0 and not any(value[k] for k in ("gearHeld", "libraryHeld", "resourcePending", "creationHeld", "offerPending")) and not value["intentMarkers"]
        if ready and (before is None or ((value.get("terminal") or {}).get("TransactionId") != (before.get("terminal") or {}).get("TransactionId") and value.get("savedRetirement") is True)):
            idle(value)
            if before is not None: assert_pair(before, value, "gear", slot)
            return value
        r.errors(); need(time.monotonic() < deadline, "exact remote gear retirement/owner hold did not settle")
        time.sleep(0.25)


def tree(r, window):
    value = r.call("/ui/tree", window=window, all=1, limit=5000)
    need(value.get("truncated") is False and window in value["openWindows"] and "ingameMenu" not in value["openWindows"], "actual visible owned surface required")
    return value


def node(tree_value, path):
    matches = [n for n in tree_value["nodes"] if n["path"] == path]
    need(len(matches) == 1 and matches[0].get("clickable") is True and matches[0].get("rect", [0,0,0,0])[2] > 0, "unique visible native physical cell required")
    return matches[0]


def strict(r, window, source, target, count):
    r.guard_idle(); value = tree(r, window)
    need(node(value, source).get("item", {}).get("count") == count and count > 0, "positive native held-drag source required")
    need(node(value, target).get("item") is None, "native destination must be empty")
    reply = r.call("/ui/drag", "POST", **{"from.path": source, "to.path": target, "strict": 1})
    need(reply.get("placedByClick") is False and reply.get("stillHeld") is None, "strict release failed or fallback used")
    need(reply.get("from", {}).get("path") == source and reply.get("to", {}).get("path") == target, "native receipt selected different cell")
    r.errors(); return reply


def same(a, b, bag=None):
    idle(a); idle(b)
    for key in ("entityId", "worldKey", "creationId", "_bridgePid", "_bridgeStartedUtc", "belt", "bagLocks", "cursor", "terminal", "receipts"):
        need(a[key] == b[key], "owner/terminal scope changed during access: " + key)
    need(b["bag"] == (a["bag"] if bag is None else bag), "exact physical native backpack mismatch")


def access(r, definition, loot=False):
    window = definition["window"]
    need(window not in r.call("/ui/tree", window=window)["openWindows"], "access window already open and unowned")
    r.actions(definition["openActions"], window)
    before = settled(r); ui = tree(r, window)
    slot = definition["sourceSlot"]; stack = before["bag"][slot]
    need(stack["count"] > 0 and stack["itemData"] and not before["bagLocks"][slot], "original unlocked physical marker required")
    source, target = definition["sourcePath"], definition["targetPath"]
    displayed = node(ui, source).get("item")
    need(displayed and displayed["name"] == definition["item"] and displayed["count"] == stack["count"], "physical marker/path proof differs")
    expected = deepcopy(before["bag"]); expected[slot] = {"count": 0, "itemData": ""}
    if not loot:
        destination = definition["targetSlot"]
        need(destination != slot and before["bag"][destination]["count"] == 0 and not before["bagLocks"][destination], "physical free movement destination required")
        expected[destination] = stack
    original_nodes = {n["path"]: n.get("item") for n in ui["nodes"] if "item" in n}
    forward = strict(r, window, source, target, stack["count"])
    deposited = settled(r); same(before, deposited, expected)
    need(node(tree(r, window), source).get("item") is None, "physical source not cleared")
    need(node(tree(r, window), target).get("item") == displayed, "destination native name/count/quality differs")
    if not loot: assert_pair(before, deposited, "move")
    reverse = strict(r, window, target, source, stack["count"])
    after = settled(r); same(before, after)
    restored_ui = tree(r, window)
    need({n["path"]: n.get("item") for n in restored_ui["nodes"] if "item" in n} == original_nodes, "original visible slot contents not restored")
    # Close only the surface opened by this phase, never cleanup after failure.
    tree(r, window); r.actions(definition["closeActions"])
    closed = r.call("/ui/tree", window=window)
    need(window not in closed["openWindows"] and "ingameMenu" not in closed["openWindows"], "owned access surface not closed")
    same(before, settled(r))
    return {"kind": "loot-marker-roundtrip" if loot else "backpack-roundtrip", "before": before, "after": after,
            "fullNativeMarker": stack, "forward": forward, "reverse": reverse,
            "nativePrivateLeaseIdentityObserved": False, "visibleOriginalRestored": True}


def main():
    p = argparse.ArgumentParser(__doc__); p.add_argument("plan"); p.add_argument("report"); args = p.parse_args()
    need(not Path(args.report).exists(), "new evidence path required")
    plan = json.loads(Path(args.plan).read_text(encoding="utf-8-sig")); report = {"schema": 1, "pass": False, "plan": plan}
    r = Runner(report, Path(args.report))
    try:
        need(plan["schema"] == 1 and [(s["slot"], s["item"]) for s in plan["steps"]]
             == [("backpack", "rebirthGearDaypack"), ("walkman", "rebirthGearWalkmanHeadphones")], "exact sequential reported equipment required")
        ping = r.call("/ping"); need(ping["state"] == "ingame", "existing remote session required"); r.start = ping["lastLogSeq"]
        for step in plan["steps"]:
            for part in (step, step["move"], step["loot"]):
                need(part.get("openActions") and part.get("closeActions"), "complete observed owned-surface inputs required")
                need(type(part.get("sourceSlot")) is int and part["sourceSlot"] >= 0 and part.get("sourcePath")
                     and "TODO" not in part["sourcePath"] and "TODO" not in part["window"], "observed physical source/window required")
            need(step.get("equipActions") and type(step["move"].get("targetSlot")) is int and step["move"]["targetSlot"] >= 0, "real equip inputs and physical move destination required")
            for part in (step["move"], step["loot"]):
                need(part.get("targetPath") and "TODO" not in part["targetPath"] and "TODO" not in part["item"], "observed access target/marker required")
            before = settled(r)
            need(step["window"] not in r.call("/ui/tree", window=step["window"])["openWindows"], "equipment window already open and unowned")
            r.actions(step["openActions"], step["window"])
            shown = node(tree(r, step["window"]), step["sourcePath"]).get("item")
            need(shown and shown["name"] == step["item"] and shown["count"] == 1 and before["bag"][step["sourceSlot"]]["count"] == 1, "original physical equipment source must be positive count1")
            r.actions(step["equipActions"])
            equipped = settled(r, before, step["slot"])
            need(equipped["offer"]["plan"]["GearAfter"]["ItemData"] == before["bag"][step["sourceSlot"]]["itemData"], "new equipment native bytes differ from original source")
            tree(r, step["window"]); r.actions(step["closeActions"])
            need(step["window"] not in r.call("/ui/tree", window=step["window"])["openWindows"], "owned equip surface did not close")
            entry = {"slot": step["slot"], "before": before, "equipped": equipped,
                     "move": access(r, step["move"]), "loot": access(r, step["loot"], True)}
            report.setdefault("steps", []).append(entry); r.save()
        report["pass"] = True
    except Exception as error:
        report["failure"] = type(error).__name__ + ": " + str(error); raise
    finally: r.save()


if __name__ == "__main__": main()
