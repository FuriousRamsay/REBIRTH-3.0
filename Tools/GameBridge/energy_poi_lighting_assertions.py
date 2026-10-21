"""Evidence assertions for parent-run native POI lighting batches; no game calls.

PLAN BEFORE ON OFF [RELOAD_OFF]. PLAN positions are independently reviewed native
block positions, never inferred from successful button replies or AFTER results.
"""
import argparse
import json
from pathlib import Path


def rows(snapshot):
    devices = snapshot.get("devices", snapshot.get("energy", {}).get("devices"))
    assert devices is not None, "actual /energy device evidence required"
    assert devices.get("authoritative") is True, "authoritative native observation required"
    assert devices.get("ordinaryPoiLightsTruncated") is False, "truncated lighting proof"
    return {tuple(row["position"]): row for row in devices["ordinaryPoiLights"]}


def qualify(plan, before, on, off, reload_off=None):
    expected = {tuple(p) for p in plan["ordinaryLightPositions"]}
    assert expected, "at least one independently reviewed ordinary POI light required"
    assert plan["world"] == "CodexTest", "only disposable parent-owned world"
    stages = [("default", before, False), ("on", on, True), ("off", off, False)]
    if reload_off is not None:
        stages.append(("reload-off", reload_off, False))
    for name, snapshot, lit in stages:
        observed = rows(snapshot)
        assert expected <= observed.keys(), name + ": native supported light missing"
        for position in expected:
            row = observed[position]
            assert row["tileType"] == "TileEntityLight", "wrong native light class"
            assert row["nativeRequiredPower"] is None, "ordinary conversion is not native power"
            assert row["configuredProvisionalWatts"] == plan["ordinaryPoiLightWatts"]
            assert row["nativeLightOn"] is lit, name + ": native meta state differs"
            if lit:
                assert row["ownedByCamper"] is True
                assert row["baseline"]["proofed"] is True
                assert row["baseline"]["hasWritten"] is True
                assert row["baseline"]["expectedCurrentStateMatches"] is True
                assert row["activeMeteredDemand"] >= 1, "on cannot be free power"
            else:
                assert row["activeMeteredDemand"] == 0
    return {"ordinaryLightCount": len(expected), "nativeOnOffQualified": True,
            "reloadOffQualified": reload_off is not None,
            "scope": "requires separate gestures/screenshots, source debit, trader, reset and wired-switch evidence"}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("plan")
    parser.add_argument("before")
    parser.add_argument("on")
    parser.add_argument("off")
    parser.add_argument("reload_off", nargs="?")
    args = parser.parse_args()
    load = lambda path: json.loads(Path(path).read_text(encoding="utf-8-sig"))
    print(json.dumps(qualify(load(args.plan), load(args.before), load(args.on), load(args.off),
                             load(args.reload_off) if args.reload_off else None), indent=2))


if __name__ == "__main__":
    main()
