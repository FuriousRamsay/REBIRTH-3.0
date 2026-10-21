"""Read-only staged context geometry capture and offline regression assertions.
Parent performs all UI input. This helper never launches, grants, clicks or drags.
"""
import argparse
import json
import math
from collections import Counter
from pathlib import Path
from camper_consolidated_regression_live import Runner, need

KINDS = ("context", "drag", "returned", "filter-top", "filter-bottom", "filter-selected")
CHROME = ("title-normal", "close-normal", "close-hover")


def capture(args):
    path = Path(args.output)
    need(not path.exists(), "capture exists; preserve original evidence")
    report = {"schema": 1, "readOnly": True, "kind": args.kind, "fixture": args.fixture, "passed": False}
    runner = Runner(report, path)
    try:
        # Ordinary crafting preflight requires an empty cursor. This staged
        # observer must preserve the genuine picked-up cursor for drag proof.
        ping = runner.call("/ping")
        need(ping["state"] == "ingame" and ping["pid"] == runner.scope["pid"], "current ingame process required")
        need(runner.call("/guard").get("enabled") is True, "guard must remain enabled")
        report["arrival"] = runner.call("/surroundings")
        nearby = runner.call("/entities", radius=8, limit=100)["entities"]
        need(any(r["id"] == args.fixture and math.isfinite(r["distance"]) and r["distance"] <= 8 for r in nearby), "exact nearby fixture required")
        state = runner.call("/state", sections="player,world,inventory")
        need(state["world"]["gameName"] == "CodexTest", "real save refused")
        camper = runner.call("/camper", entity=args.fixture)
        need(camper.get("pending") is False, "unresolved fixture transfer refused")
        need(camper.get("cursorEmpty") is (args.kind != "drag"), "cursor state does not match manually staged capture")
        report["fixtureState"] = {"state": state, "camper": camper}
        report["session"] = runner.scope
        report["physical"] = runner.source_observation()
        report["ui"] = runner.call("/ui/tree", all=1)
        need(report["ui"].get("truncated") is False, "truncated UI is not geometry evidence")
        if args.kind not in ("close-normal", "close-hover"):
            need(context_slots(report), "parent must open actual context workspace before capture")
        if args.kind in ("close-normal", "close-hover"):
            need(args.target_path, "actual close action path required")
            report["targetPath"] = args.target_path
            if args.kind == "close-hover":
                need(args.hover_evidence, "parent-owned real hover request/reply evidence required")
                report["hoverEvidence"] = json.loads(Path(args.hover_evidence).read_text(encoding="utf-8-sig"))
        runner.errors()
        report["passed"] = True
    except Exception as error:
        report["failure"] = type(error).__name__ + ": " + str(error)
        raise
    finally:
        runner.save()


def context_slots(cap):
    return [s for s in cap["physical"]["visibleSlots"] if s.get("presented") is True
            and s.get("intersectsViewport") is True and "contextBackpack" in s]


def sources(cap):
    return cap["physical"]["sources"] + cap["physical"]["contextSources"]


def unique(rows, reason):
    need(len(rows) == 1, reason + " matches=" + str(len(rows)))
    return rows[0]


def rect(view):
    value = view.get("rect")
    need(value and len(value) == 4 and value[2] > 0 and value[3] > 0, "actual nonzero native screen rectangle required")
    return value


def close(a, b, tolerance=.02):
    need(abs(a-b) <= tolerance, "geometry mismatch: " + str(a) + " vs " + str(b))


def topleft(view):
    x, y = view["position"]
    w, h = view["size"]
    pivot = view["pivot"]
    if pivot in ("Top", "Center", "Bottom"): x -= w/2
    if pivot in ("TopRight", "Right", "BottomRight"): x -= w
    if pivot in ("Left", "Center", "Right"): y += h/2
    if pivot in ("BottomLeft", "Bottom", "BottomRight"): y += h
    return x, y


