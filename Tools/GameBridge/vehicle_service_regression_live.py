"""Source-only service regression. Parent opens/prepares disposable service UI.
Run PLAN.json REPORT.json; all targets resolve from current visible nodes.
No launch, give, direct inventory writes or drag fallback. Failure retains evidence.
"""
import argparse
from collections import Counter
from copy import deepcopy
import json
from pathlib import Path
import re
from camper_consolidated_regression_live import Runner, need

WINDOW = "vehicle"
CLOSE = "vehicle/windowVehicle/vehicleEditorBody/vehicleEditorClose"


def tree(runner):
    value = runner.call("/ui/tree", window=WINDOW, all=1, limit=5000)
    need(value.get("truncated") is False and WINDOW in value["openWindows"], "open untruncated service tree required")
    return value


def node(value, path, controller=None):
    rows = [row for row in value["nodes"] if row["path"] == path]
    need(len(rows) == 1, "unique visible node required: " + path)
    row = rows[0]
    need(row.get("rect") and row["rect"][2] > 0 and row["rect"][3] > 0, "visible nonempty target bounds required")
    if controller:
        need(row.get("ctrl") == controller, "wrong actual service controller")
    return row


def image(runner):
    value = runner.call("/gearobserve")
    need(value.get("schema") == 1 and value.get("gameName") == "CodexTest", "exact disposable native inventory required")
    for key in ("gearHeld", "libraryHeld", "resourcePending", "creationHeld", "offerPending"):
        need(value[key] is False, "unrelated custody pending: " + key)
    return value


def inventory(value):
    result = Counter()
    for row in value["bag"] + value["belt"] + [value["cursor"]]:
        need(type(row["count"]) is int and row["count"] >= 0 and isinstance(row["itemData"], str), "malformed native item image")
        if row["count"]:
            need(row["itemData"], "missing native item image")
            result[row["itemData"]] += row["count"]
    return result


def same(before, after):
    for key in ("entityId", "worldKey", "creationId", "bag", "belt", "bagLocks", "cursor"):
        need(before[key] == after[key], "original exact state changed: " + key)


def bindings(value, path):
    return {row["path"]: {key: row[key] for key in ("text", "sprite") if key in row}
            for row in value["nodes"] if row["path"].startswith(path + "/")}


def action(runner, endpoint, **args):
    runner.actions([{"endpoint": endpoint, "args": args}])


def strict(runner, source, target):
    action(runner, "/ui/drag", **{"from.path": source, "to.path": target, "strict": 1})
    # Runner already refuses ok:false. Actual destination/custody assertions below
    # are still required; HTTP input success cannot count as installation success.


def normal_roundtrip(runner, case):
    before = image(runner); need(before["cursor"]["count"] == 0, "empty original cursor required")
    value = tree(runner); source = node(value, case["source"], "XUiC_RebirthEditorBackpackSlot")
    target = node(value, case["target"], "XUiC_RebirthEditorBackpackSlot")
    wanted = source.get("item")
    need(wanted and wanted["name"] == case["item"] and target.get("item") is None, "exact preparatory stack and empty backpack target required")
    strict(runner, case["source"], case["target"])
    value = tree(runner)
    need(node(value, case["source"]).get("item") is None and node(value, case["target"]).get("item") == wanted, "strict normal backpack deposit did not complete")
    mid = image(runner); need(mid["cursor"]["count"] == 0 and inventory(mid) == inventory(before), "normal drag custody/conservation failure")
    strict(runner, case["target"], case["source"])
    same(before, image(runner))
    runner.report["normalRoundtrip"] = True; runner.save()


def service_case(runner, case):
    before = image(runner); need(before["cursor"]["count"] == 0, "empty original cursor required")
    value = tree(runner); source = node(value, case["source"], "XUiC_RebirthEditorBackpackSlot")
    controller = {"modifier": "XUiC_RebirthVehiclePartSlot", "cosmetic": "XUiC_RebirthVehicleCosmeticSlot"}[case["kind"]]
    node(value, case["target"], controller)
    wanted = source.get("item"); need(wanted and wanted["name"] == case["item"] and wanted["count"] == 1, "one exact preparatory native part required")
    origin = int(re.search(r"/editorSlot(\d+)$", case["source"]).group(1))
    original = before["bag"][origin]; need(original["count"] == 1, "exact source slot preimage required")
    empty = bindings(value, case["target"])
    need(empty and case["emptyText"] in [row.get("text") for row in empty.values()], "target must be native empty slot")

    def installed():
        current = image(runner); expected = deepcopy(before["bag"]); expected[origin] = {"itemData": "", "count": 0}
        # Encoded cells may expose extra presentation fields: use exact item fields.
        need([(x["itemData"], x["count"]) for x in current["bag"]] == [(x["itemData"], x["count"]) for x in expected]
             and current["belt"] == before["belt"] and current["cursor"]["count"] == 0, "native installation did not debit exactly one original stack")
        shown = bindings(tree(runner), case["target"])
        need(case["installedText"] in [row.get("text") for row in shown.values()]
             and any(row.get("sprite") == case["installedIcon"] for row in shown.values()), "native target did not present accepted part")

    def returned():
        strict(runner, case["target"], case["source"])
        same(before, image(runner)); need(bindings(tree(runner), case["target"]) == empty, "native target not restored empty")

    # Ordinary click picks the source onto native cursor, then returns it to source.
    action(runner, "/ui/click", path=case["source"])
    held = image(runner); need(held["cursor"]["itemData"] == original["itemData"] and held["cursor"]["count"] == 1
                              and inventory(held) == inventory(before), "ordinary click did not pick up original custody")
    action(runner, "/ui/click", path=case["source"]); same(before, image(runner))
    action(runner, "/ui/click", path=case["source"], shift=1)
    if case["shiftOutcome"] == "refuse":
        same(before, image(runner)); need(bindings(tree(runner), case["target"]) == empty, "shift refusal mutated native target")
    else:
        need(case["shiftOutcome"] == "install", "explicit native shift outcome required"); installed(); returned()
    strict(runner, case["source"], case["target"]); installed()
    # Native BasePartStack ordinary mouse release inspects an occupied part;
    # it must not silently return/debit it like the preferred-location gesture.
    installed_image = image(runner)
    action(runner, "/ui/click", path=case["target"])
    same(installed_image, image(runner)); installed()
    returned()
    runner.report.setdefault("acceptedCases", []).append(case["kind"]); runner.save()


