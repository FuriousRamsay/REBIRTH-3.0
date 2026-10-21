"""Prepared genuine-package regression in CodexTest; never launches or grants items.

Usage: python test_fetch_packages_live.py prepared_fetch_plan.json
See _Documentation/TraderJobs/FETCH_BRIDGE_QUALIFICATION_20261013.md.
The operator prepares exact real UI selectors for each stage. No completion cheats,
inventory repair, generated quest items, save writes or cleanup are performed.
"""
import collections
import datetime
import json
from pathlib import Path
import sys
import time

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "TraderVoices"))
from test_live import call as native_call, root, session as initial_session

active_report = None
active_destination = None
scope = {key: initial_session[key] for key in ("pid", "startedUtc", "port")}


def save_report():
    if active_report is not None and active_destination is not None:
        active_destination.write_text(json.dumps(active_report, indent=2), encoding="utf-8")


def call(endpoint, method="GET", **args):
    current = json.loads((root / "Tools/GameBridge/out/session.json").read_text(encoding="utf-8-sig"))
    require(all(current[key] == value for key, value in scope.items()), "bridge session drift; no replacement-session input")
    entry = {"endpoint": endpoint, "method": method, "args": args}
    if active_report is not None:
        active_report.setdefault("requests", []).append(entry)
    save_report()
    try:
        entry["reply"] = native_call(endpoint, method, **args)
        return entry["reply"]
    except Exception as error:
        entry["failure"] = type(error).__name__ + ": " + str(error)
        raise
    finally:
        save_report()


def require(condition, message):
    if not condition:
        raise AssertionError(message)


def observe(storage, allow_ui=False):
    reply = call("/console", "POST", cmd="rbfetchtestobserve " + " ".join(map(str, storage)))
    require(not reply.get("hadError"), "fetch observation command reported error")
    lines = [line for entry in reply["results"] for line in entry["output"]
             if isinstance(line, str) and line.startswith("{")]
    require(len(lines) == 1, "exactly one fetch JSON observation required")
    data = json.loads(lines[0])
    require(data.get("schema") == 1 and data.get("observationOnly") is True
            and data.get("gameName") == "CodexTest", "unsupported observation/save")
    if active_report is not None and active_report.get("baseline"):
        require(data["playerId"] == active_report["baseline"]["playerId"], "native player identity changed during batch")
    require(data.get("cursor") is None, "cursor still holds an item; recover through real UI before retry")
    require(allow_ui or data["modalOpen"] is False and data["inputActive"] is False,
            "close interactive UI before persisted inventory/storage assertions")
    require(data["storage"]["capacity"] > 0, "storage must have usable capacity")
    return data


def inventory(snapshot):
    return snapshot["backpack"] + snapshot["toolbelt"]


def physical_items(cells):
    return sorted((c["location"], c["slot"], c["count"], c["payload"]) for c in cells)


def owner(snapshot):
    witness = snapshot.get("ownerInventory")
    require(isinstance(witness, dict) and witness.get("schema") == 1
            and all(witness.get(key) for key in ("bagPayload", "beltPayload", "cursorPayload")),
            "full actual native owner bag/belt/cursor byte witnesses required")
    return witness


def preserve_other_fetch(before, after, changing_codes=()):
    def rows(snapshot):
        return sorted((q["questCode"], q["questId"], q["objectiveIndex"], json.dumps(q, sort_keys=True))
                      for q in snapshot["quests"] if q["questCode"] not in changing_codes)
    require(rows(before) == rows(after), "unrelated native fetch objective/quest state changed")
    def journal(snapshot):
        require(isinstance(snapshot.get("allQuestJournal"), list), "all native quest identities/states required")
        return sorted((q["id"], q["questCode"], q.get("uniqueId") or "", json.dumps(q, sort_keys=True))
                      for q in snapshot["allQuestJournal"] if q["questCode"] not in changing_codes)
    require(journal(before) == journal(after), "unrelated quest identity/phase/objective state changed")


def totals(cells):
    result = collections.Counter()
    for cell in cells:
        require(cell["count"] > 0 and cell["payload"], "missing exact occupied-cell evidence")
        result[cell["payload"]] += cell["count"]
    return result


def bound(cell, objective):
    return (cell["type"] == objective["expectedType"] and cell["seed"] == objective["expectedSeed"]
            and (cell["meta"] == objective["questCode"]
                 or cell["questCodeMetadata"] == objective["questCode"]))


def quest(snapshot, code):
    matches = [q for q in snapshot["quests"] if q["questCode"] == code]
    require(len(matches) == 1, "exactly one fetch objective required for quest code " + str(code))
    require(matches[0]["expectedReady"], "expected item uninitialized; observation never initializes it")
    return matches[0]


