"""Replay exact native observations for incompatible-liquid shortcut regressions.

No game access. The enclosing lead-owned test records real shift-click/sort
inputs and rbfetchtestobserve results. Requiring source identities in storage
prevents unchanged/no-op observations from passing.
"""
import argparse
import collections
import json
from pathlib import Path


def observation(path):
    reply = json.loads(Path(path).read_text(encoding="utf-8-sig"))
    assert reply.get("ok") and not reply.get("hadError"), "Observation failed"
    result = reply["results"][0]
    assert not result.get("hadError")
    return json.loads(result["output"][0])


def cells(value):
    return value["toolbelt"] + value["backpack"] + value["storage"]["slots"]


def inventory(value):
    assert value.get("cursor") is None, "Cursor must be empty"
    result = collections.Counter()
    for cell in cells(value):
        assert cell["count"] > 0 and cell["payload"]
        result[(cell["name"], cell["payload"])] += cell["count"]
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--before", required=True)
    parser.add_argument("--after", required=True, nargs="+")
    parser.add_argument("--storage-seed", required=True, nargs="+", type=int)
    args = parser.parse_args()
    baseline = observation(args.before)
    expected = inventory(baseline)
    source_seeds = set(args.storage_seed)
    for seed in source_seeds:
        assert sum(c["count"] for c in baseline["storage"]["slots"] if c["seed"] == seed) == 0
        assert sum(c["count"] for c in baseline["toolbelt"] + baseline["backpack"] if c["seed"] == seed) == 1
    for path in args.after:
        current = observation(path)
        assert inventory(current) == expected, f"Native item payload/count conservation failed: {path}"
        for seed in source_seeds:
            assert sum(c["count"] for c in current["storage"]["slots"] if c["seed"] == seed) == 1, f"Source did not transfer separately: {seed}"
    print(json.dumps({"status": "PASS_EXACT_NATIVE_SHORTCUT_CONSERVATION", "observations": len(args.after), "transferredSeeds": sorted(source_seeds)}))


if __name__ == "__main__":
    main()
