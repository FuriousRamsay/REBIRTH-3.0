"""Fail-closed real-input camper regression; parent owns launch/save/quit/reload.

prepare PLAN.json REPORT.json; verify-reload REPORT.json
Plan selectors must be observed in the current disposable session. Never inject queues.
"""
import argparse
import collections
import json
import math
import os
import re
from pathlib import Path
import time
import urllib.error
import urllib.parse
import urllib.request

ROOT = Path(__file__).resolve().parents[2]
SESSION = ROOT / "Tools/GameBridge/out/session.json"
ALLOWED = {"/lookat", "/press", "/release", "/activate", "/key", "/ui/click", "/ui/drag", "/ui/type", "/ui/hover", "/ui/scroll"}
PREPARATORY = {"campfire", "workbench", "toolCookingPot", "resourceWood", "foodCropPotato",
               "resourceBulletTip", "resourceGunPowder", "resourceBulletCasing",
               "FuriousRamsayHammerPliers", "FuriousRamsayScrewdriver"}
AREAS = ("input", "tools", "fuel", "output")


def need(value, message):
    if not value:
        raise AssertionError(message)


def allows_recipe_area(area, witness):
    need(isinstance(witness, dict) and isinstance(witness.get("rawAreas"), str)
         and witness["rawAreas"] and isinstance(witness.get("normalizedAreas"), list), "actual native station area witness required")
    normalized = ["" if alias.strip() == "player" else alias.strip() for alias in witness["rawAreas"].split(",")]
    need(normalized == witness["normalizedAreas"], "native alias witness differs from production admission semantics")
    need(area is None or isinstance(area, str), "native recipe area malformed")
    return any(alias.casefold() == (area or "").casefold() for alias in normalized)


def counts(cells):
    result = collections.Counter()
    for cell in cells:
        if cell.get("count", 0):
            need(cell.get("name") and type(cell["count"]) is int and cell["count"] > 0, "invalid inventory cell")
            result[cell["name"]] += cell["count"]
    return result


def stations(snapshot):
    camper = snapshot["camper"]
    need(camper.get("inventorySchema") == 1 and camper.get("cursorEmpty") is True, "missing inventory schema or occupied cursor")
    need(camper["pending"] is False and camper["capacity"] == camper["cargoSlots"], "pending transfer/cargo capacity mismatch")
    found = {row["slot"]: row for row in camper["stations"]}
    for row in found.values():
        need(row.get("id") and type(row.get("queueCapacity")) is int and type(row.get("outputCapacity")) is int, "missing station identity/capacity")
        for area in AREAS:
            need(isinstance(row.get(area), list), "missing station cells")
            for cell in row[area]:
                need(cell["count"] == 0 or cell.get("nativeHash"), "missing native stack fingerprint")
        need(isinstance(row["queue"], list), "missing queue observation")
    return found


def total_items(snapshot):
    result = counts(snapshot["state"]["backpack"] + snapshot["state"]["toolbelt"])
    result.update(counts(snapshot["camper"]["cargo"]))
    for row in snapshot["camper"]["stations"]:
        for area in AREAS:
            result.update(counts(row[area]))
    return result


def original_cargo(before, after):
    for field in ("vehicleIdentity", "storage", "storageCount", "cargo", "capacity", "cargoSlots"):
        need(before["camper"][field] == after["camper"][field], "original cargo/storage changed: " + field)
    old, new = stations(before), stations(after)
    for slot in (0, 1):
        need(slot in new and old[slot]["id"] == new[slot]["id"] and old[slot]["block"] == new[slot]["block"], "original installation changed")


def exact_station(before, after, slots=(0, 1)):
    original_cargo(before, after)
    old, new = stations(before), stations(after)
    for slot in slots:
        for field in (*AREAS, "queue", "burning", "fuelSeconds", "queueCapacity", "outputCapacity"):
            need(old[slot][field] == new[slot][field], "original station changed: " + str(slot) + "." + field)


