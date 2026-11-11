"""Verify exact solo Pasta source conservation from native Game Bridge evidence."""
import argparse
import hashlib
import json
import re
from pathlib import Path
from urllib.parse import parse_qs, urlsplit
from xml.etree import ElementTree

ITEM = "foodCanPasta"
CASES = ("backpack-button", "backpack-keybind", "closed-toolbelt")
SCOPE_FIELDS = ("entityId", "gameName", "ownerKey", "worldKey", "creationId", "deaths")
GRIDS = ("backpack", "toolbelt")


def require(value, message):
    if not value:
        raise ValueError(message)


def load(path):
    return json.loads(path.read_text(encoding="utf-8-sig"))


def same_scope(actor, seal):
    return (
        all(actor.get(field) == seal[field] for field in SCOPE_FIELDS)
        and actor.get("localActor") is True
        and actor.get("server") is True
        and actor.get("remote") is False
        and actor.get("dedicated") is False
    )


def response(packet):
    status = packet.get("http", packet.get("status"))
    body = packet["body"]
    require(status == 200 and body.get("ok", True) and not body.get("hadError"),
            "Native input or observer refused")
    return body


def trace(packet):
    lines = [
        text for result in response(packet)["results"]
        for text in result["output"] if isinstance(text, str) and text.startswith("{")
    ]
    require(len(lines) == 1, "Expected one structured authority trace")
    return json.loads(lines[0])


def grid(state, actor):
    require(state["state"] == "ingame" and state["player"]["entityId"] == actor,
            "Expected in-game observation of the sealed actor")
    content = state["consumption"]
    require(content["ownerEntityId"] == actor and content["remote"] is False,
            "Expected the solo authority's native inventory")
    cells = {}
    for area in GRIDS:
        for index, cell in enumerate(content[area]):
            require(cell["slot"] == index, "Native grid indexing changed")
            cells[area, index] = cell
    return cells


def digest(text):
    return hashlib.sha256(text.encode("utf-8")).hexdigest()


def durable_witnesses(save_root, seal, after_scope, before_scope, preimage):
    if save_root is None:
        return []
    old = {record["operation"] for record in before_scope.get("records", [])}
    result = []
    for row in after_scope.get("records", []):
        operation = row["operation"]
        if (
            operation in old or not re.fullmatch("[0-9a-f]{32}", operation)
            or not row.get("diskPresent") or row.get("unknown")
            or not {"source-saved", "ingestion-saved"} <= set(row.get("stages", []))
        ):
            continue
        key = digest(f"{seal['ownerKey']}|{operation}")[:40]
        path = save_root / "PC" / key / "prepared.xml"
        if not path.is_file() or not path.resolve().is_relative_to(save_root.resolve()):
            continue
        require(path.stat().st_size <= 64 * 1024 * 1024, "Prepared stage exceeds native bound")
        wrapper = ElementTree.parse(path).getroot()
        original = wrapper.findtext("original", "")
        require(wrapper.tag == "playerConsumptionStage" and wrapper.attrib["stage"] == "prepared"
                and digest(original) == row["binding"], "Prepared-stage binding mismatch")
        body = ElementTree.fromstring(original)
        attrs = body.attrib
        require(
            body.tag == "playerSourceConsumption" and attrs["operation"] == operation
            and digest(attrs["owner"]) == seal["ownerKey"] and attrs["creation"] == seal["creationId"]
            and attrs["world"].lower() == seal["worldKey"] and int(attrs["deaths"]) == seal["deaths"],
            "Prepared source owner or life mismatch",
        )
        area = {"0": "toolbelt", "1": "backpack"}.get(attrs["sourceKind"])
        target = (area, int(attrs["sourceSlot"]))
        if target not in preimage:
            continue
        source = body.find("source")
        cell = preimage[target]
        encoded = hashlib.sha256(source.text.encode("utf-8")).digest()
        from base64 import b64encode
        if int(source.attrib["count"]) == cell["count"] and b64encode(encoded).decode() == cell["nativeHash"]:
            result.append(target)
    return result


def verify_input(case, before, button, record, directory, bag, belt):
    body = response(record)
    url = urlsplit(record["path"])
    query = parse_qs(url.query)
    if case == "closed-toolbelt":
        windows = directory("preflight")["windows"]
        require("crafting" not in windows and "backpack" not in windows, "Backpack was open for held use")
        require(before["player"]["holdingSlot"] == belt and url.path == "/press"
                and query.get("action") == ["Primary"] and body["pressed"] == "Primary"
                and body["inputWitness"]["injectedFrames"] > 0, "Expected one native held Primary input")
        return
    selected = response(directory("select-backpack"))["clicked"]
    require(
        re.fullmatch(r"crafting/.+/rebirthCraftingInventoryViewport/inventory/" + str(bag), selected["path"])
        and selected["item"]["name"] == ITEM
        and selected["item"]["count"] == before["consumption"]["backpack"][bag]["count"],
        "The selected native controller is not the sealed Backpack source",
    )
    require(button["label"]["text"].lower() in ("use", "eat")
            and button["control"]["path"] == button["label"]["path"].removesuffix("/name") + "/background",
            "Selected Use label and press collider do not share a native entry")
    if case == "backpack-button":
        path = button["control"]["path"]
        require(url.path == "/ui/click" and query.get("path") == [path] and body["clicked"]["path"] == path,
                "Expected the actual selected Use button click")
    else:
        key = button["shortcut"].strip().strip("<>")
        require(re.fullmatch("[A-Z0-9]", key) and url.path == "/key" and query.get("name") == [key]
                and "gui.GUI D-Pad Left" in body["actions"], "Expected the actual displayed Use shortcut")
        bindings = directory("native-bindings")["actions"]["gui"]
        action = [entry for entry in bindings if entry.startswith("DPad_Left [")]
        require(len(action) == 1 and key in action[0][11:-1].split(", "), "Live DPad_Left binding changed")


