"""Explicit reviewed changed-file overlay only. MANIFEST.json NEW_E_SHARED_DIR.
No discovery, deletion, source writes, archive, build, or game operations.
"""
import argparse
import hashlib
import json
from pathlib import Path, PurePosixPath
import re

ROOT = Path(__file__).resolve().parents[2]
DELIVERY_ROOT = Path("E:/_Haven/shared")
ALLOWED = {"Scripts", "Config", "Tools", "_Documentation", "ItemIcons", "Resources", "Prefabs", "UIAtlases", "Localization"}
ROOT_FILES = {"RebirthUtils.dll", "RebirthUtils.csproj", "ModInfo.xml", "Localization.txt"}
FORBIDDEN = {".git", ".agents", ".codex", ".claude", "bin", "obj", "out", "history", "archive", "archives", "saves", "save", "secrets", "credentials", "__pycache__", "node_modules"}


def need(condition, reason):
    if not condition: raise ValueError(reason)


def digest(data): return hashlib.sha256(data).hexdigest()


def file_path(relative, source_fence=False):
    need(isinstance(relative, str) and "\\" not in relative and ":" not in relative, "canonical relative forward-slash path required")
    p = PurePosixPath(relative)
    need(not p.is_absolute() and str(p) == relative and all(x not in ("", ".", "..") for x in p.parts), "unsafe/noncanonical manifest path")
    need(not any(x.lower() in FORBIDDEN for x in p.parts), "history/save/foreign output path refused")
    need(p.parts[0] in ALLOWED or relative in ROOT_FILES or (source_fence and len(p.parts) == 1 and p.suffix == ".cs"), "foreign delivery root refused")
    name = p.name.lower()
    need(source_fence or (not name.startswith(".env") and not any(t in name for t in ("session.json", "token", "password", "secret", "credential"))), "secret/session filename refused")
    need(p.suffix.lower() not in {".zip", ".7z", ".rar", ".log", ".pdb", ".exe", ".bak", ".pem", ".key", ".map", ".ttw", ".ttp", ".region"}, "history/runtime/binary artifact refused")
    need(p.suffix.lower() != ".dll" or relative == "RebirthUtils.dll", "foreign DLL refused")
    actual = (ROOT / relative).resolve(strict=True)
    need(actual.is_relative_to(ROOT.resolve()) and actual.is_file(), "source escaped workspace or is not a file")
    return actual


def rows_verified(rows, collect=False, source_fence=False):
    need(isinstance(rows, list) and rows, "nonempty explicit reviewed files required")
    seen = set(); images = {}; normalized = []
    for row in rows:
        relative = row["path"]; key = relative.casefold()
        need(key not in seen, "duplicate case-insensitive path"); seen.add(key)
        need(re.fullmatch(r"[0-9a-f]{64}", row["sha256"]) is not None, "explicit lowercase SHA256 required")
        data = file_path(relative, source_fence).read_bytes()
        need(digest(data) == row["sha256"], "reviewed source changed: " + relative)
        if "bytes" in row: need(row["bytes"] == len(data), "reviewed length differs: " + relative)
        normalized.append({"path": relative, "bytes": len(data), "sha256": row["sha256"]})
        if collect: images[relative] = data
    return sorted(normalized, key=lambda r: r["path"]), images


def fence(plan):
    rows, _ = rows_verified(plan["sourceFence"], source_fence=True)
    # Conservative full runtime source inventory catches additions/removals too,
    # including conditionally excluded source. This never discovers delivery files.
    required = {p.relative_to(ROOT).as_posix() for p in (ROOT / "Scripts").rglob("*.cs")}
    required.update(p.relative_to(ROOT).as_posix() for p in ROOT.glob("*.cs"))
    required.add("RebirthUtils.csproj")
    need(required <= {r["path"] for r in rows}, "full current runtime source/project inventory missing from candidate fence")
    # Parent records this exact digest against the accepted build candidate.
    canonical = json.dumps(rows, sort_keys=True, separators=(",", ":")).encode("utf-8")
    source_hash = digest(canonical)
    need(source_hash == plan["sourceFenceSha256"], "compiled source fence digest differs")
    candidate = plan["dllCandidate"]
    need(candidate["sourceFenceSha256"] == source_hash, "candidate was not reviewed against this exact source fence")
    path = Path(candidate["path"]).resolve(strict=True)
    need(path.is_relative_to(DELIVERY_ROOT.resolve()) and path.name == "RebirthUtils.dll", "explicit E shared DLL candidate required")
    need(digest(path.read_bytes()) == candidate["sha256"], "DLL candidate changed")
    need(digest((ROOT / "RebirthUtils.dll").read_bytes()) == candidate["sha256"], "current DLL does not match reviewed candidate")
    return rows


def main():
    p = argparse.ArgumentParser(__doc__); p.add_argument("manifest"); p.add_argument("destination"); args = p.parse_args()
    raw_manifest = Path(args.manifest).read_bytes(); plan = json.loads(raw_manifest.decode("utf-8-sig"))
    need(plan.get("schema") == 1 and plan.get("reviewed") is True and plan.get("scope") == "changed-files-overlay", "explicit parent-reviewed overlay manifest required")
    need(not plan.get("deleted"), "deletion manifests are outside this packager")
    target = Path(args.destination).resolve()
    need(target.is_relative_to(DELIVERY_ROOT.resolve()) and target != DELIVERY_ROOT.resolve() and not target.exists(), "new E shared delivery directory required")
    fence_rows = fence(plan); files, images = rows_verified(plan["files"], True)
    need(any(r["path"] == "RebirthUtils.dll" and r["sha256"] == plan["dllCandidate"]["sha256"] for r in files), "reviewed current DLL must be in delivery")
    fenced = {r["path"] for r in fence_rows}
    need("RebirthUtils.csproj" in fenced and all(r["path"] in fenced for r in files if r["path"].startswith("Scripts/") and r["path"].endswith(".cs")), "production source/project missing from build fence")
    need("DELIVERY_SHA256.json" not in images and "REVIEWED_MANIFEST.json" not in images, "reserved delivery manifest collision")
    target.mkdir(parents=True, exist_ok=False)
    for relative, data in images.items():
        output = target / relative; output.parent.mkdir(parents=True, exist_ok=True)
        with output.open("xb") as f: f.write(data)
        need(digest(output.read_bytes()) == digest(data), "delivery copy hash mismatch")
    # Recheck all review/build inputs after copies; uncertainty never produces a
    # success manifest. Partial directory is retained for parent review, no delete.
    fence(plan); rows_verified(plan["files"])
    with (target / "REVIEWED_MANIFEST.json").open("xb") as f: f.write(raw_manifest)
    result = {"schema": 1, "scope": plan["scope"], "fullProject": False, "verified": True,
              "reviewedManifestSha256": digest(raw_manifest), "sourceFenceSha256": plan["sourceFenceSha256"], "files": files}
    with (target / "DELIVERY_SHA256.json").open("x", encoding="utf-8") as f: json.dump(result, f, indent=2)
    print(json.dumps({"directory": str(target), "verifiedFiles": len(files), "sourceFenceSha256": plan["sourceFenceSha256"]}))


if __name__ == "__main__": main()