class Runner:
    def __init__(self, report, path, session_path=None, base_url=None):
        self.report, self.path = report, path
        self.session_path = Path(session_path) if session_path is not None else Path(os.environ.get("REBIRTH_BRIDGE_SESSION", str(SESSION)))
        self.session = json.loads(self.session_path.read_text(encoding="utf-8-sig"))
        self.base_url = base_url or "http://127.0.0.1:" + str(self.session["port"])
        self.scope = {key: self.session[key] for key in ("pid", "startedUtc", "port")}
        self.held = set()
        self.owned_windows = set()
        self.start = 0

    def save(self):
        self.path.parent.mkdir(parents=True, exist_ok=True)
        self.path.write_text(json.dumps(self.report, indent=2), encoding="utf-8")

    def call(self, endpoint, method="GET", **args):
        current = json.loads(self.session_path.read_text(encoding="utf-8-sig"))
        need(all(current[key] == self.scope[key] for key in self.scope), "bridge session changed during phase")
        need(current.get("token") == self.session.get("token"), "bridge authentication session changed during phase")
        entry = {"endpoint": endpoint, "method": method, "args": args}
        self.report.setdefault("requests", []).append(entry)
        self.save()
        request = urllib.request.Request(self.base_url + endpoint + "?" + urllib.parse.urlencode(args),
                                         headers={"X-Bridge-Token": self.session["token"], **({"X-Bridge-Run": os.environ["REBIRTH_BRIDGE_RUN_TOKEN"]} if os.environ.get("REBIRTH_BRIDGE_RUN_TOKEN") else {})}, method=method,
                                         data=b"" if method == "POST" else None)
        try:
            with urllib.request.urlopen(request, timeout=60) as response:
                value = json.load(response)
            entry["reply"] = value
            need(value.get("ok") is True, "bridge action/observation refused")
            return value
        except Exception as error:
            entry["failure"] = type(error).__name__ + ": " + str(error)
            if isinstance(error, urllib.error.HTTPError):
                entry["httpStatus"] = error.code
                entry["errorBody"] = error.read().decode("utf-8", errors="replace")
            raise
        finally:
            self.save()

    def errors(self):
        errors = self.call("/log", since=self.start, level="error", limit=200)
        need(errors["count"] == 0, "native error/exception logs present")

    def snapshot(self, entity):
        deadline = time.monotonic() + 35
        while True:
            camper = self.call("/camper", entity=entity)
            if camper.get("pending") is False and camper.get("cursorEmpty") is True:
                break
            self.errors()
            need(time.monotonic() < deadline, "transfer/cursor custody did not settle within35s")
            time.sleep(0.25)
        value = {"session": self.scope, "state": self.call("/state", sections="player,world,inventory"), "camper": camper}
        need(value["state"]["world"]["gameName"] == "CodexTest", "real save refused")
        stations(value)
        self.errors()
        return value

    def preflight(self, entity, nearby=True):
        ping = self.call("/ping")
        need(ping["state"] == "ingame" and ping["pid"] == self.scope["pid"], "current in-game process required")
        guard = self.call("/guard")
        need(guard.get("enabled") is True, "guard must remain on")
        self.report["arrival"] = self.call("/surroundings")
        nearby_rows = self.call("/entities", radius=8, limit=100)["entities"]
        if nearby:
            need(any(row["id"] == entity and math.isfinite(row["distance"]) and row["distance"] <= 8 for row in nearby_rows), "exact nearby camper required")
        self.errors()  # Since0 intentionally refuses existing errors in this process.
        self.start = ping["lastLogSeq"]
        return self.snapshot(entity)

    def guard_idle(self):
        deadline, stable = time.monotonic() + 30, None
        while True:
            guard = self.call("/guard")
            need(guard.get("enabled") is True, "guard disabled during phase")
            if guard.get("busy") is False:
                stable = time.monotonic() if stable is None else stable
                if time.monotonic() - stable >= 1:
                    return
            else:
                stable = None
            need(time.monotonic() < deadline, "guard did not settle before real UI input")
            time.sleep(0.25)

    def actions(self, actions, expected_window=None):
        need(isinstance(actions, list) and actions, "nonempty observed real-input phase required")
        self.guard_idle()
        for action in actions:
            need(action["endpoint"] in ALLOWED, "non-input route refused")
            args = action.get("args", {})
            if action.get("sourceWitness"):
                self.source_proof([action["sourceWitness"]])
                visible = self.source_observation()["visibleSlots"]
                source_path = args.get("from.path") or args.get("path")
                need(any(row["path"] == source_path and row.get("source") == action["sourceWitness"] for row in visible),
                     "visible source path no longer maps to the prepared physical cell")
            if action["endpoint"] == "/ui/drag":
                need(args.get("strict") == 1, "every regression drag must be strict")
            if action["endpoint"] == "/press" and args.get("hold"):
                self.held.add(args["action"])
            reply = self.call(action["endpoint"], "POST", **args)
            if action["endpoint"] == "/release":
                self.held.discard(args.get("action"))
            wait = float(action.get("wait", 0.3))
            need(0 <= wait <= 5, "bounded action wait required")
            time.sleep(wait)
            if action.get("expectedRecipe"):
                wanted = action["expectedRecipe"]
                matching = [row for row in self.source_observation()["selectedRecipes"]
                            if row["path"].startswith(wanted["window"] + "/")]
                need(len(matching) == 1 and matching[0]["recipe"] == wanted["recipe"] and matching[0]["tier"] == wanted["tier"],
                     "actual selected recipe/tier changed before ingredient payment")
            self.errors()
            if expected_window == "rebirthCamperStationManagement":
                need(not self.call("/ui/tree", window="workstation_workbench").get("nodes"),
                     "fresh Workbench must never be opened before initial capacity observation")
        if expected_window:
            deadline = time.monotonic() + 8
            while True:
                rows = self.call("/ui/tree", window=expected_window)
                if rows.get("nodes"):
                    self.owned_windows.add(expected_window)
                    return
                need(time.monotonic() < deadline, "expected window not opened: " + expected_window)
                time.sleep(0.25)

    def closed(self, entity):
        value = self.snapshot(entity)
        need(value["camper"]["lockOwner"] == -1 and value["camper"]["cursorEmpty"] is True, "close did not retire native lease/cursor")
        return value

    def recipe(self, rule):
        name, batches, tier = rule["recipe"], rule["batches"], rule.get("tier", 0)
        need(isinstance(name, str) and re.fullmatch(r"[A-Za-z0-9_][A-Za-z0-9_.-]{0,127}", name)
             and type(batches) is int and 1 <= batches <= 600 and type(tier) is int and 0 <= tier <= 6,
             "exact canonical native recipe ID and bounded batches/tier required")
        reply = self.call("/console", "POST", cmd=f"rbcamperregrecipe {name} {batches} {tier}")
        need(not reply.get("hadError"), "recipe observer refused")
        lines = [line for row in reply["results"] for line in row["output"] if isinstance(line, str) and line.startswith("{")]
        need(len(lines) == 1, "one native recipe definition required")
        definition = json.loads(lines[0])
        need(definition["schema"] == 1 and definition["observationOnly"] is True and definition["recipe"] == name,
             "invalid native recipe definition")
        need(definition["batches"] == batches and definition["tier"] == tier, "native definition quantity/tier differs from declared job")
        need(definition["outputPerBatch"] > 0 and definition["expectedOutput"] == definition["outputPerBatch"] * batches, "invalid independently calculated output")
        need(definition["ingredients"] and all(isinstance(row["name"], str)
             and re.fullmatch(r"[A-Za-z0-9_][A-Za-z0-9_.-]{0,127}", row["name"])
             and type(row["payment"]) is int and row["payment"] > 0 for row in definition["ingredients"]),
             "complete positive native ingredient payment required")
        return definition

    def source_observation(self):
        reply = self.call("/console", "POST", cmd="rbcamperregprofile")
        need(reply.get("hadError") is False, "physical source observer refused")
        lines = [line for row in reply["results"] for line in row["output"] if isinstance(line, str) and line.startswith("{")]
        need(len(lines) == 1, "one physical source observation required")
        observation = json.loads(lines[0])
        need(observation.get("schema") == 1 and observation.get("observationOnly") is True, "unsupported physical source observation")
        return observation

    def source_proof(self, witnesses):
        actual = self.source_observation()["sources"]
        for witness in witnesses:
            matches = [source for source in actual if source["location"] == witness["location"] and source["slot"] == witness["slot"]]
            need(len(matches) == 1 and matches[0] == witness, "physical prepared source changed before input")

    def cleanup(self, entity):
        # Never ReleaseAll/stop, rewrite a cursor, or close an unowned replacement UI.
        for action in list(self.held):
            try:
                self.call("/release", "POST", action=action)
                self.held.discard(action)
            except Exception:
                pass
        # The HTTP observations do not expose the exact private window lease
        # object. A visible name/lock-owner alone cannot distinguish a replacement
        # opened by the parent/user, so failure never blindly Esc-closes a UI.
        self.report["uiCleanupDeferred"] = "Exact native window lease is not exposed; recover cursor/close through parent-owned real UI."
        self.save()


