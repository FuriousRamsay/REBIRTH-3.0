"""Prepared, NOT RUN. Coordinate already authorized independent CodexTest peers.
No launch, build, process control or save setup. Tokens never enter reports.
"""
import argparse
import ipaddress
import json
from pathlib import Path
from urllib.parse import urlsplit
from camper_consolidated_regression_live import Runner, need, ALLOWED


def endpoint(value):
    parsed = urlsplit(value)
    need(parsed.scheme == "http" and parsed.hostname and parsed.port and not parsed.username and not parsed.password
         and parsed.path in ("", "/") and not parsed.query and not parsed.fragment, "literal private Bridge endpoint required")
    address = ipaddress.ip_address(parsed.hostname)
    need(address.is_private or address.is_loopback, "public Bridge endpoint refused")
    return value.rstrip("/")


def topology(runner, player=None):
    args = {"domain": "topology"}
    if player is not None:
        args["player"] = player
    value = runner.call("/auditstate", **args)["domains"]["topology"]
    need(value.get("schema") == 1 and value.get("gameName") == "CodexTest", "current disposable native world required")
    need(value.get("processIdentity") and value.get("ownerKey") and value.get("creationId"), "native topology identity unavailable")
    return value


def qualify(peers, dedicated):
    client = topology(peers["client"])
    host = topology(peers["host"], client["entityId"] if dedicated else None)
    need(client["remote"] is True and client["connected"] is True and client["localActor"] is True,
         "actual connected owner-client required")
    need(host["server"] is True and host["remote"] is False and host["dedicated"] is dedicated,
         "actual requested authoritative topology required")
    need(client["processIdentity"] != host["processIdentity"], "same process is not multiplayer")
    need(client["worldKey"] and client["worldKey"] == host["worldKey"], "peers do not share actual world identity")
    if not dedicated:
        need(host["localActor"] is True and host["ownerKey"] != client["ownerKey"], "independent native player identities required")
    mirror = topology(peers["host"], client["entityId"])
    need(mirror.get("authoritativePeer") is True, "host has no logged-in native connection for this client")
    for key in ("ownerKey", "creationId", "entityId", "worldKey"):
        need(mirror[key] == client[key], "authoritative client mirror differs: " + key)
    return {"host": host, "client": client, "authoritativeClient": mirror}


def resolve(values, path):
    value = values
    for part in path:
        value = value[part]
    return value


def run(plan, output, authorized):
    need(not output.exists(), "new report path required")
    need(plan.get("topology") in ("listen", "dedicated"), "explicit topology required")
    need(isinstance(plan.get("phases"), list) and plan["phases"], "nonempty qualified scenario phases required")
    need(set(plan["peers"]) == {"host", "client"}, "explicit host/client sessions required")
    report = {"status": "RUNNING", "requestedTopology": plan["topology"], "phases": []}
    peers = {}
    for name, config in plan["peers"].items():
        need(not output.with_name(output.stem + "." + name + ".json").exists(), "new peer report path required")
        peers[name] = Runner({"peer": name}, output.with_name(output.stem + "." + name + ".json"),
                             session_path=config["session"], base_url=endpoint(config["endpoint"]))
    try:
        report["initialTopology"] = qualify(peers, plan["topology"] == "dedicated")
        for runner in peers.values():
            ping=runner.call("/ping")
            need(ping["pid"] == runner.scope["pid"], "endpoint does not match pinned native process")
            runner.start = ping["lastLogSeq"]
        need(authorized or not any(phase.get("actions") for phase in plan["phases"]),
             "explicit --authorized-gameplay required for input phases")
        for phase in plan["phases"]:
            row = {"name": phase["name"], "before": qualify(peers, plan["topology"] == "dedicated"), "observations": {}}
            report["phases"].append(row)
            for action in phase.get("actions", []):
                need(action["endpoint"] in ALLOWED,
                     "only native input endpoints allowed; setup/process/save control refused")
                need(plan["topology"] != "dedicated" or action["peer"] == "client", "dedicated host has no local gameplay actor")
                peers[action["peer"]].actions([action])
            for observation in phase["observations"]:
                need(observation["endpoint"] in ("/auditstate", "/state", "/ui/tree", "/camper", "/gearobserve", "/fetchobserve"), "read-only observation endpoint required")
                row["observations"][observation["name"]] = peers[observation["peer"]].call(observation["endpoint"], **observation.get("args", {}))
            need(phase.get("assertions"), "observations alone do not prove a scenario")
            for assertion in phase["assertions"]:
                actual = resolve(row["observations"], assertion["path"])
                expected = resolve(row["observations"], assertion["equalsPath"]) if "equalsPath" in assertion else assertion["equals"]
                need(actual == expected, "exact phase assertion failed: " + str(assertion["path"]))
            row["after"] = qualify(peers, plan["topology"] == "dedicated")
            for runner in peers.values():
                runner.errors()
            for name in ("host", "client"):
                for key in ("processIdentity", "ownerKey", "worldKey", "creationId", "entityId"):
                    need(row["before"][name][key] == row["after"][name][key], "phase scope changed: " + key)
            row["status"] = "PASS"
        report["status"] = "PASS"
    except Exception as error:
        report["status"] = "FAIL"
        report["error"] = type(error).__name__ + ": " + str(error)
        raise
    finally:
        for runner in peers.values():
            for action in tuple(runner.held):
                try:
                    runner.call("/release", "POST", action=action)
                    runner.held.discard(action)
                except Exception as cleanup:
                    report.setdefault("cleanupFailures", []).append(type(cleanup).__name__ + ": " + str(cleanup))
                    report["status"]="FAIL"
        output.parent.mkdir(parents=True, exist_ok=True)
        output.write_text(json.dumps(report, indent=2), encoding="utf-8")
        for runner in peers.values():
            runner.save()
    need(report["status"] == "PASS", "batch cleanup failed; inspect retained report")


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("plan", type=Path)
    parser.add_argument("report", type=Path)
    parser.add_argument("--authorized-gameplay", action="store_true")
    args = parser.parse_args()
    run(json.loads(args.plan.read_text(encoding="utf-8-sig")), args.report, args.authorized_gameplay)