def geometry(cap):
    physical = {(s["location"], s["slot"]): s for s in sources(cap)}
    for s in context_slots(cap):
        size = 65 if s["contextBackpack"] else 78
        g = s["geometry"]
        need(g["root"]["size"] == [size, size] and g["root"]["scale"] == [1, 1, 1], "context native dimensions/root scale drift")
        need(g["icon"]["size"] == [size-11, size-11], "native icon dimensions drift")
        need(g["count"]["size"] == [size-5, 28] and g["count"]["fontSize"] == 26, "native count layout drift")
        need(len(g["rails"]) == 3, "three native quality/durability rails required")
        for rail in g["rails"]:
            need(rail["size"] == [size-5, 10] and rail["position"] == [1, -(4*size//5)], "native rail dimensions/position drift")
        rect(g["root"]); rect(g["icon"])
        if s.get("stack"):
            key = ("backpack" if s["contextBackpack"] else "container", s["slotNumber"])
            need(key in physical, "visible proxy has no physical source")
            for field in ("name", "count", "payload"):
                need(s["stack"][field] == physical[key][field], "proxy changed physical source identity/count")


def contents(cap):
    result = Counter()
    for s in sources(cap): result[s["payload"]] += s["count"]
    cursors = [s for s in cap["physical"]["visibleSlots"] if s.get("dragCursor") and s.get("stack")]
    need(len(cursors) <= 1, "ambiguous native cursor")
    for s in cursors: result[s["stack"]["payload"]] += s["stack"]["count"]
    return result


def contained(inner, outer):
    a, b = rect(inner), rect(outer)
    return a[0] >= b[0]-1 and a[1] >= b[1]-1 and a[0]+a[2] <= b[0]+b[2]+1 and a[1]+a[3] <= b[1]+b[3]+1


def held_quality_rails(before, drag, returned):
    """Screen-space rails of the actual occupied cursor, not selected-item bars."""
    need(before["session"] == drag["session"] == returned["session"] and before["ui"]["screen"] == drag["ui"]["screen"] == returned["ui"]["screen"], "held rail session/screen drift")
    cursor = unique([s for s in drag["physical"]["visibleSlots"] if s.get("dragCursor") and s.get("stack")], "actual occupied native drag cursor")
    stack = cursor["stack"]
    need(stack["hasQuality"] and stack["quality"] > 0 and stack["count"] > 0, "positive quality cursor required")
    need(drag["fixtureState"]["camper"]["cursorEmpty"] is False, "selected slot cannot substitute for held cursor")
    need(contents(before) == contents(drag) == contents(returned) and sources(returned) == sources(before), "held drag/return changed native payloads")
    current = {(s["location"], s["slot"]): s for s in sources(drag)}
    debited = [s for s in sources(before) if s["payload"] == stack["payload"]
               and s["count"] - current.get((s["location"], s["slot"]), {}).get("count", 0) == stack["count"]]
    physical = unique(debited, "original exact physical dragged source")
    reference = unique([s for s in context_slots(before) if s.get("stack", {}).get("payload") == stack["payload"]
                        and s["slotNumber"] == physical["slot"]
                        and s["contextBackpack"] == (physical["location"] == "backpack")], "original visible dragged slot geometry")
    src, dst = reference["geometry"], cursor["geometry"]
    background = unique([r for r in dst["rails"] if r["id"] == "durabilityBackground" and r["visible"]], "actual visible held quality background")
    fill = unique([r for r in dst["rails"] if r["id"] == "durability" and r["kind"] == "XUiV_Sprite" and r["visible"]], "actual visible held quality fill")
    original_background = unique([r for r in src["rails"] if r["id"] == background["id"] and r["kind"] == background["kind"]], "native source quality background")
    icon_a, icon_b = rect(src["icon"]), rect(dst["icon"])
    ratio = icon_b[2] / icon_a[2]
    close(icon_b[3] / icon_a[3], ratio)
    source_rect = rect(original_background)
    expected = [icon_b[0] + (source_rect[0]-icon_a[0])*ratio,
                icon_b[1] + (source_rect[1]-icon_a[1])*ratio,
                source_rect[2]*ratio, source_rect[3]*ratio]
    actual = rect(background)
    for a, b in zip(actual, expected): close(a, b, 1.0)
    # NGUI reports the filled sprite widget's full geometry independent of its
    # fill amount; background/fill must occupy the same native rail rectangle.
    for a, b in zip(rect(fill), actual): close(a, b, 1.0)
    need(contained(background, dst["root"]) and contained(fill, dst["root"]), "held rail lies outside actual cursor rectangle")
    return {"cursorPath": cursor["path"], "physicalSource": physical, "referencePath": reference["path"],
            "cursorRect": rect(dst["root"]), "backgroundRect": actual, "fillRect": rect(fill),
            "expectedBackgroundRect": expected, "observedIconScale": ratio, "screenTolerance": 1.0,
            "nativePointerHeldBitObserved": False}


def dropdown(cap, target):
    nodes = cap["ui"]["nodes"]
    viewport = unique([n for n in nodes if n.get("id") == "listViewport" and "camperCargoCategoryScroll/" in n["path"]], "cargo dropdown viewport")
    row = unique([n for n in nodes if n.get("id") == target], "cargo dropdown choice " + target)
    need(row.get("clickable") is True and contained(row, viewport), "dropdown choice not fully accessible within actual viewport")
    return row


def verify(args):
    path = Path(args.output)
    need(not path.exists(), "result exists; preserve original evidence")
    report = {"schema": 1, "readOnly": True, "passed": False, "captureFiles": args.captures}
    try:
        captures = {}
        report["captures"] = captures
        for file in args.captures:
            cap = json.loads(Path(file).read_text(encoding="utf-8"))
            need(cap.get("passed") is True and cap.get("readOnly") is True and cap["kind"] not in captures, "failed/duplicate capture")
            captures[cap["kind"]] = cap
        need(set(captures) == set(KINDS), "all six manually staged captures required")
        before = captures["context"]
        for cap in captures.values():
            need(cap["session"] == before["session"] and cap["fixture"] == before["fixture"]
                 and cap["ui"]["screen"] == before["ui"]["screen"], "session/fixture/viewport drift")
            geometry(cap)
            need(contents(cap) == contents(before), "staged UI operation changed exact native payload totals")
        for kind in ("returned", "filter-top", "filter-bottom", "filter-selected"):
            need(sources(captures[kind]) == sources(before), "filter/return changed physical source slots or hashes")
        drag = captures["drag"]
        report["heldQualityRailGeometry"] = held_quality_rails(before, drag, captures["returned"])
        cursor = unique([s for s in drag["physical"]["visibleSlots"] if s.get("dragCursor") and s.get("stack")], "genuine picked-up cursor")
        need(cursor["stack"]["hasQuality"] and cursor["stack"]["quality"] > 0, "quality item required for rail/count qualification")
        reference = next((s for s in context_slots(drag) if s["contextBackpack"]), None)
        need(reference is not None, "actual visible proxy geometry reference absent")
        src, dst = reference["geometry"], cursor["geometry"]
        ratio = dst["icon"]["size"][0] / src["icon"]["size"][0]
        sx, sy = topleft(src["icon"]); dx, dy = topleft(dst["icon"])
        for source_rail in src["rails"]:
            target_rail = unique([r for r in dst["rails"] if r["kind"] == source_rail["kind"] and r["id"] == source_rail["id"]], "matching native drag rail")
            x, y = topleft(source_rail)
            need(target_rail["pivot"] == "TopLeft" and target_rail["size"] == source_rail["size"], "drag rail native pivot/size drift")
            close(target_rail["position"][0], round(dx+(x-sx)*ratio), .51)
            close(target_rail["position"][1], round(dy+(y-sy)*ratio), .51)
            close(target_rail["scale"][0], ratio); close(target_rail["scale"][1], ratio)
            if target_rail["visible"]: rect(target_rail)
        count = dst["count"]; rail = src["rails"][0]
        label_x = topleft(rail)[0] + (rail["size"][0]-src["count"]["size"][0])/2
        label_y = topleft(src["count"])[1]
        close(count["position"][0], round(dx+(label_x-sx)*ratio), .51)
        close(count["position"][1], round(dy+(label_y-sy)*ratio), .51)
        need(count["text"] == str(cursor["stack"]["quality"]) and count["alignment"] == "Center", "drag quality count text/alignment wrong")
        close(count["scale"][0], ratio); close(count["scale"][1], ratio); rect(count)
        first = dropdown(captures["filter-top"], "camperCargoCategoryChoice0")
        bottom_nodes = captures["filter-bottom"]["ui"]["nodes"]
        choices = [n for n in bottom_nodes if n.get("id", "").startswith("camperCargoCategoryChoice") and n.get("clickable")]
        need(choices, "actual native category rows missing")
        last_id = "camperCargoCategoryChoice" + str(captures["filter-bottom"]["physical"]["categoryChoiceCount"]-1)
        last = dropdown(captures["filter-bottom"], last_id)
        need(last_id != first["id"], "distinct dropdown end choices required")
        selected = captures["filter-selected"]
        expected = [s for s in selected["physical"]["contextSources"] if s["category"] == args.category]
        shown = [s for s in context_slots(selected) if not s["contextBackpack"] and s.get("stack")]
        need(expected and shown and all(s["stack"]["category"] == args.category for s in shown), "selected category is not native-classified cargo subset")
        report.update({"passed": True, "dragReference": reference, "cursor": cursor,
                       "accessibleFirst": first, "accessibleLast": last, "selectedCategory": args.category})
    except Exception as error:
        report["failure"] = type(error).__name__ + ": " + str(error)
        raise
    finally:
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(json.dumps(report, indent=2), encoding="utf-8")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    modes = parser.add_subparsers(dest="mode", required=True)
    cap = modes.add_parser("capture")
    cap.add_argument("kind", choices=KINDS+CHROME); cap.add_argument("output"); cap.add_argument("--fixture", type=int, required=True)
    cap.add_argument("--target-path"); cap.add_argument("--hover-evidence")
    check = modes.add_parser("verify")
    check.add_argument("output"); check.add_argument("--captures", nargs="+", required=True); check.add_argument("--category", required=True)
    held = modes.add_parser("verify-held")
    held.add_argument("output"); held.add_argument("--captures", nargs=3, required=True)
    held.add_argument("--strict-drag-receipt", required=True, help="Recorded /ui/drag request entry with args.strict=1 and native reply")
    chrome = modes.add_parser("verify-chrome")
    chrome.add_argument("output"); chrome.add_argument("--captures", nargs=3, required=True)
    args = parser.parse_args()
    if args.mode == "capture": capture(args)
    elif args.mode == "verify": verify(args)
    elif args.mode == "verify-chrome":
        from camper_chrome_geometry_assertions import verify_chrome
        verify_chrome(args)
    else:
        path = Path(args.output); need(not path.exists(), "new evidence destination required")
        report = {"schema": 1, "readOnly": True, "passed": False, "captureFiles": args.captures}
        try:
            caps = [json.loads(Path(f).read_text(encoding="utf-8-sig")) for f in args.captures]
            need(all(c.get("passed") is True and c.get("readOnly") is True for c in caps), "successful original native captures required")
            by_kind = {c["kind"]: c for c in caps}; need(set(by_kind) == {"context", "drag", "returned"}, "distinct before/held/returned captures required")
            evidence = held_quality_rails(by_kind["context"], by_kind["drag"], by_kind["returned"])
            request = json.loads(Path(args.strict_drag_receipt).read_text(encoding="utf-8-sig"))
            need(request.get("endpoint") == "/ui/drag" and request.get("method") == "POST" and request.get("args", {}).get("strict") == 1,
                 "original recorded strict request required; raw reply does not prove strict mode")
            receipt = request["reply"]
            need(receipt.get("ok") is True and receipt.get("placedByClick") is False and receipt.get("stillHeld") is None, "successful native strict held release required; no fallback")
            need(receipt.get("from", {}).get("path") == evidence["referencePath"], "strict source is not the observed physical held source")
            need(request["args"].get("from.path") == evidence["referencePath"] and request["args"].get("to.path") == receipt.get("to", {}).get("path"), "strict recorded selectors differ")
            original = evidence["physicalSource"]; actual = receipt["from"].get("item", {})
            need(actual.get("name") == original["name"] and actual.get("count") == original["count"] and actual.get("quality") == original["quality"], "strict receipt positive quality source differs")
            report.update({"passed": True, "geometry": evidence, "strictRequestReceipt": request, "captures": caps})
        except Exception as error:
            report["failure"] = type(error).__name__ + ": " + str(error); raise
        finally:
            path.parent.mkdir(parents=True, exist_ok=True); path.write_text(json.dumps(report, indent=2), encoding="utf-8")


if __name__ == "__main__":
    main()