def cell(tree, path):
    rows = [row for row in tree["nodes"] if row.get("path") == path]
    need(len(rows) == 1, "exact visible cell path required: " + path)
    return rows[0].get("item")


def grid_case(runner, plan, case):
    entity, window = plan["entity"], "workstation_campfire"
    runner.actions(plan["openCampfire"], window)
    before = runner.snapshot(entity)
    tree = runner.call("/ui/tree", window=window)
    source, target = case["backpackPath"], case["gridPath"]
    if case.get("sourceWitness"):
        runner.source_proof([case["sourceWitness"]])
        need(any(row["path"] == source and row.get("source") == case["sourceWitness"]
                 for row in runner.source_observation()["visibleSlots"]), "grid source path changed physical cell")
    item = cell(tree, source)
    target_rows = [row for row in tree["nodes"] if row.get("path") == target]
    expected_controller = {"fuel": "XUiC_RebirthCookingFuelSlot", "tool": "XUiC_RebirthCookingToolSlot",
                           "ingredient": "XUiC_RebirthCookingSlot"}[case["kind"]]
    need(len(target_rows) == 1 and target_rows[0].get("ctrl") == expected_controller,
         "actual cooking grid controller does not match requested case")
    need(item and item["name"] == case["item"] and item["count"] == case["count"] and cell(tree, target) is None,
         "exact preparatory source stack and empty target required")
    runner.actions([{"endpoint": "/ui/click", "args": {"path": source, "shift": 1}}])
    tree = runner.call("/ui/tree", window=window)
    need(cell(tree, source) == item and cell(tree, target) is None
         and runner.call("/camper", entity=entity)["cursorEmpty"] is True, "backpack shift insertion did not refuse unchanged source/grid")
    runner.actions([{"endpoint": "/ui/drag", "args": {"from.path": source, "to.path": target, "strict": 1}}])
    tree = runner.call("/ui/tree", window=window)
    need(cell(tree, source) is None and cell(tree, target) == item, "strict grid deposit failed")
    need(counts(runner.call("/state", sections="inventory")["backpack"])[case["item"]]
         == counts(before["state"]["backpack"])[case["item"]] - item["count"], "grid deposit did not debit exact backpack count")
    # A click must pick up, not quick-return; then put back to the same grid cell.
    runner.actions([{"endpoint": "/ui/click", "args": {"path": target}}])
    need(runner.call("/camper", entity=entity)["cursorEmpty"] is False, "ordinary click did not pick up the actual stack")
    need(counts(runner.call("/state", sections="inventory")["backpack"])[case["item"]]
         == counts(before["state"]["backpack"])[case["item"]] - item["count"], "ordinary click silently returned to backpack")
    runner.actions([{"endpoint": "/ui/click", "args": {"path": target}}])
    need(runner.call("/camper", entity=entity)["cursorEmpty"] is True, "ordinary click put-back retained cursor")
    runner.actions([{"endpoint": "/ui/click", "args": {"path": target, "shift": 1}}])
    need(cell(runner.call("/ui/tree", window=window), target) == item
         and runner.call("/camper", entity=entity)["cursorEmpty"] is True, "shift gesture did not refuse unchanged grid/cursor")
    runner.actions([{"endpoint": "/ui/drag", "args": {"from.path": target, "to.path": source, "strict": 1}}])
    need(cell(runner.call("/ui/tree", window=window), source) == item, "strict grid return failed")
    runner.actions(plan["closeCampfire"])
    after = runner.closed(entity)
    exact_station(before, after)
    need(counts(before["state"]["backpack"] + before["state"]["toolbelt"])
         == counts(after["state"]["backpack"] + after["state"]["toolbelt"]), "roundtrip changed inventory")
    runner.report.setdefault("gridCases", []).append({"case": case, "before": before, "after": after})
    runner.save()


