"""AUTHORED_NOT_RUN. Actual MP027 placed enqueue/cancel/completion acceptance.

start PLAN REPORT; resume PLAN REPORT; verify PLAN REPORT
Setup and genuine disconnect/reload remain caller-owned. Never launches a game,
grants items, writes a receipt, fabricates a queue or invokes a financial service.
PLAN supplies observed real UI actions and owner/host (optional viewer) sessions.
"""
import argparse
import base64
import json
import time
import xml.etree.ElementTree as ET
from pathlib import Path
from camper_consolidated_regression_live import Runner, need, ALLOWED


def observe(runner, operation=None, player=None):
    args = {} if player is None else {"player": player}
    topology = runner.call("/auditstate", domain="topology", **args)["domains"]["topology"]
    need(topology["gameName"] == "CodexTest", "Disposable CodexTest required")
    if operation:
        args["operation"] = operation
    evidence = runner.call("/auditstate", domain="recyclerPlacedOriginals", **args)["domains"]["recyclerPlacedOriginals"]
    need(evidence.get("available") is True and evidence["installed"] is True, "Actual recycler evidence unavailable or bounded view incomplete")
    need(evidence["owner"] == topology["ownerKey"], "Actual owner identity differs")
    return {"topology": topology, "evidence": evidence}


def record_map(sample):
    result = {}
    for row in sample["evidence"]["records"]:
        need(row["suffix"] not in result, "Ambiguous original record")
        root = ET.fromstring(row["xml"])
        need(root.tag == "workstationCompletionIntent", "Unexpected original record grammar")
        result[row["suffix"]] = (root, row["digest"])
    return result


def canonical(node):
    return ET.tostring(node, encoding="unicode")


def same_station(actual, expected, offset):
    left, right = base64.b64decode(actual, validate=True), base64.b64decode(expected, validate=True)
    need(13 <= offset <= len(left) - 8 and len(left) == len(right), "Complete native station frame required")
    return left[:offset] == right[:offset] and left[offset + 8:] == right[offset + 8:]


