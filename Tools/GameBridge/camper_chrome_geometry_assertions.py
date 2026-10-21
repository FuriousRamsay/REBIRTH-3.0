"""Offline assertions for parent-staged real UI geometry and close-button hover."""
import json
from pathlib import Path
from camper_slot_geometry_profile import unique, rect, contained, CHROME
from camper_consolidated_regression_live import need


def title_nonoverlap(cap):
    nodes = cap["ui"]["nodes"]
    title = unique([n for n in nodes if n.get("id") == "contextContainerName"], "actual storage title")
    toggle = unique([n for n in nodes if n.get("id") == "camperCargoCategoryToggle"], "actual visible cargo dropdown")
    a, b = rect(title), rect(toggle)
    need(title.get("text") and toggle.get("clickable") is True, "actual title and dropdown required")
    need(a[0]+a[2] <= b[0]+1, "storage title rectangle overlaps cargo dropdown")
    need(a[1] < b[1]+b[3] and b[1] < a[1]+a[3], "dropdown is not beside the actual storage title row")
    return {"title": title, "dropdown": toggle}


def close_chrome(cap, hovering):
    target = unique([n for n in cap["ui"]["nodes"] if n["path"] == cap.get("targetPath")], "observed close action target")
    need(target.get("ctrl") == "XUiC_RebirthVehicleActionButton" and target.get("clickable") is True
         and target.get("enabled") is not False and "close" in target.get("id", "").lower(), "actual enabled close action renderer required")
    # Exact inspected RebirthVehicleActionButton resolves this sibling ID.
    frame_path = target["path"].rsplit("/", 1)[0] + "/" + target["id"] + "ActionFrame"
    frame = unique([n for n in cap["ui"]["nodes"] if n["path"] == frame_path], "actual close action frame")
    need(frame.get("sprite") == ("ui_game_select_row" if hovering else "menu_empty2px"), "actual close hover visual state not witnessed")
    need(contained(frame, target), "close highlight exceeds actual native button rectangle")
    if hovering:
        proof = cap.get("hoverEvidence", {})
        need(proof.get("session") == cap["session"] and proof.get("endpoint") == "/ui/hover"
             and proof.get("method") == "POST" and proof.get("args", {}).get("path") == target["path"], "same-session parent real hover request required")
        reply = proof.get("reply", {})
        need(reply.get("ok") is True and reply.get("hoveredNode", {}).get("path") == target["path"]
             and reply.get("hovered") == target["id"], "native hover reply does not identify actual close button")
    return {"target": target, "frame": frame}


def verify_chrome(args):
    path = Path(args.output)
    need(not path.exists(), "existing chrome result preserved")
    report = {"schema": 1, "readOnly": True, "passed": False, "captureFiles": args.captures, "captures": {}}
    try:
        for file in args.captures:
            cap = json.loads(Path(file).read_text(encoding="utf-8-sig"))
            need(cap.get("passed") is True and cap.get("readOnly") is True and cap["kind"] in CHROME
                 and cap["kind"] not in report["captures"], "failed/ambiguous chrome capture")
            report["captures"][cap["kind"]] = cap
        captures = report["captures"]
        need(set(captures) == set(CHROME), "title plus normal/real-hover close captures required")
        before = captures["title-normal"]
        for cap in captures.values():
            need(cap["session"] == before["session"] and cap["fixture"] == before["fixture"]
                 and cap["ui"]["screen"] == before["ui"]["screen"], "chrome session/fixture/viewport drift")
            need(cap["physical"]["sources"] == before["physical"]["sources"], "chrome observation changed native owner source identities")
            need(cap["fixtureState"]["camper"]["cargo"] == before["fixtureState"]["camper"]["cargo"], "chrome observation changed native cargo slot hashes/counts")
        report["titleGeometry"] = title_nonoverlap(before)
        normal, hover = close_chrome(captures["close-normal"], False), close_chrome(captures["close-hover"], True)
        need(normal["target"]["path"] == hover["target"]["path"], "different close buttons cannot qualify hover")
        need(rect(normal["target"]) == rect(hover["target"]), "native close button rectangle expanded on hover")
        need(rect(normal["frame"]) == rect(hover["frame"]), "close highlight rectangle expanded on hover")
        report.update({"passed": True, "normal": normal, "hover": hover})
    except Exception as error:
        report["failure"] = type(error).__name__ + ": " + str(error)
        raise
    finally:
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(json.dumps(report, indent=2), encoding="utf-8")
