"""Capture real disposable-save cleanup fixtures without changing game state.

The producer is ordinary gameplay (or a Game Bridge scenario). This tool checks
the resulting native state and copies the save only after the game has exited.
It never writes a receipt, queue marker, or player/vehicle save itself.
"""
import argparse
import hashlib
import json
import re
import shutil
import sys
import urllib.parse
import urllib.request
from datetime import datetime, timezone
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "Tools/GameBridge/out/fixtures/native-cleanup"
SNAPSHOTS = Path("E:/_Haven/shared/REBIRTHFixtures/native-cleanup")
KINDS = ("ready-cooking", "prepared-cooking", "pending-payment", "removal-parcel")
JOURNAL_NAME = re.compile(r"^([0-9a-f]{32})\.([0-3])\.rcj$")


def require(ok, message):
    if not ok:
        raise RuntimeError(message)


def session(path):
    value = json.loads(Path(path).read_text(encoding="utf-8-sig"))
    require(isinstance(value.get("port"), int) and value.get("token") and value.get("pid"), "invalid bridge session")
    return value


def bridge(session_file, endpoint, **query):
    current = session(session_file)
    url = "http://127.0.0.1:" + str(current["port"]) + endpoint
    if query:
        url += "?" + urllib.parse.urlencode(query)
    request = urllib.request.Request(url, headers={"X-Bridge-Token": current["token"]})
    with urllib.request.urlopen(request, timeout=15) as response:
        result = json.load(response)
    require(result.get("ok") is True, "bridge refused " + endpoint)
    return result


def inspect_cooking(args, prepared):
    value = bridge(args.session, "/auditstate", domain="cookingQueue", x=args.x, y=args.y, z=args.z)
    state = value["domains"]["cookingQueue"]
    require(state.get("available") is True, "campfire tile is unavailable")
    rows = [row for row in state.get("rows", []) if row.get("ready") is True and
            (row.get("recipe") == "foodBakedPotato" or row.get("primaryOutput") in ("foodBakedPotato", "Baked Potato")) and
            bool(row.get("preparedOutput")) is prepared]
    require(len(rows) == 1, "expected exactly one real ready Baked Potato row with preparedOutput=" + str(prepared))
    require(rows[0].get("batch") and rows[0].get("recipeCount", 0) > 0, "row lacks a saved batch/output")
    return {"station": {"x": args.x, "y": args.y, "z": args.z}, "row": rows[0], "queue": state}


def inspect_payment(args):
    require(args.save_dir is not None, "--save-dir is required for actor payment")
    folder = Path(args.save_dir) / "REBIRTH/NativeActorPayments"
    require(folder.is_dir(), "native actor payment journal directory is absent")
    grouped = {}
    for entry in folder.iterdir():
        match = JOURNAL_NAME.fullmatch(entry.name)
        require(match is not None and entry.is_file() and not entry.is_symlink(), "unexpected payment journal entry")
        grouped.setdefault(match.group(1), set()).add(int(match.group(2)))
    pending = {operation: sorted(phases) for operation, phases in grouped.items()
               if 0 in phases and 2 not in phases and 3 not in phases}
    require(len(pending) == 1, "expected exactly one native payment with Prepared/Uncertain but no terminal journal")
    operation, phases = next(iter(pending.items()))
    return {"operation": operation, "journalPhases": phases,
            "journalFiles": [f"{operation}.{phase}.rcj" for phase in phases]}


def inspect_parcel(args):
    value = bridge(args.session, "/auditstate", domain="removalParcelOriginals")
    state = value["domains"]["removalParcelOriginals"]
    require(state.get("available") is True and state.get("installed") is True, "removal producer audit unavailable")
    require(not state.get("unknown") and not state.get("bounded"), "removal original is incomplete or bounded")
    baseline_file = OUT / args.name / "baseline.json"
    require(baseline_file.is_file(), "capture a removal-parcel baseline before the native removal action")
    baseline = json.loads(baseline_file.read_text(encoding="utf-8"))
    current = session(args.session)
    require(baseline.get("pid") == current["pid"] and baseline.get("startedUtc") == current["startedUtc"],
            "removal baseline came from a different game process")
    rows = [row for row in state.get("originals", []) if row.get("operation") and
            row["operation"] not in baseline.get("operations", [])]
    require(len(rows) == 1 and rows[0].get("unknown") is False,
            "expected exactly one new complete native removal original")
    return {"original": rows[0], "audit": state}


