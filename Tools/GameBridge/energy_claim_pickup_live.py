"""Parent-run native claim/menu batch per real owner/ally/foreign client.

run ROLE PLAN REPORT; summarize OWNER_REPORT ALLY_REPORT FOREIGN_REPORT
No setup, role switching, fake pickup, build, launch or timeout restart.
"""
import argparse
import json
import time
from pathlib import Path
from camper_consolidated_regression_live import Runner, need
from energy_regression_live import INPUT, assertions, at

COVERAGE = {"insideCube", "outsideCube", "packingRevoke", "travelRevoke", "physicalClaim"}


def capture(runner, plan):
    state = runner.call("/state", sections="player,world,inventory")
    need(state["world"]["gameName"] == "CodexTest", "only disposable CodexTest allowed")
    owner = runner.call("/gearobserve")
    need(owner["entityId"] == plan["actorEntityId"], "actual peer role identity differs")
    need(owner.get("remote") is True and owner.get("connected") is True, "real connected remote client required")
    need(owner["cursor"]["count"] == 0, "preserve occupied cursor and stop")
    for field in ("gearHeld", "libraryHeld", "resourcePending", "creationHeld", "offerPending"):
        need(owner.get(field) is False, "owner custody unavailable: " + field)
    energy = runner.call("/energy", entity=plan["vehicle"], limit=512)
    need(energy.get("schema") == 1 and energy.get("observationOnly") is True,
         "actual built-in Energy observer required")
    return {"state": state, "owner": owner, "energy": energy,
            "ui": runner.call("/ui/tree", all=1, limit=2500), "target": runner.call("/target")}


def run(role, plan_path, report_path):
    plan = json.loads(plan_path.read_text(encoding="utf-8-sig"))
    need(plan.get("schema") == 1 and plan.get("reviewed") is True and plan.get("role") == role,
         "independently reviewed native peer-role plan required")
    need(plan.get("setupEvidence") and plan.get("preservePaths") and plan.get("preflightChecks"),
         "separate setup, original preservation and explicit visible UI preconditions required")
    need(COVERAGE <= {s.get("coverage") for s in plan["steps"]}, "missing claim coverage")
    report = {"schema": 1, "role": role, "plan": plan, "passed": False}
    runner = Runner(report, report_path)
    try:
        need(runner.call("/ping")["state"] == "ingame", "parent-owned live session required")
        need(runner.call("/guard").get("enabled") is True, "guard must remain on")
        baseline = capture(runner, plan)
        assertions(baseline, plan["preflightChecks"])
        report["baseline"] = baseline
        for step in plan["steps"]:
            need(step.get("checks") and step.get("name"), "independent observable checks required")
            need(step.get("nativeTargetWitness"), "actual block/position/native pickup-menu witness required")
            for action in step.get("actions", []):
                endpoint, args = action["endpoint"], action.get("args", {})
                need(endpoint in INPUT, "setup, grants, console, save and restart forbidden")
                if endpoint.startswith("/ui/"):
                    need("x" not in args and "y" not in args, "observed native selectors required")
                if endpoint == "/ui/drag":
                    need(args.get("strict") == 1 and args.get("from.path") and args.get("to.path"), "strict physical paths required")
                reply = runner.call(endpoint, "POST", **args)
                if endpoint == "/ui/drag":
                    need(reply.get("placedByClick") is False, "fallback drag cannot qualify")
            deadline = time.monotonic() + min(60, step.get("timeoutSeconds", 25))
            while True:
                observed = capture(runner, plan)
                try:
                    assertions(observed, step["checks"])
                    break
                except AssertionError:
                    need(time.monotonic() < deadline, "native claim/menu state never qualified: " + step["name"])
                    time.sleep(.25)
            report.setdefault("steps", []).append({"name": step["name"], "coverage": step["coverage"],
                "nativeTargetWitness": step["nativeTargetWitness"], "observed": observed,
                "screenshot": runner.call("/screenshot", name="energy_claim_" + role + "_" + step["name"])})
            runner.errors()
            runner.save()
        final = capture(runner, plan)
        for path in plan["preservePaths"]:
            need(at(baseline, path) == at(final, path), "original source/physical claim changed: " + repr(path))
        report["final"], report["passed"] = final, True
        report["scope"] = "reviewed native peer menu/pickup checks only; quota policy not assumed; visual review required"
    except Exception as error:
        report["failure"] = type(error).__name__ + ": " + str(error)
        raise
    finally:
        runner.save()


def main():
    parser = argparse.ArgumentParser(__doc__)
    parser.add_argument("mode", choices=("run", "summarize"))
    parser.add_argument("inputs", nargs=3)
    args = parser.parse_args()
    if args.mode == "run":
        role, plan, report = args.inputs
        need(role in {"owner", "ally", "foreign"}, "actual role required")
        run(role, Path(plan), Path(report))
    else:
        reports = [json.loads(Path(p).read_text()) for p in args.inputs]
        need({r["role"] for r in reports} == {"owner", "ally", "foreign"}, "three real roles required")
        need(all(r.get("passed") is True for r in reports), "all peer roles must independently qualify")
        need(len({r["baseline"]["owner"]["entityId"] for r in reports}) == 3, "three distinct actual peer actors required")
        need(len({r["baseline"]["owner"]["worldKey"] for r in reports}) == 1, "same actual disposable world required")
        need(len({r["baseline"]["energy"]["vehicleIdentity"] for r in reports}) == 1, "same actual camper identity required")
        print(json.dumps({"passed": True, "reports": args.inputs, "scope": "reviewed three-role native pickup/menu assertions; physical quota policy pending"}))


if __name__ == "__main__":
    main()
