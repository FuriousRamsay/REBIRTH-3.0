"""Qualify a real saved cooking row around native UI Take input.

The row must already exist in a disposable loaded save. This test never creates a
prepared-output marker or changes a save file. The enclosing batch owns the
Bridge lease and verifies errors; this driver verifies row identity/retirement.
"""
import argparse
import json
import os
from pathlib import Path
import subprocess

from camper_consolidated_regression_live import Runner, need
from mp010_pull_lifecycle_live import topology, require_peers


HERE = Path(__file__).resolve().parent
SCENARIOS = {
    "local-prepared": HERE / "tests/cooking_saved_prepared_take_20261020.json",
    "remote-ready": HERE / "tests/cooking_remote_take.json",
}


def ready_row(runner, x, y, z, prepared):
    state = runner.call("/auditstate", domain="cookingQueue", x=x, y=y, z=z)["domains"]["cookingQueue"]
    need(state.get("available") is True, "native campfire queue audit unavailable")
    matches = [row for row in state.get("rows", [])
               if row.get("recipe") == "foodBakedPotato" and row.get("ready") is True
               and (prepared is None or row.get("preparedOutput") is prepared)]
    need(len(matches) == 1, "exactly one real ready Baked Potato row with required prepared state is needed")
    row = matches[0]
    need(isinstance(row.get("batch"), str) and len(row["batch"]) == 32 and
         row.get("primaryOutput") == "foodBakedPotato" and row.get("recipeCount") == 1,
         "saved batch identity/output differs from fixture")
    return row


def run(args, report):
    owner = Runner(report, Path(args.report), session_path=args.owner_session)
    if args.mode == "local-prepared":
        require_peers(topology(owner), topology(owner), local=True)
        server = owner
        prepared = True
    else:
        need(args.server_session and args.server_player_id > 0, "remote case needs actual server session and authenticated player ID")
        server = Runner(report, Path(args.report), session_path=args.server_session)
        require_peers(topology(owner), topology(server, args.server_player_id))
        prepared = None
    need(owner.call("/guard")["enabled"] is True, "native guard must stay enabled")
    before = ready_row(server, args.x, args.y, args.z, prepared)
    report["beforeRow"] = before
    scenario = SCENARIOS[args.mode]
    env = os.environ.copy()
    env["REBIRTH_BRIDGE_SESSION"] = str(owner.session_path)
    command = ["powershell", "-NoProfile", "-ExecutionPolicy", "Bypass", "-File",
               str(HERE / "gamebridge.ps1"), "test", str(scenario)]
    result = subprocess.run(command, cwd=HERE, env=env, capture_output=True, text=True, timeout=180)
    report["scenario"] = {"file": str(scenario), "exitCode": result.returncode,
                          "stdout": result.stdout, "stderr": result.stderr}
    need(result.returncode == 0, "native UI Take scenario failed")
    summaries = []
    for line in result.stdout.splitlines():
        try:
            summaries.append(json.loads(line))
        except json.JSONDecodeError:
            continue
    need(summaries and summaries[-1].get("passed") is True, "scenario did not confirm exact meal gain")
    after = server.call("/auditstate", domain="cookingQueue", x=args.x, y=args.y, z=args.z)["domains"]["cookingQueue"]
    need(after.get("available") is True, "native queue audit unavailable after Take")
    need(all(row.get("batch") != before["batch"] for row in after.get("rows", [])),
         "same saved batch remains available after Take")
    report["afterQueue"] = after
    report["status"] = "PASS"
    report["passed"] = True


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--mode", choices=SCENARIOS, required=True)
    parser.add_argument("--report", required=True)
    parser.add_argument("--owner-session", default=os.environ.get("REBIRTH_BRIDGE_SESSION"))
    parser.add_argument("--server-session")
    parser.add_argument("--server-player-id", type=int, default=0)
    parser.add_argument("--x", type=int, default=14)
    parser.add_argument("--y", type=int, default=45)
    parser.add_argument("--z", type=int, default=995)
    args = parser.parse_args()
    report = {"status": "FAIL", "passed": False, "mode": args.mode,
              "station": {"x": args.x, "y": args.y, "z": args.z}}
    try:
        run(args, report)
    except Exception as error:
        report["failure"] = type(error).__name__ + ": " + str(error)
        raise
    finally:
        path = Path(args.report)
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(json.dumps(report, indent=2), encoding="utf-8")


if __name__ == "__main__":
    main()
