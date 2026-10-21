"""Reviewed real-input Energy batch. Never launches, prepares fixtures, or reloads.

run PLAN REPORT; checkpoint PLAN REPORT; resume PLAN REPORT
Setup is parent-owned and is recorded separately, never counted as qualification.
"""
import argparse
import json
import time
from pathlib import Path
from camper_consolidated_regression_live import Runner, need

INPUT = {"/activate", "/lookat", "/key", "/press", "/release", "/move",
         "/ui/click", "/ui/drag", "/ui/hover", "/ui/type", "/ui/scroll"}
REQUIRED = {"install", "component", "refuel", "camp", "travel", "driving", "poi", "defense"}


def at(value, path):
    for part in path:
        value = value[part]
    return value


def capture(runner, plan):
    state = runner.call("/state", sections="player,world,inventory")
    need(state["world"]["gameName"] == "CodexTest", "only disposable CodexTest is authorized")
    return {"state": state, "owner": runner.call("/gearobserve"),
            "energy": runner.call("/energy", entity=plan["vehicle"], limit=512),
            "camper": runner.call("/camper", entity=plan["vehicle"]),
            "ui": runner.call("/ui/tree", all=1, limit=2500),
            "physical": runner.source_observation()}


def idle(observed):
    owner = observed["owner"]
    need(owner.get("cursor", {}).get("count") == 0, "cursor not empty; preserve it and stop")
    for name in ("gearHeld", "libraryHeld", "resourcePending", "creationHeld", "offerPending"):
        need(owner.get(name) is False, "owner custody unavailable: " + name)
    need(observed["camper"].get("pending") is False, "camper transfer still pending")
    need(observed["energy"].get("held") is False and
         observed["energy"]["devices"].get("leaseWitnessPending") is False,
         "Energy source/device persistence is still held")


def assertions(observed, checks):
    need(checks, "action acceptance alone cannot qualify a step")
    for check in checks:
        # Values are reviewed independently in the plan, never copied from after.
        need(at(observed, check["path"]) == check["equals"],
             "independent expected state differs: " + repr(check["path"]))


def steps(runner, plan, entries):
    for step in entries:
        need(step.get("name") and step.get("checks"), "named step with independent checks required")
        for action in step.get("actions", []):
            endpoint, args = action["endpoint"], action.get("args", {})
            need(endpoint in INPUT, "setup/console/save/restart forbidden in gesture phase")
            need("x" not in args and "y" not in args or endpoint in {"/lookat", "/activate"},
                 "UI gestures require observed selectors, never screen coordinates")
            if endpoint == "/ui/drag":
                need(args.get("strict") == 1 and args.get("from.path") and args.get("to.path"),
                     "strict physical source/target paths required")
            result = runner.call(endpoint, "POST", **args)
            if endpoint == "/ui/drag":
                need(result.get("placedByClick") is False, "fallback click cannot qualify held drag")
        deadline = time.monotonic() + min(60, step.get("timeoutSeconds", 20))
        while True:
            observed = capture(runner, plan)
            try:
                assertions(observed, step["checks"])
                idle(observed)
                break
            except AssertionError:
                need(time.monotonic() < deadline, "step state never qualified: " + step["name"])
                time.sleep(.25)
        runner.report.setdefault("evidence", []).append({"name": step["name"], "observed": observed,
            "screenshot": runner.call("/screenshot", name="energy_" + step["name"])})
        runner.errors()
        runner.save()


def main():
    parser = argparse.ArgumentParser(__doc__)
    parser.add_argument("phase", choices=("run", "checkpoint", "resume"))
    parser.add_argument("plan", type=Path)
    parser.add_argument("report", type=Path)
    args = parser.parse_args()
    plan = json.loads(args.plan.read_text(encoding="utf-8-sig"))
    need(plan.get("schema") == 1 and plan.get("reviewed") is True,
         "parent reviewed observed-session plan required")
    need(plan.get("setupEvidence") and plan.get("preservePaths"),
         "separate setup evidence and exact original-source preservation paths required")
    report = json.loads(args.report.read_text()) if args.phase == "resume" else {"schema": 1, "plan": plan, "passed": False}
    runner = Runner(report, args.report)
    try:
        ping = runner.call("/ping")
        need(ping["state"] == "ingame", "explicit parent-launched current game required")
        need(runner.call("/guard").get("enabled") is True, "guard must stay on")
        baseline = capture(runner, plan)
        idle(baseline)
        runner.report.setdefault("baseline", baseline)
        if args.phase == "resume":
            need(report.get("awaitingExplicitReload") is True, "no checkpoint to resume")
            need(report["checkpointSession"]["pid"] != runner.scope["pid"] or
                 report["checkpointSession"]["startedUtc"] != runner.scope["startedUtc"],
                 "reload needs a separately parent-owned new process")
            assertions(baseline, plan["reloadChecks"])
            # A restart resets process clocks; no elapsed-time or resource continuity is inferred.
            steps(runner, plan, plan["resumeSteps"])
            report["awaitingExplicitReload"] = False
        else:
            need(REQUIRED <= {step.get("coverage") for step in plan["steps"]}, "missing required Energy coverage")
            assertions(baseline, plan["preflightChecks"])
            steps(runner, plan, plan["steps"])
        final = capture(runner, plan)
        idle(final)
        for path in plan["preservePaths"]:
            need(at(report["baseline"], path) == at(final, path), "original source/cargo changed: " + repr(path))
        report["final"] = final
        if args.phase == "checkpoint":
            assertions(final, plan["checkpointChecks"])
            report["checkpointSession"] = runner.scope
            report["awaitingExplicitReload"] = True
            report["qualification"] = "gesture batch complete; save/reload NOT qualified"
        else:
            report["passed"] = True
            report["qualification"] = "reviewed observable assertions only; screenshots require visual review"
    except Exception as error:
        report["passed"] = False
        report["failure"] = type(error).__name__ + ": " + str(error)
        raise
    finally:
        runner.save()


if __name__ == "__main__":
    main()