def output_conservation(before, after, definitions):
    old, new = stations(before), stations(after)
    before_items, after_items = total_items(before), total_items(after)
    for slot, definition in definitions.items():
        jobs = [job for job in new[slot]["queue"] if job["recipe"] == definition["recipe"]]
        need(len(jobs) <= 1 and len(jobs) == len(new[slot]["queue"]), "duplicate/unrelated paid queue jobs")
        remaining = sum(job["count"] for job in jobs)
        need(0 <= remaining <= definition["batches"], "invalid remaining paid batches")
        produced = (definition["batches"] - remaining) * definition["outputPerBatch"]
        name = definition["outputItem"]
        need(after_items[name] - before_items[name] == produced, "native output/payment conservation failed: " + name)


def continuing_station(before, after, definitions):
    """Independent paid batch/output identities; never adopt after as expected."""
    original_cargo(before, after)
    old, new = stations(before), stations(after)
    a, b = before["camper"].get("nativeGameTicks"), after["camper"].get("nativeGameTicks")
    need(type(a) is int and type(b) is int, "native elapsed tick witnesses required")
    same_process = before["session"] == after["session"]
    need(not same_process or b >= a, "same-process native clock regressed")
    elapsed = (b - a) / 20.0 if same_process else None
    for slot, definition in definitions.items():
        for field in ("tools", "input", "queueCapacity", "outputCapacity"):
            need(old[slot][field] == new[slot][field], "continuing reload changed fixed station field: " + field)
        jobs_a, jobs_b = old[slot]["queue"], new[slot]["queue"]
        need(len(jobs_a) <= 1 and len(jobs_b) <= 1 and all(j["recipe"] == definition["recipe"] for j in jobs_a + jobs_b), "foreign/duplicate job across continuing reload")
        ca = sum(j["count"] for j in jobs_a); cb = sum(j["count"] for j in jobs_b)
        need(0 <= cb <= ca <= definition["batches"], "paid batch count regressed/duplicated")
        seconds = definition["nativeSecondsPerBatch"]
        need(seconds > 0, "independently observed native batch duration required")
        def work(jobs):
            if not jobs: return 0.0
            job = jobs[0]
            need(0 <= job["remaining"] <= seconds + 0.25, "queue timer outside native batch duration")
            return (job["count"] - 1) * seconds + job["remaining"]
        advance = work(jobs_a) - work(jobs_b)
        need(advance >= -0.25, "remaining paid work increased across reload")
        if same_process:
            need(advance <= elapsed + 0.25, "remaining paid work violates native elapsed timer bound")
        name = definition["outputItem"]
        need(counts(new[slot]["output"])[name] - counts(old[slot]["output"])[name]
             == (ca - cb) * definition["outputPerBatch"], "dropped/duplicated batches or cross-station output")
        need([c for c in old[slot]["output"] if c.get("name") != name and c["count"]]
             == [c for c in new[slot]["output"] if c.get("name") != name and c["count"]], "unrelated output changed")
        fuel_a, fuel_b = counts(old[slot]["fuel"]), counts(new[slot]["fuel"])
        need(all(fuel_b[n] <= fuel_a[n] for n in fuel_b), "continuing station created fuel")
        need(new[slot]["fuelSeconds"] <= old[slot]["fuelSeconds"] + 0.25, "remaining native fuel increased")
        if same_process:
            need(old[slot]["fuelSeconds"] - new[slot]["fuelSeconds"] <= elapsed + 0.25, "fuel timer exceeds native elapsed bound")
    ignored = {d["outputItem"] for d in definitions.values()}
    ignored.update(c["name"] for s in old.values() for c in s["fuel"] if c["count"])
    totals_a, totals_b = total_items(before), total_items(after)
    need({n: c for n, c in totals_a.items() if n not in ignored and c}
         == {n: c for n, c in totals_b.items() if n not in ignored and c}, "material/source payment replay or unrelated count loss across continuing interval")
    return {"elapsedNativeSeconds": elapsed, "timerToleranceSeconds": 0.25, "mode": "continuing",
            "timerQualification": "bounded-same-process" if same_process else "limited-no-saved-cutover-clock",
            "startupNativeSeconds": after["camper"].get("nativeTicksSinceWorldLoaded", 0) / 20.0,
            "nativeClockAtWorldLoaded": after["camper"].get("nativeClockAtWorldLoaded")}