def evidence(args):
    current = session(args.session)
    status = bridge(args.session, "/ping")
    require(status.get("state") in ("ingame", "server"), "game bridge is not in a loaded world")
    if args.kind == "ready-cooking":
        observed = inspect_cooking(args, False)
    elif args.kind == "prepared-cooking":
        observed = inspect_cooking(args, True)
    elif args.kind == "pending-payment":
        observed = inspect_payment(args)
    else:
        observed = inspect_parcel(args)
    return {"schema": 1, "kind": args.kind, "capturedUtc": datetime.now(timezone.utc).isoformat(),
            "bridge": {key: current[key] for key in ("pid", "startedUtc", "port")},
            "status": {key: status.get(key) for key in ("state", "pid")}, "observed": observed}


def save_evidence(args):
    result = evidence(args)
    destination = OUT / args.name
    if args.kind == "removal-parcel":
        require((destination / "baseline.json").is_file() and not (destination / "capture.json").exists(),
                "removal fixture needs a fresh baseline and no earlier capture")
    else:
        require(not destination.exists(), "fixture name already exists; choose a new name")
        destination.mkdir(parents=True)
    (destination / "capture.json").write_text(json.dumps(result, indent=2), encoding="utf-8")
    print(json.dumps({"result": "CAPTURED_LIVE_STATE", "capture": str(destination / "capture.json"),
                      "saveSnapshot": "NOT_YET_CAPTURED"}))


def snapshot(args):
    source = Path(args.save_dir).resolve(strict=True)
    require(source.is_dir() and source.name.casefold() == "codextest", "only the disposable CodexTest save may be copied")
    captured = OUT / args.name / "capture.json"
    capture = json.loads(captured.read_text(encoding="utf-8"))
    require(capture["kind"] == args.kind, "capture kind differs")
    destination = Path(args.snapshot_root).resolve() / args.name
    require(destination != source and source not in destination.parents and destination not in source.parents,
            "snapshot must be separate from the disposable save")
    require(not (destination / "save").exists(), "snapshot already exists")
    try:
        bridge(args.session, "/ping")
    except (OSError, ValueError):
        pass
    else:
        raise RuntimeError("game bridge still responds; quit the game and allow the native save to finish first")
    files = sorted(path for path in source.rglob("*") if path.is_file())
    require(files, "disposable save is empty")
    if args.kind == "pending-payment":
        payment = capture["observed"]
        journal = source / "REBIRTH/NativeActorPayments"
        require(all((journal / name).is_file() for name in payment["journalFiles"]), "pending payment journal changed before snapshot")
        require(not (journal / f'{payment["operation"]}.2.rcj').exists() and
                not (journal / f'{payment["operation"]}.3.rcj').exists(), "payment became terminal before snapshot")
    destination.mkdir(parents=True, exist_ok=False)
    shutil.copy2(captured, destination / "capture.json")
    baseline = OUT / args.name / "baseline.json"
    if args.kind == "removal-parcel":
        require(baseline.is_file(), "removal baseline missing at snapshot")
        shutil.copy2(baseline, destination / "baseline.json")
    shutil.copytree(source, destination / "save", symlinks=False)
    hashes = {}
    for path in files:
        relative = path.relative_to(source)
        copied = destination / "save" / relative
        source_hash = hashlib.sha256(path.read_bytes()).hexdigest()
        copy_hash = hashlib.sha256(copied.read_bytes()).hexdigest()
        require(source_hash == copy_hash, "native save changed during fixture copy: " + str(relative))
        hashes[str(relative)] = copy_hash
    manifest = {"schema": 1, "kind": args.kind, "source": str(source),
                "savedUtc": datetime.now(timezone.utc).isoformat(), "files": hashes,
                "capture": "capture.json", "status": "CAPTURED_DISPOSABLE_NATIVE_SAVE"}
    (destination / "manifest.json").write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    print(json.dumps({"result": manifest["status"], "path": str(destination), "fileCount": len(files)}))


