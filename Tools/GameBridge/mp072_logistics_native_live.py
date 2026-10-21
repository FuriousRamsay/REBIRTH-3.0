"""AUTHORED_NOT_RUN. Actual owner input + host/owner original composition.

start PLAN REPORT; resume PLAN REPORT; verify PLAN REPORT
The caller owns disposable setup, genuine disconnect/death/reload and process
control. This driver never sets a receipt/phase, starts a game or grants items.
"""
import argparse
import json
import time
import xml.etree.ElementTree as ET
from pathlib import Path
from camper_consolidated_regression_live import Runner, need, ALLOWED


def observe(runner, player=None, operation=None):
    args = {} if player is None else {"player": player}
    topology = runner.call("/auditstate", domain="topology", **args)["domains"]["topology"]
    need(topology["gameName"] == "CodexTest", "Disposable CodexTest required")
    if operation:
        args["operation"] = operation
    row = runner.call("/auditstate", domain="logisticsOriginals", **args)["domains"]["logisticsOriginals"]
    need(row.get("available") is True and row["active"] is True, "Actual active logistics route unavailable")
    need(row["owner"] == topology["ownerKey"] and row["world"] == topology["worldKey"], "Native owner/world observation differs")
    return {"topology": topology, "original": row}


def check(report, sample, expected_outcome=None, physical=False):
    row = sample["original"]
    need(row.get("originalAvailable") is True, "Same actual immutable original unavailable")
    need(row["operation"] == report["operation"], "Original operation changed")
    for field in ("owner", "world", "creation"):
        need(row[field] == report["scope"][field], "Original identity changed: " + field)
    if report.get("binding"):
        need(row["binding"] == report["binding"], "Original plan changed across native lifecycle")
    else:
        report["binding"] = row["binding"]
    root = ET.fromstring(row["journal"])
    need(root.tag == "logisticsTransfer" and root.attrib["operation"] == row["operation"], "Canonical original journal required")
    need(int(root.attrib["phase"]) == row["phase"] and int(root.attrib["outcome"]) == row["outcome"], "Projected phase differs from actual canonical journal")
    previous = report.get("last", {})
    need(row["phase"] >= previous.get("phase", -1), "Original phase regressed")
    need(not previous.get("outcome") or previous["outcome"] == row["outcome"], "Saved original outcome changed")
    for area in ("nativeLegs", "npcLegs"):
        old = {leg["id"]: leg for leg in previous.get(area, [])}
        need(not old or set(old) == {leg["id"] for leg in row[area]}, "Original source legs replaced")
        for leg in row[area]:
            if leg["id"] in old:
                need(leg["attempted"] >= old[leg["id"]]["attempted"] and leg["published"] >= old[leg["id"]]["published"], "Source attempt/publication regressed")
    legs = row["nativeLegs"] + row["npcLegs"]
    if any(leg["attempted"] for leg in legs):
        need(bool(row.get("ownerDebitProof")), "Source effects preceded saved original owner debit")
    if any(leg["attempted"] >= 2 for leg in legs):
        need(all(leg["published"] >= 1 for leg in legs), "Credit preceded complete original source debit publication")
    report["last"] = row
    if expected_outcome is None:
        return
    need(row["phase"] == 4 and row["outcome"] == expected_outcome, "Actual original has not reached expected terminal")
    if expected_outcome == 1:
        need(all(leg["published"] == 2 for leg in legs), "Committed original lacks actual endpoint publication")
        need(any(root.findtext(name) != root.findtext("ownerBefore") for name in ("ownerDebited", "ownerAfter")) or bool(legs), "Scenario performed no transfer")
    if expected_outcome == 3:
        need(row["rejecting"] and row.get("ownerNoEffectProof") and not row.get("ownerDebitProof"), "No-effect terminal lacks saved native owner proof")
        need(all(leg["attempted"] == 0 and leg["published"] == 0 for leg in legs), "No-effect terminal contains source effects")
    if physical:
        body = row.get("physical", {})
        need(body.get("binding") == report["binding"], "Physical original binding differs")
        if expected_outcome == 3:
            need(body.get("state") == 1 and body.get("nativeBeforeOnly") is True and not body["parcels"], "No-effect physical terminal lacks its positive immutable native-before-only proof")
            required_proofs = ("beforeProof", "ownerFileProof")
        else:
            need(body.get("state") == 4 and body.get("outcome") == expected_outcome, "Original physical settlement is incomplete")
            need(body.get("frozenPlanValid") is True, "Actual saved physical plan is not conserved")
            required_proofs = ("beforeProof", "finalProof", "ownerFileProof")
        need(all(isinstance(body.get(key), str) and len(body[key]) == 64 for key in required_proofs), "Actual native body/owner publication missing")
        for parcel in body["parcels"]:
            need(parcel["state"] >= 3 and len(parcel.get("beforeProof") or "") == 64 and len(parcel.get("finalProof") or "") == 64, "Native overflow parcel publication missing")
    elif not row["remote"]:
        need(row.get("savedOperation") == report["operation"] and row.get("savedPhase") in (3, 4, 5), "Actual final native owner file has not acknowledged original")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("phase", choices=("start", "resume", "verify"))
    parser.add_argument("plan", type=Path)
    parser.add_argument("report", type=Path)
    args = parser.parse_args()
    plan = json.loads(args.plan.read_text(encoding="utf-8-sig"))
    report = {"status": "RUNNING", "samples": []} if args.phase == "start" else json.loads(args.report.read_text(encoding="utf-8"))
    owner = Runner(report, args.report, session_path=plan["ownerSession"], base_url=plan.get("ownerUrl"))
    host = Runner(report, args.report, session_path=plan["hostSession"], base_url=plan.get("hostUrl"))
    first = observe(owner)
    need(first["topology"]["remote"] is True and first["topology"]["connected"] is True, "Genuine connected remote owner required")
    host_first = observe(host, first["topology"]["entityId"])
    need(host_first["topology"]["server"] and host_first["topology"]["authoritativePeer"], "Actual authoritative native peer required")
    need(first["topology"]["processIdentity"] != host_first["topology"]["processIdentity"], "Independent owner and host processes required")
    need(plan.get("topology") in ("listen", "dedicated"), "Plan must name the actual required listen or dedicated host topology")
    need(host_first["topology"]["dedicated"] is (plan["topology"] == "dedicated"), "Actual native host topology differs from required scenario")
    if args.phase == "verify":
        before = report["before"]
        owner_changed = first["topology"]["processIdentity"] != before["owner"]["topology"]["processIdentity"]
        host_changed = host_first["topology"]["processIdentity"] != before["host"]["topology"]["processIdentity"]
        need(owner_changed and host_changed if plan.get("coldBothProcesses", False) else owner_changed or host_changed, "Cold verification requires a genuinely changed native process, not another resume sample")
        for sample in (first, host_first):
            for field in ("owner", "world", "creation"):
                need(sample["original"][field] == report["scope"][field], "Cold process changed original identity: " + field)
        report["coldObserved"] = {"ownerProcessChanged": owner_changed, "hostProcessChanged": host_changed}
    if args.phase == "start":
        need(not first["original"]["held"], "Existing original custody must finish before starting")
        report["scope"] = {key: first["original"][key] for key in ("owner", "world", "creation")}
        report["before"] = {"owner": first, "host": host_first}
        # Observed UI selectors invoke actual native input; no service/journal
        # methods or caller-supplied source snapshots are injected.
        need(bool(plan.get("actions")), "Observed native UI input plan required")
        for action in plan["actions"]:
            need(action["endpoint"] in ALLOWED, "Only actual player input endpoints permitted")
            owner.call(action["endpoint"], "POST", **action.get("args", {}))
        deadline = time.monotonic() + plan.get("timeout", 90)
        while True:
            current = observe(owner)
            operation = current["original"].get("nativeOperation")
            if operation and operation != first["original"].get("nativeOperation"):
                report["operation"] = operation
                break
            need(time.monotonic() < deadline, "Actual native UI produced no new original")
            time.sleep(0.2)
    need(bool(report.get("operation")), "Original operation missing from prior actual phase")
    deadline = time.monotonic() + plan.get("timeout", 90)
    while True:
        owner_sample = observe(owner, operation=report["operation"])
        host_sample = observe(host, owner_sample["topology"]["entityId"], report["operation"])
        if host_sample["original"].get("originalAvailable"):
            check(report, host_sample)
        report["samples"].append({"owner": owner_sample, "host": host_sample})
        owner.save()
        if args.phase == "start" and plan.get("checkpointPhase") is not None:
            need(host_sample["original"].get("phase", -1) <= plan["checkpointPhase"], "Requested actual interruption checkpoint already passed; no simulated hold accepted")
            if host_sample["original"].get("phase") == plan["checkpointPhase"]:
                report["status"] = "CHECKPOINT_RECORDED"
                break
        elif host_sample["original"].get("phase") == 4 and not owner_sample["original"]["held"]:
            check(report, host_sample, plan.get("outcome", 1), plan.get("physical", False))
            need(owner_sample["original"].get("nativeOperation") == report["operation"] and owner_sample["original"].get("nativePhase") in (4, 5), "Owner has not retired the same actual native original")
            if owner_sample["original"].get("originalAvailable"):
                need(owner_sample["original"].get("binding") == report["binding"], "Owner terminal plan differs from authoritative original")
            report["status"] = "PASS"
            break
        need(time.monotonic() < deadline, "Same original has not settled; retained custody is not completion")
        time.sleep(0.25)
    owner.save()
    print(json.dumps({"status": report["status"], "operation": report["operation"], "report": str(args.report)}))


if __name__ == "__main__":
    main()