def prepare(runner, plan):
    need(plan.get("confirm") == "CodexTest", "explicit disposable-save confirmation required")
    if plan.get("existingInstalledStations") is True:
        return prepare_existing(runner, plan)
    reload_mode = plan.get("reloadMode", "continuing")
    need(reload_mode in ("continuing", "paused"), "explicit supported reload mode required")
    if reload_mode == "paused":
        need(plan.get("pauseForReload"), "strict paused qualification requires observed real controls")
    for phase in ("installCampfire", "installFreshWorkbench", "closeManagement", "openCampfire", "closeCampfire",
                  "mountDriver", "dismountDriver"):
        need(isinstance(plan.get(phase), list) and plan[phase], "complete observed input plan required before preparation: " + phase)
    need(len(plan.get("gridCases", [])) == 3 and {case["kind"] for case in plan["gridCases"]} == {"fuel", "tool", "ingredient"}, "exactly three observed grid cases required")
    for case in plan["gridCases"]:
        need(case["item"] in PREPARATORY and type(case["count"]) is int and case["count"] > 0
             and all(isinstance(case[key], str) and case[key] and "TODO" not in case[key]
                     for key in ("backpackPath", "gridPath")), "complete observed grid cell/count required")
    for slot in (0, 1):
        need(plan["jobs"][str(slot)].get("actions") and plan["jobs"][str(slot)].get("close"), "real paid job inputs required")
    grants = collections.Counter()
    for grant in plan["prepareItems"]:
        need(grant["item"] in PREPARATORY and type(grant["count"]) is int and 1 <= grant["count"] <= 600, "unsupported preparatory grant")
        grants[grant["item"]] += grant["count"]
    prepared = plan.get("preparedItems") is True
    if prepared:
        need(not plan["prepareItems"] and plan.get("preparedSourceWitnesses"), "discovered profile cannot give items again")
        for name in ("campfire", "workbench"):
            plain = [source for source in plan["preparedSourceWitnesses"] if source["name"] == name]
            need(len(plain) == 1 and plain[0]["count"] == 1 and plain[0]["plainStation"] is True, "exact plain prepared station proof required")
    else:
        need(grants["campfire"] == 1 and grants["workbench"] == 1, "exactly one plain preparatory station of each type required")
    entity = plan["entity"]
    original_entity = plan["originalEntity"]
    need(entity != original_entity and plan.get("fixtureVehicleConfirmedEmpty") is True,
         "separate parent-prepared empty disposable fixture vehicle required")
    before = runner.preflight(entity)
    need(before["state"]["world"].get("paused") is False, "close the parent-owned pause menu before prepare")
    original = runner.snapshot(original_entity)
    runner.report["original"] = original
    found = stations(original)
    need(set(found) == {0, 1} and found[0]["block"] == "campfire" and found[1]["block"] == "workbench", "original distinct campfire0/workbench1 required")
    need(all(not row["queue"] and not row["burning"] for row in found.values()), "original fixtures must be idle")
    need(not stations(before) and before["camper"]["vehicleIdentity"] != original["camper"]["vehicleIdentity"], "fixture already contains stations or reuses original identity")
    need(before["camper"]["lockOwner"] == -1 and before["camper"]["localPlayerAttachedEntityId"] == -1, "start outside owned UI/vehicle")
    initial_inventory = counts(before["state"]["backpack"] + before["state"]["toolbelt"])
    need(initial_inventory["workbench"] == (1 if prepared else 0) and initial_inventory["campfire"] == (1 if prepared else 0),
         "unambiguous fresh plain station sources required")
    if prepared:
        need(runner.scope == plan["discoverySession"], "candidate discovery session changed; recapture before input")
        runner.source_proof(plan["preparedSourceWitnesses"])
        observation = runner.source_observation()
        need(observation["modalOpen"] is False and observation["inputActive"] is False, "parent must close discovery UI before prepare")
        need(runner.call("/ui/tree", all=1)["screen"] == plan["discoveryScreen"], "captured viewport changed before input")
    # Only named, count-bounded preparation is permitted. No material/queue cheats.
    for grant in plan["prepareItems"]:
        need(grant["item"] in PREPARATORY and type(grant["count"]) is int and 1 <= grant["count"] <= 600, "unsupported preparatory grant")
        old = counts(runner.call("/state", sections="inventory")["backpack"])
        runner.call("/give", "POST", item=grant["item"], count=grant["count"])
        new = counts(runner.call("/state", sections="inventory")["backpack"])
        need(new[grant["item"]] - old[grant["item"]] == grant["count"], "preparatory give did not conserve expected count")
    runner.actions(plan["installCampfire"], "rebirthCamperStationManagement")
    need(set(stations(runner.snapshot(entity))) == {0}, "fresh campfire0 not installed into empty fixture")
    runner.actions(plan["installFreshWorkbench"], "rebirthCamperStationManagement")
    fresh = runner.snapshot(entity)
    new = stations(fresh)
    need(set(new) == {0, 1} and new[0]["block"] == "campfire" and new[1]["block"] == "workbench"
         and new[0]["id"] != new[1]["id"]
         and new[1]["id"] not in {found[0]["id"], found[1]["id"]}, "fresh Workbench1 not installed alongside one distinct campfire")
    need(new[1]["queueCapacity"] == 16 and new[1]["outputCapacity"] == 28 and not new[1]["queue"], "fresh Workbench capacity wrong before its first opening")
    fresh_inventory = counts(fresh["state"]["backpack"] + fresh["state"]["toolbelt"])
    need(fresh_inventory["workbench"] == 0 and fresh_inventory["campfire"] == 0, "fresh source stations were not consumed exactly once by real installation")
    exact_station(original, runner.snapshot(original_entity))
    runner.report["freshWorkbench"] = fresh
    runner.actions(plan["closeManagement"])
    restored = runner.closed(entity)
    need(set(stations(restored)) == {0, 1}, "fresh distinct installations changed after management close")
    exact_station(fresh, restored)
    need({case["kind"] for case in plan["gridCases"]} == {"fuel", "tool", "ingredient"}, "all three strict cooking grid kinds required")
    for case in plan["gridCases"]:
        grid_case(runner, plan, case)
    base = runner.closed(entity)
    return prepare_active_jobs(runner, plan, base, original, original_entity)