def source_proof(action, storage, evidence):
    witness = action.get("sourceWitness")
    if witness is None:
        return
    require(isinstance(witness, dict) and set(witness) == {"location", "slot", "payload", "count"}, "exact physical source witness required")
    observation = observe(storage, allow_ui=True)
    source = [cell for cell in inventory(observation) if cell["location"] == witness["location"] and cell["slot"] == witness["slot"]]
    require(len(source) == 1 and all(source[0][key] == value for key, value in witness.items()), "native source changed before real input")
    reply = call("/console", "POST", cmd="rbcamperregprofile")
    require(not reply.get("hadError"), "visible native source profile refused")
    lines = [line for entry in reply["results"] for line in entry["output"] if isinstance(line, str) and line.startswith("{")]
    require(len(lines) == 1, "one visible source profile required")
    path = action["args"].get("from.path") if action["endpoint"] == "/ui/drag" else action["args"].get("path")
    require(path, "witnessed input requires exact source path")
    candidates = [row for row in json.loads(lines[0])["visibleSlots"] if row["path"] == path and row.get("source")]
    require(len(candidates) == 1 and all(candidates[0]["source"].get(key) == value for key, value in witness.items()), "UI path does not bind witnessed native physical source")
    target = action.get("targetWitness")
    if target is not None:
        require(action["endpoint"] == "/ui/drag" and isinstance(target, dict) and set(target) == set(witness), "exact native drag target witness required")
        require(target != witness, "distinct native package stacking target required")
        target_cells = [c for c in inventory(observation) if c["location"] == target["location"] and c["slot"] == target["slot"]]
        require(len(target_cells) == 1 and all(target_cells[0][key] == value for key, value in target.items()), "native target package changed before stacking attempt")
        target_rows = [row for row in json.loads(lines[0])["visibleSlots"] if row["path"] == action["args"].get("to.path") and row.get("source")]
        require(len(target_rows) == 1 and all(target_rows[0]["source"].get(key) == value for key, value in target.items()), "stacking target path does not bind genuine other package")
    evidence.append({"sourceWitness": witness, "path": path, "observation": observation, "nativeUiSource": candidates[0]})


def execute_actions(actions, evidence, storage):
    require(isinstance(actions, list) and actions, "every qualification stage requires real input actions")
    allowed = {"/activate", "/lookat", "/key", "/ui/click", "/ui/drag", "/ui/type", "/ui/scroll", "/walkto"}
    for action in actions:
        require(set(action) <= {"endpoint", "args", "wait", "sourceWitness", "targetWitness"}, "unknown action fields")
        require("targetWitness" not in action or "sourceWitness" in action, "target witness needs source witness")
        endpoint = action["endpoint"]
        require(endpoint in allowed, "only explicit real UI/player input routes allowed")
        args = action.get("args", {})
        if endpoint == "/key":
            require(args.get("name") in {"Escape", "Tab", "E", "W"}, "unsupported scenario key")
        wait = float(action.get("wait", 0.3))
        require(0 <= wait <= 5, "action wait must be 0..5 seconds")
        attempt = {"input": action}
        evidence.append(attempt)
        source_proof(action, storage, evidence)
        try:
            attempt["reply"] = call(endpoint, "POST", **args)
        except Exception as error:
            attempt["failure"] = type(error).__name__ + ": " + str(error)
            raise
        time.sleep(wait)


