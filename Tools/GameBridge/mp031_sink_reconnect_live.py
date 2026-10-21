"""AUTHORED NOT RUN. Already-prepared genuine MP sink reconnect fixture.
No launch/restart/save control or synthetic receipts. Caller prepares actual
Prepared-before-delivery interruption in disposable peers and supplies its GUID.
"""
import argparse
import collections
import json
import time
from pathlib import Path
from camper_consolidated_regression_live import Runner, need
from multipeer_batch_live import endpoint, qualify

CONVERSIONS = {
    "drinkJarRiverWater": ("drinkJarEmpty", 500),
    "drinkJarEmpty": ("drinkJarRiverWater", -500),
    "bucketRiverWater": ("bucketEmpty", 10000),
    "bucketEmpty": ("bucketRiverWater", -10000),
}


def execute(args):
    output = Path(args.report)
    need(not output.exists(), "new report required")
    peers = {}
    for name in ("host", "client"):
        report = output.with_name(output.stem + "." + name + ".json")
        need(not report.exists(), "new peer report required")
        peers[name] = Runner({"fixture": "MP031", "peer": name}, report,
                             session_path=getattr(args, name + "_session"),
                             base_url=endpoint(getattr(args, name + "_endpoint")))
    result = {"status": "RUNNING", "mode": args.mode, "operation": args.operation}
    try:
        topology = qualify(peers, args.dedicated)
        result["topology"] = topology
        actor = topology["client"]["entityId"]
        for runner in peers.values():
            ping = runner.call("/ping")
            need(ping["pid"] == runner.scope["pid"], "endpoint/pinned process mismatch")
            runner.start = ping["lastLogSeq"]

        def observe():
            row = peers["host"].call("/auditstate", domain="sinkOriginal", player=actor,
                                     entity=args.vehicle, operation=args.operation)["domains"]["sinkOriginal"]
            need(row.get("available") is True, "actual same-owner sink original required")
            need(row["operation"].lower() == args.operation.replace("-", "").lower(), "operation differs")
            return row

        def inventory():
            row = peers["client"].call("/auditstate", domain="nativeInventory")["domains"]["nativeInventory"]
            need(row.get("available") is True, "actual native owner image required")
            return row

        before = observe()
        owner_before = inventory()
        result["before"], result["ownerBefore"] = before, owner_before
        need(before["phase"] == 0 and not before["savedConversion"] and not before["savedCancellation"],
             "fixture must be actual Prepared before owner outcome")
        target, unit = CONVERSIONS[before["beforeItem"]]
        need(before["count"] > 0 and before["afterItem"] == target and
             before["afterMl"] - before["beforeMl"] == unit * before["count"], "native vessel/litre contract differs")
        need(args.authorized_gameplay, "explicit authorized gameplay required")
        # Native look input makes the real sink prompt query its retained operation.
        # Coordinates must be observed sink geometry, not the vehicle centre.
        peers["client"].actions([{"endpoint": "/lookat", "args": {"x": args.x, "y": args.y, "z": args.z}}])
        deadline = time.monotonic() + args.timeout
        expected_phase = 2 if args.mode == "resume" else 3
        while True:
            after = observe()
            if after["phase"] == expected_phase:
                break
            need(after["phase"] in (0, 1), "unexpected terminal sink outcome")
            need(time.monotonic() < deadline, "sink did not settle within fixture timeout")
            time.sleep(0.2)
        owner_after = inventory()
        result["after"], result["ownerAfter"] = after, owner_after
        if args.mode == "resume":
            need(after["savedConversion"] and not after["savedCancellation"] and
                 after["currentMl"] == before["afterMl"], "conversion/native reservoir publication missing")
            expected = collections.Counter(owner_before["itemsByName"])
            expected[before["beforeItem"]] -= before["count"]
            expected[target] += before["count"]
            need(all(value >= 0 for value in expected.values()), "fixture original vessel missing")
            need(+expected == +collections.Counter(owner_after["itemsByName"]), "native item conservation differs")
        else:
            need(after["savedCancellation"] and not after["savedConversion"] and
                 after["currentMl"] == before["beforeMl"], "cancellation changed reservoir or lacks saved proof")
            need(owner_before["fingerprint"] == owner_after["fingerprint"], "cancellation mutated owner inventory")
        final = qualify(peers, args.dedicated)
        for name in ("host", "client"):
            for key in ("processIdentity", "ownerKey", "worldKey", "creationId", "entityId"):
                need(final[name][key] == topology[name][key], "fixture scope changed: " + key)
        for runner in peers.values():
            runner.errors()
        result["status"] = "PASS"
    except Exception as error:
        result["status"], result["error"] = "FAIL", type(error).__name__ + ": " + str(error)
        raise
    finally:
        output.parent.mkdir(parents=True, exist_ok=True)
        output.write_text(json.dumps(result, indent=2), encoding="utf-8")
        for runner in peers.values():
            runner.save()


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ("host", "client"):
        parser.add_argument("--" + name + "-session", required=True)
        parser.add_argument("--" + name + "-endpoint", required=True)
    parser.add_argument("--operation", required=True)
    parser.add_argument("--vehicle", type=int, required=True)
    for axis in ("x", "y", "z"):
        parser.add_argument("--" + axis, type=float, required=True)
    parser.add_argument("--mode", choices=("resume", "cancel"), required=True)
    parser.add_argument("--dedicated", action="store_true")
    parser.add_argument("--authorized-gameplay", action="store_true")
    parser.add_argument("--timeout", type=float, default=30)
    parser.add_argument("--report", required=True)
    arguments = parser.parse_args()
    need(0 < arguments.timeout <= 60, "bounded quick fixture timeout required")
    execute(arguments)