def prepare_existing(runner, plan):
    need(not plan.get("prepareItems") and not plan.get("installCampfire") and not plan.get("installFreshWorkbench") and not plan.get("gridCases"),
         "existing installed mode cannot give/reinstall/run installation or grid qualification")
    entity = plan["entity"]
    need(plan["originalEntity"] == entity, "existing installed mode uses the original camper itself")
    for phase in ("mountDriver", "dismountDriver"):
        need(plan.get(phase), "actual driver inputs required")
    for slot in (0, 1):
        for phase in ("actions", "close", "reopen", "reclose"):
            need(plan["jobs"][str(slot)].get(phase), "observed paid job/reopen inputs required")
    before = runner.preflight(entity); base = runner.closed(entity)
    need(before["state"]["world"].get("paused") is False, "start from unpaused original camper")
    found = stations(base)
    need(set(found) == {0, 1} and found[0]["block"] == "campfire" and found[1]["block"] == "workbench", "actual original distinct Campfire0/Workbench1 required")
    expected = plan.get("installedStations")
    need(isinstance(expected, list) and len(expected) == 2, "independently observed installed identities required")
    need(sorted(expected, key=lambda s: s["slot"]) == [{"slot": slot, "id": found[slot]["id"], "block": found[slot]["block"]} for slot in (0, 1)], "original station identity/type preflight mismatch")
    for station in found.values():
        need(not station["queue"] and station["burning"] is False and all(c["count"] == 0 for area in ("input", "tools", "output") for c in station[area]),
             "initial original station must have no jobs/input/tools/output and be stopped")
        # Native unused fuel and residual stopped burn time are preserved.
        need(station["fuelSeconds"] >= 0, "invalid original residual fuel timer")
    need(plan.get("preparedSourceWitnesses"), "actual preparatory material/tool source witnesses required")
    runner.source_proof(plan["preparedSourceWitnesses"])
    runner.report["original"] = base; runner.report["qualificationMode"] = "existing-installed-active-jobs"
    return prepare_active_jobs(runner, plan, base, base, entity)