def verify(args):
    """Verify the captured native save and its original observation, without loading it."""
    source = Path(args.snapshot_root).resolve() / args.name
    manifest = json.loads((source / "manifest.json").read_text(encoding="utf-8"))
    capture = json.loads((source / "capture.json").read_text(encoding="utf-8"))
    require(manifest.get("schema") == 1 and manifest.get("kind") == args.kind and
            manifest.get("status") == "CAPTURED_DISPOSABLE_NATIVE_SAVE", "snapshot manifest mismatch")
    require(capture.get("schema") == 1 and capture.get("kind") == args.kind and
            capture.get("status", {}).get("state") == "ingame", "live capture evidence mismatch")
    require(Path(manifest.get("source", "")).name.casefold() == "codextest", "not a disposable CodexTest source")
    expected = manifest.get("files")
    require(isinstance(expected, dict) and expected, "snapshot file manifest is empty")
    save = source / "save"
    actual = {str(path.relative_to(save)): hashlib.sha256(path.read_bytes()).hexdigest()
              for path in save.rglob("*") if path.is_file() and not path.is_symlink()}
    require(actual == expected, "native save files differ from the captured SHA-256 manifest")
    observed = capture.get("observed", {})
    if args.kind in ("ready-cooking", "prepared-cooking"):
        row = observed.get("row", {})
        require(row.get("ready") is True and row.get("recipe") == "foodBakedPotato" and
                row.get("preparedOutput") is (args.kind == "prepared-cooking") and
                row.get("primaryOutput") == "foodBakedPotato" and row.get("batch"),
                "saved cooking row was not observed in the required state")
    elif args.kind == "pending-payment":
        payment = observed
        journal = save / "REBIRTH/NativeActorPayments"
        require(payment.get("operation") and 0 in payment.get("journalPhases", []) and
                all((journal / name).is_file() for name in payment.get("journalFiles", [])) and
                not (journal / f'{payment["operation"]}.2.rcj').exists() and
                not (journal / f'{payment["operation"]}.3.rcj').exists(),
                "native actor payment is missing or terminal")
    else:
        original = observed.get("original", {})
        baseline_file = source / "baseline.json"
        require(baseline_file.is_file(), "native removal baseline absent")
        baseline = json.loads(baseline_file.read_text(encoding="utf-8"))
        require(original.get("operation") and original.get("unknown") is False and
                observed.get("audit", {}).get("available") is True and
                original["operation"] not in baseline.get("operations", []) and
                baseline.get("pid") == capture.get("bridge", {}).get("pid"),
                "complete native removal original was not captured")
    print(json.dumps({"result": "VERIFIED_NATIVE_SNAPSHOT", "kind": args.kind,
                      "path": str(source), "fileCount": len(actual)}))


def baseline(args):
    require(args.kind == "removal-parcel", "baseline is only defined for native removal parcels")
    current = session(args.session)
    state = bridge(args.session, "/auditstate", domain="removalParcelOriginals")["domains"]["removalParcelOriginals"]
    require(state.get("available") is True and state.get("installed") is True,
            "native removal producer audit unavailable")
    destination = OUT / args.name
    require(not destination.exists(), "fixture name already exists; choose a new name")
    destination.mkdir(parents=True)
    value = {"schema": 1, "kind": args.kind, "pid": current["pid"],
             "startedUtc": current["startedUtc"],
             "operations": sorted(row["operation"] for row in state.get("originals", []) if row.get("operation"))}
    (destination / "baseline.json").write_text(json.dumps(value, indent=2), encoding="utf-8")
    print(json.dumps({"result": "REMOVAL_BASELINE_CAPTURED", "path": str(destination / "baseline.json"),
                      "existingOriginals": len(value["operations"])}))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("command", choices=("baseline", "capture", "snapshot", "verify"))
    parser.add_argument("kind", choices=KINDS)
    parser.add_argument("name", help="new unique fixture folder name")
    parser.add_argument("--session", default=str(ROOT / "Tools/GameBridge/out/session.json"))
    parser.add_argument("--save-dir", help="absolute path to disposable CodexTest save")
    parser.add_argument("--snapshot-root", default=str(SNAPSHOTS), help="short writable path for native save snapshots")
    parser.add_argument("--x", type=int, default=14)
    parser.add_argument("--y", type=int, default=45)
    parser.add_argument("--z", type=int, default=995)
    args = parser.parse_args()
    require(re.fullmatch(r"[a-z0-9][a-z0-9_-]{0,63}", args.name) is not None, "fixture name must be simple and unique")
    {"baseline": baseline, "capture": save_evidence, "snapshot": snapshot,
     "verify": verify}[args.command](args)


if __name__ == "__main__":
    try:
        main()
    except Exception as error:
        print(json.dumps({"result": "FAIL", "error": str(error)}), file=sys.stderr)
        raise SystemExit(1)