def check(report, sample, purpose, terminal=False):
    evidence = sample["evidence"]
    if not evidence.get("original"):
        need(not terminal, "Same durable original missing")
        return
    original = ET.fromstring(evidence["original"])
    need(original.tag == "placedRecyclerOriginal", "Actual owner original required")
    need(original.attrib["operation"] == report["operation"] and original.attrib["purpose"] == purpose, "Wrong original operation/purpose")
    for name in ("owner", "account", "world", "creation"):
        need(original.attrib[name] == report["identity"][name], "Original identity differs: " + name)
    encoded = canonical(original)
    if report.get("original"):
        need(encoded == report["original"], "Retry/reentry replaced immutable original")
    else:
        report["original"] = encoded
    records = record_map(sample)
    for root, digest in records.values():
        need(root.attrib["operation"] == report["operation"] and canonical(root.find("before/placedRecyclerOriginal")) == encoded, "Saved record is not bound to same original")
        need(isinstance(digest, str) and len(digest) == 64, "Published primary digest unavailable")
    if not terminal:
        return
    need(".terminal.xml" in records and evidence.get("terminalArtifactsValid") is True, "Exact saved native owner/station terminal artifacts required")
    if report.get("viewer"):
        need(evidence.get("nativeViewer") == report["viewer"], "Completion lacks the exact original foreign native viewer provenance")
    terminal_root, terminal_digest = records[".terminal.xml"]
    pair = terminal_root.find("recyclerPlacedTerminal")
    need(pair is not None and pair.find("actualStationSaved") is not None, "Native station publication missing")
    completed = records.get(".cleanup-saved.xml") or records.get(".remote-completed.xml")
    need(completed is not None, "Saved native marker cleanup has not completed")
    cleanup = completed[0].find("recyclerCleanupSaved")
    if cleanup is None:
        remote = completed[0].find("recyclerRemoteCompleted")
        need(remote is not None, "Unknown terminal release record")
        cleanup = remote.find("recyclerCleanupSaved")
        need(remote.attrib["terminal"] == terminal_digest, "Remote release has wrong pair terminal")
    else:
        need(cleanup.attrib["terminal"] == terminal_digest, "Local release has wrong pair terminal")
    need(cleanup is not None, "Actual saved cleanup evidence required")
    need(not evidence["held"] and not evidence.get("stationHeld") and not evidence.get("nativeMarker"), "Retained custody is not terminal release")
    need(same_station(evidence["stationNative"], original.findtext("afterNative"), int(original.attrib["afterOffset"])), "Actual native station does not match paid after image")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("phase", choices=("start", "resume", "verify"))
    parser.add_argument("plan", type=Path)
    parser.add_argument("report", type=Path)
    args = parser.parse_args()
    plan = json.loads(args.plan.read_text(encoding="utf-8-sig"))
    need(plan["purpose"] in ("enqueue", "cancel", "complete"), "One actual placed operation required")
    report = {"status": "RUNNING", "samples": []} if args.phase == "start" else json.loads(args.report.read_text(encoding="utf-8"))
    owner = Runner(report, args.report, session_path=plan["ownerSession"], base_url=plan.get("ownerUrl"))
    host = Runner(report, args.report, session_path=plan["hostSession"], base_url=plan.get("hostUrl"))
    viewer = Runner(report, args.report, session_path=plan["viewerSession"], base_url=plan.get("viewerUrl")) if plan.get("viewerSession") else owner
    first = observe(owner)
    authority = observe(host, player=first["topology"]["entityId"])
    need(authority["topology"]["server"], "Actual authoritative host required")
    need(first["evidence"]["owner"] == authority["evidence"]["owner"], "Host selected a different recipient")
    if first["topology"]["remote"]:
        need(first["topology"]["connected"] and authority["topology"]["authoritativePeer"], "Actual connected native owner/peer required")
        need(first["topology"]["processIdentity"] != authority["topology"]["processIdentity"], "Independent remote process required")
    if plan.get("dedicated"):
        need(authority["topology"]["dedicated"] is True, "Genuine dedicated endpoint required")
    if viewer is not owner:
        viewed = viewer.call("/auditstate", domain="topology")["domains"]["topology"]
        need(viewed["gameName"] == "CodexTest" and viewed["remote"] and viewed["connected"], "Actual remote native viewer required")
        need(viewed["ownerKey"] != first["topology"]["ownerKey"], "Foreign viewer and original reward recipient must differ")
        need(viewed["processIdentity"] != authority["topology"]["processIdentity"], "Viewer must be independent of authority")
    if args.phase == "start":
        need(not first["evidence"]["issued"] and not authority["evidence"]["issued"], "Prior actual custody must finish before setup")
        known = set(first["evidence"]["operations"]) | set(authority["evidence"]["operations"])
        report["identity"] = {key: first["evidence"][key] for key in ("owner", "account", "world", "creation")}
        report["sessions"] = {"owner": owner.scope, "host": host.scope}
        report["processes"] = {"owner": first["topology"]["processIdentity"], "host": authority["topology"]["processIdentity"]}
        if viewer is not owner:
            report["viewer"] = {"entity": viewed["entityId"], "creation": viewed["creationId"], "deaths": viewed["deaths"]}
        need(bool(plan.get("actions")), "Observed real native UI actions required")
        for action in plan["actions"]:
            need(action["endpoint"] in ALLOWED, "Only real player input routes permitted")
            viewer.call(action["endpoint"], "POST", **action.get("args", {}))
        deadline = time.monotonic() + plan.get("timeout", 90)
        while True:
            actual = observe(host, player=first["topology"]["entityId"])
            added = set(actual["evidence"]["operations"]) - known
            need(len(added) <= 1, "Prepare exactly one new native operation")
            if added:
                report["operation"] = added.pop()
                break
            need(time.monotonic() < deadline, "Real input produced no original")
            time.sleep(.25)
    else:
        need(report.get("operation") and report.get("identity"), "Prior actual source report required")
        if args.phase == "verify":
            need(first["topology"]["processIdentity"] != report["processes"]["owner"] or authority["topology"]["processIdentity"] != report["processes"]["host"], "Genuine new native process required for cold verification")
        for action in plan.get("resumeActions", []):
            need(action["endpoint"] in ALLOWED, "Only actual native reopen/input permitted")
            viewer.call(action["endpoint"], "POST", **action.get("args", {}))
    deadline = time.monotonic() + plan.get("timeout", 90)
    while True:
        local = observe(owner, report["operation"])
        remote = observe(host, report["operation"], local["topology"]["entityId"])
        report["samples"].append({"owner": local, "host": remote})
        check(report, remote, plan["purpose"])
        check(report, local, plan["purpose"])
        owner.save()
        if args.phase == "start" and plan.get("stopAfterOriginal") and report.get("original"):
            report["status"] = "ORIGINAL_RECORDED_NOT_SETTLED"
            break
        host_records = record_map(remote)
        if ".terminal.xml" in host_records and not local["evidence"]["held"] and not remote["evidence"]["held"]:
            check(report, remote, plan["purpose"], True)
            original = ET.fromstring(report["original"])
            need(local["evidence"]["bag"] == original.findtext("afterBag") and local["evidence"]["belt"] == original.findtext("afterBelt"), "Actual owner grids differ from conserved original after images")
            if local["topology"]["remote"]:
                need(".remote-retired.xml" in record_map(local), "Actual owner has not saved authenticated terminal retirement")
            if viewer is not owner:
                viewed_final = observe(viewer, report["operation"])
                report["viewerFinal"] = viewed_final
                need(not viewed_final["evidence"]["viewedHeld"], "Foreign viewer has not released its nonfinancial completion hold")
            report["status"] = "PASS"
            break
        need(time.monotonic() < deadline, "Original has not completed; preserved custody is not a pass")
        time.sleep(.25)
    owner.save()
    print(json.dumps({"status": report["status"], "operation": report["operation"], "report": str(args.report)}))


if __name__ == "__main__":
    main()