def prepare_active_jobs(runner, plan, base, original, original_entity):
    entity = plan["entity"]
    reload_mode = plan.get("reloadMode", "continuing")
    need(reload_mode in ("continuing", "paused"), "supported reload qualification required")
    if reload_mode == "paused": need(plan.get("pauseForReload"), "strict paused mode requires observed pause controls")
    definitions = {slot: runner.recipe(plan["jobs"][str(slot)]) for slot in (0, 1)}
    need(definitions[0]["recipe"] == "foodBakedPotato" and definitions[1]["recipe"] != definitions[0]["recipe"], "two distinct declared native cooking/workbench jobs required")
    installed = stations(base)
    need(installed[0]["block"] == "campfire" and installed[1]["block"] == "workbench", "actual distinct station block identities changed")
    need(all(allows_recipe_area(definitions[slot]["area"], installed[slot].get("recipeAdmission")) for slot in (0, 1)),
         "declared native recipe is outside the installed station's actual allowed areas")
    need(definitions[1].get("workbenchAdmission") == installed[1]["recipeAdmission"],
         "recipe observer/current installed workbench area witnesses disagree")
    runner.report["nativeDefinitions"] = definitions
    # Queue the slower multi-batch workbench first; cook last so both genuine
    # jobs are still eligible to progress when the driver starts moving.
    for slot in (1, 0):
        old = runner.closed(entity)
        runner.actions(plan["jobs"][str(slot)]["actions"], "workstation_campfire" if slot == 0 else "workstation_workbench")
        runner.actions(plan["jobs"][str(slot)]["close"])
        new = runner.closed(entity)
        definition = definitions[slot]
        # Count inventory and real station input together so staging is not mistaken for payment.
        old_items, new_items = total_items(old), total_items(new)
        for ingredient in definition["ingredients"]:
            need(old_items[ingredient["name"]] - new_items[ingredient["name"]] == ingredient["payment"], "native exact paid ingredient mismatch")
        jobs = stations(new)[slot]["queue"]
        need(len(jobs) == 1 and jobs[0]["recipe"] == definition["recipe"] and 0 < jobs[0]["count"] <= definition["batches"], "real paid job not queued")
        runner.report.setdefault("paidJobs", []).append({"slot": slot, "before": old, "after": new})
        original_cargo(base, new)
    queued = runner.closed(entity)
    runner.report["queued"] = queued
    runner.actions(plan["mountDriver"])
    driven_start = runner.snapshot(entity)
    c = driven_start["camper"]
    need(c["vehicleDriverEntityId"] == c["localPlayerEntityId"] and c["localPlayerAttachedEntityId"] == entity, "actual driver attachment required")
    vehicle_rows = [row for row in runner.call("/entities", radius=8, limit=100)["entities"] if row["id"] == entity]
    need(len(vehicle_rows) == 1, "actual starting vehicle position unavailable")
    original_position = vehicle_rows[0]["position"]
    distance = 0.0
    for _ in range(8):
        move = runner.call("/move", "POST", forward=1, seconds=4)
        need(move.get("movement") == "vehicle" and move.get("vehicleEntityId") == entity, "correct native driver action routing required")
        distance = math.dist(original_position, move["position"])
        runner.errors()
        if distance >= 20:
            break
    need(distance >= 20, "actual driver did not travel20m in bounded real input")
    driven_end = runner.snapshot(entity)
    need(driven_end["camper"]["vehicleDriverEntityId"] == c["localPlayerEntityId"]
         and driven_end["camper"]["localPlayerAttachedEntityId"] == entity, "driver attachment changed during progress")
    for slot, definition in definitions.items():
        old = [job for job in stations(driven_start)[slot]["queue"] if job["recipe"] == definition["recipe"]]
        new = [job for job in stations(driven_end)[slot]["queue"] if job["recipe"] == definition["recipe"]]
        need(len(old) == 1 and (not new or new[0]["count"] < old[0]["count"] or new[0]["remaining"] < old[0]["remaining"]), "distinct station did not progress during actual driving")
    output_conservation(base, driven_end, definitions)
    runner.report["drive"] = {"distance": distance, "before": driven_start, "after": driven_end}
    runner.actions(plan["dismountDriver"])
    paused = runner.closed(entity)
    need(paused["camper"]["localPlayerAttachedEntityId"] == -1, "driver did not dismount")
    for slot in (0, 1):
        job = plan["jobs"][str(slot)]
        if job.get("reopen"):
            before_open = runner.closed(entity)
            runner.actions(job["reopen"], "workstation_campfire" if slot == 0 else "workstation_workbench")
            opened = runner.snapshot(entity)
            proof = continuing_station(before_open, opened, definitions)
            runner.actions(job["reclose"])
            after_close = runner.closed(entity)
            continuing_station(before_open, after_close, definitions)
            output_conservation(base, after_close, definitions)
            runner.report.setdefault("activeReopens", []).append({"slot": slot, "before": before_open, "opened": opened, "after": after_close, "proof": proof})
    # Operator pauses through real controls; exact timers must remain fixed before
    # parent saves/quits. No successful reload is inferred from merely capturing.
    if reload_mode == "paused":
        runner.actions(plan["pauseForReload"])
    baseline = runner.closed(entity)
    time.sleep(2)
    stability = runner.closed(entity)
    if reload_mode == "paused":
        exact_station(baseline, stability)
    else:
        runner.report["reloadPreparationProgression"] = continuing_station(baseline, stability, definitions)
    need(any(row["queue"] for row in stations(baseline).values()), "reload baseline must retain real paid queue work")
    need(any(counts(row["output"]) for row in stations(baseline).values()), "reload baseline must contain actual produced station output")
    output_conservation(base, baseline, definitions)
    runner.report["reloadBaseline"] = baseline
    runner.report["reloadMode"] = reload_mode
    runner.report["reloadOriginalOwner"] = runner.call("/gearobserve")
    runner.report["paidOutputBase"] = base
    original_after = runner.snapshot(original_entity)
    if original_entity == entity:
        original_cargo(original, original_after)
    else:
        exact_station(original, original_after)
    runner.report["originalAfter"] = original_after
    runner.report["phase"] = "awaiting-parent-save-reload"
    runner.report["preparePassed"] = True
    runner.save()


