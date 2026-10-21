"""Read-only capture and offline exact-item assertions; never launches or inputs the game."""
import argparse
from collections import Counter
import json
from pathlib import Path
import urllib.request


def require(condition, message):
    if not condition:
        raise ValueError(message)


def validate(value):
    require(isinstance(value, dict) and value.get("schema") == 1, "unsupported observation schema")
    require(value.get("ok") is True, "bridge observation failed")
    require(value.get("remote") is True and value.get("connected") is True, "non-host connected owner required")
    require(isinstance(value.get("creationId"), str) and value["creationId"], "missing projected creation")
    for key in ("bag", "belt", "bagLocks", "intentMarkers", "recovery"):
        require(isinstance(value.get(key), list), "missing array: " + key)
    require(isinstance(value.get("cursor"), dict), "missing native cursor")
    require(isinstance(value.get("receipts"), dict), "missing receipts")
    return value


def add_stack(counts, stack, native=False):
    require(isinstance(stack, dict), "missing exact stack")
    count = stack.get("Count" if native else "count")
    data = stack.get("ItemData" if native else "itemData")
    require(type(count) is int and count >= 0 and isinstance(data, str), "malformed stack")
    require((count == 0 and data == "") or (count > 0 and data != ""), "invalid empty/native item encoding")
    if count:
        counts[data] += count


def inventory(value):
    counts = Counter()
    for stack in value["bag"] + value["belt"] + [value["cursor"]]:
        add_stack(counts, stack)
    return counts


def idle(value):
    validate(value)
    for key in ("gearHeld", "libraryHeld", "resourcePending", "creationHeld", "offerPending"):
        require(value.get(key) is False, "custody remains pending: " + key)
    require(value["intentMarkers"] == [], "original intent marker remains")
    require(value["cursor"].get("count") == 0, "cursor not empty")


def same_scope(before, after):
    require(type(before.get("_bridgePid")) is int and isinstance(before.get("_bridgeStartedUtc"), str),
            "use authenticated capture files with bridge session evidence")
    for key in ("entityId", "worldKey", "creationId", "_bridgePid", "_bridgeStartedUtc"):
        require(before.get(key) == after.get(key), "owner/session scope changed: " + key)


def assert_pair(before, after, mode, slot=None, expected=None):
    idle(before)
    idle(after)
    same_scope(before, after)
    if mode == "loot":
        require(isinstance(expected, list) and expected, "loot requires explicit exact expected stacks")
        expected_inventory = inventory(before)
        for stack in expected:
            require(stack.get("count", 0) > 0, "expected loot must be nonempty")
            add_stack(expected_inventory, stack)
        require(expected_inventory == inventory(after), "exact native loot gain failed")
        require(before.get("terminal") == after.get("terminal"), "new gear transaction during loot")
        require(before["bagLocks"] == after["bagLocks"], "native player locks changed during loot")
        return  # Source-container debit needs separately paired source evidence.
    if mode == "move":
        require(inventory(before) == inventory(after), "native inventory/cursor items changed during movement")
        require(before.get("terminal") == after.get("terminal"), "new gear transaction during movement")
        require(before["bagLocks"] == after["bagLocks"], "native player locks changed during movement")
        require(before["bag"] != after["bag"] or before["belt"] != after["belt"], "no stack actually moved")
        return
    terminal, offer = after.get("terminal"), after.get("offer")
    require(isinstance(terminal, dict) and isinstance(offer, dict), "missing retained exact settled original")
    tx = offer.get("transactionId")
    require(isinstance(tx, str) and tx, "missing original transaction")
    require(terminal.get("TransactionId") == tx and terminal.get("CreationId") == before["creationId"], "terminal/original mismatch")
    require(terminal.get("Applied") is True and after.get("savedRetirement") is True, "no exact saved applied retirement")
    require((before.get("terminal") or {}).get("TransactionId") != tx, "no new equip/unequip transaction")
    require(after["receipts"].get(offer.get("receiptKey")) == 1, "original Applied receipt missing")
    require(offer.get("slotId") == slot, "unexpected equipment slot")
    plan = offer.get("plan")
    require(isinstance(plan, dict), "missing conserved original plan")
    old, new = inventory(before), inventory(after)
    add_stack(old, plan.get("GearBefore"), True)
    add_stack(new, plan.get("GearAfter"), True)
    expected_recovery = Counter()
    for entry in plan.get("Recovery", []):
        add_stack(expected_recovery, entry.get("Item"), True)
    live_recovery = Counter()
    publications = set()
    for entity in after["recovery"]:
        publication = entity.get("publicationId")
        require(publication and publication not in publications, "duplicate/missing recovery publication")
        publications.add(publication)
        for stack in entity.get("stacks", []):
            add_stack(live_recovery, stack)
    require(live_recovery == expected_recovery, "exact live recovery unavailable or inconsistent")
    new.update(live_recovery)
    require(old == new, "exact ItemValue/count conservation failed")
    retained = min(len(before["bagLocks"]), len(after["bagLocks"]))
    require(before["bagLocks"][:retained] == after["bagLocks"][:retained], "existing native player locks changed")


def capture(session_path, destination):
    destination = Path(destination)
    destination.parent.mkdir(parents=True, exist_ok=True)
    session = json.loads(Path(session_path).read_text(encoding="utf-8-sig"))
    request = urllib.request.Request("http://127.0.0.1:" + str(session["port"]) + "/gearobserve",
                                     headers={"X-Bridge-Token": session["token"]})
    with urllib.request.urlopen(request, timeout=20) as reply:
        raw = json.load(reply)
    raw["_bridgePid"] = session["pid"]
    raw["_bridgeStartedUtc"] = session["startedUtc"]
    # Write evidence before assertions, including failure/unsupported-state replies.
    destination.write_text(json.dumps(raw, indent=2), encoding="utf-8")
    validate(raw)
    print(str(destination.resolve()))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    sub = parser.add_subparsers(dest="command", required=True)
    cap = sub.add_parser("capture")
    cap.add_argument("output")
    cap.add_argument("--session", default=str(Path(__file__).parent / "out/session.json"),
                     help="existing authenticated local bridge session file")
    check = sub.add_parser("assert")
    check.add_argument("before")
    check.add_argument("after")
    check.add_argument("--mode", choices=("gear", "move", "loot"), required=True)
    check.add_argument("--slot", choices=("backpack", "walkman"))
    check.add_argument("--expected", help="loot-only JSON array of exact count/itemData stacks")
    args = parser.parse_args()
    if args.command == "capture":
        capture(args.session, args.output)
    else:
        require(args.mode != "gear" or args.slot is not None, "gear mode requires --slot")
        assert_pair(json.loads(Path(args.before).read_text(encoding="utf-8-sig")),
                    json.loads(Path(args.after).read_text(encoding="utf-8-sig")), args.mode, args.slot,
                    json.loads(Path(args.expected).read_text(encoding="utf-8-sig")) if args.expected else None)
        print("PASS exact owner custody and " + ("loot gain" if args.mode == "loot" else "item conservation") + ": " + args.mode)


if __name__ == "__main__":
    main()
