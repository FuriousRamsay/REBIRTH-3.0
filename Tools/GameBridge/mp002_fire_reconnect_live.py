"""Read-only MP002 capture/verify around externally authorized reconnect or reload.

AUTHORED_NOT_RUN. Never launches, quits, disconnects, saves, ignites or presses input.
capture PLAN NATIVE_REPORT REPORT; verify PLAN CAPTURE_REPORT REPORT --kind reconnect|reload
Native report must have passed the 22 real-input cases. Plans supply current observed
host/client/observer sessions, hostPlayer, and two reconnectPositions by native case name
and kind (fire/cooldown). Both native rows must still exist and be saved at capture.
"""
import argparse
import json
import time
from pathlib import Path
from camper_consolidated_regression_live import Runner, need


def read(path):
    return json.loads(Path(path).read_text(encoding="utf-8-sig"))


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("phase", choices=("capture", "verify"))
    parser.add_argument("plan", type=Path)
    parser.add_argument("prior", type=Path)
    parser.add_argument("report", type=Path)
    parser.add_argument("--kind", choices=("reconnect", "reload"))
    args = parser.parse_args()
    plan, prior = read(args.plan), read(args.prior)
    need(args.report.resolve() not in (args.plan.resolve(), args.prior.resolve()), "preserve original plan/evidence")
    need(prior.get("status") == "PASS", "positive preceding phase evidence required")
    report = {"status": "RUNNING", "phase": args.phase, "kind": args.kind, "observations": [],
              "prior": str(args.prior.resolve()), "validation": "read-only actual authority/owner/observer"}
    runners = {role: Runner(report, args.report, session_path=plan[role + "Session"])
               for role in ("host", "client", "observer")}
    host = runners["host"]

    def topology(role):
        query = {"player": plan["hostPlayer"]} if role == "host" else {}
        return runners[role].call("/auditstate", domain="topology", **query)["domains"]["topology"]

    def authority(position):
        value = host.call("/auditstate", domain="fire", player=plan["hostPlayer"], saved=1, radius=0, **position)["domains"]["fire"]
        need(value.get("available") and value.get("authoritative") and value.get("enabled") and not value["truncated"], "current complete authority fire state required")
        return value

    def projection(role, position):
        value = runners[role].call("/auditstate", domain="fire", radius=0, **position)["domains"]["fire"]
        need(value.get("available") and value.get("projection") and not value.get("authoritative") and value.get("enabled")
             and not value.get("snapshotPending"), "current settled owner-local projection required")
        return value

    def fire_identity(row):
        return {k: row[k] for k in ("x", "y", "z", "igniter", "blockType", "ignitedAt")}

    try:
        current = {role: topology(role) for role in runners}
        need(all(t["gameName"] == "CodexTest" for t in current.values()), "disposable CodexTest only")
        need(current["host"]["server"] and not current["host"]["remote"], "actual authority required")
        need(len({t["processIdentity"] for t in current.values()}) == 3
             and len({r.scope["pid"] for r in runners.values()}) == 3, "three independent processes required")
        need(current["host"]["worldKey"] and len({t["worldKey"] for t in current.values()}) == 1, "same actual world required")
        need(current["client"]["ownerKey"] and current["observer"]["ownerKey"]
             and current["client"]["ownerKey"] != current["observer"]["ownerKey"], "independent native owner clients required")
        original = prior["topology"]
        for role in ("client", "observer"):
            native = current[role]
            need(native["remote"] and native["connected"] and native["localActor"], "actual connected owner client required")
            need(all(native[k] == original[role][k] for k in ("worldKey", "ownerKey", "creationId", "deaths", "gameName")), "original owner/world/life required")
            at_host = host.call("/auditstate", domain="topology", player=native["entityId"])["domains"]["topology"]
            need(at_host["authoritativePeer"] and all(at_host[k] == native[k] for k in ("ownerKey", "creationId", "deaths")), "authentic native rejoined peer required")
            need(runners[role].call("/guard")["enabled"] is True, "guard must remain enabled")
            report.setdefault("arrivals", {})[role] = runners[role].call("/surroundings")
        need(current["host"]["dedicated"] == original["host"]["dedicated"], "same actual authority topology required")
        need(current["host"]["worldKey"] == original["host"]["worldKey"], "original authority save required")
        if not current["host"]["dedicated"]:
            need(current["host"]["localActor"] and all(current["host"][k] == original["host"][k]
                 for k in ("ownerKey", "creationId", "deaths")), "original listen-host native owner/life required")
        if args.phase == "capture":
            need(len(prior.get("cases", [])) == 22 and all(c.get("status") == "PASS" for c in prior["cases"]), "complete actual native producer cohort required")
            need(args.kind is None, "capture precedes an external lifecycle transition")
            need(all(current[role][k] == original[role][k] for role in runners
                     for k in ("processIdentity", "worldInstance", "entityId")), "capture must use original native input sessions")
            selectors = plan.get("reconnectPositions")
            need(isinstance(selectors, list) and len(selectors) == 2
                 and {s.get("kind") for s in selectors} == {"fire", "cooldown"}, "one native fire and one native cooldown case required")
            cases = {c["name"]: c for c in prior["cases"]}
            positions = []
            for selector in selectors:
                need(selector.get("case") in cases, "position must come from recorded native input case")
                case = cases[selector["case"]]
                need(case["outcome"] == "hit", "lifecycle baseline must follow a positive native action")
                need(case["source"] == "extinguisher" if selector["kind"] == "cooldown" else case["after"]["fires"], "genuine producer outcome required")
                positions.append({"kind": selector["kind"], "case": selector["case"], "position": case["after"]["center"]})
        else:
            need(prior.get("phase") == "capture" and args.kind is not None, "captured baseline and explicit verification kind required")
            positions = prior["positions"]
            old, new = original["host"], current["host"]
            if args.kind == "reconnect":
                need(old["processIdentity"] == new["processIdentity"] and old["worldInstance"] == new["worldInstance"], "reconnect must preserve original authority native World")
                role = plan.get("reconnectRole")
                need(role in ("client", "observer"), "explicit reconnected native owner role required")
                before, after = original[role], current[role]
                need(before["processIdentity"] != after["processIdentity"] or before["worldInstance"] != after["worldInstance"], "actual native client World replacement required; unchanged projection is not reconnect evidence")
            else:
                need(old["processIdentity"] != new["processIdentity"] or old["worldInstance"] != new["worldInstance"], "actual authority native World reload required")
        for runner in runners.values():
            runner.start = runner.call("/ping")["lastLogSeq"]
        report["topology"] = current
        observed = []
        for row in positions:
            position = row["position"]
            need(set(position) == {"x", "y", "z"} and all(type(v) is int for v in position.values()), "actual native block position required")
            deadline = time.monotonic() + 75
            while True:
                value = authority(position)
                saved = value.get("saved")
                if saved and saved.get("available") and saved["revision"] == value["revision"]:
                    break
                need(time.monotonic() < deadline, "existing autosave did not publish current state; no save was forced")
                time.sleep(0.25)
            if row["kind"] == "fire":
                need(len(value["fires"]) == 1 and len(saved["fires"]) == 1, "retained native fire must exist live and in primary")
                identity = fire_identity(value["fires"][0])
                need(identity == fire_identity(saved["fires"][0]), "native saved fire original differs from live state")
                if args.phase == "verify":
                    need(identity == row["identity"], "reconnect/reload did not preserve native fire original")
            else:
                need(not value["fires"] and len(value["cooldowns"]) == 1 and len(saved["cooldowns"]) == 1, "retained extinguished native cooldown required")
                identity = value["cooldowns"][0]
                need(identity == saved["cooldowns"][0] and identity["expires"] > value["worldTime"], "unexpired native cooldown must be saved exactly")
                if args.phase == "verify":
                    need(identity == row["identity"], "native cooldown original changed across lifecycle transition")
            projections = {}
            for role in ("client", "observer"):
                deadline = time.monotonic() + 12
                while True:
                    projected = projection(role, position)
                    expected = row["kind"] == "fire"
                    if projected["active"] is expected and projected["visible"] is expected and (expected or projected["cooldown"] == identity["expires"]):
                        projections[role] = projected
                        break
                    need(time.monotonic() < deadline, "rejoined native projection/particle/cooldown did not resynchronize")
                    time.sleep(0.2)
            observed.append({**row, "identity": identity})
            report["observations"].append({"kind": row["kind"], "authority": value, "clients": projections})
        report["positions"] = observed
        for role, runner in runners.items():
            final = topology(role)
            need(all(final[k] == current[role][k] for k in ("processIdentity", "worldInstance", "worldKey", "ownerKey", "entityId", "creationId", "deaths")), "native scope changed during read-only qualification")
            runner.errors()
        report["status"] = "PASS"
        report["coverage"] = "recorded normal native reconnect/reload only; no crash recovery, other topology or gameplay inferred"
    except Exception as error:
        report["status"] = "FAIL"
        report["failure"] = type(error).__name__ + ": " + str(error)
        raise
    finally:
        host.save()


if __name__ == "__main__":
    main()
