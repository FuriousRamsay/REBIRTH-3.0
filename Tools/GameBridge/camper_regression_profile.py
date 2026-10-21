"""Opt-in READ-ONLY staged capture and candidate profile discovery.

Parent manually opens each UI and records reviewed ingress actions beforehand.
This program never sends player/UI input, gives items, saves, quits or launches.
"""
import argparse
import copy
import json
from pathlib import Path
import time
from camper_consolidated_regression_live import Runner, need, PREPARATORY, stations, allows_recipe_area

WINDOWS = {"management": "rebirthCamperStationManagement", "campfire": "workstation_campfire",
           "workbench": "workstation_workbench", "paused": "ingameMenu", "picker": "rebirthCamperUseStation"}


def capture(args):
    path = Path(args.output)
    need(not path.exists(), "existing capture will not be overwritten")
    report = {"schema": 1, "readOnly": True, "kind": args.kind, "fixture": args.fixture,
              "originalEntity": args.original, "existingInstalledStations":args.existing_installed_stations, "passed": False}
    runner = Runner(report, path)
    try:
        need(args.existing_installed_stations or args.fixture != args.original, "distinct prepared fixture/original required outside explicit installed mode")
        report["fixtureState"] = runner.preflight(args.fixture)
        installed = stations(report["fixtureState"])
        if args.existing_installed_stations:
            need(args.fixture == args.original, "installed mode qualifies the same original camper")
            need(set(installed) == {0, 1} and installed[0]["block"] == "campfire" and installed[1]["block"] == "workbench",
                 "actual campfire0/workbench1 installations required")
            for row in installed.values():
                need(not row["queue"] and row["burning"] is False
                     and all(not any(c["count"] for c in row[area]) for area in ("input", "tools", "output")),
                     "installed fixture must be idle with empty input/tools/output")
            report["installedStations"] = [{"slot":slot,"id":installed[slot]["id"],"block":installed[slot]["block"]} for slot in (0, 1)]
        else:
            need(not installed, "profile discovery requires parent-prepared empty fixture")
        report["originalState"] = runner.snapshot(args.original)
        report["session"] = runner.scope
        report["physical"] = runner.source_observation()
        report["ui"] = runner.call("/ui/tree", **({"window": WINDOWS[args.kind], "all": 1} if args.kind in WINDOWS else {"all": 1}))
        need(report["ui"].get("truncated") is False, "truncated UI is not a selector proof")
        if args.kind in WINDOWS:
            need(report["ui"].get("nodes"), "parent must open the requested actual UI before capture")
        if args.kind in ("campfire", "workbench"):
            name = args.recipe or ("foodBakedPotato" if args.kind == "campfire" else "ammo9mmBulletBall")
            selected = [row for row in report["physical"]["selectedRecipes"]
                        if row["path"].startswith(WINDOWS[args.kind] + "/") and row["recipe"] == name]
            need(len(selected) == 1, "parent must select the actual expected native recipe before capture")
            report["recipe"] = runner.recipe({"recipe": name, "batches": args.batches, "tier": selected[0]["tier"]})
            need(report["recipe"]["area"] == "campfire" if args.kind == "campfire" else
                 allows_recipe_area(report["recipe"]["area"], report["recipe"].get("workbenchAdmission")),
                 "selected canonical recipe belongs to a different actual native station area")
        if args.kind == "paused":
            first = report["fixtureState"]["state"]["world"]
            need(first.get("paused") is True and report["physical"]["paused"] is True, "actual native pause proof required")
            time.sleep(2)
            second = runner.call("/state", sections="world")["world"]
            need(second["paused"] is True and first["worldTime"] == second["worldTime"], "native pause did not hold world time")
            report["pauseProof"] = {"before": first, "after": second, "observedSeconds": 2}
        if args.kind == "driver":
            c = report["fixtureState"]["camper"]
            need(c["localPlayerAttachedEntityId"] == args.fixture and c["vehicleDriverEntityId"] == c["localPlayerEntityId"], "parent must be the actual fixture driver")
        if args.kind == "closed":
            c = report["fixtureState"]["camper"]
            need(c["lockOwner"] == -1 and c["localPlayerAttachedEntityId"] == -1, "closed/unattached fixture proof required")
        runner.errors()
        report["passed"] = True
    except Exception as error:
        report["failure"] = type(error).__name__ + ": " + str(error)
        raise
    finally:
        runner.save()


def unique(rows, message):
    need(len(rows) == 1, message + " (matches=" + str(len(rows)) + ")")
    return rows[0]


def nodes(cap):
    need(cap["ui"].get("truncated") is False, "truncated selector capture")
    return cap["ui"]["nodes"]