def rejected_case(runner, case):
    before = image(runner); value = tree(runner)
    source = node(value, case["source"], "XUiC_RebirthEditorBackpackSlot"); node(value, case["target"], "XUiC_RebirthVehiclePartSlot")
    need(source.get("item") and source["item"]["name"] == case["item"], "explicit incompatible native item control required")
    origin = int(re.search(r"/editorSlot(\d+)$", case["source"]).group(1)); original = before["bag"][origin]
    need(original["count"] > 0 and before["cursor"]["count"] == 0, "exact incompatible source/cursor required")
    previous = bindings(value, case["target"])
    try:
        strict(runner, case["source"], case["target"])
    except AssertionError:
        reply = runner.report["requests"][-1].get("reply", {})
        need(reply.get("ok") is False and reply.get("placedByClick") is False and reply.get("stillHeld"), "unexpected rejection transport/input failure")
    held = image(runner)
    need(bindings(tree(runner), case["target"]) == previous and inventory(held) == inventory(before), "rejected target changed or lost items")
    need(held["cursor"]["itemData"] == original["itemData"] and held["cursor"]["count"] == original["count"]
         and held["bag"][origin]["count"] == 0, "rejection needs proven pickup and exact original cursor custody")
    action(runner, "/ui/click", path=case["source"])
    same(before, image(runner)); runner.report["rejectedControl"] = True; runner.save()


def close_hover(runner):
    value = tree(runner); close = node(value, CLOSE, "XUiC_RebirthVehicleActionButton")
    frame_path = CLOSE + "ActionFrame"; frame = node(value, frame_path)
    need(close["rect"] == frame["rect"], "visible close frame and hit-node bounds differ")
    x, y, w, h = close["rect"]
    inside = runner.call("/ui/hover", "POST", path=CLOSE, seconds=.3)
    need(inside.get("hovered") == "vehicleEditorClose", "inside visible close bounds did not hit close")
    need(node(tree(runner), frame_path).get("sprite") == "ui_game_select_row", "close inside hover style absent")
    screen = value["screen"]; candidates = [(x-w*.1, y+h*.5), (x+w*1.1, y+h*.5), (x+w*.5, y-h*.2), (x+w*.5, y+h*1.2)]
    observed = 0
    for px, py in candidates:
        if not (0 <= px < screen[0] and 0 <= py < screen[1]):
            continue
        reply = runner.call("/ui/hover", "POST", x=px, y=py, seconds=.3)
        need(reply.get("hovered") != "vehicleEditorClose", "close hit region extends outside visible node rect")
        need(node(tree(runner), frame_path).get("sprite") == "menu_empty2px", "close stayed hovered outside visible rect")
        observed += 1
    need(observed >= 2, "at least two visible outside close boundaries required")
    runner.report["closeHoverBounds"] = close["rect"]; runner.save()


def main():
    parser = argparse.ArgumentParser(__doc__); parser.add_argument("plan"); parser.add_argument("report"); args = parser.parse_args()
    plan = json.loads(Path(args.plan).read_text(encoding="utf-8-sig")); report = {"schema": 1, "pass": False, "plan": plan}
    runner = Runner(report, Path(args.report))
    try:
        runner.start = runner.call("/status")["lastLogSeq"]
        need(plan.get("schema") == 1 and {case["kind"] for case in plan["accepted"]} == {"modifier", "cosmetic"}, "both different native accepted target types required")
        def filled(value):
            if isinstance(value, dict):
                for child in value.values():
                    filled(child)
            elif isinstance(value, list):
                for child in value:
                    filled(child)
            elif isinstance(value, str):
                need(not value.startswith("<"), "unfilled plan placeholder")
        filled(plan)
        original = image(runner); visible = tree(runner); runner.errors()
        for case in [plan["normal"], *plan["accepted"], plan["rejected"]]:
            need(re.search(r"/editorSlot\d+$", case["source"]), "observed editor source slot path required")
            source = node(visible, case["source"], "XUiC_RebirthEditorBackpackSlot")
            need(source.get("item") and source["item"]["name"] == case["item"], "prepared source identity mismatch")
            node(visible, case["target"])
        for case in plan["accepted"]:
            need(case["shiftOutcome"] in ("install", "refuse") and case["installedText"] and case["installedIcon"], "explicit native accepted/shift control required")
        report["original"] = original; runner.save()
        normal_roundtrip(runner, plan["normal"])
        for case in plan["accepted"]:
            service_case(runner, case)
        rejected_case(runner, plan["rejected"]); close_hover(runner); same(original, image(runner)); runner.errors()
        action(runner, "/ui/click", path=CLOSE)
        need(WINDOW not in runner.call("/ui/tree", window=WINDOW)["openWindows"], "native close button did not close service")
        same(original, image(runner)); runner.errors()
        report["pass"] = True
    except Exception as error:
        report["failure"] = type(error).__name__ + ": " + str(error); raise
    finally:
        runner.save()

if __name__ == "__main__":
    main()