def verify_reload(runner, old_report):
    need(old_report.get("preparePassed") is True and old_report.get("phase") == "awaiting-parent-save-reload", "successful prepared parent-reload baseline required")
    baseline, entity = old_report["reloadBaseline"], old_report["plan"]["entity"]
    need(runner.scope["pid"] != baseline["session"]["pid"] or runner.scope["startedUtc"] != baseline["session"]["startedUtc"], "genuinely fresh process/session required")
    after = runner.preflight(entity)
    runner.report["reloadAfter"] = after
    need(after["state"]["world"]["gameWorld"] == baseline["state"]["world"]["gameWorld"], "same world required after reload")
    if old_report.get("reloadMode", "paused") == "paused":
        exact_station(baseline, after)
        need(total_items(baseline) == total_items(after), "inventory/station count changed across paused reload")
    else:
        definitions = {int(slot): value for slot, value in old_report["nativeDefinitions"].items()}
        runner.report["reloadProgression"] = continuing_station(baseline, after, definitions)
        output_conservation(old_report["paidOutputBase"], after, definitions)
        saved_owner, new_owner = old_report["reloadOriginalOwner"], runner.call("/gearobserve")
        for key in ("bag", "belt", "bagLocks", "cursor", "worldKey", "creationId"):
            need(saved_owner[key] == new_owner[key], "continuing reload changed original owner: " + key)
        need(new_owner["cursor"]["count"] == 0 and not any(new_owner[k] for k in ("gearHeld", "libraryHeld", "resourcePending", "offerPending")), "owner/cursor custody pending after reload")
    original_after = runner.snapshot(old_report["plan"]["originalEntity"])
    if old_report["plan"].get("existingInstalledStations") is True:
        original_cargo(old_report["original"], original_after)
        definitions = {int(slot): value for slot, value in old_report["nativeDefinitions"].items()}
        continuing_station(after, original_after, definitions)
    else:
        exact_station(old_report["originalAfter"], original_after)
    runner.report["originalAfterReload"] = original_after
    runner.report["passed"] = True
    runner.report["phase"] = "reload-verified"
    runner.save()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    sub = parser.add_subparsers(dest="mode", required=True)
    prep = sub.add_parser("prepare")
    prep.add_argument("plan"); prep.add_argument("report")
    reload = sub.add_parser("verify-reload"); reload.add_argument("report")
    args = parser.parse_args()
    path = Path(args.report)
    if args.mode == "prepare":
        need(not path.exists(), "choose a new report path; original evidence is never overwritten")
        plan = json.loads(Path(args.plan).read_text(encoding="utf-8-sig"))
        report = {"passed": False, "preparePassed": False, "phase": "preflight", "plan": plan}
        old = None
    else:
        old = json.loads(path.read_text(encoding="utf-8-sig"))
        plan = old["plan"]
        path = path.with_name(path.stem + "_reload.json")
        need(not path.exists(), "choose distinct reload evidence; existing report preserved")
        report = {"passed": False, "phase": "reload-preflight", "baselineReport": str(Path(args.report).resolve()), "plan": plan}
    runner = None
    try:
        runner = Runner(report, path)
        if old is None:
            prepare(runner, plan)
        else:
            verify_reload(runner, old)
        print(report["phase"], str(path.resolve()))
    except Exception as error:
        report["failure"] = type(error).__name__ + ": " + str(error)
        if runner is not None:
            runner.cleanup(plan["entity"])
        raise
    finally:
        if runner is not None:
            runner.save()
        else:
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_text(json.dumps(report, indent=2), encoding="utf-8")


if __name__ == "__main__":
    main()
