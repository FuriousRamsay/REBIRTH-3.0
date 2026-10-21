"""Build a two-case remote cooking batch from observed connected CodexTest peers.

The plan contains no injected state. It seeds a meal with actual client input,
then runs the exact Take/queue-retirement driver in the same loaded session.
"""
import argparse
import json
from pathlib import Path

from camper_consolidated_regression_live import Runner, need
from mp010_pull_lifecycle_live import topology, require_peers


HERE = Path(__file__).resolve().parent
OUT = HERE / "out/plans"


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--client-session", required=True)
    parser.add_argument("--server-session", required=True)
    parser.add_argument("--name", default="remote-cooking-current")
    args = parser.parse_args()
    need(args.name.replace("-", "").isalnum(), "plan name must be simple")
    OUT.mkdir(parents=True, exist_ok=True)
    owner = Runner({}, OUT / (args.name + "-client-observation.json"), session_path=args.client_session)
    server = Runner({}, OUT / (args.name + "-server-observation.json"), session_path=args.server_session)
    own = topology(owner)
    host = topology(server, own["entityId"])
    require_peers(own, host)
    need(owner.call("/ping").get("state") == "ingame", "remote client has not loaded the disposable world")
    plan = {
        "schema": 1,
        "name": "Real remote Baked Potato seed and Take in one connected session",
        "world": "West Xuyofu Territory",
        "save": "CodexTest",
        "captureScreenshots": True,
        "cases": [
            {"id": "remote_seed_ready", "group": "remote-cooking", "name": "Remote client Pull Cook Ready",
             "kind": "scenario", "file": "tests/native_cleanup_remote_ready_seed_20261020.json",
             "enabled": True, "requires": ["remote-campfire-empty-pot-fuel-heat"],
             "timeoutSeconds": 240,
             "scope": "Real client input produces one ready meal; no Take yet."},
            {"id": "remote_take_exact_batch", "group": "remote-cooking", "name": "Remote Take and authoritative queue retirement",
             "kind": "python", "file": "native_cleanup_take_live.py",
             "args": ["--mode", "remote-ready", "--server-session", str(server.session_path),
                      "--server-player-id", str(own["entityId"]), "--x", "14", "--y", "45", "--z", "995",
                      "--report", "{report}"],
             "resultFile": "driver-result.json", "enabled": True,
             "requires": ["remote-campfire-empty-pot-fuel-heat"], "timeoutSeconds": 240,
             "scope": "Distinct connected peers, exact server batch before/after, one native client item gain."}
        ]
    }
    output = OUT / (args.name + ".json")
    need(not output.exists(), "plan already exists; choose a new name to preserve evidence")
    output.write_text(json.dumps(plan, indent=2), encoding="utf-8")
    print(json.dumps({"result": "REMOTE_BATCH_PREPARED", "plan": str(output),
                      "clientSession": str(owner.session_path), "serverSession": str(server.session_path),
                      "playerId": own["entityId"]}))


if __name__ == "__main__":
    main()