def node_id(cap, id, input=False):
    row = unique([row for row in nodes(cap) if row.get("id") == id], "missing/ambiguous actual control " + id)
    need(row.get("rect") and (row.get("ctrl") == "XUiC_TextInput" if input else row.get("clickable") is True), "actual input/press control required: " + id)
    return row


def visible_slot(cap, path):
    return unique([row for row in cap["physical"]["visibleSlots"] if row["path"] == path], "missing/ambiguous native slot binding")


def source(cap, name):
    physical = unique([row for row in cap["physical"]["sources"] if row["name"] == name], "unique physical source required: " + name)
    visible = unique([row for row in cap["physical"]["visibleSlots"] if row.get("source") == physical
                      and row["path"].startswith(WINDOWS[cap["kind"]] + "/")], "source not uniquely visible: " + name)
    ui_row = unique([row for row in nodes(cap) if row["path"] == visible["path"]], "source lacks actual UI geometry")
    need(ui_row.get("rect") and ui_row.get("clickable") is True, "source not an actual pressable visible cell")
    return visible["path"], physical


def ingredient_sources(cap, name, payment):
    need(type(payment) is int and payment > 0, "positive independently resolved ingredient payment required")
    candidates = sorted([s for s in cap["physical"]["sources"] if s["name"] == name], key=lambda s:s["slot"])
    need(candidates and sum(s["count"] for s in candidates) >= payment, "actual native ingredient sources cannot cover payment: " + name)
    selected, count = [], 0
    for physical in candidates:
        rows = [s for s in cap["physical"]["visibleSlots"] if s.get("source") == physical
                and s["path"].startswith(WINDOWS[cap["kind"]]+"/")]
        # Hidden/clipped sources are not selectors. Parent must scroll/capture
        # sufficient genuine physical stacks; never manufacture their merge.
        if not rows: continue
        visible = unique(rows, "ambiguous native ingredient source binding")
        ui = unique([s for s in nodes(cap) if s["path"] == visible["path"]], "ingredient source lacks actual UI geometry")
        need(ui.get("rect") and ui.get("clickable") is True, "ingredient source is not actually pressable")
        selected.append((visible["path"], physical)); count += physical["count"]
        if count >= payment: break
    need(count >= payment, "scroll and capture sufficient actual visible ingredient sources: " + name)
    return selected


def target(cap, role, index=None, tool=None):
    rows = [row for row in cap["physical"]["visibleSlots"] if row["path"].startswith(WINDOWS[cap["kind"]] + "/")
            and row.get("role") == role and row["empty"] is True]
    if index is not None:
        rows = [row for row in rows if row["slotNumber"] == index]
    if tool:
        rows = [row for row in rows if tool in row.get("acceptsTools", [])]
    row = unique(rows, "missing/ambiguous empty accepted " + role + " slot")
    ui_row = unique([node for node in nodes(cap) if node["path"] == row["path"]], "target lacks UI geometry")
    need(ui_row.get("rect") and ui_row.get("clickable") is True, "target is not actual visible press cell")
    return row["path"]


def action(endpoint, **args):
    return {"endpoint": endpoint, "args": args, "wait": 0.3}


def deposit(cap, name, path):
    origin, witness = source(cap, name)
    result = action("/ui/drag", **{"from.path": origin, "to.path": path, "strict": 1})
    result["sourceWitness"] = witness
    return result


def recipe_selector(cap):
    definition = cap["recipe"]
    canonical = [row for row in nodes(cap) if row.get("recipe") == definition["recipe"]
                 and row.get("ctrl") == "XUiC_RebirthCraftingRecipeEntry"]
    if canonical:
        row = unique(canonical, "ambiguous exact canonical native recipe row")
        need(row.get("rect"), "actual native recipe row rectangle required")
        if row.get("clickable") is True:
            return row["path"]
        hits = [child for child in nodes(cap) if child.get("clickable") is True
                and child.get("enabled") is not False and child["path"].startswith(row["path"] + "/")
                and child["path"].rsplit("/", 1)[0] == row["path"]
                and child.get("id", "").startswith("rebirthCraftingRecipeRowHit")
                and child.get("rect") == row["rect"]]
        return unique(hits, "exact canonical recipe row lacks unique native child hit region")["path"]
    need(cap["kind"] != "workbench", "actual canonical Workbench recipe row required; labels alone cannot select it")
    direct = [row for row in nodes(cap) if row.get("recipe") == definition["recipe"] and row.get("clickable") is True]
    if direct:
        return unique(direct, "ambiguous native recipe selector")["path"]
    # Current authored catalogue rows carry localized labels, while the selected
    # recipe observer supplies the exact native ID independently. Restrict to the
    # actual recipe viewport/rows; never mistake the detail/output title for a row.
    labels = [row for row in nodes(cap) if row.get("text") == definition["localizedOutput"]
              and ("/rebirthCraftingRecipeViewport/" in row["path"] or "/recipeRows/" in row["path"])]
    label = unique(labels, "exact visible canonical recipe row required")
    ancestors = [row for row in nodes(cap) if row.get("clickable") is True and label["path"].startswith(row["path"] + "/")]
    need(ancestors, "recipe label lacks actual press parent")
    return max(ancestors, key=lambda row: len(row["path"]))["path"]