def verify_case(evidence, seal, case, save_root=None):
    def artifact(suffix):
        return load(evidence / f"{case}-{suffix}.json")

    before = artifact("before")
    first = artifact("first-after")
    after = artifact("after-no-input")
    actor = seal["entityId"]
    pre = grid(before, actor)
    post = grid(after, actor)
    require(grid(first, actor) == post and pre.keys() == post.keys(), "Second native readback is not stable")
    sources = [key for key, cell in pre.items() if cell["name"] == ITEM]
    require(len(sources) == 2 and {grid_name for grid_name, _ in sources} == set(GRIDS),
            "Expected one Pasta cell in each native source grid")
    bag = next(index for area, index in sources if area == "backpack")
    belt = next(index for area, index in sources if area == "toolbelt")
    b, t = (pre[key] for key in sources)
    require(b["count"] > 0 and t["count"] > 0 and b["nativeHash"]
            and all(b[key] == t[key] for key in ("nativeHash", "type", "seed"))
            and before["player"]["holdingSlot"] == belt, "Pasta sources are not identical or belt selection changed")
    verify_input(case, before, None if case == "closed-toolbelt" else artifact("action-control"),
                 artifact("use-once"), artifact, bag, belt)

    wanted = ("toolbelt", belt) if case == "closed-toolbelt" else ("backpack", bag)
    changed = [key for key in pre if pre[key] != post[key]]
    require(len(changed) == 1, "Expected exactly one native inventory cell to change")
    actual = changed[0]
    a, z = pre[actual], post[actual]
    require(actual in sources and z["count"] == a["count"] - 1
            and (z["count"] == 0 or z["nativeHash"] == a["nativeHash"]), "Native cell debit differs from one serving")
    start = trace(artifact("trace-before"))["lastSequence"]
    events = trace(artifact("trace-after"))["events"]
    source_kind = {"Backpack": "backpack", "HeldToolbelt": "toolbelt"}
    direct = [
        (source_kind.get(row["sourceKind"]), row["slot"]) for row in events
        if row["sequence"] > start and row["player"] == actor and row["remoteWorld"] is False
        and row["stage"] == "debit.observed" and row.get("success")
        and row["before"]["digest"] == a["nativeHash"]
        and row["before"]["count"] == a["count"] and row["after"]["count"] == z["count"]
    ]
    durable = durable_witnesses(save_root, seal, artifact("serving-scope"), artifact("preflight")["serving"], pre)
    require(len(direct) <= 1 and len(durable) <= 1 and set(direct + durable) == {actual},
            "One matching direct or durable authoritative source witness required")

    admit = re.compile(rf"DigestionAudit\] admit entity={actor} entry=\S+ item={ITEM}(?: |$)")
    complete = re.compile(rf"ConsumeAnimation\] complete entity={actor} item={ITEM}(?: |$)")
    require(sum(bool(admit.search(row["msg"])) for row in artifact("intake")["entries"]) == 1,
            "Expected one accepted authority intake")
    require(sum(bool(complete.search(row["msg"])) for row in artifact("animation")["entries"]) == 1,
            "Expected one native animation completion")
    return {"case": case, "passed": wanted == actual, "selected": wanted, "debited": actual,
            "before": a["count"], "after": z["count"], "authority": "direct" if direct else "durable"}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--evidence", type=Path, required=True)
    parser.add_argument("--fixture-seal", type=Path, required=True)
    parser.add_argument("--save-root", type=Path, help="Disposable primary save for durable-only evidence")
    parser.add_argument("--case", action="append", choices=CASES)
    args = parser.parse_args()
    try:
        seal = load(args.fixture_seal)
        require(
            re.fullmatch(r"CodexTestReports-[0-9a-f]{10}", seal["gameName"])
            and re.fullmatch("[0-9a-f]{64}", seal["ownerKey"])
            and all(re.fullmatch("[0-9a-f]{32}", seal[key]) for key in ("worldKey", "creationId"))
            and re.fullmatch("[0-9A-F]{64}", seal["candidate"])
            and seal["entityId"] > 0 and seal["deaths"] >= 0,
            "Exact disposable fixture seal required",
        )
        startup = load(args.evidence / "startup.json")
        preflight = load(args.evidence / "closed-preflight.json")
        require(same_scope(startup["actor"], seal) and preflight["pin"] == seal["candidate"],
                "Native startup actor or candidate differs from fixture seal")
        results = [verify_case(args.evidence, seal, case, args.save_root) for case in args.case or CASES]
        print(json.dumps({"passed": all(row["passed"] for row in results), "cases": results}, indent=2))
        return 0 if all(row["passed"] for row in results) else 1
    except (ValueError, OSError, KeyError, TypeError, ElementTree.ParseError) as error:
        print(json.dumps({"passed": False, "reason": str(error)}, indent=2))
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
