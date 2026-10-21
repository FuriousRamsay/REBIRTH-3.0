"""Offline checks of parent-captured real overview/station UI rectangles."""
import argparse
import json
from pathlib import Path


def need(value, message):
    if not value:
        raise AssertionError(message)


def nodes(cap):
    value = cap.get("ui", cap)
    result = value.get("nodes")
    need(isinstance(result, list), "actual native UI nodes required")
    return result


def unique(items, label):
    need(len(items) == 1, "unique visible " + label + " required")
    return items[0]


def rectangle(node):
    value = node.get("rect")
    need(isinstance(value, list) and len(value) == 4 and all(isinstance(x, (int, float)) for x in value)
         and value[2] > 0 and value[3] > 0, "actual nonempty native rectangle required")
    return value


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--overview", required=True)
    parser.add_argument("--station", required=True)
    parser.add_argument("--output", required=True)
    args = parser.parse_args()
    output = Path(args.output)
    need(not output.exists(), "existing frame result preserved")
    report = {"schema": 1, "passed": False, "inputs": vars(args)}
    try:
        overview, station = [json.loads(Path(p).read_text(encoding="utf-8-sig")) for p in (args.overview, args.station)]
        need(overview.get("session") and overview["session"] == station.get("session"), "same parent-captured game session required")
        a, b = nodes(overview), nodes(station)
        frame_a = unique([n for n in a if n.get("id") == "camperOverviewFrame"], "overview outer frame")
        frame_b = unique([n for n in b if n.get("id") == "rebirthCraftingRootFrame"], "station outer frame")
        nav_a = unique([n for n in a if n.get("id") == "camperStationSwitch"], "overview navigation")
        nav_b = unique([n for n in b if n.get("id") == "camperStationSwitch"], "station navigation")
        need(not any(n.get("id") in {"camperStationPickerClose", "camperStationPickerCloseActionFrame", "camperOverviewCloseCaption"} for n in a), "overview Close chrome remains")
        need(rectangle(frame_a) == rectangle(frame_b), "overview/station outer frame size or position differs")
        need(rectangle(nav_a) == rectangle(nav_b), "overview/station navigation size or position differs")
        report.update(passed=True, overviewFrame=frame_a, stationFrame=frame_b, overviewNavigation=nav_a, stationNavigation=nav_b)
    except Exception as error:
        report["failure"] = type(error).__name__ + ": " + str(error)
        raise
    finally:
        output.parent.mkdir(parents=True, exist_ok=True)
        output.write_text(json.dumps(report, indent=2), encoding="utf-8")


if __name__ == "__main__":
    main()