def reviewed_stage(ingress, name, captures, fixture, original):
    stage = ingress["stages"][name]
    actions = stage["actions"]
    need(actions and stage["proof"] in captures, "recorded ingress actions and actual after-capture required: " + name)
    proof = captures[stage["proof"]]
    need(proof["passed"] is True, "ingress after-capture failed")
    evidence = stage.get("evidence", [])
    result = copy.deepcopy(actions)
    for old, replay in zip(actions, result):
        need(any(row.get("endpoint") == old["endpoint"] and row.get("args", {}) == old.get("args", {})
                 and row.get("reply", {}).get("ok") is True for row in evidence), "successful recorded actual input proof missing: " + name)
        if replay["endpoint"] in {"/lookat", "/activate"} and "entity" in replay.get("args", {}):
            need(replay["args"]["entity"] in {fixture, original}, "unrelated ingress entity")
            replay["args"]["entity"] = fixture
        for coordinate, maximum in zip(("x", "y"), ingress["screen"]):
            if coordinate in replay.get("args", {}) and replay["endpoint"] in {"/ui/hover", "/ui/click"}:
                need(0 <= replay["args"][coordinate] < maximum, "recorded input outside actual captured viewport")
    return result


def build(args):
    path = Path(args.output)
    need(not path.exists(), "existing candidate is not overwritten")
    result = {"schema": 1, "readOnlyDiscovery": True, "candidateReady": False}
    try:
        ingress = json.loads(Path(args.ingress).read_text(encoding="utf-8-sig"))
        reload_mode = ingress.get("reloadMode", "continuing")
        existing = ingress.get("existingInstalledStations", False)
        need(type(existing) is bool, "explicit installed-station mode must be boolean")
        result["existingInstalledStations"] = existing
        result["reloadMode"] = reload_mode
        need(reload_mode in ("continuing", "paused"), "unsupported reviewed reload mode")
        captures = {}
        for file in args.captures:
            cap = json.loads(Path(file).read_text(encoding="utf-8-sig"))
            need(cap.get("schema") == 1 and cap.get("readOnly") is True and cap.get("passed") is True, "invalid read-only capture")
            need(cap["kind"] not in captures, "duplicate capture kind")
            captures[cap["kind"]] = cap
        required = {"management", "campfire", "workbench", "driver", "closed"}
        if reload_mode == "paused": required.add("paused")
        need(set(captures) >= required, "all mode-required actual UI/driver/closed captures required")
        base = captures["management"]
        prepared_names = set(PREPARATORY) | {ingredient["name"] for cap in captures.values()
                         for ingredient in cap.get("recipe", {}).get("ingredients", [])}
        baseline_sources = sorted([row for row in base["physical"]["sources"] if row["name"] in prepared_names], key=lambda row: row["slot"])
        for cap in captures.values():
            need(cap.get("existingInstalledStations", False) is existing, "capture/ingress fixture mode differs")
            if existing:
                need(cap.get("installedStations") == base.get("installedStations") and base.get("installedStations"),
                     "actual installed station identities changed across captures")
            need(cap["session"] == base["session"] and cap["fixture"] == base["fixture"]
                 and cap["originalEntity"] == base["originalEntity"] and cap["ui"]["screen"] == base["ui"]["screen"], "capture session/entity/viewport changed")
            need(sorted([row for row in cap["physical"]["sources"] if row["name"] in prepared_names], key=lambda row: row["slot"])
                 == baseline_sources, "prepared sources moved/changed/vanished between staged captures")
        if reload_mode == "paused":
            need(captures["paused"].get("pauseProof", {}).get("observedSeconds", 0) >= 2, "native stable pause proof absent")
        need(ingress.get("schema") == 1 and ingress.get("reviewed") is True and ingress["session"] == base["session"]
             and ingress["screen"] == base["ui"]["screen"], "explicit same-session reviewed ingress required")
        fixture, original = base["fixture"], base["originalEntity"]
        def stage(name): return reviewed_stage(ingress, name, captures, fixture, original)
        proofs = {"openManagement":"management", "openCampfire":"campfire", "openWorkbench":"workbench",
                  "mountDriver":"driver", "dismountDriver":"closed",
                  "closeManagement":"closed", "closeCampfire":"closed", "closeWorkbench":"closed"}
        if reload_mode == "paused": proofs["pauseForReload"] = "paused"
        for name, kind in proofs.items():
            need(ingress["stages"][name]["proof"] == kind, "wrong native ingress outcome proof: " + name)
        witness_by_slot = {}
        for cap in captures.values():
            for physical in cap["physical"]["sources"]:
                key = (physical["location"], physical["slot"])
                if key in witness_by_slot:
                    need(witness_by_slot[key] == physical, "prepared physical source drift across captures")
                witness_by_slot[key] = physical
        sources = [row for row in base["physical"]["sources"] if row["name"] in prepared_names]
        if not existing:
            for name in ("campfire", "workbench"):
                _, witness = source(base, name)
                need(witness["plainStation"] is True and witness["count"] == 1, "fresh plain station metadata proof missing")
        plan = {"confirm":"CodexTest", "entity":fixture, "originalEntity":original,"reloadMode":reload_mode,
                "preparedItems":True,
                "preparedSourceWitnesses":sources, "discoverySession":base["session"], "discoveryScreen":base["ui"]["screen"]}
        if existing:
            need(fixture == original, "explicit installed mode must use original camper")
            plan.update({"existingInstalledStations":True,"installedStations":base["installedStations"]})
        else:
            plan.update({"fixtureVehicleConfirmedEmpty":True,"prepareItems":[]})
            campfire_target, workbench_target = node_id(base,"camperModule0"), node_id(base,"camperModule1")
            need(visible_slot(base,campfire_target["path"])["empty"] is True and visible_slot(base,workbench_target["path"])["empty"] is True,
                 "empty fixture physical module cells required")
            plan["installCampfire"] = stage("openManagement") + [deposit(base,"campfire",campfire_target["path"])]
            plan["installFreshWorkbench"] = [deposit(base,"workbench",workbench_target["path"])]
        copied_stages = ["closeManagement","openCampfire","closeCampfire","mountDriver","dismountDriver"]
        if reload_mode == "paused": copied_stages.append("pauseForReload")
        for key in copied_stages:
            plan[key] = stage(key)
        cooking, workbench = captures["campfire"], captures["workbench"]
        if not existing: plan["gridCases"] = []
        grid_specs = () if existing else (("fuel","resourceWood",target(cooking,"fuel",0)),
                                        ("tool","toolCookingPot",target(cooking,"tool",tool="toolCookingPot")),
                                        ("ingredient","foodCropPotato",target(cooking,"ingredient",0)))
        for kind, name, destination in grid_specs:
            origin, witness = source(cooking,name)
            plan["gridCases"].append({"kind":kind,"item":name,"count":witness["count"],"backpackPath":origin,
                                     "gridPath":destination,"sourceWitness":witness})
        cooking_actions = stage("openCampfire") + [action("/ui/click",path=recipe_selector(cooking)),
            action("/ui/type",path=node_id(cooking,"batchInput",True)["path"],value=str(cooking["recipe"]["batches"])),
            deposit(cooking,"toolCookingPot",target(cooking,"tool",tool="toolCookingPot")),
            deposit(cooking,"resourceWood",target(cooking,"fuel",0))]
        # Only exact native planned quantities are pulled; do not deposit an
        # arbitrary full ingredient stack then infer payment from the result.
        cooking_actions += [action("/ui/click",path=node_id(cooking,"pullIngredients")["path"]),
                            action("/ui/click",path=node_id(cooking,"cook")["path"])]
        workbench_actions = stage("openWorkbench") + [action("/ui/click",path=recipe_selector(workbench))]
        workbench_tools = ingress.get("workbenchTools", []) if existing else ("FuriousRamsayHammerPliers","FuriousRamsayScrewdriver")
        need(isinstance(workbench_tools, (list, tuple)) and all(isinstance(name, str) for name in workbench_tools), "reviewed native workbench tools required")
        for name in workbench_tools:
            workbench_actions.append(deposit(workbench,name,target(workbench,"tool",tool=name)))
        input_rows = [row for row in workbench["physical"]["visibleSlots"]
                      if row["path"].startswith(WINDOWS["workbench"]+"/") and row.get("role") == "input"]
        ingredient_scope = "native-workstation-input" if input_rows else "native-player-inventory"
        for index, ingredient in enumerate(workbench["recipe"]["ingredients"]):
            if not input_rows:
                # Exact native OnActivated falls back to PlayerInventory.RemoveItems
                # when this actual working window has no WorkstationInputGrid.
                # Native bag payment aggregates separate physical stacks itself;
                # it does not require any hidden-grid or fabricated merge action.
                available = [s for s in workbench["physical"]["sources"] if s["name"] == ingredient["name"]]
                need(sum(s["count"] for s in available) >= ingredient["payment"], "actual backpack stacks cannot cover native payment")
                continue
            destination = target(workbench,"input",index)
            fragments = ingredient_sources(workbench,ingredient["name"],ingredient["payment"])
            if not existing:
                need(sum(witness["count"] for _, witness in fragments) == ingredient["payment"], "fresh prepared ingredient sources must equal native payment")
            need(len({witness["payload"] for _, witness in fragments}) == 1, "native same-slot staging cannot combine distinct ingredient metadata identities")
            for origin, witness in fragments:
                entry=action("/ui/drag", **{"from.path":origin,"to.path":destination,"strict":1})
                entry["sourceWitness"]=witness;workbench_actions.append(entry)
        # Native craft count is bounded by actually staged inputs. Enter the
        # declared batch only after real staging, never into an empty-input max0.
        workbench_actions.append(action("/ui/type",path=node_id(workbench,"count_input",True)["path"],value=str(workbench["recipe"]["batches"])))
        workbench_actions.append(action("/ui/click",path=node_id(workbench,"btnRebirthCraftingCraft")["path"]))
        for cap, actions in ((cooking,cooking_actions),(workbench,workbench_actions)):
            # After the observed selector press, verify the actual native recipe
            # identity/tier; localized labels alone never authorize payment.
            selector = next(row for row in actions if row["endpoint"] == "/ui/click" and row["args"].get("path") == recipe_selector(cap))
            selector["expectedRecipe"] = {"window":WINDOWS[cap["kind"]],"recipe":cap["recipe"]["recipe"],"tier":cap["recipe"]["tier"]}
        plan["jobs"] = {}
        for slot, cap, actions, close in ((0,cooking,cooking_actions,"closeCampfire"),(1,workbench,workbench_actions,"closeWorkbench")):
            definition = cap["recipe"]
            need(definition["ingredients"] and definition["outputPerBatch"] > 0, "native expected recipe/payment/output absent")
            plan["jobs"][str(slot)] = {"recipe":definition["recipe"],"batches":definition["batches"],"tier":definition["tier"],
                                       "actions":actions,"close":stage(close)}
            if slot == 1: plan["jobs"][str(slot)]["ingredientSourceScope"] = ingredient_scope
            if existing:
                plan["jobs"][str(slot)]["reopen"] = stage("openCampfire" if slot == 0 else "openWorkbench")
                plan["jobs"][str(slot)]["reclose"] = stage(close)
        result.update({"candidateReady":True,"candidatePlan":plan,"captureFiles":args.captures,"ingressFile":args.ingress})
    except Exception as error:
        result["failure"] = type(error).__name__ + ": " + str(error)
        raise
    finally:
        path.parent.mkdir(parents=True,exist_ok=True)
        path.write_text(json.dumps(result,indent=2),encoding="utf-8")
    # Separate directly runnable plan; candidate/evidence is retained even if the
    # parent decides not to run. No input is executed during profile discovery.
    plan_path = path.with_name(path.stem + "_plan.json")
    need(not plan_path.exists(), "existing reviewed plan is not overwritten")
    plan_path.write_text(json.dumps(result["candidatePlan"],indent=2),encoding="utf-8")
    print("Candidate only:",plan_path.resolve())


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    sub=parser.add_subparsers(dest="mode",required=True)
    cap=sub.add_parser("capture")
    cap.add_argument("kind",choices=(*WINDOWS,"driver","closed"));cap.add_argument("output")
    cap.add_argument("--fixture",type=int,required=True);cap.add_argument("--original",type=int,required=True)
    cap.add_argument("--batches",type=int,default=1)
    cap.add_argument("--recipe",help="Exact parent-selected canonical recipe ID; omitted preserves historical potato/ammo defaults")
    cap.add_argument("--existing-installed-stations",action="store_true",help="Explicit same-original idle installed Campfire0/Workbench1 mode; no fresh installation")
    make=sub.add_parser("build");make.add_argument("output");make.add_argument("--ingress",required=True)
    make.add_argument("--captures",nargs="+",required=True)
    args=parser.parse_args()
    capture(args) if args.mode=="capture" else build(args)


if __name__=="__main__":
    main()