def qualify(plan, report):
    require(plan.get("confirm") == "CodexTest", "explicit disposable-save confirmation required")
    storage = plan["storage"]
    require(len(storage) == 3 and all(type(x) is int for x in storage), "integer storage coordinates required")
    codes = plan["questCodes"]
    require(len(codes) == 2 and all(type(x) is int and x != 0 for x in codes)
            and codes[0] != codes[1], "two distinct genuine quest codes required")
    require(call("/ping")["state"] == "ingame", "already loaded game required")
    guard = call("/guard")
    require(guard.get("enabled") is True, "instincts must remain enabled")
    report["surroundings"] = call("/surroundings")
    start = call("/ping")["lastLogSeq"]
    before = observe(storage)
    report["baseline"] = before
    owner(before)
    report["questPreservationScope"] = "All journal identity/phase/objective states; arbitrary objective/reward native serialization not qualified."
    require(call("/log", since=0, level="error", limit=200)["count"] == 0, "existing process errors refuse qualification")
    require(not any(c["questPackage"] for c in before["storage"]["slots"]),
            "storage must initially contain no quest packages; recover legacy packages manually")
    objectives = [quest(before, code) for code in codes]
    originals = []
    for objective in objectives:
        require(objective["state"] == "ReadyForTurnIn" and objective["currentValue"] == 3,
                "both genuine fetch jobs must already be ready for turn-in")
        cells = [c for c in inventory(before) if bound(c, objective)]
        require(len(cells) == 1 and cells[0]["count"] == 1 and cells[0]["questPackage"],
                "exactly one unmerged native package required for each quest")
        originals.append(cells[0])
        require(cells[0]["canDeposit"] is False and cells[0]["canPlaceInContainer"] is False,
                "native and QuickStack package deposit policies must refuse")
    report["packageSourceWitnesses"] = [{key: cell[key] for key in ("location", "slot", "payload", "count")} for cell in originals]
    for stage in ("stackAttempt", "storageShiftAttempt", "storageDragAttempt"):
        expected = report["packageSourceWitnesses"]
        require(any(action.get("sourceWitness") in expected for action in plan[stage]),
                stage + " must prove an exact genuine package physical source before input")
    require(any(a["endpoint"] == "/ui/drag" and a.get("sourceWitness") in report["packageSourceWitnesses"]
                and a.get("targetWitness") in report["packageSourceWitnesses"] and a["sourceWitness"] != a["targetWitness"]
                for a in plan["stackAttempt"]), "real package-to-package stack drag with both exact native witnesses required")
    direct = call("/console", "POST", cmd="rbfetchteststackguards")
    report["nativeStackQualificationReply"] = direct
    require(not direct.get("hadError"), "actual native stack qualification refused")
    definitions = [line for entry in direct["results"] for line in entry["output"] if isinstance(line, str) and line.startswith("{")]
    require(len(definitions) == 1, "one detached actual-native stack result required")
    report["nativeStackQualification"] = json.loads(definitions[0])
    require(report["nativeStackQualification"].get("qualified") is True and report["nativeStackQualification"].get("passed") is True,
            "genuine-package direct native guard qualification did not pass")
    pairs = before["stackPairs"]
    indices = {c["packageIndex"] for c in originals}
    observed_pairs = [p for p in pairs if p["a"] in indices and p["b"] in indices]
    require(len(observed_pairs) == 3 and all(p["canStack"] is False
            and p["canStackPartial"] is False for p in observed_pairs),
            "native full/partial stack comparisons must refuse both package identities")
    require(all(c["location"] == "backpack" for c in originals),
            "both package sources must be in the backpack for QuickStack qualification")
    require(all(c["quickStackSourceLocked"] is False for c in originals),
            "package source cells must be unlocked; protected-source refusal cannot qualify deposit policy")

    # Demonstrate a real destination accepts an ordinary item via the same route.
    # Native deposit booleans or empty/no-destination attempts alone cannot qualify it.
    control_name = plan["controlItem"]
    require(isinstance(control_name, str) and control_name, "ordinary control item required")
    quick_control = plan["quickStackControl"]
    require(quick_control.get("endpoint") == "/ui/click" and quick_control.get("args"),
            "explicit observed QuickStack UI activation selector required")
    require(any(a.get("endpoint") == quick_control["endpoint"] and a.get("args") == quick_control["args"]
                for a in plan["controlDeposit"])
            and any(a.get("endpoint") == quick_control["endpoint"] and a.get("args") == quick_control["args"]
                    for a in plan["quickStackAttempt"]),
            "control deposit and forbidden attempt must activate the same observed QuickStack UI control")
    evidence = report.setdefault("actions", [])
    execute_actions(plan["controlDeposit"], evidence, storage)
    control = observe(storage)
    report["controlDeposit"] = control
    old_control = sum(c["count"] for c in before["storage"]["slots"] if c["name"] == control_name)
    new_control = sum(c["count"] for c in control["storage"]["slots"] if c["name"] == control_name)
    require(new_control > old_control, "eligible ordinary-item transfer was not witnessed")
    require(totals(inventory(before) + before["storage"]["slots"])
            == totals(inventory(control) + control["storage"]["slots"]), "control transfer altered item payload/count")
    require(all(sum(c["count"] for c in inventory(control) if bound(c, obj)) == 1
                for obj in objectives), "control transfer moved a package")
    preserve_other_fetch(before, control)
    target = control["storage"]
    require(target["canQuickStackUse"] is True and target["quickStackMode"] == "Full"
            and target["distance"] <= target["quickStackRadius"] and target["emptyUnlocked"] > 0,
            "QuickStack destination must be usable, nearby, with unlocked empty capacity in Full mode")
    require(all(p["category"] != "None" and p["acceptsCategory"] is True
                for p in target["packageRouting"]),
            "destination must accept actual package categories; an unassigned/no-destination attempt cannot qualify refusal")
    control_categories = {c["quickStackCategory"] for c in control["storage"]["slots"]
                          if c["name"] == control_name}
    require(all(p["category"] in control_categories for p in target["packageRouting"]),
            "ordinary transferred control must demonstrate the same accepted categories as the packages")
    require(all(c["quickStackSourceLocked"] is True for c in control["backpack"] if not c["questPackage"]),
            "protect unrelated backpack cells after control deposit so QuickStack does not legitimately move them")

    for stage in ("stackAttempt", "storageShiftAttempt", "storageDragAttempt", "quickStackAttempt"):
        execute_actions(plan[stage], evidence, storage)
        after = observe(storage)
        report[stage] = after
        require(physical_items(inventory(after)) == physical_items(inventory(control)), stage + " changed native physical source slots/payload/count")
        require(physical_items(after["storage"]["slots"]) == physical_items(control["storage"]["slots"]), stage + " changed native storage slots/payload/count")
        preserve_other_fetch(control, after)
        require(owner(after) == owner(control), stage + " changed full native owner bag/belt/cursor bytes or locks")
        require(all(sum(c["count"] for c in inventory(after) if bound(c, obj)) == 1
                    and all(c["count"] == 1 for c in inventory(after) if bound(c, obj))
                    for obj in objectives), stage + " lost or merged a package")
        require(all(c["location"] == "backpack" and c["quickStackSourceLocked"] is False
                    for c in inventory(after) if any(bound(c, obj) for obj in objectives)),
                stage + " changed package QuickStack source eligibility")

    previous = report["quickStackAttempt"]
    remaining = totals(c for c in inventory(previous) if c["questPackage"])
    for index, original in enumerate(originals):
        stage = "firstTurnIn" if index == 0 else "secondTurnIn"
        execute_actions(plan[stage], evidence, storage)
        after = observe(storage)
        report[stage] = after
        require(quest(after, codes[index])["state"] == "Completed", stage + " did not complete the selected quest")
        preserve_other_fetch(previous, after, (codes[index],))
        prior_owner, next_owner = owner(previous), owner(after)
        require(all(prior_owner[key] == next_owner[key] for key in ("bagLocks", "beltLocks", "cursorPayload")),
                stage + " changed native owner locks/cursor")
        remaining[original["payload"]] -= 1
        remaining = +remaining
        require(totals(c for c in inventory(after) if c["questPackage"]) == remaining,
                stage + " failed exact quest-package debit or changed another package")
        require(physical_items(c for c in inventory(after) if c["questPackage"])
                == physical_items(c for c in inventory(previous) if c["questPackage"] and not bound(c, objectives[index])),
                stage + " moved or rewrote another quest package")
        require(physical_items(after["storage"]["slots"]) == physical_items(previous["storage"]["slots"]),
                stage + " changed storage")
        # Rewards may legitimately add ordinary items; existing unrelated payloads
        # must still be present. This does not claim exact reward correctness.
        old = totals(c for c in inventory(previous) if not c["questPackage"])
        new = totals(c for c in inventory(after) if not c["questPackage"])
        require(all(new[key] >= amount for key, amount in old.items()), stage + " removed unrelated inventory")
        previous = after
    report["errors"] = call("/log", since=start, level="error")
    require(report["errors"]["count"] == 0, "new native errors")
    report["passed"] = True


if __name__ == "__main__":
    require(len(sys.argv) == 2, "usage: test_fetch_packages_live.py prepared_fetch_plan.json")
    plan = json.loads(Path(sys.argv[1]).read_text(encoding="utf-8-sig"))
    report = {"passed": False, "plan": plan, "scope": "real input; genuine native packages; local-player custody observations only, no remote-server custody qualification"}
    stamp = datetime.datetime.now().strftime("%Y%m%d-%H%M%S-%f")
    destination = root / "Tools/GameBridge/out/tests" / (stamp + "_fetch_packages.json")
    destination.parent.mkdir(parents=True, exist_ok=True)
    active_report, active_destination = report, destination
    report["session"] = scope
    save_report()
    try:
        qualify(plan, report)
        print("PASS genuine fetch package stacking/storage refusal and two exact turn-ins")
    except Exception as error:
        report["failure"] = type(error).__name__ + ": " + str(error)
        raise
    finally:
        destination.write_text(json.dumps(report, indent=2), encoding="utf-8")
        print("Evidence:", destination)
